// Round lengths, swing spacing and the idle OnEndCombatRound.
// Attackers are stripped nw_bandit001 (hostile faction, +20 to hit, AI normal, no
// player in the area, no event scripts but a logging OnEndCombatRound); targets are passive, immortal stripped
// bandits (commoner faction) 1.5 m east that log the swings (OnPhysicalAttacked) and blows (OnDamaged) they take.
//   one        one attack a round, 40 s of swings: the swing-to-swing period
//   two        two attacks a round (SetBaseAttackBonus 2): first-to-second gap and round period
//   haste      one attack, hasted 10 s before the attack order: gaps as for two
//   hastenow   one attack, hasted in the same script as the attack order
//   cut        ClearAllActions(TRUE) 0.2 s after the second swing, no new order: every OnEndCombatRound after it
//   cutmiss    as cut, the target has 100% concealment (every swing misses)
//   clear      ClearAllActions() (combat state kept) 0.2 s after the second swing, no new order
//   stock      as cut, and the attacker's OnEndCombatRound also runs nw_c2_default3; what it saw first is logged
//   stockenemy as stock, the attacker made the target's temporary enemy (SetIsTemporaryEnemy) before the order
// (In the default module factions a hostile-faction attacker regards a commoner as a friend, reputation 100, and
// ActionAttack on a hostile-faction mate is dropped, attack_mate.)
// GetAILevel of the attackers is read at the first swing.
#include "ri_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
int nCell = 0;

object Make(float x, float y, float f, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(Area(), Vector(x, y, 0.0), f));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, nFaction);
    SetImmortal(o, TRUE);
    return o;
}

void Order(object a, object t) { RiMark(a, "o"); AssignCommand(a, ActionAttack(t)); }

void Haste(object a) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectHaste(), a); }

void Pair(string g, int nMode, int nAttacks = 1, int nHaste = 0, int nStock = 0)
{
    float x = 10.0 + IntToFloat(nCell % 7) * 13.0, y = 10.0 + IntToFloat(nCell / 7) * 13.0;
    nCell++;
    object a = Make(x, y, 0.0, STANDARD_FACTION_HOSTILE);
    object t = Make(x + 1.5, y, 180.0, STANDARD_FACTION_COMMONER);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "ri_atk");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "ri_dmg");
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, nStock > 0 ? "ri_eors" : "ri_eor");
    SetBaseAttackBonus(nAttacks, a);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), a);
    if (nStock == 2) SetIsTemporaryEnemy(t, a);
    if (nMode == 3) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(100), t);
    SetLocalObject(a, "T", t);
    SetLocalInt(a, "MODE", nMode);
    SetLocalString(a, "G", g);
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count");
    SetLocalObject(m, g + IntToString(n), a);
    SetLocalInt(m, g + ".count", n + 1);
    if (nHaste == 1) DelayCommand(2.0, Haste(a));
    if (nHaste == 2) DelayCommand(12.0, Haste(a));
    DelayCommand(12.0, Order(a, t));
}

// Mean and range of the gaps between swing i and swing i + nStep (swing indices from nFrom), over the group.
void Gaps(string k, string g, int nFrom, int nStep, int nEvery)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count"), i, j, c = 0;
    float sum = 0.0, lo = 99.0, hi = 0.0;
    for (i = 0; i < n; i++)
    {
        object a = GetLocalObject(m, g + IntToString(i));
        int ns = GetLocalInt(a, "sn");
        for (j = nFrom; j + nStep < ns; j += nEvery)
        {
            float d = GetLocalFloat(a, "s" + IntToString(j + nStep)) - GetLocalFloat(a, "s" + IntToString(j));
            sum += d; c++;
            if (d < lo) lo = d;
            if (d > hi) hi = d;
        }
    }
    if (c == 0) { PLog(k, "none"); return; }
    // Single gaps on nwserver are quantised by its frames (1.5-2.8 s for a 2.25 s mean): a looser tolerance for few.
    PLogF(k, sum / IntToFloat(c), c < 20 ? 0.35 : 0.15);
    WriteTimestampedLogEntry("RAW|" + k + "|n=" + IntToString(c) + " min=" + FloatToString(lo, 0, 3) + " max=" + FloatToString(hi, 0, 3));
}

// The round ends (OnEndCombatRound) at or after the clear: how many (within 20 s), the first relative to the clear,
// the second relative to the clear and to the blow that landed after it.
void Ends(string g)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count"), i, j, c = 0, c2 = 0, cb = 0;
    float f1 = 0.0, f2 = 0.0, fb = 0.0;
    for (i = 0; i < n; i++)
    {
        object a = GetLocalObject(m, g + IntToString(i));
        float x = GetLocalFloat(a, "x0");
        string s = "";
        int k = 0;
        float blow = -1.0;
        for (j = 0; j < GetLocalInt(a, "dn"); j++) if (blow < 0.0 && GetLocalFloat(a, "d" + IntToString(j)) >= x) blow = GetLocalFloat(a, "d" + IntToString(j));
        for (j = 0; j < GetLocalInt(a, "en"); j++)
        {
            float t = GetLocalFloat(a, "e" + IntToString(j));
            if (t < x || t > x + 20.0) continue;
            s += FloatToString(t - x, 0, 2) + " ";
            c++;
            if (k == 0) f1 += t - x;
            if (k == 1) { f2 += t - x; c2++; if (blow >= 0.0) { fb += t - blow; cb++; } }
            k++;
        }
        WriteTimestampedLogEntry("RAW|" + g + IntToString(i) + ".ends|" + s + "| blow " + FloatToString(GetLocalFloat(a, "d0") - x, 0, 2)
            + " | " + GetLocalString(a, "SEQ"));
    }
    if (GetLocalInt(GetLocalObject(m, g + "0"), "MODE") == 0) return;
    PLogF(g + ".ends", IntToFloat(c) / IntToFloat(n), 0.3);
    if (n > 0) PLogF(g + ".end1", f1 / IntToFloat(n), 0.4);
    if (c2 > 0) PLogF(g + ".end2", f2 / IntToFloat(c2), 0.4);
    if (cb > 0) PLogF(g + ".end2.afterBlow", fb / IntToFloat(cb), 0.4);
}

void Report(string g)
{
    object m = GetModule();
    PLogI(g + ".ailevel", GetLocalInt(GetLocalObject(m, g + "0"), "AIL"));
    Ends(g);
}

void main()
{
    PStart();
    int i;
    for (i = 0; i < 6; i++)
    {
        Pair("one", 0, 1);
        Pair("two", 0, 2);
        Pair("haste", 0, 1, 1);
        Pair("hastenow", 0, 1, 2);
    }
    for (i = 0; i < 4; i++)
    {
        Pair("cut", 1);
        Pair("cutmiss", 3);
        Pair("clear", 2);
        Pair("stock", 1, 1, 0, 1);
        Pair("stockenemy", 1, 1, 0, 2);
    }
    DelayCommand(58.0, Gaps("one.period", "one", 0, 1, 1));
    DelayCommand(58.0, Gaps("two.within", "two", 0, 1, 2));
    DelayCommand(58.0, Gaps("two.period", "two", 0, 2, 2));
    DelayCommand(58.0, Gaps("haste.within", "haste", 0, 1, 2));
    DelayCommand(58.0, Gaps("haste.period", "haste", 0, 2, 2));
    DelayCommand(58.0, Gaps("haste.first.within", "haste", 0, 1, 99));
    DelayCommand(58.0, Gaps("hastenow.within", "hastenow", 0, 1, 2));
    DelayCommand(58.0, Gaps("hastenow.period", "hastenow", 0, 2, 2));
    DelayCommand(58.0, Gaps("hastenow.first.within", "hastenow", 0, 1, 99));
    string gs = "one two haste hastenow cut cutmiss clear stock stockenemy ";
    while (gs != "")
    {
        int sp = FindSubString(gs, " ");
        DelayCommand(58.5, Report(GetStringLeft(gs, sp)));
        gs = GetSubString(gs, sp + 1, 1000);
    }
    DelayCommand(59.0, PDone());
}

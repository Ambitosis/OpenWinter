// What a charmed caster does by itself (no scripts): a level-1 wizard with Aluvian Darkstar's spells (Mage Armor and
// Summon Creature I memorised, cantrips), a dagger in its pack, charmed by a hostile bandit K 3 m east, another
// hostile bandit E 9 m north. The transitions of its weapon, Mage Armor, summons, attack target and action, and the
// spells and attacks that reach K and E.
#include "parity_time"
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
float Now() { return PNow(); }

object Spawn(string r, float x, float y, int nFaction, string g, string sRole)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, r, Location(Area(), Vector(x, y, 0.0), 0.0));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, nFaction);
    SetLocalString(o, "G", g);
    SetLocalString(o, "R", sRole);
    return o;
}

// The log keeps "event@time"; the sequence compared drops the times.
void Add(string g, string s) { object m = GetModule(); SetLocalString(m, g + ".log", GetLocalString(m, g + ".log") + " " + s); }

string Untimed(string s)
{
    string r = "";
    int i = 0, n = GetStringLength(s), skip = FALSE;
    for (i = 0; i < n; i++)
    {
        string ch = GetSubString(s, i, 1);
        if (ch == "@") skip = TRUE;
        else if (ch == " ") skip = FALSE;
        if (!skip) r += ch;
    }
    return r;
}

// The time of the nth event (from 1) whose text starts with sWhat, or -1.
float When(string s, string sWhat, int nth = 1)
{
    int at = 0, k = 0;
    while (TRUE)
    {
        int p = FindSubString(s, " " + sWhat, at);
        if (p < 0) return -1.0;
        int a = FindSubString(s, "@", p);
        int e = FindSubString(s, " ", a);
        if (++k == nth) return StringToFloat(GetSubString(s, a + 1, (e < 0 ? GetStringLength(s) : e) - a - 1));
        at = p + 1;
    }
    return -1.0;
}

string Who(object o);

// Tolerance for a time k heartbeats in (each gap uniform over 1.2 s: variance 0.12 s squared).
float BeatTolerance(int k)
{
    if (k < 1) k = 1;
    float tol = 3.5 * sqrt(2.0 * IntToFloat(k) * 0.12);
    return tol < 1.2 ? 1.2 : tol;
}

void Report(string g)
{
    string s = GetLocalString(GetModule(), g + ".log");
    PLog(g + ".seq", Untimed(s));
    object w = GetLocalObject(GetModule(), g + ".W");
    WriteTimestampedLogEntry("CHARM|" + g + "|" + s + " | hp=" + IntToString(GetCurrentHitPoints(w)) + " damager=" + Who(GetLastDamager(w)) + " attacker=" + Who(GetLastAttacker(w)));
    // The rounds: when it took up each new action after the first. Each decision is a heartbeat running the charm's
    // state script (nw_g0_charm), and heartbeats come 6.0-7.2 s apart at random (state_scripts), so after k of them
    // two runs can differ by about 3.5 standard deviations of the difference of two sums of k such gaps.
    int n;
    for (n = 2; n <= 5; n++)
    {
        float t = When(s, "a4", n);
        if (t >= 0.0) PLogF(g + ".cast" + IntToString(n), t, BeatTolerance(n - 1));
    }
    float m = When(s, "a3");
    if (m >= 0.0) PLogF(g + ".melee", m, m < 3.0 ? 0.3 : BeatTolerance(FloatToInt(m / 6.6 + 0.5)));
}

string Who(object o)
{
    if (!GetIsObjectValid(o)) return "-";
    string r = GetLocalString(o, "R");
    return r == "" ? GetResRef(o) : r;
}

void Watch(object c, string g, float fEnd)
{
    float t = Now() - GetLocalFloat(c, "T0");
    if (t > fEnd || !GetIsObjectValid(c)) return;
    string ts = "@" + FloatToString(t, 0, 1);
    string w = GetResRef(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, c));
    if (w != GetLocalString(c, "w")) { SetLocalString(c, "w", w); Add(g, "hand=" + (w == "" ? "-" : w) + ts); }
    int a = GetHasSpellEffect(SPELL_MAGE_ARMOR, c);
    if (a != GetLocalInt(c, "a")) { SetLocalInt(c, "a", a); Add(g, "armor" + IntToString(a) + ts); }
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, c);
    string sr = GetIsObjectValid(s) ? GetResRef(s) : "-";
    if (sr != GetLocalString(c, "s")) { SetLocalString(c, "s", sr); Add(g, "summon=" + sr + ts); }
    // (GetAttackTarget flickers between rounds on nwserver: not followed.)
    int ac = GetCurrentAction(c);
    if (ac != GetLocalInt(c, "ac") || !GetLocalInt(c, "acset")) { SetLocalInt(c, "acset", 1); SetLocalInt(c, "ac", ac); Add(g, "a" + IntToString(ac) + ts); }
    DelayCommand(0.1, Watch(c, g, fEnd));
}

// nKind 0 charm by K, 1 domination by K, 2 nothing (control), 3 charm by K with E absent, 4 charm by the module.
// fE: E's distance north. sRes: the caster blueprint; bHurt: starts at half hit points.
void Case(int i, string g, int nKind, string sRes = "ca_wiz", float fE = 9.0, int bHurt = FALSE)
{
    float x = 8.0 + IntToFloat(i % 5) * 30.0, y = 8.0 + IntToFloat(i / 5) * 32.0;
    object c = Spawn(sRes, x, y, STANDARD_FACTION_DEFENDER, g, "W");
    SetLocalObject(GetModule(), g + ".W", c);
    // Kept alive through the bystanders' blows (immortal creatures refuse charm).
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectTemporaryHitpoints(300)), c);
    CreateItemOnObject("nw_wswdg001", c);
    if (bHurt) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(GetMaxHitPoints(c) / 2), c);
    object k = Spawn("nw_bandit001", x + 3.0, y, STANDARD_FACTION_HOSTILE, "", "K");
    SetImmortal(k, TRUE);
    SetEventScript(k, EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT, "ca_spell");
    SetEventScript(k, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "ca_atk");
    if (nKind != 3)
    {
        object e = Spawn("nw_bandit001", x, y + fE, STANDARD_FACTION_HOSTILE, "", "E");
        SetImmortal(e, TRUE);
        SetEventScript(e, EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT, "ca_spell");
        SetEventScript(e, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "ca_atk");
        // On nwserver a stripped creature hit by harmful spells sometimes strikes back, breaking the caster's next
        // spell; kept out of it here.
        SetCommandable(FALSE, e);
    }
    SetLocalFloat(c, "T0", Now() + 5.0);
    if (nKind == 0 || nKind == 3) DelayCommand(5.0, AssignCommand(k, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCharmed(), c)));
    else if (nKind == 1) DelayCommand(5.0, AssignCommand(k, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDominated(), c)));
    else if (nKind == 4) DelayCommand(5.0, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCharmed(), c));
    DelayCommand(5.0, Watch(c, g, 36.0));
    DelayCommand(42.0, Report(g));
}

void main()
{
    Case(0, "charm0", 0);
    Case(1, "dominate", 1);
    Case(2, "control", 2);
    Case(3, "charmAlone", 3);
    Case(4, "charmModule", 4);
    Case(5, "e3", 0, "ca_wiz", 3.0);
    Case(6, "e4", 0, "ca_wiz", 4.0);
    Case(7, "e5", 0, "ca_wiz", 5.0);
    Case(8, "e6", 0, "ca_wiz", 6.0);
    Case(9, "e7", 0, "ca_wiz", 7.0);
    Case(10, "e15", 0, "ca_wiz", 15.0);
    Case(11, "e25", 0, "ca_wiz", 25.0);
    Case(12, "atk", 0, "ca_atk");
    Case(13, "atk3", 0, "ca_atk", 3.0);
    Case(14, "buf", 0, "ca_buf");
    Case(15, "sum", 0, "ca_sum");
    Case(16, "mam", 0, "ca_mam");
    Case(17, "none", 0, "ca_none");
    Case(18, "sum3", 0, "ca_sum", 3.0);
    Case(19, "buf3", 0, "ca_buf", 3.0);
    DelayCommand(43.0, PDone());
}

#include "visual_inc"
// conditionai: what the original does with a player under the conditions that take control away, and the chat lines
// of the defences. Every 0.5 s the player's offset from her start, current action and attack target are logged
// (notes.txt); the chat window is logged (chat.txt).
//   a  frightened by a hostile goblin 3 m east, 12 s
//   b  confused, a commoner 2 m east, 14 s
//   c  charmed by a hostile bandit 3 m east (another hostile bandit 9 m north), 14 s, with reputations
//   d  dominated by the same bandit, 10 s
//   e  sanctuary DC 100 on the player, a hostile goblin told to attack her; then the player attacks a goblin under
//      sanctuary DC 100, then DC 1
//   f  the player attacks goblins with 50% concealment, with damage reduction 10/+1, with slashing resistance 5;
//      a goblin attacks the player under 50% miss chance
//   g  the player casts Magic Missile (cheat) at goblins with spell resistance 50, spell immunity to Magic Missile,
//      spell level absorption; then, deaf, she casts Light (verbal) ten times
//   h  see invisibility and true seeing on the player beside an invisible goblin (shots)
//   i  time stop on the player for 6 s while a goblin walks; her own clock against the module's

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|conditionai|" + s); }

object PC() { return GetFirstPC(); }

object Npc(string sResRef, float fDx, float fDy, string sTag, int nFaction)
{
    vector v = GetPositionFromLocation(GetLocalLocation(GetModule(), "HOME"));
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(PC()), Vector(v.x + fDx, v.y + fDy, v.z), 180.0), FALSE, sTag);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Clear(string sTag)
{
    int n = 0;
    object o = GetObjectByTag(sTag, n);
    while (GetIsObjectValid(o))
    {
        DestroyObject(o);
        o = GetObjectByTag(sTag, ++n);
    }
}

string Who(object o)
{
    if (!GetIsObjectValid(o)) return "-";
    return GetTag(o);
}

void Trace(string k, float t, float fEnd)
{
    if (t > fEnd) return;
    object pc = PC();
    vector v = GetPosition(pc), h = GetPositionFromLocation(GetLocalLocation(GetModule(), "HOME"));
    Log(k + "|" + FloatToString(t, 0, 1) + "|" + FloatToString(v.x - h.x, 0, 1) + "," + FloatToString(v.y - h.y, 0, 1)
        + "|a" + IntToString(GetCurrentAction(pc)) + "|t" + Who(GetAttackTarget(pc)) + "|c" + IntToString(GetCommandable(pc)));
    DelayCommand(0.5, Trace(k, t + 0.5, fEnd));
}

void Home()
{
    object pc = PC();
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(GetLocalLocation(GetModule(), "HOME")));
}

void Rep(string k, object a, object b)
{
    Log(k + "|enemy=" + IntToString(GetIsEnemy(b, a)) + "|friend=" + IntToString(GetIsFriend(b, a)) + "|rep=" + IntToString(GetReputation(a, b)));
}

// ---------------------------------------------------------------- a-d: control taken away

void Fear()
{
    object g = Npc("nw_goblina", 3.0, 0.0, "cai_a", STANDARD_FACTION_HOSTILE);
    SetPlotFlag(g, TRUE);
    DelayCommand(1.0, AssignCommand(g, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectFrightened(), PC(), 12.0)));
    DelayCommand(1.0, Trace("a", 0.0, 15.0));
    DelayCommand(2.0, VShot("a_fear_1s"));
    DelayCommand(5.0, VShot("a_fear_4s"));
    DelayCommand(16.0, Clear("cai_a"));
}

void Confuse()
{
    object c = Npc("nw_bandit001", 2.0, 0.0, "cai_b", STANDARD_FACTION_COMMONER);
    SetImmortal(c, TRUE);
    DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectConfused(), PC(), 14.0));
    DelayCommand(1.0, Trace("b", 0.0, 17.0));
    DelayCommand(2.0, VShot("b_confuse_1s"));
    DelayCommand(8.0, VShot("b_confuse_7s"));
    DelayCommand(18.0, Clear("cai_b"));
}

void Charm(int bDominate)
{
    string k = bDominate ? "d" : "c";
    object m = Npc("nw_bandit001", 3.0, 0.0, "cai_" + k, STANDARD_FACTION_HOSTILE);
    object other = Npc("nw_bandit001", 0.0, 9.0, "cai_" + k, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(m, TRUE);
    SetPlotFlag(other, TRUE);
    float fDur = bDominate ? 10.0 : 14.0;
    if (bDominate) DelayCommand(1.0, AssignCommand(m, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDominated(), PC(), fDur)));
    else DelayCommand(1.0, AssignCommand(m, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectCharmed(), PC(), fDur)));
    DelayCommand(1.0, Trace(k, 0.0, fDur + 2.0));
    DelayCommand(1.5, Rep(k + ".pcToM", PC(), m));
    DelayCommand(1.5, Rep(k + ".mToPc", m, PC()));
    DelayCommand(1.5, Rep(k + ".pcToOther", PC(), other));
    DelayCommand(1.5, Log(k + ".master|" + Who(GetMaster(PC())) + "|assoc=" + IntToString(GetAssociateType(PC()))));
    DelayCommand(2.0, VShot(k + "_1s"));
    DelayCommand(8.0, VShot(k + "_7s"));
    DelayCommand(fDur + 3.0, Clear("cai_" + k));
}

// ---------------------------------------------------------------- e: sanctuary

void SanctuaryOnPlayer()
{
    object g = Npc("nw_goblina", 2.0, 0.0, "cai_e", STANDARD_FACTION_HOSTILE);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSanctuary(100), PC(), 9.0);
    DelayCommand(5.0, AssignCommand(g, ActionAttack(PC())));
    DelayCommand(6.0, VShot("e_sanct_on_pc"));
    DelayCommand(9.5, Log("e.goblin.action|" + IntToString(GetCurrentAction(g))));
    DelayCommand(10.0, Clear("cai_e"));
}

void PlayerAttacksSanctuary(int nDC)
{
    object g = Npc("nw_goblina", 2.0, 0.0, "cai_e", STANDARD_FACTION_HOSTILE);
    SetImmortal(g, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSanctuary(nDC), g);
    DelayCommand(4.5, AssignCommand(PC(), ActionAttack(g)));
    DelayCommand(5.5, Log("e.pcAttack" + IntToString(nDC) + ".action|" + IntToString(GetCurrentAction(PC()))));
    DelayCommand(9.0, Log("e.pcAttack" + IntToString(nDC) + ".action9|" + IntToString(GetCurrentAction(PC()))));
    DelayCommand(9.5, Home());
    DelayCommand(10.0, Clear("cai_e"));
}

// ---------------------------------------------------------------- f: defences in melee

void Melee(int nKind)
{
    object g = Npc("nw_goblina", 1.5, 0.0, "cai_f", STANDARD_FACTION_HOSTILE);
    SetImmortal(g, TRUE);
    if (nKind == 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(50), g);
    if (nKind == 1) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageReduction(10, DAMAGE_POWER_PLUS_ONE), g);
    if (nKind == 2) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageResistance(DAMAGE_TYPE_SLASHING, 5), g);
    if (nKind == 3)
    {
        ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectMissChance(50), PC(), 8.0);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), g);
        DelayCommand(1.0, AssignCommand(g, ActionAttack(PC())));
    }
    else DelayCommand(1.0, AssignCommand(PC(), ActionAttack(g)));
    DelayCommand(4.0, VShot("f_melee" + IntToString(nKind)));
    DelayCommand(8.0, AssignCommand(PC(), ClearAllActions(TRUE)));
    DelayCommand(8.0, AssignCommand(g, ClearAllActions(TRUE)));
    DelayCommand(8.5, Clear("cai_f"));
}

// ---------------------------------------------------------------- g: spell defences, deafness

void Spells(int nKind)
{
    object g = Npc("nw_goblina", 5.0, 0.0, "cai_g", STANDARD_FACTION_HOSTILE);
    SetImmortal(g, TRUE);
    if (nKind == 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellResistanceIncrease(50), g);
    if (nKind == 1) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellImmunity(SPELL_MAGIC_MISSILE), g);
    if (nKind == 2) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellLevelAbsorption(9, 0), g);
    DelayCommand(1.0, AssignCommand(PC(), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, g, METAMAGIC_ANY, TRUE)));
    DelayCommand(4.0, VShot("g_spell" + IntToString(nKind)));
    DelayCommand(6.0, Clear("cai_g"));
}

void Deaf()
{
    object pc = PC();
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDeaf(), pc, 40.0);
    int n;
    for (n = 0; n < 10; n++) AssignCommand(pc, ActionCastSpellAtObject(SPELL_LIGHT, pc, METAMAGIC_ANY, TRUE));
    DelayCommand(3.0, VShot("g_deaf"));
}

// ---------------------------------------------------------------- h: seeing the invisible

void Seeing()
{
    object g = Npc("nw_goblina", 3.0, 0.0, "cai_h", STANDARD_FACTION_MERCHANT);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectInvisibility(INVISIBILITY_TYPE_NORMAL), g);
    DelayCommand(1.0, VShot("h_invisible"));
    DelayCommand(1.5, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSeeInvisible(), PC(), 3.0));
    DelayCommand(3.0, VShot("h_seeinvis"));
    DelayCommand(5.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectTrueSeeing(), PC(), 3.0));
    DelayCommand(6.5, VShot("h_trueseeing"));
    DelayCommand(9.0, Clear("cai_h"));
}

// ---------------------------------------------------------------- i: time stop

void PTick()
{
    object m = GetModule();
    SetLocalInt(m, "p", GetLocalInt(m, "p") + 1);
    if (GetLocalInt(m, "p") < 40) DelayCommand(0.5, PTick());
}

void MTick()
{
    object m = GetModule();
    SetLocalInt(m, "m", GetLocalInt(m, "m") + 1);
    if (GetLocalInt(m, "m") < 40) DelayCommand(0.5, MTick());
}

string TypesOf(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        s += (s == "" ? "" : ",") + IntToString(GetEffectType(e));
        e = GetNextEffect(o);
    }
    return s == "" ? "-" : s;
}

void TRead(string k)
{
    object m = GetModule();
    object w = GetObjectByTag("cai_i");
    vector v = GetPosition(w);
    Log("i." + k + "|p=" + IntToString(GetLocalInt(m, "p")) + "|m=" + IntToString(GetLocalInt(m, "m")) + "|wx=" + FloatToString(v.x - GetLocalFloat(w, "X0"), 0, 1)
        + "|pc=" + TypesOf(PC()) + "|sec=" + IntToString(GetTimeSecond()));
}

// Runs on the player.
void TStart()
{
    DelayCommand(0.2, PTick());
    DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectTimeStop(), OBJECT_SELF, 6.0));
    DelayCommand(0.9, TRead("t0_9"));
    DelayCommand(1.5, TRead("t1_5"));
    DelayCommand(3.0, TRead("t3"));
    DelayCommand(5.0, TRead("t5"));
    DelayCommand(6.5, TRead("t6_5"));
    DelayCommand(8.0, TRead("t8"));
    DelayCommand(10.0, TRead("t10"));
    DelayCommand(2.0, VShot("i_timestop"));
}

void TimeStop()
{
    object w = Npc("nw_goblina", -8.0, 6.0, "cai_i", STANDARD_FACTION_MERCHANT);
    SetLocalFloat(w, "X0", GetPosition(w).x);
    AssignCommand(w, ActionMoveToLocation(Location(GetArea(w), GetPosition(w) + Vector(20.0, 0.0, 0.0), 0.0)));
    DelayCommand(0.2, MTick());
    AssignCommand(PC(), TStart());
    DelayCommand(5.0, TRead("m5"));
    DelayCommand(12.0, TRead("m12"));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetLocalLocation(GetModule(), "HOME", GetLocation(pc));
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 50.0));
    float t = 3.0;
    DelayCommand(t, Fear()); t += 17.0;
    DelayCommand(t, Home()); t += 1.0;
    DelayCommand(t, Confuse()); t += 19.0;
    DelayCommand(t, Home()); t += 1.0;
    DelayCommand(t, Charm(FALSE)); t += 18.0;
    DelayCommand(t, Home()); t += 1.0;
    DelayCommand(t, Charm(TRUE)); t += 14.0;
    DelayCommand(t, Home()); t += 1.0;
    DelayCommand(t, VCamera(pc, 90.0, 9.0, 55.0));
    DelayCommand(t, SanctuaryOnPlayer()); t += 11.0;
    DelayCommand(t, PlayerAttacksSanctuary(100)); t += 11.0;
    DelayCommand(t, PlayerAttacksSanctuary(1)); t += 11.0;
    DelayCommand(t, Melee(0)); t += 9.0;
    DelayCommand(t, Melee(1)); t += 9.0;
    DelayCommand(t, Melee(2)); t += 9.0;
    DelayCommand(t, Melee(3)); t += 9.0;
    DelayCommand(t, Home()); t += 1.0;
    DelayCommand(t, Spells(0)); t += 7.0;
    DelayCommand(t, Spells(1)); t += 7.0;
    DelayCommand(t, Spells(2)); t += 7.0;
    DelayCommand(t, Deaf()); t += 25.0;
    DelayCommand(t, Seeing()); t += 10.0;
    DelayCommand(t, TimeStop()); t += 14.0;
    DelayCommand(t, VDone());
}

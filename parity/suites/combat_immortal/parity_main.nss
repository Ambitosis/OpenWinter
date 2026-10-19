// An attacker whose target can't die (SetImmortal, plot), with and without the stock AI scripts. Found by the
// multiplayer harness (scene mp_delivery): a bandit and an immortal goblin with their default scripts attacking each
// other. Each case counts the attacks each side makes (its target's OnPhysicalAttacked, which then runs the stock
// nw_c2_default5 when the case keeps its scripts) and reads the attacker's action and attack target over time.
#include "pcombat_inc"

object Make(string sResRef, float fX, float fY, float fFacing, int nFaction, string sCase, string sSide, int bStrip)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(oArea, Vector(fX, fY, 0.0), fFacing));
    if (bStrip) PStrip(o);
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, nFaction);
    SetLocalString(o, "C", sCase);
    SetLocalString(o, "S", sSide);
    // Count the attacks on it, then run whatever OnPhysicalAttacked it had.
    SetLocalString(o, "ATK", GetEventScript(o, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED));
    SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_imm_atk");
    return o;
}

void Probe(object a, string sKey)
{
    PLogI(sKey + ".action", GetCurrentAction(a));
    PLogI(sKey + ".hastarget", GetIsObjectValid(GetAttackTarget(a)));
    PLogI(sKey + ".incombat", GetIsInCombat(a));
}

// An attack count. With the stock scripts the two are really enemies (Prelude's faction table, suite.json) and the AI
// answers a round earlier or later from run to run on nwserver, so those counts get a tolerance of one.
void Count(string sKey, int n, string sCase)
{
    if (GetStringLeft(sCase, 3) == "ai.") PLogF(sKey, IntToFloat(n), 1.0);
    else PLogI(sKey, n);
}

// Case n: attacker a (bandit) at one end of a 10 m cell, target b (goblin) 1.5 m away.
// nMode: 0 immortal target, 1 plot target, 2 immortal target at 1 HP, 3 mortal target (control, 200 HP).
void Case(int n, string sCase, int bStripA, int bStripB, int bMutual, int nMode)
{
    float x = IntToFloat(n % 6) * 12.0 + 6.0;
    float y = IntToFloat(n / 6) * 12.0 + 6.0;
    object a = Make("nw_bandit001", x, y, 0.0, STANDARD_FACTION_COMMONER, sCase, "a", bStripA);
    object b = Make("nw_goblina", x + 1.5, y, 180.0, STANDARD_FACTION_HOSTILE, sCase, "b", bStripB);
    SetImmortal(a, TRUE);
    if (nMode == 0 || nMode == 2) SetImmortal(b, TRUE);
    if (nMode == 1) SetPlotFlag(b, TRUE);
    if (nMode == 2) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(GetCurrentHitPoints(b) - 1), b);
    if (nMode == 3) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(200), b);
    DelayCommand(0.5, AssignCommand(a, ActionAttack(b)));
    if (bMutual) DelayCommand(0.5, AssignCommand(b, ActionAttack(a)));
    DelayCommand(9.5, Count(sCase + ".a.attacks9", PGet(sCase + ".b.attacked"), sCase));
    DelayCommand(9.5, Count(sCase + ".b.attacks9", PGet(sCase + ".a.attacked"), sCase));
    DelayCommand(3.0, Probe(a, sCase + ".a.t3"));
    DelayCommand(9.0, Probe(a, sCase + ".a.t9"));
    DelayCommand(24.0, Probe(a, sCase + ".a.t24"));
    DelayCommand(24.5, Count(sCase + ".a.attacks24", PGet(sCase + ".b.attacked"), sCase));
    DelayCommand(24.5, Count(sCase + ".b.attacks24", PGet(sCase + ".a.attacked"), sCase));
    DelayCommand(24.5, PLogI(sCase + ".b.hp1", GetCurrentHitPoints(b) == 1));
}

void Setup()
{
    // As in mp_delivery: both keep their stock scripts, both immortal, attacking each other.
    Case(0, "ai.mutual", FALSE, FALSE, TRUE, 0);
    Case(1, "ai.mutual.b", FALSE, FALSE, TRUE, 0);
    // Stock-scripted attacker, passive (stripped) immortal target.
    Case(2, "ai.passive", FALSE, TRUE, FALSE, 0);
    // Stock-scripted attacker, stock-scripted target left to answer by itself.
    Case(3, "ai.answer", FALSE, FALSE, FALSE, 0);
    // Scripted (stripped) attacker: immortal, plot, immortal at 1 HP, mortal with 200 temporary hit points.
    Case(4, "bare.immortal", TRUE, TRUE, FALSE, 0);
    Case(5, "bare.plot", TRUE, TRUE, FALSE, 1);
    Case(6, "bare.onehp", TRUE, TRUE, FALSE, 2);
    Case(7, "bare.mortal", TRUE, TRUE, FALSE, 3);
    // Stock scripts: plot target, immortal at 1 HP, mortal control.
    Case(8, "ai.plot", FALSE, FALSE, TRUE, 1);
    Case(9, "ai.onehp", FALSE, FALSE, TRUE, 2);
    Case(10, "ai.mortal", FALSE, FALSE, TRUE, 3);
    // Both stripped, attacking each other, both immortal.
    Case(11, "bare.mutual", TRUE, TRUE, TRUE, 0);
}

void main()
{
    PClockStart(30.0);
    DelayCommand(1.0, Setup());
    DelayCommand(27.0, PDone());
}

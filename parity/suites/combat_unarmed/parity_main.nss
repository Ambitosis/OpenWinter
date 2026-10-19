// Does an unarmed attack draw an attack of opportunity? (A player attacking an armed goblin bare-handed drew one a
// round in the original: visual scene difficultypc.) nw_drowfight010 attacks a bandit holding a longsword or
// nothing, bare-handed or with a dagger, and bare-handed with Improved Unarmed Strike. Counts the attacks the passive
// target makes on its attacker (attacks of opportunity: it never attacks otherwise). PPair makes the pairs enemies
// and the suite runs with Prelude's faction table; without them the pairs were friends and no target attacked.
#include "pcombat_inc"

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Pair(string g, int bArmedTarget, int bUnarmed, int nFeat)
{
    object a = PPair(g, "nw_drowfight010", "nw_bandit001");
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(2));
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    PUnequip(a, INVENTORY_SLOT_LEFTHAND);
    if (!bUnarmed) PEquip(a, "nw_wswdg001", INVENTORY_SLOT_RIGHTHAND);
    if (nFeat >= 0) Perm(a, EffectBonusFeat(nFeat));
    object t = GetLocalObject(a, "T");
    PUnequip(t, INVENTORY_SLOT_RIGHTHAND);
    PUnequip(t, INVENTORY_SLOT_LEFTHAND);
    if (bArmedTarget) PEquip(t, "nw_wswls001", INVENTORY_SLOT_RIGHTHAND);
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_un_atk");
    return a;
}

void Setup()
{
    int i;
    for (i = 0; i < 3; i++)
    {
        Pair("bare.vs.armed", TRUE, TRUE, -1);
        Pair("bare.vs.bare", FALSE, TRUE, -1);
        Pair("dagger.vs.armed", TRUE, FALSE, -1);
        Pair("bare.ius.vs.armed", TRUE, TRUE, FEAT_IMPROVED_UNARMED_STRIKE);
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "bare.vs.armed bare.vs.bare dagger.vs.armed bare.ius.vs.armed ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PLogMean(g + ".attacks.on.attacker", IntToFloat(PGet(g + ".back.atk")), 3, 1.0, 0.7);
        PLogMean(g + ".attacks", IntToFloat(PGet(g + ".atk")), 3, 1.5, 1.0);
        s = GetSubString(s, sp + 1, 1000);
    }
    PDone();
}

void main()
{
    PClockStart(40.0);
    DelayCommand(1.0, Setup());
    DelayCommand(25.0, Report());
}

// Game difficulty between creatures that aren't players (suites difficulty_npc1 and difficulty_npc4 run this at
// difficulty 1 and 4): GetGameDifficulty, scripted damage from a creature and from the module, and weapon hits and
// critical hits (a keen longsword with Improved Critical threatens on 12-20; with Str +9 a critical hit always
// deals more than a normal hit).
#include "pcombat_inc"

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Dummy(float fX, float fY)
{
    object o = PSpawn("nw_bandit001", fX, fY);
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    Perm(o, EffectTemporaryHitpoints(500));
    return o;
}

void Hurt(object oSource, object oTarget, int nDamage, int nType)
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(nDamage, nType), oTarget);
}

void LogLoss(object o, string sKey)
{
    PLogI(sKey, GetLocalInt(o, "HP0") - GetCurrentHitPoints(o));
}

// oSource (OBJECT_INVALID: the module) deals nDamage of nType to a fresh dummy; the hit points it lost are logged.
void Scripted(string sKey, object oSource, int nDamage, int nType, float fX)
{
    object t = Dummy(fX, 70.0);
    DelayCommand(0.5, SetLocalInt(t, "HP0", GetCurrentHitPoints(t)));
    if (GetIsObjectValid(oSource)) DelayCommand(1.0, AssignCommand(oSource, Hurt(oSource, t, nDamage, nType)));
    else DelayCommand(1.0, AssignCommand(GetModule(), Hurt(GetModule(), t, nDamage, nType)));
    DelayCommand(2.0, LogLoss(t, sKey));
}

void Setup()
{
    PLogI("difficulty", GetGameDifficulty());
    int i;
    for (i = 0; i < 4; i++)
    {
        object a = PPair("crit", "nw_drowfight010", "nw_bandit001");
        SetBaseAttackBonus(1, a);
        Perm(a, EffectModifyAttacks(5));
        Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
        Perm(a, EffectAttackIncrease(20));
        PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
        object w = PEquip(a, "nw_wswls001", INVENTORY_SLOT_RIGHTHAND);
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), w);
        Perm(a, EffectBonusFeat(FEAT_IMPROVED_CRITICAL_LONG_SWORD));
        SetLocalInt(GetModule(), "crit.critat", 18);
    }
    object src = PSpawn("nw_drowfight010", 5.0, 75.0);
    ChangeToStandardFaction(src, STANDARD_FACTION_HOSTILE);
    Scripted("scripted.npc.20", src, 20, DAMAGE_TYPE_MAGICAL, 5.0);
    Scripted("scripted.npc.7", src, 7, DAMAGE_TYPE_MAGICAL, 10.0);
    Scripted("scripted.npc.1", src, 1, DAMAGE_TYPE_MAGICAL, 15.0);
    Scripted("scripted.module.20", OBJECT_INVALID, 20, DAMAGE_TYPE_MAGICAL, 20.0);
    PStartAll(1.0);
}

void Report()
{
    PLogRate("crit.critrate", PGet("crit.crit"), PGet("crit.atk"), 0.01);
    PReportHits("crit");
    // Damage per hit (the largest is left out: it depends on rare rolls).
    int n = PGet("crit.hit");
    PLogI("crit.min", PGet("crit.dmin"));
    PLogMean("crit.mean", IntToFloat(PGet("crit.dmg")), n, PSd(PGet("crit.dmg"), PGet("crit.dsq"), n), 0.05);
    PDone();
}

void DifficultyMain()
{
    PClockStart(60.0);
    DelayCommand(1.0, Setup());
    DelayCommand(45.0, Report());
}

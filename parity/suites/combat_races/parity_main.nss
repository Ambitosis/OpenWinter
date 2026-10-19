// Which racial types ignore critical hits and sneak attacks. Standard monsters of every racial type are attacked by
// nw_drowfight010 with +20 to hit, Str 29 and a +5 light mace (1d6+14: beats damage reduction up to +5): keen with
// Improved Critical (threat 15-20), where a critical hit deals about twice a normal hit, or with 10d6 sneak attack
// (the passive target is flat-footed). Logs each target's racial type and whether such hits landed, judged by the
// spread between its smallest and largest hit (percentage immunities scale both).
#include "pcombat_inc"

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

// Sneak Attack 10d6 is feat 353 (as in combat_sneak).
string TARGETS = "nw_skeleton nw_zombie01 nw_golclay nw_golflesh nw_air nw_fire nw_spidgiant nw_badger nw_wolf nw_imp nw_pixie nw_umberhulk nw_bandit001 nw_goblina nw_orca nw_gnthill nw_wererat nw_dryad nw_drgwhite001 nw_drgred001 nw_drgbrass001 nw_shmastif nw_grayooze nw_ochrejellysml nw_kobold001 nw_mummy nw_direwolf nw_worg ";

object Pair(string g, string sTarget, int bSneak)
{
    object a = PPair(g, "nw_drowfight010", sTarget);
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(5));
    Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 12));
    Perm(a, EffectAttackIncrease(20));
    object t = GetLocalObject(a, "T");
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    PUnequip(a, INVENTORY_SLOT_LEFTHAND);
    object w = PEquip(a, "nw_wblml001", INVENTORY_SLOT_RIGHTHAND);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyEnhancementBonus(5), w);
    if (bSneak) Perm(a, EffectBonusFeat(353));
    else
    {
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyKeen(), w);
        Perm(a, EffectBonusFeat(FEAT_IMPROVED_CRITICAL_LIGHT_MACE));
        Perm(t, EffectImmunity(IMMUNITY_TYPE_SNEAK_ATTACK));
    }
    Perm(t, EffectTemporaryHitpoints(1000));
    SetLocalObject(GetModule(), g, t);
    return a;
}

void Setup()
{
    string s = TARGETS;
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string r = GetStringLeft(s, sp);
        Pair("crit." + r, r, FALSE);
        Pair("sneak." + r, r, TRUE);
        s = GetSubString(s, sp + 1, 1000);
    }
    PStartAll(1.0);
}

void Report()
{
    string s = TARGETS;
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string r = GetStringLeft(s, sp);
        object t = GetLocalObject(GetModule(), "crit." + r);
        if (!GetIsObjectValid(t)) PLogI(r + ".race", -1);
        else
        {
            PLogI(r + ".race", GetRacialType(t));
            int lo = PGet("crit." + r + ".dmin"), hi = PGet("crit." + r + ".dmax");
            PLogI(r + ".crit.hits", PGet("crit." + r + ".hit") > 3);
            PLogI(r + ".crit.seen", hi > 0 && hi * 10 >= lo * 17);
            lo = PGet("sneak." + r + ".dmin"); hi = PGet("sneak." + r + ".dmax");
            PLogI(r + ".sneak.hits", PGet("sneak." + r + ".hit") > 3);
            PLogI(r + ".sneak.seen", hi > 0 && hi >= lo + 12);
        }
        s = GetSubString(s, sp + 1, 1000);
    }
    PDone();
}

void main()
{
    PClockStart(40.0);
    DelayCommand(1.0, Setup());
    DelayCommand(33.0, Report());
}

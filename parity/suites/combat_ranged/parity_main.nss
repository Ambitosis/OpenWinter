// Ranged attacks: Dexterity to hit, no Strength to damage (Mighty adds it up to its rating; bows and
// slings take a Strength penalty, crossbows don't), thrown weapons with full Strength, ammunition used
// up, Unlimited Ammunition, crossbows, Point Blank Shot.
// nw_drowfight010 (Dex 19) shoots a flat-footed nw_bandit001 (AC 11) with deflection, 8 m away.
#include "pcombat_inc"

const string FIGHTER = "nw_drowfight010";
const string TARGET = "nw_bandit001";

void Perm(object o, effect e)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

object Archer(string g, string sWeapon, string sAmmo, int nAmmoSlot, int nAmmo = 99, float fDist = 8.0, int nAC = 8)
{
    object a = PPair(g, FIGHTER, TARGET, fDist);
    SetBaseAttackBonus(1, a);
    Perm(a, EffectModifyAttacks(1));
    Perm(GetLocalObject(a, "T"), EffectACIncrease(nAC, AC_DEFLECTION_BONUS));
    Perm(GetLocalObject(a, "T"), EffectImmunity(IMMUNITY_TYPE_CRITICAL_HIT));
    PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
    PUnequip(a, INVENTORY_SLOT_LEFTHAND);
    object w = PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND, sAmmo == "" ? nAmmo : 1);
    SetLocalObject(a, "W", w);
    if (sAmmo != "") SetLocalObject(a, "AMMO", PEquip(a, sAmmo, nAmmoSlot, nAmmo));
    return a;
}

void Setup()
{
    int i;
    for (i = 0; i < 2; i++)
    {
        // Longbow 1d8 (no Strength), with Dex +10, Str +10, Mighty +3 and Str +10, Str -6.
        Archer("bow", "nw_wbwln001", "nw_wamar001", INVENTORY_SLOT_ARROWS);
        Perm(Archer("bow.dex", "nw_wbwln001", "nw_wamar001", INVENTORY_SLOT_ARROWS), EffectAbilityIncrease(ABILITY_DEXTERITY, 10));
        Perm(Archer("bow.str", "nw_wbwln001", "nw_wamar001", INVENTORY_SLOT_ARROWS), EffectAbilityIncrease(ABILITY_STRENGTH, 10));
        object a = Archer("bow.mighty", "nw_wbwln001", "nw_wamar001", INVENTORY_SLOT_ARROWS);
        Perm(a, EffectAbilityIncrease(ABILITY_STRENGTH, 10));
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyMaxRangeStrengthMod(3), GetLocalObject(a, "W"));
        Perm(Archer("bow.weak", "nw_wbwln001", "nw_wamar001", INVENTORY_SLOT_ARROWS), EffectAbilityDecrease(ABILITY_STRENGTH, 12));
        Perm(Archer("xbow.weak", "nw_wbwxl001", "nw_wambo001", INVENTORY_SLOT_BOLTS), EffectAbilityDecrease(ABILITY_STRENGTH, 12));
        Perm(Archer("sling.weak", "nw_wbwsl001", "nw_wambu001", INVENTORY_SLOT_BULLETS), EffectAbilityDecrease(ABILITY_STRENGTH, 12));
        Perm(Archer("sling.str", "nw_wbwsl001", "nw_wambu001", INVENTORY_SLOT_BULLETS), EffectAbilityIncrease(ABILITY_STRENGTH, 10));
        // Darts (1d3) and throwing axes (1d6) add Strength.
        Perm(Archer("darts.str", "nw_wthdt001", "", 0), EffectAbilityIncrease(ABILITY_STRENGTH, 10));
        Perm(Archer("axes.str", "nw_wthax001", "", 0), EffectAbilityIncrease(ABILITY_STRENGTH, 10));
        // Light crossbow (1d8): one attack a round.
        a = Archer("xbow", "nw_wbwxl001", "nw_wambo001", INVENTORY_SLOT_BOLTS);
        SetBaseAttackBonus(3, a);
        // Ammunition: 20 arrows, and 5 arrows with Unlimited Ammunition on the bow.
        Archer("ammo20", "nw_wbwln001", "nw_wamar001", INVENTORY_SLOT_ARROWS, 20);
        a = Archer("unlimited", "nw_wbwln001", "nw_wamar001", INVENTORY_SLOT_ARROWS, 5);
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyUnlimitedAmmo(), GetLocalObject(a, "W"));
        // Point Blank Shot within 5 m (and at 8 m), and shooting a target 1.5 m away.
        Perm(Archer("pbs.4m", "nw_wbwln001", "nw_wamar001", INVENTORY_SLOT_ARROWS, 99, 4.0), EffectBonusFeat(FEAT_POINT_BLANK_SHOT));
        Perm(Archer("pbs.8m", "nw_wbwln001", "nw_wamar001", INVENTORY_SLOT_ARROWS, 99, 8.0), EffectBonusFeat(FEAT_POINT_BLANK_SHOT));
    }
    PStartAll(1.0);
}

int Stack(object a)
{
    object ammo = GetLocalObject(a, "AMMO");
    if (!GetIsObjectValid(ammo)) ammo = GetItemInSlot(INVENTORY_SLOT_ARROWS, a);
    return GetIsObjectValid(ammo) ? GetItemStackSize(ammo) : 0;
}

void Report()
{
    string s = "bow bow.dex bow.str bow.mighty bow.weak xbow.weak sling.weak sling.str darts.str axes.str xbow pbs.4m pbs.8m ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PReportHits(g);
        PReportDamage(g);
        s = GetSubString(s, sp + 1, 1000);
    }
    PReportRounds("xbow");
    PReportRounds("bow");
    // Ammunition left, distance kept, and attacks made by archers with little ammunition.
    object o = GetFirstObjectInArea(GetObjectByTag("PARITY_AREA"));
    while (GetIsObjectValid(o))
    {
        string g = GetLocalString(o, "G");
        if (GetIsObjectValid(GetLocalObject(o, "T")))
        {
            if (g == "ammo20" || g == "unlimited") PAdd(g + ".left", Stack(o));
        }
        o = GetNextObjectInArea(GetObjectByTag("PARITY_AREA"));
    }
    PLogI("ammo20.left", PGet("ammo20.left"));
    PLogI("ammo20.attacks", PGet("ammo20.atk"));
    PLogI("unlimited.left", PGet("unlimited.left"));
    PLogI("unlimited.more.than.5", PGet("unlimited.atk") > 10);
    PDone();
}

void main()
{
    PClockStart(85.0);
    DelayCommand(1.0, Setup());
    DelayCommand(70.0, Report());
}

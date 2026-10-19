// Impact effects (VFX_IMP_*) on three creatures, three at a time: bursts, sparks, rising and falling motes.
#include "visual_inc"

void Imp(object o, int nVfx)
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(nVfx), o);
}

void Three(object a, object b, object c, int n1, int n2, int n3)
{
    Imp(a, n1);
    Imp(b, n2);
    Imp(c, n3);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    object a = VSpawn(oPC, "nw_bandit001", -3.0, 4.0, 270.0);
    object b = VSpawn(oPC, "nw_bandit001",  0.0, 4.0, 270.0);
    object c = VSpawn(oPC, "nw_bandit001",  3.0, 4.0, 270.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 9.0, 65.0));
    DelayCommand(5.0, Three(a, b, c, VFX_IMP_FLAME_M, VFX_IMP_FROST_L, VFX_IMP_ACID_L));
    DelayCommand(5.5, VShot("a_flame_frost_acid"));
    DelayCommand(9.0, Three(a, b, c, VFX_IMP_HEALING_X, VFX_IMP_HOLY_AID, VFX_IMP_DEATH));
    DelayCommand(9.8, VShot("b_heal_aid_death"));
    DelayCommand(13.0, Three(a, b, c, VFX_IMP_LIGHTNING_M, VFX_IMP_SUNSTRIKE, VFX_IMP_NEGATIVE_ENERGY));
    DelayCommand(13.4, VShot("c_lightning_sun_negative"));
    DelayCommand(17.0, Three(a, b, c, VFX_IMP_ELEMENTAL_PROTECTION, VFX_IMP_DIVINE_STRIKE_HOLY, VFX_IMP_MAGIC_PROTECTION));
    DelayCommand(17.8, VShot("d_elemental_divine_magic"));
    DelayCommand(21.0, VDone());
}

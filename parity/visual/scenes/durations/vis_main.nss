// Lasting effects (VFX_DUR_*) on six creatures in two rows: globes, shields, auras, skins and holds.
#include "visual_inc"

void Dur(object o, int nVfx)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(nVfx), o);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    object a = VSpawn(oPC, "nw_bandit001", -4.0, 3.0, 270.0);
    object b = VSpawn(oPC, "nw_bandit001",  0.0, 3.0, 270.0);
    object c = VSpawn(oPC, "nw_bandit001",  4.0, 3.0, 270.0);
    object d = VSpawn(oPC, "nw_bandit001", -4.0, 8.0, 270.0);
    object e = VSpawn(oPC, "nw_bandit001",  0.0, 8.0, 270.0);
    object f = VSpawn(oPC, "nw_bandit001",  4.0, 8.0, 270.0);
    Dur(a, VFX_DUR_GLOBE_INVULNERABILITY);
    Dur(b, VFX_DUR_PROT_SHADOW_ARMOR);
    Dur(c, VFX_DUR_ENTANGLE);
    Dur(d, VFX_DUR_PARALYZED);
    Dur(e, VFX_DUR_DEATH_ARMOR);
    Dur(f, VFX_DUR_MIND_AFFECTING_DOMINATED);
    DelayCommand(4.0, VCamera(oPC, 90.0, 13.0, 60.0));
    DelayCommand(7.0, VShot("a_high"));
    DelayCommand(8.0, VCamera(oPC, 90.0, 9.0, 80.0));
    DelayCommand(10.0, VShot("b_low"));
    DelayCommand(12.0, VDone());
}

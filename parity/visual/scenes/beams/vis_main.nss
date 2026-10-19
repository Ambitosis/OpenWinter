// Beams (VFX_BEAM_*) from three mages to three creatures, two sets.
#include "visual_inc"

void Beam(object oFrom, object oTo, int nBeam)
{
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectBeam(nBeam, oFrom, BODY_NODE_HAND), oTo, 3.0);
}

void Three(object m1, object m2, object m3, object a, object b, object c, int n1, int n2, int n3)
{
    Beam(m1, a, n1);
    Beam(m2, b, n2);
    Beam(m3, c, n3);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    object m1 = VSpawn(oPC, "nw_elfmage001", -4.0, 2.0, 90.0);
    object m2 = VSpawn(oPC, "nw_elfmage001",  0.0, 2.0, 90.0);
    object m3 = VSpawn(oPC, "nw_elfmage001",  4.0, 2.0, 90.0);
    object a = VSpawn(oPC, "nw_bandit001", -4.0, 9.0, 270.0);
    object b = VSpawn(oPC, "nw_bandit001",  0.0, 9.0, 270.0);
    object c = VSpawn(oPC, "nw_bandit001",  4.0, 9.0, 270.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 10.0, 55.0));
    DelayCommand(5.0, Three(m1, m2, m3, a, b, c, VFX_BEAM_LIGHTNING, VFX_BEAM_FIRE, VFX_BEAM_COLD));
    DelayCommand(6.0, VShot("a_lightning_fire_cold"));
    DelayCommand(10.0, Three(m1, m2, m3, a, b, c, VFX_BEAM_HOLY, VFX_BEAM_EVIL, VFX_BEAM_MIND));
    DelayCommand(11.0, VShot("b_holy_evil_mind"));
    DelayCommand(15.0, VDone());
}

// Ground-aligned particles: rings, glyphs and spots that lie flat on the floor or around a creature
// (emitter render modes Billboard_to_World_Z, Billboard_to_Local_Z and Aligned_to_World_Z).
#include "visual_inc"

void Glow(object o, int nVfx)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(nVfx), o);
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    object a = VSpawn(oPC, "nw_bandit001", -4.0, 6.0, 270.0);
    object b = VSpawn(oPC, "nw_bandit001",  0.0, 6.0, 270.0);
    object c = VSpawn(oPC, "nw_bandit001",  4.0, 6.0, 270.0);
    Glow(a, VFX_DUR_SPELLTURNING);
    Glow(b, 249); // VFX_DUR_MAGIC_RESISTANCE: glyph dome
    Glow(c, VFX_DUR_FREEDOM_OF_MOVEMENT);
    Glow(oPC, 495); // VFX_DUR_PROT_EPIC_ARMOR: flat ring
    DelayCommand(3.0, VCamera(oPC, 90.0, 12.0, 62.0));
    DelayCommand(6.0, VShot("a_rings_high"));
    DelayCommand(7.0, VCamera(oPC, 90.0, 10.0, 80.0));
    DelayCommand(9.0, VShot("b_rings_low"));
    DelayCommand(10.0, VCamera(oPC, 90.0, 14.0, 50.0));
    DelayCommand(11.0, VFxAt(oPC, 464, 0.0, 12.0)); // VFX_FNF_HELLBALL: ground ring waves
    DelayCommand(11.8, VShot("c_hellball_1"));
    DelayCommand(12.8, VShot("c_hellball_2"));
    DelayCommand(16.0, VDone());
}

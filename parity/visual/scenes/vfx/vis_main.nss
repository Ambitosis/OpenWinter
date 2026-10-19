// Common spell effects: durational glows on creatures, a fireball, magic missiles and a lightning bolt.
#include "visual_inc"

void Glow(object o, int nVfx)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(nVfx), o);
}

void Missiles(object oFrom, object oTo)
{
    AssignCommand(oFrom, ActionCastFakeSpellAtObject(SPELL_MAGIC_MISSILE, oTo));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    object a = VSpawn(oPC, "nw_bandit001", -4.0, 6.0, 270.0);
    object b = VSpawn(oPC, "nw_bandit001",  0.0, 6.0, 270.0);
    object c = VSpawn(oPC, "nw_bandit001",  4.0, 6.0, 270.0);
    object w = VSpawn(oPC, "nw_elfmage001", 0.0, -1.0, 90.0);
    Glow(a, VFX_DUR_GLOW_LIGHT_BLUE);
    Glow(b, VFX_DUR_PROT_STONESKIN);
    Glow(c, VFX_DUR_ELEMENTAL_SHIELD);
    Glow(oPC, VFX_DUR_PROTECTION_GOOD_MAJOR);
    DelayCommand(3.0, VCamera(oPC, 90.0, 12.0, 62.0));
    DelayCommand(6.0, VShot("a_glows"));
    // Magic missile from the mage at the middle bandit.
    DelayCommand(8.0, Missiles(w, b));
    DelayCommand(9.4, VShot("b_missile_1"));
    DelayCommand(10.0, VShot("b_missile_2"));
    // A fireball behind the row.
    DelayCommand(14.0, VFxAt(oPC, VFX_FNF_FIREBALL, 0.0, 10.0));
    DelayCommand(14.4, VShot("c_fireball_1"));
    DelayCommand(15.2, VShot("c_fireball_2"));
    DelayCommand(19.0, VFxAt(oPC, VFX_FNF_SUMMON_UNDEAD, 0.0, 10.0));
    DelayCommand(19.8, VShot("d_summon"));
    DelayCommand(23.0, VDone());
}

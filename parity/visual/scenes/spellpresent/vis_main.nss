#include "visual_inc"

void Mirv(object caster, object target, int vfx)
{
    AssignCommand(caster, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(vfx), target));
}

void Summon(object pc)
{
    AssignCommand(pc, SummonFamiliar());
    WriteTimestampedLogEntry("VISUAL|note|summon.called");
}

void Familiar(string label, object pc)
{
    object f = GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc);
    WriteTimestampedLogEntry("VISUAL|note|" + label + "|" + IntToString(GetIsObjectValid(f)));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    object target = VSpawn(pc, "nw_bandit001", 0.0, 10.0, 270.0);
    SetCommandable(FALSE, target);
    DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 55.0));
    DelayCommand(4.0, Mirv(pc, target, VFX_IMP_MIRV));
    DelayCommand(4.45, VShot("a_magic_missile"));
    DelayCommand(8.0, Mirv(pc, target, VFX_IMP_MIRV_FLAME));
    DelayCommand(8.45, VShot("b_flame_arrow"));
    DelayCommand(12.0, Mirv(pc, target, 245)); // visualeffects.2da: acid-arrow MIRV
    DelayCommand(12.45, VShot("c_acid_arrow"));
    DelayCommand(16.0, AssignCommand(pc, ActionCastSpellAtObject(SPELL_BLESS, pc, METAMAGIC_ANY, TRUE)));
    DelayCommand(16.7, VShot("d_bless_conjure"));
    DelayCommand(18.8, VShot("e_bless_impact"));
    DelayCommand(23.0, Summon(pc));
    DelayCommand(23.1, Familiar("familiar.0.1", pc));
    DelayCommand(23.7, VShot("f_summon"));
    DelayCommand(24.0, Familiar("familiar.1.0", pc));
    DelayCommand(25.0, Familiar("familiar.2.0", pc));
    DelayCommand(26.0, Familiar("familiar.3.0", pc));
    DelayCommand(26.5, VShot("g_familiar"));
    DelayCommand(30.0, VDone());
}

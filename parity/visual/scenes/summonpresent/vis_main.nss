#include "visual_inc"

void Summon(object pc)
{
    AssignCommand(pc, SummonFamiliar());
}

void Note(object pc)
{
    object f = GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc);
    vector p = GetPosition(f);
    WriteTimestampedLogEntry("VISUAL|note|familiar|" + GetName(f) + "|" + IntToString(GetAppearanceType(f))
        + "|" + FloatToString(p.x) + "," + FloatToString(p.y) + "," + FloatToString(p.z));
    SetCommandable(FALSE, f);
}

void Reference(object pc, int vfx)
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(vfx), pc);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    DelayCommand(1.0, VCamera(pc, 90.0, 7.0, 65.0));
    DelayCommand(4.0, Summon(pc));
    DelayCommand(4.1, Note(pc));
    DelayCommand(4.7, VShot("a_familiar"));
    DelayCommand(9.0, Reference(pc, VFX_FNF_SUMMON_MONSTER_1));
    DelayCommand(9.7, VShot("b_summon1"));
    DelayCommand(14.0, Reference(pc, VFX_FNF_SUMMON_MONSTER_2));
    DelayCommand(14.7, VShot("c_summon2"));
    DelayCommand(19.0, Reference(pc, VFX_IMP_UNSUMMON));
    DelayCommand(19.7, VShot("d_unsummon"));
    DelayCommand(24.0, AssignCommand(pc, SummonAnimalCompanion()));
    DelayCommand(24.7, VShot("e_companion"));
    DelayCommand(29.0, VDone());
}

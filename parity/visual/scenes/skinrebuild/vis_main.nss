#include "visual_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    DelayCommand(1.0, VCamera(pc, 270.0, 7.0, 65.0));
    // A head-attached visual only: no confusion or other mechanical condition.
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY,
        EffectVisualEffect(VFX_DUR_MIND_AFFECTING_NEGATIVE), pc, 10.0));
    DelayCommand(3.0, VShot("a_head_before"));
    DelayCommand(4.0, AssignCommand(pc, ActionCastSpellAtObject(3, pc, METAMAGIC_NONE, TRUE)));
    int head = GetCreatureBodyPart(CREATURE_PART_HEAD, pc);
    // Force real appearance messages during both conjuring and recovery; Barkskin alone
    // replaces materials, which does not rebuild the skeleton.
    DelayCommand(5.0, SetCreatureBodyPart(CREATURE_PART_HEAD, head == 1 ? 2 : 1, pc));
    DelayCommand(6.25, SetCreatureBodyPart(CREATURE_PART_HEAD, head, pc));
    DelayCommand(5.25, VShot("b_conjure"));
    DelayCommand(6.10, VShot("c_release"));
    DelayCommand(6.60, VShot("d_recovery"));
    DelayCommand(8.0, VShot("e_head_after"));
    DelayCommand(11.0, VShot("f_head_late"));
    DelayCommand(14.0, VShot("g_expired"));
    DelayCommand(16.0, VDone());
}

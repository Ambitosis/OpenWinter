// The chant must outlive the conjure-to-cast transition. No input or combat AI needed.
#include "visual_inc"

void Cast(object pc)
{
    WriteTimestampedLogEntry("VISUAL|note|cast-start");
    AssignCommand(pc, ActionCastFakeSpellAtObject(SPELL_BLESS_WEAPON, pc));
}

void Cancel(object pc)
{
    WriteTimestampedLogEntry("VISUAL|note|cancel-conjure");
    AssignCommand(pc, ClearAllActions());
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    DelayCommand(1.0, VCamera(pc, 90.0, 7.0, 65.0));
    DelayCommand(4.0, Cast(pc));
    DelayCommand(8.5, VShot("a_completed"));
    DelayCommand(11.0, Cast(pc));
    DelayCommand(11.8, Cancel(pc));
    DelayCommand(14.0, VShot("b_cancelled"));
    DelayCommand(17.0, Cast(pc));
    DelayCommand(22.0, VShot("c_completed_again"));
    DelayCommand(24.0, VDone());
}

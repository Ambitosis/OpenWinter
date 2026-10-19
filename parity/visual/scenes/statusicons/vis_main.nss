#include "visual_inc"
void Buff(object pc) {
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(2, AC_DEFLECTION_BONUS), pc, 12.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectAttackIncrease(1), pc, 12.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectAbilityIncrease(ABILITY_STRENGTH, 2), pc, 12.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectIcon(25), pc, 12.0);
}
void main() {
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    DelayCommand(2.0, VCamera(pc,90.0,8.0,60.0));
    DelayCommand(4.0, WriteTimestampedLogEntry("VISUAL|key|67|tap"));
    DelayCommand(6.0, VShot("a_before"));
    DelayCommand(8.0, Buff(pc));
    DelayCommand(10.0, VShot("b_buffs"));
    DelayCommand(12.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSavingThrowDecrease(SAVING_THROW_ALL, 2), pc, 6.0));
    DelayCommand(14.0, VShot("c_debuff"));
    DelayCommand(23.0, VShot("d_expired"));
    DelayCommand(25.0, VDone());
}

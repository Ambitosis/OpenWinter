#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }

void State(object pc, string label)
{
    Note(label + " action=" + IntToString(GetCurrentAction(pc))
        + " hp=" + IntToString(GetCurrentHitPoints(pc))
        + " slots=" + IntToString(GetHasSpell(SPELL_MAGIC_MISSILE, pc)));
}

void Cast(object pc, string label)
{
    AssignCommand(pc, ClearAllActions());
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectHeal(100), pc);
    SetMemorizedSpell(pc, CLASS_TYPE_WIZARD, 1, 0, SPELL_MAGIC_MISSILE, TRUE);
    Note(label + " start");
    AssignCommand(pc, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, pc, METAMAGIC_NONE, FALSE));
    DelayCommand(1.0, State(pc, label + " one"));
    DelayCommand(3.0, State(pc, label + " three"));
}

void Cancel(object pc)
{
    Note("cancel now");
    AssignCommand(pc, ClearAllActions());
}

void Damage(object pc)
{
    Note("damage now");
    SetPlotFlag(pc, FALSE);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(20, DAMAGE_TYPE_DIVINE), pc);
    DelayCommand(0.2, SetPlotFlag(pc, TRUE));
}

void Failure(object pc)
{
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSpellFailure(100), pc, 5.0);
    Cast(pc, "failure");
}

void Reference(object pc, string sound)
{
    Note("reference " + sound);
    AssignCommand(pc, PlaySound(sound));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    SetImmortal(pc, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillDecrease(SKILL_CONCENTRATION, 50), pc);
    DelayCommand(1.0, VCamera(pc, 90.0, 7.0, 65.0));
    DelayCommand(4.0, Cast(pc, "normal"));
    DelayCommand(10.0, Cast(pc, "cancel"));
    DelayCommand(10.8, Cancel(pc));
    DelayCommand(16.0, Cast(pc, "damage"));
    DelayCommand(16.8, Damage(pc));
    DelayCommand(22.0, Failure(pc));
    DelayCommand(25.5, VShot("failure_feedback"));
    DelayCommand(29.0, Reference(pc, "gui_failspell"));
    DelayCommand(34.0, Reference(pc, "sff_spellfail"));
    DelayCommand(39.0, Reference(pc, "vs_chant_evoc_lf"));
    DelayCommand(44.0, VDone());
}

#include "visual_inc"
void Aura(object actor, int row) { ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectVisualEffect(row), actor, 3.0); }
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc); VHidePC(pc);
    object actor = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    SetEventScript(actor, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    SetCreatureAppearanceType(actor, 31);
    WriteTimestampedLogEntry("VISUAL|note|appearance." + IntToString(GetAppearanceType(actor)) + ".race." + Get2DAString("appearance", "RACE", 31));
    DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 55.0));
    DelayCommand(3.0, VShot("baseline_far"));
    DelayCommand(4.0, Aura(actor, 548));
    DelayCommand(5.0, VShot("red_far"));
    DelayCommand(8.0, Aura(actor, 553));
    DelayCommand(9.0, VShot("white_far"));
    DelayCommand(12.0, VCamera(pc, 90.0, 7.0, 55.0));
    DelayCommand(14.0, VShot("baseline_near"));
    DelayCommand(15.0, Aura(actor, 548));
    DelayCommand(16.0, VShot("red_near"));
    DelayCommand(19.0, VShot("expired_near"));
    DelayCommand(20.0, VDone());
}

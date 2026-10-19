#include "visual_inc"

void Start(object actor, int row, float duration)
{
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectVisualEffect(row), actor, duration);
    WriteTimestampedLogEntry("VISUAL|note|row." + IntToString(row) + ".valid." + IntToString(GetIsObjectValid(actor)));
}
void Freeze(object actor)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(9), actor);
}
void Burst(string name, float duration)
{
    WriteTimestampedLogEntry("VISUAL|burst|" + name + "|" + FloatToString(duration) + "|8");
}
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    if (Get2DAString("progfx", "Param7", 0) != "500"
        || Get2DAString("visualeffects", "Label", 0) != "Authored0")
    {
        WriteTimestampedLogEntry("VISUAL|note|tables.valid.0");
        VDone();
        return;
    }
    WriteTimestampedLogEntry("VISUAL|note|tables.valid.1");
    VBegin(pc);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(10), pc);
    object left = VSpawn(pc, "nw_bandit001", -2.0, 1.0, 270.0);
    object center = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    object right = VSpawn(pc, "nw_bandit001", 2.0, 1.0, 270.0);
    SetEventScript(left, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    SetEventScript(center, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    SetEventScript(right, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    DelayCommand(1.0, VCamera(pc, 90.0, 7.0, 65.0));
    DelayCommand(2.0, Freeze(left));
    DelayCommand(2.0, Freeze(center));
    DelayCommand(2.0, Freeze(right));
    DelayCommand(3.0, VShot("baseline"));
    DelayCommand(3.5, Burst("periods", 5.0));
    DelayCommand(4.0, Start(left, 0, 4.25));
    DelayCommand(4.375, Start(center, 1, 3.875));
    DelayCommand(4.0, Start(right, 2, 4.25));
    DelayCommand(9.0, VShot("periods_expired"));
    DelayCommand(9.5, Burst("red_black", 5.0));
    DelayCommand(10.0, Start(center, 3, 4.0));
    DelayCommand(15.0, VShot("red_expired"));
    DelayCommand(16.0, Start(center, 4, 2.0));
    DelayCommand(16.5, Burst("cal_0", 1.0));
    DelayCommand(19.0, Start(center, 5, 2.0));
    DelayCommand(19.5, Burst("cal_25", 1.0));
    DelayCommand(22.0, Start(center, 6, 2.0));
    DelayCommand(22.5, Burst("cal_50", 1.0));
    DelayCommand(25.0, Start(center, 7, 2.0));
    DelayCommand(25.5, Burst("cal_75", 1.0));
    DelayCommand(28.0, Start(center, 8, 2.0));
    DelayCommand(28.5, Burst("cal_100", 1.0));
    DelayCommand(31.0, VShot("all_expired"));
    DelayCommand(32.0, VDone());
}

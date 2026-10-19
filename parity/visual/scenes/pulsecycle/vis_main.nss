#include "visual_inc"

void Start(object actor, int row, float duration)
{
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectVisualEffect(row), actor, duration);
    WriteTimestampedLogEntry("VISUAL|note|applied." + IntToString(row) + ".valid." + IntToString(GetIsObjectValid(actor)));
}

void Burst(string name)
{
    // Four frames/sec leaves enough time for client PNG readback without a burst backlog.
    // Capture-clock sampling covers onset, both directions, repetition and expiry.
    WriteTimestampedLogEntry("VISUAL|burst|" + name + "|5|4");
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    VHidePC(pc);
    object actor = VSpawn(pc, "nw_bandit001", 0.0, 1.0, 270.0);
    SetEventScript(actor, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "");
    DelayCommand(1.0, VCamera(pc, 90.0, 7.0, 65.0));
    DelayCommand(3.0, VShot("baseline"));
    DelayCommand(3.5, Burst("blue_white"));
    DelayCommand(4.0, Start(actor, 513, 4.0));
    DelayCommand(9.0, VShot("blue_white_expired"));
    DelayCommand(9.5, Burst("red_blue"));
    DelayCommand(10.0, Start(actor, 524, 4.0));
    DelayCommand(15.0, VShot("red_blue_expired"));
    DelayCommand(15.5, Burst("red_black"));
    DelayCommand(16.0, Start(actor, 530, 4.0));
    DelayCommand(21.0, VShot("red_black_expired"));
    DelayCommand(22.0, VDone());
}

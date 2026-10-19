// Game time with Mod_MinPerHour 5: minutes per hour, conversions and rollover.
#include "parity_inc"

string Clock()
{
    return IntToString(GetCalendarDay()) + " " + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute()) + ":"
        + IntToString(GetTimeSecond()) + "." + IntToString(GetTimeMillisecond());
}

void Rolled()
{
    PLog("roll.hm", IntToString(GetCalendarDay()) + " " + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute()));
    PLogF("roll.sec", IntToFloat(GetTimeSecond()) + IntToFloat(GetTimeMillisecond()) / 1000.0, 0.1);
    PDone();
}

void Main()
{
    PLogF("conv.hour", HoursToSeconds(1));
    PLogF("conv.hour3", HoursToSeconds(3));
    PLogF("conv.turn", TurnsToSeconds(1));
    PLogI("minute.before", GetTimeMinute());
    SetTime(14, 7, 0, 0);
    PLog("norm.minutes", Clock());
    SetTime(15, 4, 30, 0);
    PLog("norm.set", Clock());
    SetTime(15, 4, 60, 0);
    PLog("norm.secondsover", Clock());
    SetTime(16, 4, 58, 0);
    DelayCommand(3.0, Rolled());
}

void main()
{
    DelayCommand(1.0, Main());
}

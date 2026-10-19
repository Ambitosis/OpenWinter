// Game time: clock progression, SetTime / SetCalendar (including going backwards), day and night,
// and how a clock jump affects delayed commands. Phases run one after another, so a SetTime never
// jumps over a command another phase is waiting on.
#include "parity_inc"

string Clock()
{
    return IntToString(GetCalendarYear()) + "-" + IntToString(GetCalendarMonth()) + "-" + IntToString(GetCalendarDay())
        + " " + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute()) + ":" + IntToString(GetTimeSecond())
        + "." + IntToString(GetTimeMillisecond());
}

string DayState()
{
    return IntToString(GetIsDay()) + IntToString(GetIsNight()) + IntToString(GetIsDawn()) + IntToString(GetIsDusk());
}

// Real seconds into the current hour, from the clock.
float HourSeconds()
{
    return IntToFloat(GetTimeMinute() * 60 + GetTimeSecond()) + IntToFloat(GetTimeMillisecond()) / 1000.0;
}

void Progress(string k)
{
    PLogI(k + ".hour", GetTimeHour());
    PLogF(k + ".sec", HourSeconds(), 0.08);
}

// After a natural rollover: hour and minute exactly, the seconds within jitter.
void Rolled(string k)
{
    PLog(k + ".hm", IntToString(GetCalendarDay()) + " " + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute()));
    PLogF(k + ".sec", IntToFloat(GetTimeSecond()) + IntToFloat(GetTimeMillisecond()) / 1000.0, 0.1);
    PLog(k + ".daystate", DayState());
}

void Mark(string s)
{
    PLog("jump.fired", s);
}

void Done()
{
    PDone();
}

void Normalise()
{
    SetTime(10, 0, 0, 0);
    PLog("norm.base", Clock());
    SetTime(10, 5, 0, 0);
    PLog("norm.minutes", Clock());
    SetTime(23, 5, 0, 0);
    PLog("norm.minutes.midnight", Clock());
    SetTime(10, 0, 0, 0);
    PLog("norm.base2", Clock());
    SetTime(12, 1, 70, 1500);
    PLog("norm.seconds", Clock());
    SetTime(26, 0, 0, 0);
    PLog("norm.hours.26", Clock());
    SetTime(62, 250, 10, 10);
    PLog("norm.doc", Clock());
    SetTime(5, 120, 0, 0);
    PLog("norm.120min", Clock());
    SetTime(GetTimeHour(), GetTimeMinute(), GetTimeSecond(), GetTimeMillisecond());
    PLog("norm.equal", Clock());
    SetTime(GetTimeHour(), GetTimeMinute(), GetTimeSecond(), GetTimeMillisecond() + 1);
    PLog("norm.plus1ms", Clock());
    SetTime(GetTimeHour(), GetTimeMinute(), GetTimeSecond(), GetTimeMillisecond() - 1);
    PLog("norm.minus1ms", Clock());
    SetTime(GetTimeHour(), 0, 0, 0);
    PLog("norm.samehour", Clock());
    SetTime(12, 0, 0, -1);
    PLog("norm.negative", Clock());
    SetTime(GetTimeHour() + 1, -1, 120, 0);
    PLog("norm.negative.minute", Clock());

    int y = GetCalendarYear(), m = GetCalendarMonth(), d = GetCalendarDay();
    SetCalendar(y - 50, 1, 1);
    PLog("cal.back", Clock());
    SetCalendar(y, m - 1, d);
    PLog("cal.backmonth", Clock());
    SetCalendar(y, m, d);
    PLog("cal.same", Clock());
    SetCalendar(y + 1, 33, 10);
    PLog("cal.monthover", Clock());
    SetCalendar(y + 3, 2, 40);
    PLog("cal.dayover", Clock());
    SetCalendar(y + 3, 12, 30);
    PLog("cal.yearover", Clock());

    int h;
    for (h = 0; h < 24; h++)
    {
        SetTime(h, 0, 0, 0);
        PLog("daystate." + IntToString(h), DayState() + " " + Clock());
    }
    PLogF("conv.hour", HoursToSeconds(1));
    PLogF("conv.turn", TurnsToSeconds(1));
    PLogF("conv.round", RoundsToSeconds(1));
    DelayCommand(0.5, Done());
}

void JumpAfter()
{
    PLog("jump.later", "1");
    DelayCommand(0.2, Normalise());
}

// Delayed commands run on the game clock: jumping it forward makes the ones due run at once.
void Jump()
{
    DelayCommand(5.0, Mark("d5"));
    DelayCommand(119.0, Mark("d119"));
    DelayCommand(100.0, Mark("d100"));
    DelayCommand(0.0, Mark("d0"));
    DelayCommand(200.0, Mark("d200"));
    // Exactly one game hour (120 s) ahead.
    SetTime(GetTimeHour() + 1, GetTimeMinute(), GetTimeSecond(), GetTimeMillisecond());
    PLog("jump.after", "1");
    DelayCommand(0.5, JumpAfter());
}

void Roll(string k, int h, int m, int s, float fWait, string sNext)
{
    SetTime(h, m, s, 0);
    PLog(k + ".set", Clock());
    PLog(k + ".set.daystate", DayState());
    DelayCommand(fWait, Rolled(k));
    if (sNext == "dusk") DelayCommand(fWait + 0.2, Roll("dusk", 17, 1, 59, 2.0, "dawn"));
    else if (sNext == "dawn") DelayCommand(fWait + 0.2, Roll("dawn", 5, 1, 59, 2.0, "day"));
    else if (sNext == "day") DelayCommand(fWait + 0.2, Roll("day", 6, 1, 59, 2.0, "midnight"));
    else if (sNext == "midnight") DelayCommand(fWait + 0.2, Roll("midnight", 23, 1, 59, 2.0, "jump"));
    else DelayCommand(fWait + 0.2, Jump());
}

void main()
{
    // The original's clock has already run a few milliseconds by OnModuleLoad.
    PLog("load.clock", IntToString(GetCalendarDay()) + " " + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute()));
    PLogF("load.sec", HourSeconds(), 0.1);
    PLog("load.daystate", DayState());
    int i;
    for (i = 1; i <= 8; i++) DelayCommand(IntToFloat(i) * 0.5, Progress("prog." + IntToString(i * 5)));
    DelayCommand(4.3, Roll("hour", 13, 1, 58, 3.0, "dusk"));
}

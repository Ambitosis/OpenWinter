using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game;

/// <summary>
/// The module calendar: years, 12 months of 28 days, 24 hours. Like the original, an hour has
/// MinutesPerHour minutes of 60 real seconds each, so seconds and milliseconds run in real time.
/// </summary>
public sealed class GameClock
{
    public int Year = 1372, Month = 1, Day = 1, Hour = 13, Minute, Second, Millisecond;
    public int MinutesPerHour = 2;
    public int DawnHour = 6, DuskHour = 18;
    double _carry;

    public void Load(GffStruct ifo)
    {
        Year = (int)ifo.GetInt("Mod_StartYear", 1372);
        Month = (int)Math.Clamp(ifo.GetInt("Mod_StartMonth", 1), 1, 12);
        Day = (int)Math.Clamp(ifo.GetInt("Mod_StartDay", 1), 1, 28);
        Hour = (int)Math.Clamp(ifo.GetInt("Mod_StartHour", 13), 0, 23);
        MinutesPerHour = (int)Math.Max(1, ifo.GetInt("Mod_MinPerHour", 2));
        DawnHour = (int)ifo.GetInt("Mod_DawnHour", 6);
        DuskHour = (int)ifo.GetInt("Mod_DuskHour", 18);
    }

    /// <summary>Real milliseconds in one game hour.</summary>
    public long HourMs => MinutesPerHour * 60_000L;
    /// <summary>Milliseconds since midnight.</summary>
    public long TimeOfDayMs => Hour * HourMs + Minute * 60_000L + Second * 1000L + Millisecond;
    /// <summary>Days since year 0.</summary>
    public long DayNumber => ((long)Year * 12 + (Month - 1)) * 28 + (Day - 1);
    /// <summary>Milliseconds since year 0.</summary>
    public long TotalMs => DayNumber * 24 * HourMs + TimeOfDayMs;

    public void Advance(double realSeconds)
    {
        _carry += realSeconds * 1000.0;
        long ms = (long)_carry;
        if (ms <= 0) return;
        _carry -= ms;
        AddMilliseconds(ms);
    }

    public void AddMilliseconds(long ms) => SetDayAndTime(DayNumber, TimeOfDayMs + ms);
    public void AddSeconds(long s) => AddMilliseconds(s * 1000);
    public void AddHours(long h) => AddMilliseconds(h * HourMs);

    /// <summary>Sets the date and time from a day number and milliseconds past its midnight (which may run into later days).</summary>
    void SetDayAndTime(long day, long ms)
    {
        long dayMs = 24 * HourMs;
        day += Math.DivRem(ms, dayMs, out ms);
        if (ms < 0) { ms += dayMs; day--; }
        Hour = (int)(ms / HourMs);
        ms %= HourMs;
        Minute = (int)(ms / 60_000);
        ms %= 60_000;
        Second = (int)(ms / 1000);
        Millisecond = (int)(ms % 1000);
        Year = (int)(day / (12 * 28));
        Month = (int)(day / 28 % 12) + 1;
        Day = (int)(day % 28) + 1;
    }

    /// <summary>
    /// SetTime: overflowing fields carry into the next (milliseconds, seconds and minutes by the usual
    /// 1000/60/60, then minutes past the hour's MinutesPerHour), and a time of day earlier than now
    /// moves to the next day. The clock never goes back; negative values change nothing.
    /// </summary>
    public void SetTime(int hour, int minute, int second, int ms)
    {
        if (hour < 0 || minute < 0 || second < 0 || ms < 0) return;
        long total = ((hour * 60L + minute) * 60 + second) * 1000 + ms;
        long hours = total / 3_600_000, minutes = total % 3_600_000 / 60_000, rest = total % 60_000;
        long time = (hours + minutes / MinutesPerHour) * HourMs + minutes % MinutesPerHour * 60_000 + rest;
        SetDayAndTime(time < TimeOfDayMs ? DayNumber + 1 : DayNumber, time);
    }

    /// <summary>SetCalendar: months past 12 and days past 28 carry over; a date not after today is ignored.</summary>
    public void SetCalendar(int year, int month, int day)
    {
        long d = ((long)year * 12 + (month - 1)) * 28 + (day - 1);
        if (d > DayNumber) SetDayAndTime(d, TimeOfDayMs);
    }

    /// <summary>Sets the time of day directly (development commands; no next-day rule).</summary>
    public void Set(int hour, int minute, int second, int ms)
    {
        Hour = Math.Clamp(hour, 0, 23); Minute = Math.Clamp(minute, 0, MinutesPerHour - 1); Second = Math.Clamp(second, 0, 59); Millisecond = Math.Clamp(ms, 0, 999);
    }

    /// <summary>Hours since midnight, with the fraction (for lighting).</summary>
    public double HourOfDay => TimeOfDayMs / (double)HourMs;

    public bool IsDay => Hour > DawnHour && Hour < DuskHour;
    public bool IsNight => !IsDay && !IsDawn && !IsDusk;
    public bool IsDawn => Hour == DawnHour;
    public bool IsDusk => Hour == DuskHour;

    /// <summary>Hours since year 0 (for comparisons and calendar math).</summary>
    public long TotalHours => DayNumber * 24 + Hour;
}

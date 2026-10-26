namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Game calendar and clock.</summary>
static class TimeFunctions
{
    [NwFunc("GetTimeHour")] static object? Hour(Call c) => c.World.Clock.Hour;
    [NwFunc("GetTimeMinute")] static object? Minute(Call c) => c.World.Clock.Minute;
    [NwFunc("GetTimeSecond")] static object? Second(Call c) => c.World.Clock.Second;
    [NwFunc("GetTimeMillisecond")] static object? Millisecond(Call c) => c.World.Clock.Millisecond;
    [NwFunc("GetCalendarYear")] static object? Year(Call c) => c.World.Clock.Year;
    [NwFunc("GetCalendarMonth")] static object? Month(Call c) => c.World.Clock.Month;
    [NwFunc("GetCalendarDay")] static object? Day(Call c) => c.World.Clock.Day;
    [NwFunc("GetIsDay")] static object? IsDay(Call c) => c.World.Clock.IsDay ? 1 : 0;
    [NwFunc("GetIsNight")] static object? IsNight(Call c) => c.World.Clock.IsNight ? 1 : 0;
    [NwFunc("GetIsDawn")] static object? IsDawn(Call c) => c.World.Clock.IsDawn ? 1 : 0;
    [NwFunc("GetIsDusk")] static object? IsDusk(Call c) => c.World.Clock.IsDusk ? 1 : 0;

    // The clock only moves forward; delayed commands that come due on the way run at once.
    [NwFunc("SetTime")]
    static object? SetTime(Call c)
    {
        var k = c.World.Clock;
        long before = k.TotalMs;
        k.SetTime(c.I(0), c.I(1), c.I(2), c.I(3));
        Nwnx.NwnxCalendar.Check(c.World);
        c.World.SkipTime((k.TotalMs - before) / 1000.0);
        return null;
    }
    [NwFunc("SetCalendar")]
    static object? SetCalendar(Call c)
    {
        var k = c.World.Clock;
        long before = k.TotalMs;
        k.SetCalendar(c.I(0), c.I(1), c.I(2));
        Nwnx.NwnxCalendar.Check(c.World);
        c.World.SkipTime((k.TotalMs - before) / 1000.0);
        return null;
    }
}

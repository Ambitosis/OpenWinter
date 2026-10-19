using OpenWinter.Game;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Game clock rules measured on the original server (parity/suites/gametime).</summary>
public sealed class TimingTests
{
    static GameClock Clock(int minutesPerHour = 2) => new() { Year = 1372, Month = 6, Day = 1, Hour = 13, MinutesPerHour = minutesPerHour };

    static string Show(GameClock k) => $"{k.Year}-{k.Month}-{k.Day} {k.Hour}:{k.Minute}:{k.Second}.{k.Millisecond}";

    [Fact]
    public void SecondsRunInRealTimeAndAnHourHasMinutesPerHourMinutes()
    {
        var k = Clock();
        k.Advance(1.5);
        Assert.Equal("1372-6-1 13:0:1.500", Show(k));
        k.Advance(60);
        Assert.Equal("1372-6-1 13:1:1.500", Show(k));
        k.Advance(60);
        Assert.Equal("1372-6-1 14:0:1.500", Show(k));
    }

    [Theory]
    [InlineData(10, 5, 0, 0, "1372-6-1 12:1:0.0")]       // minutes past MinutesPerHour carry into hours
    [InlineData(23, 5, 0, 0, "1372-6-2 1:1:0.0")]
    [InlineData(12, 1, 70, 1500, "1372-6-1 13:0:11.500")]
    [InlineData(26, 0, 0, 0, "1372-6-2 2:0:0.0")]        // a later absolute time: no extra day
    [InlineData(62, 250, 10, 10, "1372-6-3 23:0:10.10")]  // 250 min = 4 h 10 min, then 10 min = 5 h
    [InlineData(5, 120, 0, 0, "1372-6-2 7:0:0.0")]
    [InlineData(9, 0, 0, 0, "1372-6-2 9:0:0.0")]         // earlier today: tomorrow
    [InlineData(12, 0, 0, -1, "1372-6-1 10:0:0.0")]      // negative: ignored
    public void SetTimeNormalises(int h, int m, int s, int ms, string expected)
    {
        var k = Clock();
        k.Hour = 10;
        k.SetTime(h, m, s, ms);
        Assert.Equal(expected, Show(k));
    }

    [Fact]
    public void SetTimeToNowChangesNothingButOneMillisecondEarlierIsTomorrow()
    {
        var k = Clock();
        k.SetTime(13, 0, 0, 0);
        Assert.Equal("1372-6-1 13:0:0.0", Show(k));
        k.Millisecond = 5;
        k.SetTime(13, 0, 0, 4);
        Assert.Equal("1372-6-2 13:0:0.4", Show(k));
    }

    [Fact]
    public void SetCalendarCarriesOverAndNeverGoesBack()
    {
        var k = Clock();
        k.SetCalendar(1300, 1, 1);
        Assert.Equal("1372-6-1 13:0:0.0", Show(k));
        k.SetCalendar(1373, 33, 10);
        Assert.Equal("1375-9-10 13:0:0.0", Show(k));
        k.SetCalendar(1375, 2, 40);   // 1375-3-12: earlier, ignored
        Assert.Equal("1375-9-10 13:0:0.0", Show(k));
        k.SetCalendar(1378, 12, 30);
        Assert.Equal("1379-1-2 13:0:0.0", Show(k));
    }

    [Fact]
    public void DawnAndDuskHoursAreNeitherDayNorNight()
    {
        var k = Clock();
        k.Hour = 6;
        Assert.True(k.IsDawn && !k.IsDay && !k.IsNight);
        k.Hour = 7;
        Assert.True(k.IsDay);
        k.Hour = 18;
        Assert.True(k.IsDusk && !k.IsDay && !k.IsNight);
        k.Hour = 19;
        Assert.True(k.IsNight);
    }
}

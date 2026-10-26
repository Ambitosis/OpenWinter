using System.Runtime.CompilerServices;

namespace OpenWinter.Game.Nwnx;

/// <summary>
/// NWNX_ON_CALENDAR_HOUR / _DAY / _MONTH / _YEAR (OLD and NEW) and _DAWN / _DUSK, OBJECT_SELF the module, as the
/// game clock moves on or a script sets it (SetTime raises them before it returns; nwnx_events_creature).
/// </summary>
public static class NwnxCalendar
{
    sealed class Last { public int Hour = -1, Day, Month, Year; }

    static readonly ConditionalWeakTable<World, Last> _last = new();

    public static void Check(World world)
    {
        var k = world.Clock;
        var last = _last.GetValue(world, _ => new Last());
        if (last.Hour < 0) { Remember(last, k); return; }
        if (last.Hour == k.Hour && last.Day == k.Day && last.Month == k.Month && last.Year == k.Year) return;
        var (hour, day, month, year) = (last.Hour, last.Day, last.Month, last.Year);
        Remember(last, k);
        if (hour != k.Hour) Fire(world, "NWNX_ON_CALENDAR_HOUR", hour, k.Hour);
        if (day != k.Day) Fire(world, "NWNX_ON_CALENDAR_DAY", day, k.Day);
        if (month != k.Month) Fire(world, "NWNX_ON_CALENDAR_MONTH", month, k.Month);
        if (year != k.Year) Fire(world, "NWNX_ON_CALENDAR_YEAR", year, k.Year);
        if (hour != k.Hour && k.Hour == k.DawnHour) NwnxEvent.Begin(world, "NWNX_ON_CALENDAR_DAWN", world.Module)?.Fire();
        if (hour != k.Hour && k.Hour == k.DuskHour) NwnxEvent.Begin(world, "NWNX_ON_CALENDAR_DUSK", world.Module)?.Fire();
    }

    static void Remember(Last last, GameClock k) => (last.Hour, last.Day, last.Month, last.Year) = (k.Hour, k.Day, k.Month, k.Year);

    static void Fire(World world, string name, int old, int now) =>
        NwnxEvent.Begin(world, name, world.Module)?.Add("OLD", old).Add("NEW", now).Fire();
}

using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

// Save games need the private run-time queues of the world and the script host. These
// partial declarations expose them to SaveGames code only (internal), without widening
// the classes' public surface.

namespace OpenWinter.Game
{
    public sealed partial class World
    {
        /// <summary>World fields a save handles itself rather than by reflection.</summary>
        internal static readonly HashSet<string> SnapshotExcludedFields = new(StringComparer.Ordinal)
        {
            nameof(_objects), nameof(Systems), nameof(_timers), nameof(_timerSeq), nameof(_inventory), nameof(Output), nameof(_spawnQueue),
        };

        internal uint NextObjectId { get => _nextId; set => _nextId = value; }
        internal long TimerSequence { get => _timerSeq; set => _timerSeq = value; }

        /// <summary>Save-persistent engine timers (World.After) with their due time and sequence.</summary>
        internal IEnumerable<(Action Action, double At, long Seq)> PendingTimers =>
            _timers.UnorderedItems.Where(x => x.Element.Persist).Select(x => (x.Element.Action, x.Priority.Item1, x.Priority.Item2)).OrderBy(x => x.Item2).ThenBy(x => x.Item3);

        internal void ReplaceTimers(IEnumerable<(Action Action, double At, long Seq)> timers)
        {
            _timers.Clear();
            foreach (var (action, at, seq) in timers) _timers.Enqueue((action, true), (at, seq));
        }

        /// <summary>Replaces the object registry (loading a save): ids are kept exactly as saved.</summary>
        internal void ReplaceObjects(IEnumerable<GameObject> registered)
        {
            _objects.Clear();
            foreach (var o in registered) _objects[o.Id] = o;
        }

        internal bool IsRegistered(GameObject o) => o.Id != 0 && _objects.TryGetValue(o.Id, out var x) && ReferenceEquals(x, o);
    }
}

namespace OpenWinter.Game.Scripting
{
    public sealed partial class ScriptHost
    {
        /// <summary>A compiled script by resref (cached), for re-binding saved closures.</summary>
        internal NcsProgram? ProgramFor(string resref) => Program(resref);

        internal long ScheduleSequence { get => _seq; set => _seq = value; }

        /// <summary>DelayCommand / AssignCommand closures waiting to run, in run order.</summary>
        internal IEnumerable<(ScriptSituation Situation, uint Self, double At, long Seq)> PendingScheduled =>
            _scheduled.UnorderedItems.Select(x => (x.Element.Situation, x.Element.Self, x.Priority.Item1, x.Priority.Item2))
                .OrderBy(x => x.Item3).ThenBy(x => x.Item4);

        internal void ReplaceScheduled(IEnumerable<(ScriptSituation Situation, uint Self, double At, long Seq)> items)
        {
            _scheduled.Clear();
            foreach (var (s, self, at, seq) in items) _scheduled.Enqueue((s, self), (at, seq));
        }
    }
}

namespace OpenWinter.Game.Areas
{
    public sealed partial class Area
    {
        /// <summary>Drops the cached pathfinder after the walkmesh was rebuilt.</summary>
        internal void ResetPathfinder() => _pathfinder = null;
    }
}

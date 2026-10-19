using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Perception, nearest-creature searches, listening, line of sight and persistent objects.</summary>
static partial class SocietyFunctions
{
    // ---- perception ----

    [NwFunc("GetLastPerceived")] static object? LastPerceived(Call c) =>
        c.Self is Creature && c.Event.Values.TryGetValue("Perceived", out var v) && v is uint id ? id : GameObject.InvalidId;
    [NwFunc("GetLastPerceptionHeard")] static object? PerceptionHeard(Call c) => c.Event.Int("PerceptionHeard");
    [NwFunc("GetLastPerceptionInaudible")] static object? PerceptionInaudible(Call c) => c.Event.Int("PerceptionInaudible");
    [NwFunc("GetLastPerceptionSeen")] static object? PerceptionSeen(Call c) => c.Event.Int("PerceptionSeen");
    [NwFunc("GetLastPerceptionVanished")] static object? PerceptionVanished(Call c) => c.Event.Int("PerceptionVanished");

    [NwFunc("GetObjectSeen")] static object? ObjectSeen(Call c)
    {
        if (c.Obj(0) is not Creature target || c.ObjOrSelf(1) is not Creature source) return 0;
        return target == source || source.Society.Seen.Contains(target.Id) ? 1 : 0;
    }

    [NwFunc("GetObjectHeard")] static object? ObjectHeard(Call c)
    {
        if (c.Obj(0) is not Creature target || c.ObjOrSelf(1) is not Creature source) return 0;
        return target == source || source.Society.Heard.Contains(target.Id) ? 1 : 0;
    }

    // ---- nearest creature ----

    static (int, int)[] Criteria(Call c, int first) =>
        new[] { (c.I(0, -1), c.I(1, -1)), (c.I(first, -1), c.I(first + 1, -1)), (c.I(first + 2, -1), c.I(first + 3, -1)) };

    [NwFunc("GetNearestCreature")] static object? Nearest(Call c)
    {
        // nFirstCriteriaType, nFirstCriteriaValue, oTarget, nNth, nSecond..., nThird...
        var target = c.ObjOrSelf(2);
        if (target == null) return GameObject.InvalidId;
        var area = target as Area ?? target.Area;
        return Id(NearestCreature.Find(c.World, area, target.Position, target, target, c.I(3, 1), Criteria(c, 4)));
    }

    [NwFunc("GetNearestCreatureToLocation")] static object? NearestToLocation(Call c)
    {
        var loc = c.L(2);
        var area = c.World.Get(loc.Area) as Area;
        // Reputation and perception are judged by the caller.
        return Id(NearestCreature.Find(c.World, area, loc.Position, null, c.Self, c.I(3, 1), Criteria(c, 4)));
    }

    // ---- listening ----

    [NwFunc("GetIsListening")] static object? IsListening(Call c) => c.Obj(0) is Creature cr && cr.Society.Listening ? 1 : 0;

    [NwFunc("SetListening")] static object? SetListening(Call c)
    {
        if (c.Obj(0) is Creature cr) cr.Society.Listening = c.I(1) != 0;
        return null;
    }

    [NwFunc("SetListenPattern")] static object? SetListenPattern(Call c)
    {
        if (c.Obj(0) is not Creature cr) return null;
        var list = cr.Society.ListenPatterns;
        int number = c.I(2);
        int i = list.FindIndex(p => p.Number == number);
        var entry = new ListenPattern(number, c.S(1));
        if (i >= 0) list[i] = entry;
        else list.Add(entry);
        return null;
    }

    [NwFunc("TestStringAgainstPattern")] static object? TestPattern(Call c)
    {
        // Unlike a heard match, this leaves GetMatchedSubstring alone.
        return ListenPatterns.Match(c.S(0), c.S(1)) != null ? 1 : 0;
    }

    static List<string>? Matched(Call c) => c.Event.Values.TryGetValue("MatchedSubstrings", out var v) ? v as List<string> : null;

    [NwFunc("GetMatchedSubstring")] static object? MatchedSubstring(Call c)
    {
        var list = Matched(c);
        int n = c.I(0);
        return list != null && n >= 0 && n < list.Count ? list[n] : "";
    }

    // -1 in a conversation event without matched text (e.g. associate commands); 0 outside listen events, as in the original.
    [NwFunc("GetMatchedSubstringsCount")] static object? MatchedCount(Call c) =>
        Matched(c)?.Count ?? (c.Event.Values.ContainsKey("ListenPatternNumber") ? -1 : 0);

    [NwFunc("GetListenPatternNumber")] static object? PatternNumber(Call c) =>
        c.Event.Values.TryGetValue("ListenPatternNumber", out var v) && v is int n ? n : -1;

    // ---- line of sight ----

    [NwFunc("LineOfSightObject")] static object? LosObject(Call c) =>
        c.Obj(0) is { } a && c.Obj(1) is { } b && LineOfSight.Between(c.World, a, b) ? 1 : 0;

    [NwFunc("LineOfSightVector")] static object? LosVector(Call c)
    {
        // Must run on an object in the area the vectors are in.
        if (c.Self?.Area is not Area area) return 0;
        return LineOfSight.Clear(c.World, area, c.V(0), c.V(1)) ? 1 : 0;
    }

    // ---- chairs, persistent objects, areas of effect ----

    [NwFunc("GetSittingCreature")] static object? Sitting(Call c) =>
        c.Obj(0) is Placeable chair ? Id(Service(c).SittingOn(chair)) : GameObject.InvalidId;

    [NwFunc("GetFirstInPersistentObject")] static object? FirstInPersistent(Call c)
    {
        var p = c.ObjOrSelf(0);
        if (p == null) return GameObject.InvalidId;
        // nPersistentZone: only PERSISTENT_ZONE_ACTIVE is meaningful (FOLLOW is unused).
        var list = Service(c).PersistentResidents(p, c.I(1, 1));
        Service(c).PersistentCursors[p.Id] = (list, 0);
        return list.Count > 0 ? list[0] : GameObject.InvalidId;
    }

    [NwFunc("GetNextInPersistentObject")] static object? NextInPersistent(Call c)
    {
        var p = c.ObjOrSelf(0);
        if (p == null || !Service(c).PersistentCursors.TryGetValue(p.Id, out var cur)) return GameObject.InvalidId;
        int filter = c.I(1, 1);
        int i = cur.Index + 1;
        while (i < cur.List.Count && (c.World.Get(cur.List[i]) is not { } o || (ObjectFunctions.NwType(o) & filter) == 0)) i++;
        Service(c).PersistentCursors[p.Id] = (cur.List, i);
        return i < cur.List.Count ? cur.List[i] : GameObject.InvalidId;
    }

    [NwFunc("GetAreaOfEffectCreator")] static object? AoeCreator(Call c) =>
        c.ObjOrSelf(0) is { } aoe ? SocietyService.AreaOfEffectCreator(aoe) : GameObject.InvalidId;
}

using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Encounter: the encounter's own state (<see cref="EncounterState"/>), so the encounter system spawns from what
/// scripts set. Measured on nwserver with NWNX:EE (parity suite nwnx_encounter):
/// <list type="bullet">
/// <item>The creature list is indexed from its last entry (index 0 is the blueprint's last creature).</item>
/// <item>A negative index or reset time fails its argument check (nothing returned, scripts read 0); an index past
/// the end reads as empty and is ignored when set. Any faction id is taken.</item>
/// <item>Spawn points come back with their stored orientation as the location's facing.</item>
/// <item>Geometry reads "{x, y, z}" per vertex with six decimals; setting it needs at least three vertices (a missing
/// z is the ground's height), else nothing changes.</item>
/// </list>
/// </summary>
static class EncounterPlugin
{
    const string P = "Encounter";

    static Encounter? Pop(NwnxCall n) => n.PopObject<Encounter>();

    /// <summary>The state index of creature list index <paramref name="i"/>, or -1 outside the list.</summary>
    static int Entry(EncounterState st, int i) => i >= 0 && i < st.Creatures.Count ? st.Creatures.Count - 1 - i : -1;

    [NwnxFunc(P, "Destroy")]
    static void Destroy(NwnxCall n)
    {
        if (Pop(n) is { } e) n.World.Destroy(e);
    }

    [NwnxFunc(P, "GetNumberOfCreaturesInEncounterList")] static void Count(NwnxCall n) => n.Push(Pop(n)?.State.Creatures.Count ?? 0);

    [NwnxFunc(P, "GetEncounterCreatureByIndex")]
    static void GetCreature(NwnxCall n)
    {
        var e = Pop(n); int index = n.PopInt();
        if (index < 0) return;
        int i = e == null ? -1 : Entry(e.State, index);
        var c = i >= 0 ? e!.State.Creatures[i] : null;
        n.Push(c?.ResRef ?? "");
        n.Push(c?.CR ?? 0f);
        n.Push(c?.SingleSpawn ?? false);
        n.Push(c != null && e!.State.UsedUnique.Contains(i));
    }

    [NwnxFunc(P, "SetEncounterCreatureByIndex")]
    static void SetCreature(NwnxCall n)
    {
        var e = Pop(n); int index = n.PopInt(); var resref = n.PopString(); float cr = n.PopFloat();
        bool unique = n.PopInt() != 0, used = n.PopInt() != 0;
        if (e == null || Entry(e.State, index) is not (>= 0 and var i)) return;
        e.State.Creatures[i] = new EncounterState.Entry(resref, cr, unique);
        if (used) e.State.UsedUnique.Add(i); else e.State.UsedUnique.Remove(i);
    }

    [NwnxFunc(P, "GetFactionId")] static void GetFaction(NwnxCall n) => n.Push(Pop(n)?.State.Faction ?? 0);
    [NwnxFunc(P, "SetFactionId")]
    static void SetFaction(NwnxCall n)
    {
        var e = Pop(n); int v = n.PopInt();
        if (e != null) e.State.Faction = v;
    }

    [NwnxFunc(P, "GetPlayerTriggeredOnly")] static void GetPlayerOnly(NwnxCall n) => n.Push(Pop(n)?.State.PlayerOnly ?? false);
    [NwnxFunc(P, "SetPlayerTriggeredOnly")]
    static void SetPlayerOnly(NwnxCall n)
    {
        var e = Pop(n); bool v = n.PopInt() != 0;
        if (e != null) e.State.PlayerOnly = v;
    }

    [NwnxFunc(P, "GetCanReset")] static void GetReset(NwnxCall n) => n.Push(Pop(n)?.State.Reset ?? false);
    [NwnxFunc(P, "SetCanReset")]
    static void SetReset(NwnxCall n)
    {
        var e = Pop(n); bool v = n.PopInt() != 0;
        if (e != null) e.State.Reset = v;
    }

    [NwnxFunc(P, "GetResetTime")] static void GetResetTime(NwnxCall n) => n.Push(Pop(n) is { } e ? (int)e.State.ResetTime : 0);
    [NwnxFunc(P, "SetResetTime")]
    static void SetResetTime(NwnxCall n)
    {
        var e = Pop(n); int v = n.PopInt();
        if (e != null && v >= 0) e.State.ResetTime = v;
    }

    [NwnxFunc(P, "GetNumberOfSpawnPoints")] static void SpawnPoints(NwnxCall n) => n.Push(Pop(n)?.State.SpawnPoints.Count ?? 0);

    [NwnxFunc(P, "GetSpawnPointByIndex")]
    static void SpawnPoint(NwnxCall n)
    {
        var e = Pop(n); int i = n.PopInt();
        if (i < 0) return;
        var points = e?.State.SpawnPoints;
        bool found = points != null && i < points.Count;
        n.Push(new NwnxValue(NwnxType.Vector, found ? points![i].Position : Vector3.Zero));
        n.Push(found ? points![i].Facing : 0f);
    }

    [NwnxFunc(P, "GetMinNumSpawned")] static void Min(NwnxCall n) => n.Push(Pop(n)?.State.RecCreatures ?? 0);
    [NwnxFunc(P, "GetMaxNumSpawned")] static void Max(NwnxCall n) => n.Push(Pop(n)?.State.MaxCreatures ?? 0);

    [NwnxFunc(P, "GetCurrentNumSpawned")]
    static void Current(NwnxCall n)
    {
        var e = Pop(n);
        n.Push(e == null ? 0 : e.State.Spawned.Count(id => n.World.Get<Creature>(id) is { IsDead: false, IsDestroyed: false }));
    }

    [NwnxFunc(P, "GetGeometry")]
    static void GetGeometry(NwnxCall n)
    {
        var e = Pop(n);
        var sb = new StringBuilder();
        if (e != null)
            foreach (var v in e.Geometry)
                sb.Append('{').Append(F(v.X)).Append(", ").Append(F(v.Y)).Append(", ").Append(F(v.Z)).Append('}');
        n.Push(sb.ToString());
        static string F(float f) => f.ToString("F6", CultureInfo.InvariantCulture);
    }

    [NwnxFunc(P, "SetGeometry")]
    static void SetGeometry(NwnxCall n)
    {
        var e = Pop(n); var text = n.PopString();
        if (e == null) return;
        var points = new List<Vector3>();
        foreach (Match m in Regex.Matches(text, @"\{([^}]*)\}"))
        {
            var parts = m.Groups[1].Value.Split(',');
            if (parts.Length < 2 || !Number(parts[0], out float x) || !Number(parts[1], out float y)) continue;
            float z = parts.Length > 2 && Number(parts[2], out float pz) ? pz : e.Area?.Walkmesh.HighestGround(x, y) ?? 0f;
            points.Add(new Vector3(x, y, z));
        }
        if (points.Count >= 3) e.Geometry = points;

        static bool Number(string s, out float v) => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
}

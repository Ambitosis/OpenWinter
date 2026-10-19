using System.Numerics;
using System.Text.Json;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;

namespace OpenWinter.Game;

public sealed partial class GameServer
{
    bool DeferAreaEntry(GameObject obj, Area area, Vector3 position, float facing, bool exact, bool enterEvent)
    {
        if (obj is not Creature { Player: { AreaReadyHandshake: true, CommittingArea: false } session } pc
            || pc.Area == area) return false;
        // Already loading this area for this creature (a DM avatar returning): keep the one load.
        if (session.PendingEntry is { } pending && pending.Creature == pc && pending.Area == area)
        {
            session.PendingEntry = (pc, area, position, facing, exact, enterEvent);
            return true;
        }
        StageAreaEntry(session, area, position, facing, exact, enterEvent);
        return true;
    }

    /// <summary>
    /// Places a loading creature the session no longer waits for (control moved to another creature, or the
    /// session left) straight into its destination, so it is never left outside every area.
    /// </summary>
    internal void SettleAreaEntry(PlayerSession s)
    {
        if (s.PendingEntry is not { } entry) return;
        s.PendingEntry = null;
        s.IsAreaLoading = false;
        if (!entry.Creature.IsDestroyed) CommitAreaEntry(s, entry);
    }

    void CommitAreaEntry(PlayerSession s, (Creature Creature, Area Area, Vector3 Position, float Facing, bool Exact, bool EnterEvent) entry)
    {
        var pc = entry.Creature;
        s.CommittingArea = true;
        try { World.AddToArea(pc, entry.Area, entry.Position, entry.Facing, entry.Exact, entry.EnterEvent); }
        finally { s.CommittingArea = false; }
        if (pc.Society.PerceptionArea != entry.Area.Id)
        {
            pc.Society.PerceptionArea = GameObject.InvalidId;
            pc.Society.NextPerception = -1;
        }
        World.System<WorldObjects.MapExploration>().Sync(pc);
        foreach (var other in _sessions)
            if (other != s && other.Area == entry.Area) SendSpawn(other, pc);
        AnnounceArea(s, entry.Area);
    }

    void StageAreaEntry(PlayerSession s, Area area, Vector3 position, float facing, bool exact, bool enterEvent)
    {
        if (s.Creature is not { } pc) return;
        if (s.PendingEntry?.Creature != pc) SettleAreaEntry(s);
        s.IsAreaLoading = true;
        s.PendingEntry = (pc, area, position, facing, exact, enterEvent);
        // No creature grid, collision, AI perception, movement, attacks or area-enter
        // hooks until this particular client has finished rendering the destination.
        World.SuspendForAreaLoad(pc);
        // Hide the preview without firing object-removal hooks: party, faction,
        // script UI and extension state survive an ordinary area transition.
        OnRemoved(pc);
        pc.Position = position;
        pc.Facing = facing;
        foreach (var id in s.Known) s.Send(new DespawnMessage { ObjectId = id }.Write());
        s.Known.Clear();
        s.Area = area;
        World.System<Players.CreatureVisibilityFeed>().Invalidate(s);
        var load = area.Describe(World.Resources.Talk);
        load.LoadToken = ++s.LoadToken;
        s.Send(load.Write());
        s.Send(Clock().Write());
        foreach (var obj in area.Objects) SendSpawn(s, obj);
        SendSpawn(s, pc); // private preview; it is not in the world yet
        s.Send(new ControlObjectMessage { ObjectId = pc.Id }.Write());
        s.Send(new NotifyMessage { Kind = "area-snapshot-ready", Subject = area.Id,
            Json = JsonSerializer.Serialize(new { token = s.LoadToken }) }.Write());
    }

    void CompleteAreaEntry(PlayerSession s, string json)
    {
        if (!s.IsAreaLoading || s.PendingEntry is not { } entry || entry.Creature.IsDestroyed) return;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return; }
        using var parsed = doc;
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("token", out var token) || token.ValueKind != JsonValueKind.Number || !token.TryGetUInt32(out uint revision)
            || revision != s.LoadToken) return; // old load, duplicate, or another session's token
        s.PendingEntry = null;
        s.IsAreaLoading = false;
        CommitAreaEntry(s, entry);
    }
}

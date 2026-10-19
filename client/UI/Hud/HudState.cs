using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>Vitals of one creature or object as the server's HUD feed reports them.</summary>
public sealed record Vitals(uint Id, string Name, string Portrait, int Hp, int MaxHp, bool Dead)
{
    public float Health { get; init; } = 1;
    public int TempHp { get; init; }
    public int Level { get; init; }
    public string Attitude { get; init; } = "";
    public bool Pc { get; init; }
    public int Associate { get; init; }
    /// <summary>The name of the area the creature is in (party bar hover text); empty when not reported.</summary>
    public string Area { get; init; } = "";
}

/// <summary>
/// Client side of the server's "hud" notify (see HudFeed): the player's hit points and experience,
/// the party, and the selected target. Also tracks which object the player has selected.
/// </summary>
public sealed class HudState
{
    public sealed record QueuedAction(uint Id, int Action, int Spell, string Icon, string Name);
    public Vitals? Self;
    public int Level, Xp, XpLevel, XpNext, Gold;
    public bool CanLevelUp;
    /// <summary>The player pause is on (the classic portrait shows po_pause meanwhile).</summary>
    public bool Paused;
    public readonly List<Vitals> Party = new();
    /// <summary>The player's action queue as the server shows it: ACTION_* and the spell for casts (-1 none), current first.</summary>
    public readonly List<QueuedAction> Queue = new();
    public Vitals? Target;
    public uint Leader;
    public uint Selected { get; private set; }

    public event Action? Changed;
    /// <summary>Tells every listener to redraw from the current state (after the interface was switched).</summary>
    public void Refresh() => Changed?.Invoke();
    /// <summary>Fires with the old and new level when the player gains a level, and when level-up becomes available.</summary>
    public event Action? LevelUpAvailable;

    readonly ClientSession _session;

    public HudState(ClientSession session)
    {
        _session = session;
        session.NotifyReceived += OnNotify;
        session.AreaLoaded += () => Select(0);
        session.ControlledChanged += _ =>
        {
            Self = Target = null;
            Selected = Leader = 0;
            Party.Clear(); Queue.Clear();
            Level = Xp = XpLevel = XpNext = Gold = 0;
            CanLevelUp = false;
            Changed?.Invoke();
        };
    }

    /// <summary>Selects an object for the target frame (0 clears).</summary>
    public void Select(uint id)
    {
        if (id == Selected) return;
        Selected = id;
        if (id == 0) Target = null;
        _session.Request("hud-target", new { id });
        Changed?.Invoke();
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind == "pause")
        {
            using var pause = System.Text.Json.JsonDocument.Parse(m.Json);
            Paused = (pause.RootElement.GetProperty("state").GetInt32() & 2) != 0;
            Changed?.Invoke();
        }
        if (m.Kind == "party")
        {
            using var pd = JsonDocument.Parse(m.Json);
            Leader = pd.RootElement.TryGetProperty("leader", out var l) ? l.GetUInt32() : 0;
            Changed?.Invoke();
            return;
        }
        if (m.Kind == "attack")
        {
            // Attacking something targets it.
            using var ad = JsonDocument.Parse(m.Json);
            if (ad.RootElement.GetProperty("attacker").GetUInt32() == _session.ControlledId) Select(ad.RootElement.GetProperty("target").GetUInt32());
            return;
        }
        if (m.Kind != "hud" || m.Subject != _session.ControlledId) return;
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        var s = d.GetProperty("self");
        bool couldLevel = CanLevelUp;
        Self = Read(s) with { TempHp = s.GetProperty("tempHp").GetInt32() };
        Level = s.GetProperty("level").GetInt32();
        Xp = s.GetProperty("xp").GetInt32();
        XpLevel = s.GetProperty("xpLevel").GetInt32();
        XpNext = s.GetProperty("xpNext").GetInt32();
        Gold = s.GetProperty("gold").GetInt32();
        CanLevelUp = s.GetProperty("canLevelUp").GetBoolean();
        Queue.Clear();
        if (s.TryGetProperty("queue", out var q) && q.ValueKind == JsonValueKind.Array)
            foreach (var a in q.EnumerateArray()) Queue.Add(new(a.GetProperty("id").GetUInt32(), a.GetProperty("action").GetInt32(),
                a.GetProperty("spell").GetInt32(), a.GetProperty("icon").GetString() ?? "", a.GetProperty("name").GetString() ?? ""));
        Party.Clear();
        foreach (var p in d.GetProperty("party").EnumerateArray())
            Party.Add(Read(p) with { Pc = p.GetProperty("pc").GetBoolean(), Associate = p.GetProperty("associate").GetInt32() });
        Target = d.TryGetProperty("target", out var t) && t.ValueKind == JsonValueKind.Object
            ? Read(t) with { Health = t.GetProperty("health").GetSingle(), Level = t.GetProperty("level").GetInt32(), Attitude = t.GetProperty("attitude").GetString() ?? "" }
            : null;
        if (Target != null && Target.Id != Selected) Target = null;
        Changed?.Invoke();
        if (CanLevelUp && !couldLevel) LevelUpAvailable?.Invoke();
    }

    static Vitals Read(JsonElement e) => new(
        e.GetProperty("id").GetUInt32(), Kit.NwnText.Strip(e.GetProperty("name").GetString() ?? ""), e.GetProperty("portrait").GetString() ?? "",
        e.GetProperty("hp").GetInt32(), e.GetProperty("maxHp").GetInt32(), e.GetProperty("dead").GetBoolean())
    {
        Area = e.TryGetProperty("area", out var a) ? Kit.NwnText.Strip(a.GetString() ?? "") : "",
    };
}

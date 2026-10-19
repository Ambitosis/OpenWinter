using OpenWinter.Game.Objects;

namespace OpenWinter.Game;

/// <summary>
/// Reputation rules on top of the module's faction matrix:
/// <list type="bullet">
/// <item>PC parties have their own faction ids (≥ <see cref="FirstPartyFaction"/>); NPC factions feel about a
/// PC the way they feel about the module's PC faction unless that PC has a personal standing with them
/// (AdjustReputation / SetStandardFactionReputation on a PC change only that PC's standing).</item>
/// <item>PCs have no feelings of their own toward NPCs: a PC's reputation toward an NPC mirrors the NPC's
/// reputation toward the PC. PCs in different parties are neutral (50).</item>
/// <item>Personal reputation (SetIsTemporaryFriend/Enemy/Neutral, ClearPersonalReputation) is a list of
/// adjustments from one object toward another that are added to the faction value. Scripted dispositions
/// replace the offset and decay in measured percentage steps, with a separate requested expiry.</item>
/// </list>
/// </summary>
public sealed partial class FactionTable
{
    public const int FirstPartyFaction = 100000;
    public const int HostileTo = 10, FriendFrom = 90;

    sealed class PersonalAdjustment
    {
        public int Amount;
        public bool Decays;
        public bool Scripted;
        public double Start, Duration;
    }

    readonly Dictionary<(uint Source, uint Target), List<PersonalAdjustment>> _personalAdj = new();
    /// <summary>How an NPC faction feels about one particular PC.</summary>
    readonly Dictionary<(int Faction, uint Pc), int> _pcStanding = new();
    /// <summary>Faction overrides for non-creature objects (ChangeFaction on placeables, doors, ...).</summary>
    readonly Dictionary<uint, int> _objectFaction = new();

    /// <summary>Set by the society system: the world the table belongs to (time for decay, object lookups).</summary>
    public World? World { get; internal set; }
    double Now => World?.Time ?? 0;

    public static bool IsPartyFaction(int faction) => faction >= FirstPartyFaction;

    public bool IsValidFaction(int id) => IsPartyFaction(id) || (id >= 0 && id < Factions.Count);

    /// <summary>The faction id of any object that has one: creatures, placeables, doors, triggers, encounters, stores.</summary>
    public int? FactionOf(GameObject? o)
    {
        switch (o)
        {
            case null: return null;
            case Creature c:
                if (c.IsPC && World != null) return Society.PartyService.Of(World).Ensure(c);
                return NpcFaction(c.Faction);
        }
        if (_objectFaction.TryGetValue(o.Id, out var f)) return f;
        return o switch
        {
            Placeable p => (int)p.Template.GetInt("Faction", Commoner),
            Door d => (int)d.Template.GetInt("Faction", Commoner),
            Encounter e => (int)e.Template.GetInt("Faction", Hostile),
            Store s => (int)s.Template.GetInt("Faction", Merchant),
            Trigger => Hostile,
            _ => null,
        };
    }

    /// <summary>Moves an object to a faction (creatures change their Faction field; other objects get an override).</summary>
    public void SetObjectFaction(GameObject o, int faction)
    {
        if (o is Creature c) c.Faction = faction;
        else _objectFaction[o.Id] = faction;
    }

    /// <summary>How <paramref name="source"/> feels about <paramref name="target"/> (0-100), including personal adjustments; -1 if either is invalid.</summary>
    public int ReputationOf(GameObject? source, GameObject? target)
    {
        if (source == null || target == null || source.IsDestroyed || target.IsDestroyed) return -1;
        int? sf = FactionOf(source), tf = FactionOf(target);
        if (sf == null || tf == null) return 50;
        if (source == target) return 100;
        // A charmed creature regards whoever charmed it as a friend while the charm lasts. Measured on nwserver
        // (cond_charm): only its own view of the charmer changes, not its view of the charmer's faction, nor the
        // charmer's view of it; a charm made by the module changes nothing. A charmed player's view doesn't change
        // (visual scene conditionai).
        if (source is Creature { IsPC: false } charmed && target is Creature && CharmedBy(charmed, target.Id)) return 100;
        // A recruited associate remains friendly to its master even in a neutral NPC faction.
        if (source is Creature sc && target is Creature tc &&
            (sc.Master == tc.Id || tc.Master == sc.Id)) return 100;
        // A PC's view of an NPC mirrors the NPC's view of the PC.
        if (IsPartyFaction(sf.Value) && !IsPartyFaction(tf.Value))
            return Math.Clamp(ReputationOf(target, source) + PersonalSum(source.Id, target.Id), 0, 100);
        return Math.Clamp(BaseReputation(sf.Value, tf.Value, target) + PersonalSum(source.Id, target.Id), 0, 100);
    }

    static bool CharmedBy(Creature c, uint charmer)
    {
        foreach (var e in c.Effects)
            if (e.Type == Combat.CombatK.EffectCharmed && e.Creator == charmer) return true;
        return false;
    }

    /// <summary>Faction-level feeling of faction <paramref name="sf"/> toward <paramref name="target"/> (in faction <paramref name="tf"/>), without personal adjustments.</summary>
    public int BaseReputation(int sf, int tf, GameObject? target)
    {
        if (sf == tf) return FactionReputation(sf, tf);
        bool sp = IsPartyFaction(sf), tp = IsPartyFaction(tf);
        if (sp && tp) return 50;
        if (sp) return FactionReputation(tf, sf); // mirrored
        if (tp && target != null && _pcStanding.TryGetValue((sf, target.Id), out var standing)) return standing;
        return FactionReputation(sf, tf);
    }

    public bool IsEnemyOf(GameObject source, GameObject target) { int r = ReputationOf(source, target); return r >= 0 && r <= HostileTo; }
    public bool IsFriendOf(GameObject source, GameObject target) => ReputationOf(source, target) >= FriendFrom;
    public bool IsNeutralTo(GameObject source, GameObject target) { int r = ReputationOf(source, target); return r > HostileTo && r < FriendFrom; }

    /// <summary>
    /// AdjustReputation: how <paramref name="sourceMember"/>'s faction feels about <paramref name="target"/>.
    /// For a PC target only that PC's standing changes; for an NPC target the whole target faction is affected.
    /// Fails when the source is a PC (or in a PC party) or a plot object.
    /// </summary>
    public bool Adjust(GameObject target, GameObject sourceMember, int adjustment)
    {
        int? sf = FactionOf(sourceMember), tf = FactionOf(target);
        if (sf == null || tf == null || sourceMember.Plot || IsPartyFaction(sf.Value) || sf == tf) return false;
        if (IsPartyFaction(tf.Value))
        {
            if (target is not Creature { IsPC: true }) return false;
            _pcStanding[(sf.Value, target.Id)] = Math.Clamp(BaseReputation(sf.Value, tf.Value, target) + adjustment, 0, 100);
        }
        else SetFactionReputation(sf.Value, tf.Value, FactionReputation(sf.Value, tf.Value) + adjustment);
        return true;
    }

    /// <summary>SetStandardFactionReputation: how faction <paramref name="faction"/> feels about <paramref name="creature"/>.</summary>
    public void SetStanding(int faction, Creature creature, int value)
    {
        value = Math.Clamp(value, 0, 100);
        int cf = FactionOf(creature) ?? creature.Faction;
        if (cf == faction) return;
        if (IsPartyFaction(cf)) _pcStanding[(faction, creature.Id)] = value;
        else SetFactionReputation(faction, cf, value);
    }

    /// <summary>GetStandardFactionReputation: how faction <paramref name="faction"/> feels about <paramref name="creature"/> (no personal adjustments).</summary>
    public int Standing(int faction, Creature creature) => BaseReputation(faction, FactionOf(creature) ?? creature.Faction, creature);

    // ---- personal reputation ----

    int PersonalSum(uint source, uint target)
    {
        if (!_personalAdj.TryGetValue((source, target), out var list)) return 0;
        double now = Now;
        double sum = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var a = list[i];
            if (!a.Decays) { sum += a.Amount; continue; }
            if (a.Scripted && now - a.Start >= a.Duration) { list.RemoveAt(i); continue; }
            double left = a.Duration <= 0 ? 0 : 1 - (now - a.Start) / (a.Scripted ? 180.0 : a.Duration);
            if (left <= 0) { list.RemoveAt(i); continue; }
            if (a.Scripted)
            {
                // Native decays in whole percentage steps, then truncates the removed offset.
                int percent = (int)Math.Ceiling((now - a.Start) * 100 / 180.0);
                sum += a.Amount - a.Amount * percent / 100;
            }
            else sum += a.Amount * left;
        }
        if (list.Count == 0) _personalAdj.Remove((source, target));
        return (int)Math.Round(sum, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Internal personal-standing operation: adds an adjustment so that <paramref name="source"/>'s
    /// reputation toward <paramref name="target"/> becomes <paramref name="value"/>. With
    /// <paramref name="decays"/> the adjustment shrinks to nothing over <paramref name="duration"/> seconds;
    /// otherwise it lasts until cleared.
    /// </summary>
    public void SetTemporary(GameObject source, GameObject target, int value, bool decays, double duration)
    {
        if (source == target) return;
        int current = ReputationOf(source, target);
        if (current < 0) return;
        int amount = value - current;
        if (amount == 0 && !decays) return;
        if (!_personalAdj.TryGetValue((source.Id, target.Id), out var list)) _personalAdj[(source.Id, target.Id)] = list = new();
        list.Add(new PersonalAdjustment { Amount = amount, Decays = decays && duration > 0, Start = Now, Duration = duration });
    }

    /// <summary>Measured NWScript dispositions replace the offset, independently of faction standing.
    /// Decay uses 180 seconds even when the requested expiry is shorter.</summary>
    public void SetScriptedTemporary(GameObject source, GameObject target, int value, bool decays, double duration)
    {
        // PC-to-PC reputation/PvP has its existing path; these probes cover NPCs and PC/NPC pairs.
        if (source is Creature { IsPC: true } && target is Creature { IsPC: true })
        { SetTemporary(source, target, value, decays, duration); return; }
        if (source is Creature { IsPC: true } || source == target || FactionOf(source) == FactionOf(target)) return;
        if (ReputationOf(source, target) < 0) return;
        int amount = value == 100 ? 100 : value == 0 ? -100 : -50;
        _personalAdj[(source.Id, target.Id)] = new List<PersonalAdjustment>
        {
            new() { Amount = amount, Scripted = true, Decays = decays && duration > 0, Start = Now, Duration = duration }
        };
    }

    public void ClearPersonal(uint source, uint target) => _personalAdj.Remove((source, target));

    /// <summary>True when <paramref name="source"/> has any personal adjustment toward <paramref name="target"/>.</summary>
    public bool HasPersonal(uint source, uint target) => PersonalSum(source, target) != 0 || _personalAdj.ContainsKey((source, target));

    /// <summary>Forgets everything about a destroyed object.</summary>
    public void Forget(uint id)
    {
        foreach (var key in _personalAdj.Keys.Where(k => k.Source == id || k.Target == id).ToList()) _personalAdj.Remove(key);
        foreach (var key in _pcStanding.Keys.Where(k => k.Pc == id).ToList()) _pcStanding.Remove(key);
        _objectFaction.Remove(id);
    }
}

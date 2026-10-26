using System.Numerics;
using System.Reflection;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Society;

/// <summary>
/// Social glue for the society package: chat and listen patterns, faction membership queries,
/// iteration cursors, hostility tracking, chairs, and hooks other packages fill in.
/// </summary>
public sealed class SocietyService : IWorldSystem
{
    public const int EventScriptCreatureOnNotice = 5001, EventScriptCreatureOnDialogue = 5007, EventScriptPlaceableOnDialogue = 9014;
    // TALKVOLUME_*.
    public const int VolumeTalk = 0, VolumeWhisper = 1, VolumeShout = 2, VolumeSilentTalk = 3, VolumeSilentShout = 4, VolumePartyChat = 5, VolumeTell = 6;
    public const float TalkRange = 20f, WhisperRange = 3f;

    readonly World _world;

    /// <summary>GetFirst/NextFactionMember cursors, one per faction.</summary>
    internal readonly Dictionary<int, (List<uint> List, int Index)> FactionCursors = new();
    /// <summary>GetFirst/NextInPersistentObject cursors, one per persistent object.</summary>
    internal readonly Dictionary<uint, (List<uint> List, int Index)> PersistentCursors = new();
    /// <summary>Chair → creature sitting on it.</summary>
    readonly Dictionary<uint, uint> _sitting = new();
    int? _maxHenchmen;

    /// <summary>Creator of an area-of-effect object, if the AoE implementation doesn't expose a "Creator" member.</summary>
    public static Func<GameObject, uint?>? AreaOfEffectCreatorHook;
    /// <summary>Objects inside a persistent object (AoE), if the AoE implementation doesn't expose an "Inside" member.</summary>
    public static Func<GameObject, IEnumerable<uint>?>? PersistentResidentsHook;

    public SocietyService(World world)
    {
        _world = world;
        world.Factions.World = world;
        world.ObjectRemoved += o =>
        {
            world.Factions.Forget(o.Id);
            _sitting.Remove(o.Id);
            foreach (var kv in _sitting.Where(kv => kv.Value == o.Id).ToList()) _sitting.Remove(kv.Key);
        };
    }

    public void Step(float dt) { }

    public static SocietyService Of(World world) => world.System<SocietyService>();

    // ---- henchmen limit ----

    public int MaxHenchmen
    {
        get => _maxHenchmen ??= _world.Module.Ifo.Has("Mod_MaxHenchmen") ? (int)_world.Module.Ifo.GetInt("Mod_MaxHenchmen") : 1;
        set => _maxHenchmen = Math.Max(0, value);
    }

    // ---- state helpers ----

    /// <summary>Dead for perception and selection purposes (NPCs at 0 HP or less, PCs at -10 or less, or flagged dead).</summary>
    public static bool IsDead(Creature c) => c.IsDead || (c.IsPC ? c.CurrentHitPoints <= -10 : c.CurrentHitPoints <= 0);

    /// <summary>Records who last acted with hostility toward <paramref name="victim"/> (attacks, damage, harmful spells, pickpocketing).</summary>
    public static void SetLastHostileActor(World world, GameObject victim, GameObject? actor)
    {
        if (victim is not Creature v || v.Plot || actor == null) return;
        // Area-of-effect objects count as their creator; other non-creatures never overwrite it.
        if (actor.Type == ObjectType.AreaOfEffect) actor = world.Get(AreaOfEffectCreator(actor));
        if (actor is not Creature a || IsDead(a)) return;
        v.Society.LastHostileActor = a.Id;
    }

    /// <summary>Chairs: ActionSit should call this when a creature sits down (and with null when it stands up).</summary>
    public void SetSitting(GameObject chair, Creature? sitter)
    {
        foreach (var kv in _sitting.Where(kv => sitter != null && kv.Value == sitter.Id).ToList()) _sitting.Remove(kv.Key);
        if (sitter == null) _sitting.Remove(chair.Id);
        else _sitting[chair.Id] = sitter.Id;
    }

    public Creature? SittingOn(GameObject chair)
    {
        if (_sitting.TryGetValue(chair.Id, out var id) && _world.Get<Creature>(id) is { } c && c.Area == chair.Area && !IsDead(c)
            && Vector3.Distance(c.Position, chair.Position) < 2.5f)
            return c;
        _sitting.Remove(chair.Id);
        // Fallback: someone whose current action is ACTION_SIT right at the chair.
        if (chair.Area == null) return null;
        foreach (var o in chair.Area.Objects)
            if (o is Creature cr && cr.Actions.Count > 0 && cr.Actions[0].ActionType == 37 && Vector3.Distance(cr.Position, chair.Position) < 1.0f)
                return cr;
        return null;
    }

    // ---- factions ----

    /// <summary>Living-or-dead, non-destroyed creatures in a faction (party factions: PCs first in join order, then associates by id).</summary>
    public List<Creature> FactionMembers(int faction, bool pcOnly)
    {
        var result = new List<Creature>();
        if (FactionTable.IsPartyFaction(faction))
        {
            result.AddRange(PartyService.Of(_world).Members(faction));
            if (pcOnly) return result;
        }
        foreach (var o in _world.AllObjects)
            if (o is Creature c && !c.IsDestroyed && (c.IsPC ? c.Faction : _world.Factions.NpcFaction(c.Faction)) == faction && !result.Contains(c) && (!pcOnly || c.IsPC))
                result.Add(c);
        // Membership order: the order the creatures joined the faction.
        if (!FactionTable.IsPartyFaction(faction)) result.Sort((a, b) => a.FactionJoinOrder != b.FactionJoinOrder ? a.FactionJoinOrder.CompareTo(b.FactionJoinOrder) : a.Id.CompareTo(b.Id));
        return result;
    }

    // ---- area of effect objects ----

    /// <summary>The creator of an area-of-effect object, or OBJECT_INVALID.</summary>
    public static uint AreaOfEffectCreator(GameObject aoe)
    {
        if (aoe.Type != ObjectType.AreaOfEffect) return GameObject.InvalidId;
        if (AreaOfEffectCreatorHook?.Invoke(aoe) is uint hooked) return hooked;
        return Member(aoe, "Creator") is uint id ? id : GameObject.InvalidId;
    }

    /// <summary>Objects currently inside a trigger, encounter or area of effect, nearest the centre first.</summary>
    public List<uint> PersistentResidents(GameObject persistent, int typeFilter)
    {
        var area = persistent.Area;
        var result = new List<GameObject>();
        if (area == null) return new List<uint>();
        IEnumerable<uint>? listed = null;
        switch (persistent)
        {
            case Trigger t:
                foreach (var o in area.Objects) if (o != t && Inside(t.Geometry, o.Position)) result.Add(o);
                break;
            case Encounter e:
                foreach (var o in area.Objects) if (o != e && Inside(e.Geometry, o.Position)) result.Add(o);
                break;
            default:
                if (persistent.Type != ObjectType.AreaOfEffect) return new List<uint>();
                listed = PersistentResidentsHook?.Invoke(persistent) ?? Member(persistent, "Inside") as IEnumerable<uint> ?? Member(persistent, "Objects") as IEnumerable<uint>;
                if (listed == null && Member(persistent, "Radius") is float radius && radius > 0)
                {
                    // A circular AoE without a membership list: test distance.
                    foreach (var o in area.Objects)
                        if (o != persistent && Vector2.Distance(new(o.Position.X, o.Position.Y), new(persistent.Position.X, persistent.Position.Y)) <= radius) result.Add(o);
                }
                break;
        }
        if (listed != null) foreach (var id in listed) if (_world.Get(id) is { } o) result.Add(o);
        return result.Where(o => !o.IsDestroyed && (Scripting.Functions.ObjectFunctions.NwType(o) & typeFilter) != 0)
            .OrderBy(o => Vector3.DistanceSquared(o.Position, persistent.Position)).Select(o => o.Id).ToList();
    }

    static bool Inside(List<Vector3> poly, Vector3 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y + 1e-12f) + a.X) inside = !inside;
        }
        return inside;
    }

    /// <summary>Reads a public field or property by name (for object classes owned by other packages).</summary>
    static object? Member(object o, string name)
    {
        var type = o.GetType();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        if (type.GetField(name, flags) is { } f) return f.GetValue(o);
        if (type.GetProperty(name, flags) is { } p && p.GetIndexParameters().Length == 0) return p.GetValue(o);
        return null;
    }

    // ---- armour class for GetFactionBestAC / GetFactionWorstAC ----

    /// <summary>
    /// The armour class GetFactionBestAC/WorstAC compare: the AC against no particular attacker, without the positive
    /// Dexterity bonus (kept with Uncanny Dodge or Defensive Awareness 1), without any dodge AC and without bonuses that
    /// apply only against some race, alignment or damage type; size, armour, shield, natural, deflection and the other
    /// AC effects count, and so does a Dexterity penalty. Measured on nwserver (parity suite faction_bestac:
    /// Dexterity 30 counted 10, with feat 195 or 949 20; dodge +9 left out; deflection, natural armour, armour
    /// enchantment and a small creature's +1 counted; a deflection bonus versus humans left out; Dexterity 3 counted -4).
    /// </summary>
    public static int FactionArmorClass(World world, Creature c)
    {
        var p = Rules.CreatureRules.ArmorClass(world, c);
        int ac = p.Total - p.Dodge;
        if (p.Dex > 0 && !Rules.CreatureRules.HasFeat(world, c, 195, ignoreUses: true) && !Rules.CreatureRules.HasFeat(world, c, 949, ignoreUses: true))
            ac -= p.Dex;
        return ac;
    }

    // ---- chat and listening ----

    /// <summary>
    /// Someone said something. Every listening creature within earshot whose listen patterns match runs its
    /// OnConversation script (after the current script) with GetLastSpeaker = <paramref name="speaker"/>,
    /// GetListenPatternNumber = the pattern's number and GetMatchedSubstring(n) = the matched pieces.
    /// Call it for PC chat (from the server's chat handler) and for NPC speech (SpeakString and friends).
    /// </summary>
    public static void OnChat(World world, Creature speaker, string text, int volume) => OnSpeech(world, speaker, text, volume);

    /// <summary><see cref="OnChat"/> for any speaking object (placeables and doors can SpeakString too).</summary>
    public static void OnSpeech(World world, GameObject speaker, string text, int volume)
    {
        if (speaker.Area == null && volume != VolumePartyChat) return;
        foreach (var listener in Listeners(world, speaker, volume)) Hear(world, speaker, listener, text);
    }

    /// <summary>One listening creature hears a line meant for it (its first matching listen pattern runs OnConversation).</summary>
    public static void Hear(World world, GameObject speaker, Creature listener, string text)
    {
        var s = listener.Society;
        if (!s.Listening || SocietyService.IsDead(listener) || PerceptionSystem.Has(listener, PerceptionSystem.EffectDeaf)) return;
        foreach (var pattern in s.ListenPatterns)
        {
            var matched = ListenPatterns.Match(pattern.Pattern, text);
            if (matched == null) continue;
            var ctx = new EventContext()
                .Set("Speaker", speaker.Id)
                .Set("ListenPatternNumber", pattern.Number)
                .Set("MatchedSubstrings", matched)
                .Set("EventScriptType", EventScriptCreatureOnDialogue);
            world.After(0, () => world.RunEvent(listener, "ScriptDialogue", ctx));
            break;
        }
    }

    static IEnumerable<Creature> Listeners(World world, GameObject speaker, int volume)
    {
        if (volume == VolumeTell) yield break;
        if (volume == VolumePartyChat)
        {
            int? party = world.Factions.FactionOf(speaker);
            if (party == null) yield break;
            foreach (var o in world.AllObjects)
                if (o is Creature c && c != speaker && !c.IsDestroyed && c.Faction == party) yield return c;
            yield break;
        }
        var area = speaker.Area!;
        float range = volume switch
        {
            VolumeWhisper => WhisperRange,
            VolumeShout or VolumeSilentShout => float.MaxValue,
            _ => TalkRange,
        };
        // An NPC's speech, at any of these volumes, reaches only creatures that hear it: on their perception list as
        // heard (nwserver ai_melee: of two fighters perceiving each other in turn, only the later one's
        // NW_I_WAS_ATTACKED is heard; sched_events: a listener 30 m from a shout that saw but didn't hear the speaker,
        // and a blind one 6 m away, got nothing). Speech from a placeable or door reaches listeners within their own
        // hearing range (sched_events: talk and shout to 8 and 16 m, not 25 or 40). The listeners' events run west to
        // east, in order of increasing X (sched_events). Players' speech is left as it was (not measurable on
        // nwserver).
        bool needsHearing = speaker is Creature { IsPC: false };
        bool listenRange = speaker is not Creature;
        var perception = world.System<PerceptionSystem>();
        var found = new List<Creature>();
        foreach (var o in area.Objects)
        {
            if (o is not Creature c || c == speaker || c.IsDestroyed) continue;
            float d = Vector3.Distance(c.Position, speaker.Position);
            if (d > range || needsHearing && !c.Society.Heard.Contains(speaker.Id)) continue;
            if (listenRange) { perception.Ranges(c, out _, out float hearing); if (d > hearing) continue; }
            found.Add(c);
        }
        foreach (var c in found.OrderBy(c => c.Position.X)) yield return c;
    }
}

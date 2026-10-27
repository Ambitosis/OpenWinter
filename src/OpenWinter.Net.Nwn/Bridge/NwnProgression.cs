using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdatePlayerinfoMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;
using GameWorld = OpenWinter.Game.World;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Levelling up on an NWN server. The server describes the player's creature (classes, abilities, feats, known
/// spells, skill ranks, unspent skill points, associates, whether a level is due); the bridge keeps a copy of it as an
/// OpenWinter creature and answers the level-up window's requests with OpenWinter's rules, as nwmain works them out
/// itself. Opening the window asks the server which classes it allows (its answer lists them by classes.2da row);
/// taking the level sends the choices as nwmain does, and the server confirms or refuses them.
/// </summary>
/// <remarks>
/// The level-up needs more of the creature than the other bridges keep (the ranks and unspent points of the level-up
/// information, known spells by class slot, the server's base scores), so it keeps its own copy, for each creature
/// the player has controlled: the server describes a creature again in full when control of it begins, except for
/// its ability scores, which it sends only when they change (measured: possessing a creature and returning).
/// </remarks>
public sealed class NwnProgression(NwnRules rules, Action<NwnMessage> send)
{
    sealed class ClassSlot
    {
        public int Class, Level, School = -1, Domain1 = -1, Domain2 = -1;
    }

    /// <summary>What the server's player info has said about one creature.</summary>
    sealed class Described
    {
        public readonly int[] Abilities = [10, 10, 10, 10, 10, 10];
        public readonly SortedSet<int> Feats = [];
        public readonly HashSet<int>[] Known = Enumerable.Range(0, 8).Select(_ => new HashSet<int>()).ToArray();
        public sbyte[] Ranks = [];
        public int SkillPoints, Package = 255, Familiar = 255, Companion = 255;
        public string FamiliarName = "", CompanionName = "";
        public bool CanLevelUp, Complete;
        public uint? Experience;
    }

    /// <summary>The highest skills.2da rows a level can raise: the level's skill list is flagged in a 32-bit field.</summary>
    public const int SkillsInMessage = 32;

    /// <summary>"Your character has gained enough experience to advance a level."</summary>
    const uint LevelDue = 8302;

    /// <summary>The creatures' rules fields and names from their updates (the player's come before its player info).</summary>
    readonly Dictionary<uint, PrimaryAttributes> _attributes = [];
    readonly Dictionary<uint, string> _names = [];
    /// <summary>The creatures the server has sent player info about (the ones the player has controlled).</summary>
    readonly Dictionary<uint, Described> _described = [];
    readonly List<ClassSlot> _classes = [];
    uint _player = GameObjUpdateMessages.InvalidObjectId;

    GameWorld? _world;
    Creature? _creature;
    bool _dirty = true;
    List<bool>? _validClasses;
    bool _awaitingClasses;
    /// <summary>The character level of a level sent and not yet answered; -1 when none.</summary>
    int _pendingLevel = -1;
    /// <summary>The character level a confirmed level brings, until the server's update shows it; -1 when none.</summary>
    int _confirmedLevel = -1;

    /// <summary>Whether the server says a level is due (the level-up button).</summary>
    public bool CanLevelUp => Me?.CanLevelUp == true;

    /// <summary>What is known of the controlled creature; null before the server has described it.</summary>
    Described? Me => _described.GetValueOrDefault(_player);

    /// <summary>
    /// The creature the player controls. Levels are planned for it alone: taking control of another (possessing a
    /// familiar, and back) ends a level-up in progress.
    /// </summary>
    public uint Player
    {
        get => _player;
        set
        {
            if (value == _player) return;
            if (_awaitingClasses || _pendingLevel >= 0)
                Refused([new RuleError("control", "Control of the character changed.")]);
            _player = value;
            rules.Subject = value;
            (_validClasses, _awaitingClasses, _pendingLevel, _confirmedLevel) = (null, false, -1, -1);
            _classes.Clear();
            _dirty = true;
        }
    }

    // -- server to client ---------------------------------------------------

    /// <summary>The object list's entries about the player's creature.</summary>
    public void ObjectList(GameObjUpdateMessages.GameObjUpdateObjList list)
    {
        foreach (var entry in list.Entries)
        {
            switch (entry)
            {
                case GameObjUpdateObjListUpdateCreature { PrimaryAttributes: { } a } u:
                    Keep(_attributes, u.ObjectId, a);
                    _dirty |= u.ObjectId == _player;
                    break;
                case GameObjUpdateObjListAppearanceCreature { Name.Value: OriginalCreatureName n } a:
                    Keep(_names, a.ObjectId, $"{Text(n.First)} {Text(n.Last)}".Trim());
                    break;
                case GameObjUpdateObjListPlayerInfo p:
                    PlayerInfo(p);
                    break;
                case GameObjUpdateMessages.ObjListGuiEntry { Value: GuiCharacterSheet { Experience: { } xp } s } when _described.TryGetValue(s.ObjectId, out var d):
                    d.Experience = xp;
                    _dirty = true;
                    break;
            }
        }
        if (_confirmedLevel >= 0 && _attributes.GetValueOrDefault(_player) is { } shown && shown.Classes.Sum(k => k.Level) >= _confirmedLevel)
            _confirmedLevel = -1;
        AnswerOpen();
    }

    /// <summary>Keeps a creature's value; past a few hundred creatures only the player's are kept.</summary>
    void Keep<T>(Dictionary<uint, T> values, uint id, T value)
    {
        if (values.Count >= 512 && !values.ContainsKey(id))
            foreach (var other in values.Keys.Where(k => k != _player).ToList()) values.Remove(other);
        values[id] = value;
    }

    void PlayerInfo(GameObjUpdateObjListPlayerInfo p)
    {
        _dirty = true;
        if (!_described.TryGetValue(p.ObjectId, out var d))
        {
            if (_described.Count >= 64) _described.Clear();
            _described[p.ObjectId] = d = new Described();
        }
        // Control of a creature begins with its player info in full: its feats and known spells are additions to
        // nothing. Its ability scores come only when they change.
        if ((p.Flags & UpdatePlayerCreatureFlags.Newobject) != 0)
        {
            d.Feats.Clear();
            foreach (var k in d.Known) k.Clear();
            d.Complete = true;
        }
        if (p.AcGoldWeightLevelup is { } agw)
        {
            d.CanLevelUp = agw.CanLevelUp;
            if (agw.LevelUpInfo is { } info)
            {
                d.SkillPoints = info.SkillPts;
                d.Ranks = [.. info.SkillRanks];
                (d.Familiar, d.FamiliarName) = (info.FamiliarType, info.FamiliarName);
                (d.Companion, d.CompanionName) = (info.CompanionType, info.CompanionName);
                d.Package = info.StartingPackageNew <= byte.MaxValue ? (int)info.StartingPackageNew : 255;
            }
        }
        if (p.AbilityScores?.Values is { } v)
        {
            int[] scores = [v.StrBase, v.DexBase, v.ConBase, v.IntBase, v.WisBase, v.ChaBase];
            scores.CopyTo(d.Abilities, 0);
        }
        // The creature's own feats. Bonus feats (from items and effects) are left out: enforce legal characters
        // doesn't count them towards a level's feat prerequisites (measured).
        if (p.Feats is { } feats)
            foreach (var c in feats.Changes)
                switch ((FeatChangeType)c.ChangeType)
                {
                    case FeatChangeType.Add: d.Feats.Add(c.FeatId); break;
                    case FeatChangeType.Delete: d.Feats.Remove(c.FeatId); break;
                }
        if (p.KnownSpells is { } known)
            for (int slot = 0; slot < Math.Min(d.Known.Length, known.Count); slot++)
            {
                foreach (var s in known[slot].SpellsToDelete) d.Known[slot].Remove(unchecked((int)s));
                foreach (var s in known[slot].SpellsToAdd) d.Known[slot].Add(unchecked((int)s));
            }
    }

    /// <summary>The server's level-up answers; true when the message was one.</summary>
    public bool ToClient(NwnMessage message)
    {
        switch (message)
        {
            case LevelupMessages.LevelUpBeginS2C begin:
                _validClasses = begin.ValidClasses;
                AnswerOpen();
                return true;
            case LevelupMessages.LevelUpConfirm:
                if (_pendingLevel < 0) return true;
                int level = _pendingLevel;
                _confirmedLevel = level;
                _pendingLevel = -1;
                rules.Notify("levelup-result", new { ok = true, errors = Array.Empty<object>(), level });
                return true;
            case LevelupMessages.LevelUpDeny:
                _awaitingClasses = false;
                Refused([new RuleError("server", "The server refused the level.")]);
                return true;
            case LevelupMessages.LevelUpButtonOn:
                if (Me is { } me) me.CanLevelUp = true;
                return true;
            case ClientSideMessageFeedbackMessages.ClientSideMessageFeedback { Payload.Value: ClientSideMessageFeedbackMessages.FeedbackStatsLevelUp }:
                // A level is due: the line the original writes for it.
                if (rules.World?.Resources.TlkString(LevelDue) is { Length: > 0 } line)
                    rules.Emit(new ChatMessage { Channel = ChatChannel.Server, Text = line }.Write());
                return false;
        }
        return false;
    }

    /// <summary>
    /// Answers the opened window once the server has said which classes it allows and has shown the last level taken
    /// (planning on the creature from before it would plan the same level again).
    /// </summary>
    void AnswerOpen()
    {
        if (!_awaitingClasses || _validClasses == null || LevelInFlight) return;
        _awaitingClasses = false;
        Guarded(() =>
        {
            if (Creature() is not { } c) throw new InvalidOperationException("the server hasn't described the character yet");
            rules.Notify("levelup-data", Summary(rules.World!, c));
        });
    }

    /// <summary>A level sent and not answered, or confirmed and not yet shown in the server's update.</summary>
    bool LevelInFlight => _pendingLevel >= 0 || (_confirmedLevel >= 0 && _classes.Sum(c => c.Level) < _confirmedLevel);

    void Refused(List<RuleError> errors)
    {
        _pendingLevel = -1;
        rules.Notify("levelup-result", new
        {
            ok = false,
            errors = errors.Select(e => new { code = e.Code, message = e.Message }).ToArray(),
            level = _classes.Sum(c => c.Level),
        });
    }

    // -- client to server ---------------------------------------------------

    /// <summary>The level-up window's requests; true when the request was one.</summary>
    public bool FromClient(string kind, string json)
    {
        if (kind is not ("levelup-data" or "levelup-apply")) return false;
        Guarded(() => Request(kind, json));
        return true;
    }

    /// <summary>Runs a step; a failure ends the level-up with an error rather than leaving the window waiting.</summary>
    void Guarded(Action run)
    {
        try { run(); }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _awaitingClasses = false;
            Refused([new RuleError("bridge", $"The level can't be worked out here: {e.Message}")]);
        }
    }

    void Request(string kind, string json)
    {
        var data = json.Length == 0 ? default : JsonDocument.Parse(json).RootElement;
        var choice = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("choice", out var ch) && ch.ValueKind == JsonValueKind.Object ? ch : (JsonElement?)null;
        if (kind == "levelup-data" && choice == null)
        {
            // Opening the window: the server says which classes may be taken, and answers nothing while it keeps
            // the window shut.
            _awaitingClasses = true;
            _validClasses = null;
            send(new LevelupMessages.LevelUpBegin());
            return;
        }
        if (Creature() is not { } c) throw new InvalidOperationException("the server hasn't described the character yet");
        var w = rules.World!;
        if (kind == "levelup-apply")
        {
            Apply(w, c, choice is { } chosen ? LevelChoice.FromJson(chosen) : new LevelChoice());
            return;
        }
        // A choice's options and errors (and the summary's hit points) come from the rules.
        OpenWinter.Game.Requests.ClientRequests.Handle(w, c, kind, json);
    }

    void Apply(GameWorld w, Creature c, LevelChoice ch)
    {
        var errors = new List<RuleError>();
        if (LevelInFlight) errors.Add(new("pending", "The server is still taking the last level."));
        else if (!CanLevelUp) errors.Add(new("xp", "You don't have enough experience to gain a level."));
        else if (_validClasses is { } valid && (ch.Class < 0 || ch.Class >= valid.Count || !valid[ch.Class]))
            errors.Add(new("class", "The server does not allow that class."));
        // The level's skill list is flagged in a 32-bit field: how a server with more skills takes the rest isn't known.
        if (ch.Skills.Any(k => k.Key >= SkillsInMessage && k.Value != 0))
            errors.Add(new("skills", "Skills past the 32nd can't be raised on this server yet."));
        if (errors.Count == 0)
        {
            var plan = ProgressionRules.Plan(w, c, ch);
            errors.AddRange(plan.Errors);
            if (errors.Count == 0)
            {
                _pendingLevel = plan.NewCharacterLevel;
                send(LevelUp(w, c, ch, plan));
                return;
            }
        }
        rules.Notify("levelup-result", new { ok = false, errors = errors.Select(e => new { code = e.Code, message = e.Message }).ToArray(), level = c.TotalLevel });
    }

    /// <summary>
    /// The level as nwmain sends it: the class, the raised ability when one is due, the hit points the level gives
    /// (the roll the summary showed, else rolled now), the ranks added to each skill raised, the skill points left,
    /// the feats chosen and granted, the spells learnt by spell level, the associates, and the class's domains or school. A
    /// sorcerer's or bard's exchange of a known spell is not offered (the window has none), so nothing is removed.
    /// </summary>
    public static LevelupMessages.LevelUpNull LevelUp(GameWorld w, Creature c, LevelChoice ch, LevelPlan plan)
    {
        int die = LevelUpRequests.HeldHitDie(c, ch.Class, plan.NewCharacterLevel)
            ?? LevelApplier.HitPointsForLevel(w, plan.NewCharacterLevel, plan.HitDie, w.System<ProgressionSystem>().AlwaysMaxHitPoints, w.System<ProgressionSystem>().Random);
        int skills = Math.Min(SkillsInMessage, w.Resources.TwoDA("skills").RowCount);
        var m = new LevelupMessages.LevelUpNull
        {
            ClassLeveledUpIn = (byte)ch.Class,
            HasAbilityGain = plan.AbilityIncrease,
            AbilityGain = (byte)Math.Max(0, ch.Ability),
            HitDie = (byte)die,
            SkillChanges = new LevelupMessages.LevelUpSkillChanges
            {
                Ranks = [.. Enumerable.Range(0, skills).Select(s => ch.SkillRanks(s) is var n and not 0 ? (sbyte?)n : null)],
            },
            SkillPointsRemaining = (ushort)Math.Max(0, plan.SkillPointsLeft),
            // The feats chosen, then those the class grants at the level: the server adds only the feats it is sent
            // (measured: a rogue's second level without Evasion stays without it).
            Feats = [.. ch.Feats.Concat(plan.GrantedFeats).Distinct().Select(f => (ushort)f)],
            SpellChanges = [.. Enumerable.Range(0, LevelupMessages.SpellLevels).Select(l => new LevelupMessages.LevelUpSpellLevelChanges
            {
                Added = [.. ch.Spells.GetValueOrDefault(l, []).Select(s => (uint)s)],
                Removed = [],
            })],
            FamiliarCreatureType = plan.PickFamiliar && ch.Familiar >= 0 ? ch.Familiar : c.FamiliarType,
            FamiliarName = plan.PickFamiliar && ch.Familiar >= 0 ? ch.FamiliarName : c.FamiliarName,
            AnimalCompanionCreatureType = plan.PickCompanion && ch.Companion >= 0 ? ch.Companion : c.CompanionType,
            AnimalCompanionName = plan.PickCompanion && ch.Companion >= 0 ? ch.CompanionName : c.CompanionName,
        };
        var existing = c.Classes.FirstOrDefault(k => k.Class == ch.Class);
        m.Domain1 = (byte)Math.Max(0, plan.PickDomains ? ch.Domain1 : existing?.Domain1 ?? 0);
        m.Domain2 = (byte)Math.Max(0, plan.PickDomains ? ch.Domain2 : existing?.Domain2 ?? 0);
        m.School = (byte)Math.Max(0, plan.PickSchool ? ch.School : existing?.School ?? 0);
        return m;
    }

    /// <summary>What the level-up window opens with, the classes the server allows marked available.</summary>
    object Summary(GameWorld w, Creature c)
    {
        var summary = JsonSerializer.SerializeToNode(LevelUpRequests.Summary(w, c))!.AsObject();
        if (_validClasses is { } valid && summary["classes"] is JsonArray classes)
            foreach (var node in classes)
            {
                if (node is not JsonObject cls) continue;
                int id = cls["id"]!.GetValue<int>();
                bool allowed = id < valid.Count && valid[id];
                bool available = cls["available"]!.GetValue<bool>();
                if (allowed == available) continue;
                cls["available"] = allowed;
                cls["reasons"] = allowed ? new JsonArray() : new JsonArray("The server does not allow this class.");
            }
        summary["canLevelUp"] = CanLevelUp;
        return summary.ToJsonString();
    }

    // -- the player's creature ----------------------------------------------

    /// <summary>The player's creature as the rules see it, brought up to date; null before the server describes it.</summary>
    Creature? Creature()
    {
        if (Me is not { Complete: true } || rules.World is not { } w) return null;
        if (_creature == null || !ReferenceEquals(w, _world))
        {
            _world = w;
            _creature = w.Creatures.FromTemplate(CharacterFactory.BlankTemplate(w, new NewCharacter { Race = 0 }));
            CreatureRules.EnsureInit(w, _creature);
            _dirty = true;
        }
        if (_dirty) Sync(w, _creature, Me!);
        _dirty = false;
        return _creature;
    }

    void Sync(GameWorld w, Creature c, Described d)
    {
        if (_attributes.GetValueOrDefault(_player) is { } a)
        {
            c.Race = (byte)Math.Min(a.Race, (ushort)byte.MaxValue);
            c.Gender = a.Gender is 0 or 1 ? a.Gender : (byte)0;
            c.Subrace = a.Subrace;
            c.Deity = a.Deity;
            c.GoodEvil = a.AlignmentGoodEvil;
            c.LawfulChaotic = a.AlignmentLawChaos;
            _classes.Clear();
            foreach (var k in a.Classes)
                _classes.Add(new ClassSlot
                {
                    Class = k.ClassId, Level = k.Level, School = k.School ?? -1,
                    Domain1 = k.Domain1 ?? -1, Domain2 = k.Domain2 ?? -1,
                });
        }
        c.Name = new OpenWinter.Aurora.Formats.LocString(_names.GetValueOrDefault(_player, ""));
        // The server's base scores are the stored ones, before racial adjustments, as the creature keeps them.
        d.Abilities.CopyTo(c.Abilities, 0);
        c.Classes.Clear();
        for (int i = 0; i < _classes.Count; i++)
        {
            var s = _classes[i];
            var cc = new CreatureClass { Class = s.Class, Level = s.Level, Domain1 = s.Domain1, Domain2 = s.Domain2, School = s.School };
            // Known spells come by class slot, without their levels: the class's spell table gives them.
            if (i < d.Known.Length)
                foreach (var spell in d.Known[i])
                    if (CreatureRules.SpellLevelForClass(w, s.Class, spell) is var level and >= 0 and < 10) cc.KnownSpells[level].Add(spell);
            c.Classes.Add(cc);
        }
        c.Feats.Clear();
        c.Feats.AddRange(d.Feats);
        c.Skills = new int[Math.Max(32, d.Ranks.Length)];
        for (int i = 0; i < d.Ranks.Length; i++) c.Skills[i] = d.Ranks[i];
        c.Template.SetWord("SkillPoints", d.SkillPoints);
        c.StartingPackage = d.Package;
        (c.FamiliarType, c.FamiliarName) = (d.Familiar, d.FamiliarName);
        (c.CompanionType, c.CompanionName) = (d.Companion, d.CompanionName);
        // The experience shows only on the character sheet; a level that is due needs at least the next level's.
        int total = c.TotalLevel;
        int next = total < CreatureRules.MaxLevel ? CreatureRules.XPForLevel(w, total + 1) : int.MaxValue;
        int xp = d.Experience is { } known ? (int)Math.Min(known, (uint)int.MaxValue) : CreatureRules.XPForLevel(w, Math.Max(1, total));
        c.Experience = d.CanLevelUp ? Math.Max(xp, next) : Math.Min(xp, next - 1);
    }

    string Text(NwnLocString s) => s.IsStrRef ? (rules.World?.Resources.TlkString(s.StrRef) ?? "") : s.Text;
}

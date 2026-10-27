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
public sealed class NwnProgression(NwnRules rules, Action<NwnMessage> send)
{
    sealed class ClassSlot
    {
        public int Class, Level, School = -1, Domain1 = -1, Domain2 = -1;
    }

    /// <summary>The creatures' rules fields and names from their updates (the player's come before its player info).</summary>
    readonly Dictionary<uint, PrimaryAttributes> _attributes = [];
    readonly Dictionary<uint, string> _names = [];
    readonly List<ClassSlot> _classes = [];
    readonly int[] _abilities = [10, 10, 10, 10, 10, 10];
    readonly SortedSet<int> _feats = [];
    readonly HashSet<int>[] _known = Enumerable.Range(0, 8).Select(_ => new HashSet<int>()).ToArray();
    sbyte[] _ranks = [];
    int _skillPoints, _package = 255;
    int _familiar = 255, _companion = 255;
    string _familiarName = "", _companionName = "";
    bool _canLevelUp;
    uint? _experience;
    uint _playerInfoFor = GameObjUpdateMessages.InvalidObjectId;

    GameWorld? _world;
    Creature? _creature;
    bool _dirty = true;
    List<bool>? _validClasses;
    bool _awaitingClasses;
    int _pendingLevel = -1;

    /// <summary>"Your character has gained enough experience to advance a level."</summary>
    const uint LevelDue = 8302;

    /// <summary>Whether the server says a level is due (the level-up button).</summary>
    public bool CanLevelUp => _canLevelUp;

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
                    _dirty |= u.ObjectId == _playerInfoFor;
                    break;
                case GameObjUpdateObjListAppearanceCreature { Name.Value: OriginalCreatureName n } a:
                    Keep(_names, a.ObjectId, $"{Text(n.First)} {Text(n.Last)}".Trim());
                    break;
                case GameObjUpdateObjListPlayerInfo p:
                    PlayerInfo(p);
                    break;
                case GameObjUpdateMessages.ObjListGuiEntry { Value: GuiCharacterSheet { Experience: { } xp } s } when s.ObjectId == _playerInfoFor:
                    _experience = xp;
                    _dirty = true;
                    break;
            }
        }
    }

    /// <summary>Keeps a creature's value; past a few hundred creatures only the player's are kept.</summary>
    void Keep<T>(Dictionary<uint, T> values, uint id, T value)
    {
        if (values.Count >= 512 && !values.ContainsKey(id))
            foreach (var other in values.Keys.Where(k => k != _playerInfoFor).ToList()) values.Remove(other);
        values[id] = value;
    }

    void PlayerInfo(GameObjUpdateObjListPlayerInfo p)
    {
        _playerInfoFor = p.ObjectId;
        rules.Subject = p.ObjectId;
        _dirty = true;
        if (p.AcGoldWeightLevelup is { } agw)
        {
            _canLevelUp = agw.CanLevelUp;
            if (agw.LevelUpInfo is { } info)
            {
                _skillPoints = info.SkillPts;
                _ranks = [.. info.SkillRanks];
                (_familiar, _familiarName) = (info.FamiliarType, info.FamiliarName);
                (_companion, _companionName) = (info.CompanionType, info.CompanionName);
                _package = (int)info.StartingPackageNew;
            }
        }
        if (p.AbilityScores?.Values is { } v)
        {
            int[] scores = [v.StrBase, v.DexBase, v.ConBase, v.IntBase, v.WisBase, v.ChaBase];
            scores.CopyTo(_abilities, 0);
        }
        if (p.Feats is { } feats)
            foreach (var c in feats.Changes)
                switch ((FeatChangeType)c.ChangeType)
                {
                    case FeatChangeType.Add: _feats.Add(c.FeatId); break;
                    case FeatChangeType.Delete: _feats.Remove(c.FeatId); break;
                }
        if (p.KnownSpells is { } known)
            for (int slot = 0; slot < Math.Min(_known.Length, known.Count); slot++)
            {
                foreach (var s in known[slot].SpellsToDelete) _known[slot].Remove((int)s);
                foreach (var s in known[slot].SpellsToAdd) _known[slot].Add((int)s);
            }
    }

    /// <summary>The server's level-up answers; true when the message was one.</summary>
    public bool ToClient(NwnMessage message)
    {
        switch (message)
        {
            case LevelupMessages.LevelUpBeginS2C begin:
                _validClasses = begin.ValidClasses;
                if (_awaitingClasses)
                {
                    _awaitingClasses = false;
                    if (Creature() is { } c) rules.Notify("levelup-data", Summary(rules.World!, c));
                }
                return true;
            case LevelupMessages.LevelUpConfirm:
                Result(ok: true, []);
                return true;
            case LevelupMessages.LevelUpDeny:
                _awaitingClasses = false;
                Result(ok: false, [new RuleError("server", "The server refused the level.")]);
                return true;
            case LevelupMessages.LevelUpButtonOn:
                _canLevelUp = true;
                return true;
            case ClientSideMessageFeedbackMessages.ClientSideMessageFeedback { Payload.Value: ClientSideMessageFeedbackMessages.FeedbackStatsLevelUp }:
                // A level is due: the line the original writes for it.
                if (rules.World?.Resources.TlkString(LevelDue) is { Length: > 0 } line)
                    rules.Emit(new ChatMessage { Channel = ChatChannel.Server, Text = line }.Write());
                return false;
        }
        return false;
    }

    void Result(bool ok, List<RuleError> errors)
    {
        if (_pendingLevel < 0 && ok) return;
        int level = ok ? _pendingLevel : _classes.Sum(c => c.Level);
        _pendingLevel = -1;
        rules.Notify("levelup-result", new
        {
            ok,
            errors = errors.Select(e => new { code = e.Code, message = e.Message }).ToArray(),
            level,
        });
    }

    // -- client to server ---------------------------------------------------

    /// <summary>The level-up window's requests; true when the request was one.</summary>
    public bool FromClient(string kind, string json)
    {
        if (kind is not ("levelup-data" or "levelup-apply")) return false;
        var data = json.Length == 0 ? default : JsonDocument.Parse(json).RootElement;
        var choice = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("choice", out var ch) && ch.ValueKind == JsonValueKind.Object ? ch : (JsonElement?)null;
        if (Creature() is not { } c) return true;
        var w = rules.World!;
        if (kind == "levelup-apply")
        {
            Apply(w, c, choice is { } chosen ? LevelChoice.FromJson(chosen) : new LevelChoice());
            return true;
        }
        if (choice == null)
        {
            // Opening the window: the server says which classes may be taken, and answers nothing while it keeps
            // the window shut.
            _awaitingClasses = true;
            send(new LevelupMessages.LevelUpBegin());
            return true;
        }
        // A choice's options and errors (and the summary's hit points) come from the rules.
        OpenWinter.Game.Requests.ClientRequests.Handle(w, c, kind, json);
        return true;
    }

    void Apply(GameWorld w, Creature c, LevelChoice ch)
    {
        var errors = new List<RuleError>();
        if (!_canLevelUp) errors.Add(new("xp", "You don't have enough experience to gain a level."));
        else if (_validClasses is { } valid && (ch.Class < 0 || ch.Class >= valid.Count || !valid[ch.Class]))
            errors.Add(new("class", "The server does not allow that class."));
        var plan = ProgressionRules.Plan(w, c, ch);
        errors.AddRange(plan.Errors);
        if (errors.Count > 0)
        {
            rules.Notify("levelup-result", new { ok = false, errors = errors.Select(e => new { code = e.Code, message = e.Message }).ToArray(), level = c.TotalLevel });
            return;
        }
        _pendingLevel = plan.NewCharacterLevel;
        send(LevelUp(w, c, ch, plan));
    }

    /// <summary>
    /// The level as nwmain sends it: the class, the raised ability when one is due, the hit points the level gives
    /// (the roll the summary showed, else rolled now), the ranks added to each skill raised, the skill points left,
    /// the feats chosen, the spells learnt by spell level, the associates, and the class's domains or school.
    /// </summary>
    public static LevelupMessages.LevelUpNull LevelUp(GameWorld w, Creature c, LevelChoice ch, LevelPlan plan)
    {
        int die = LevelUpRequests.HeldHitDie(c, ch.Class, plan.NewCharacterLevel)
            ?? LevelApplier.HitPointsForLevel(w, plan.NewCharacterLevel, plan.HitDie, w.System<ProgressionSystem>().AlwaysMaxHitPoints, w.System<ProgressionSystem>().Random);
        int skills = w.Resources.TwoDA("skills").RowCount;
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
            Feats = [.. ch.Feats.Select(f => (ushort)f)],
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
        summary["canLevelUp"] = _canLevelUp;
        return summary.ToJsonString();
    }

    // -- the player's creature ----------------------------------------------

    /// <summary>The player's creature as the rules see it, brought up to date; null before the server describes it.</summary>
    Creature? Creature()
    {
        if (_playerInfoFor == GameObjUpdateMessages.InvalidObjectId || rules.World is not { } w) return null;
        if (_creature == null || !ReferenceEquals(w, _world))
        {
            _world = w;
            _creature = w.Creatures.FromTemplate(CharacterFactory.BlankTemplate(w, new NewCharacter { Race = 0 }));
            CreatureRules.EnsureInit(w, _creature);
            _dirty = true;
        }
        if (_dirty) Sync(w, _creature);
        _dirty = false;
        return _creature;
    }

    void Sync(GameWorld w, Creature c)
    {
        if (_attributes.GetValueOrDefault(_playerInfoFor) is { } a)
        {
            c.Race = (byte)a.Race;
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
        c.Name = new OpenWinter.Aurora.Formats.LocString(_names.GetValueOrDefault(_playerInfoFor, ""));
        // The server's base scores are the stored ones, before racial adjustments, as the creature keeps them.
        _abilities.CopyTo(c.Abilities, 0);
        c.Classes.Clear();
        for (int i = 0; i < _classes.Count; i++)
        {
            var s = _classes[i];
            var cc = new CreatureClass { Class = s.Class, Level = s.Level, Domain1 = s.Domain1, Domain2 = s.Domain2, School = s.School };
            // Known spells come by class slot, without their levels: the class's spell table gives them.
            if (i < _known.Length)
                foreach (var spell in _known[i])
                    if (CreatureRules.SpellLevelForClass(w, s.Class, spell) is var level and >= 0 and < 10) cc.KnownSpells[level].Add(spell);
            c.Classes.Add(cc);
        }
        c.Feats.Clear();
        c.Feats.AddRange(_feats);
        c.Skills = new int[Math.Max(32, _ranks.Length)];
        for (int i = 0; i < _ranks.Length; i++) c.Skills[i] = _ranks[i];
        c.Template.SetWord("SkillPoints", _skillPoints);
        c.StartingPackage = _package;
        (c.FamiliarType, c.FamiliarName) = (_familiar, _familiarName);
        (c.CompanionType, c.CompanionName) = (_companion, _companionName);
        // The experience shows only on the character sheet; a level that is due needs at least the next level's.
        int total = c.TotalLevel;
        int next = total < CreatureRules.MaxLevel ? CreatureRules.XPForLevel(w, total + 1) : int.MaxValue;
        int xp = _experience is { } known ? (int)Math.Min(known, int.MaxValue) : CreatureRules.XPForLevel(w, Math.Max(1, total));
        c.Experience = _canLevelUp ? Math.Max(xp, next) : Math.Min(xp, next - 1);
    }

    string Text(NwnLocString s) => s.IsStrRef ? (rules.World?.Resources.TlkString(s.StrRef) ?? "") : s.Text;
}

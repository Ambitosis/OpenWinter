using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdatePlayerinfoMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The player's own creature as the server describes it: classes, feats and their uses, spells known, prepared and
/// left, skills, modes, the quickbar and the character sheet. Kept from the object list's player info, creature
/// updates and GUI entries, and given to the client as the "quickbar", "spellbook", "abilities", "character-sheet"
/// and "action-mode" notifies the OpenWinter server sends. The client's requests to use any of it (cast, use a
/// feat, skill or item, toggle a mode, rest, edit the quickbar, prepare a spell) become the matching NWN input.
/// </summary>
public sealed partial class NwnAbilities(INwnBridgeData data, Action<byte[]> emit, Action<NwnMessage> send, Func<double>? clock = null)
{
    /// <summary>Seconds, for telling when the character sheet has closed (tests give their own).</summary>
    readonly Func<double> _clock = clock ?? StopwatchSeconds();

    static Func<double> StopwatchSeconds()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        return () => watch.Elapsed.TotalSeconds;
    }

    /// <summary>The number of class slots NWN keeps spell lists for.</summary>
    const int ClassSlots = 8;
    const int SpellLevels = 10;

    sealed class ClassEntry
    {
        public int Class, Level, Domain1 = -1, Domain2 = -1, School = -1;
    }

    /// <summary>A prepared spell in a slot.</summary>
    readonly record struct Prepared(int Spell, int Meta, bool Ready, bool Domain);

    uint _player;
    readonly List<ClassEntry> _classes = [];
    readonly int[] _abilityBase = new int[6], _abilityFinal = new int[6];
    /// <summary>The creature's feats; the value is the uses left for feats that count them.</summary>
    readonly SortedDictionary<int, int?> _feats = [];
    readonly HashSet<int> _bonusFeats = [];
    /// <summary>The most uses each counted feat has had since it was first seen (its uses per day, after a rest).</summary>
    readonly Dictionary<int, int> _featMax = [];
    readonly Dictionary<(int Class, int Level, int Slot), Prepared> _prepared = [];
    /// <summary>Domain slots, kept while they are empty too.</summary>
    readonly HashSet<(int Class, int Level, int Slot)> _domainSlots = [];
    readonly int[,] _slots = new int[ClassSlots, SpellLevels];
    readonly int[,] _usesLeft = new int[ClassSlots, SpellLevels];
    readonly HashSet<int>[] _known = Enumerable.Range(0, ClassSlots).Select(_ => new HashSet<int>()).ToArray();
    readonly List<(int Index, int Spell, int CasterLevel, bool Ready)> _spellAbilities = [];
    int[] _formSpells = [];
    uint _skillsUsable;
    bool _stateKnown;
    short _ac;
    uint _gold;
    int _weight;
    bool _canLevelUp;
    short _hp, _maxHp, _tempHp;
    int _race = -1, _gender, _goodEvil = 50, _lawChaos = 50;
    string _subrace = "", _deity = "", _portrait = "";
    GuiCharacterSheet? _sheet;
    readonly Dictionary<int, (int Ranks, bool Usable)> _skillRanks = [];
    readonly List<(ushort Action, ushort Group, int Spell)> _queue = [];

    /// <summary>The player's items as the inventory knows them (all it carries and wears); null without one.</summary>
    public Func<IEnumerable<(uint Id, ItemAppearanceAndProps Look)>>? Carried { get; set; }

    /// <summary>The action queue as the HUD shows it ({id, action, spell, icon, name}), whenever it changes.</summary>
    public Action<JsonArray>? QueueChanged { get; set; }
    /// <summary>The player's items the server has described (on the quickbar or in an open inventory), by object id.</summary>
    readonly Dictionary<uint, ItemAppearanceAndProps> _items = [];
    /// <summary>Equipped items and their INVENTORY_SLOT_*.</summary>
    readonly Dictionary<uint, int> _equipped = [];
    string _name = "";
    float _walkRate, _runRate;
    NwnTagged?[] _quickbar = [];
    readonly List<GameObjUpdateMessages.GameObjUpdateObjList> _early = [];
    bool _quickbarWanted, _quickbarSent, _abilitiesWanted, _resting, _timingIsRest;

    /// <summary>The creature the player controls; its updates are the ones kept.</summary>
    public uint Player
    {
        get => _player;
        set
        {
            if (value == _player) return;
            // Control of another creature (possessing a familiar, or back): nothing of the last one carries over.
            if (_player != 0) Reset();
            _player = value;
            // The player's creature is described in the same frame as, and just before, control of it.
            var early = _early.ToArray();
            _early.Clear();
            foreach (var list in early) ObjectList(list);
            // A quickbar asked for before control arrived goes out now.
            if (_quickbarWanted && _quickbar.Length > 0) SendQuickbar();
        }
    }

    /// <summary>Forgets everything about the controlled creature; the server describes the next one afresh.</summary>
    void Reset()
    {
        _classes.Clear();
        Array.Clear(_abilityBase);
        Array.Clear(_abilityFinal);
        _feats.Clear();
        _bonusFeats.Clear();
        _featMax.Clear();
        _prepared.Clear();
        _domainSlots.Clear();
        Array.Clear(_slots);
        Array.Clear(_usesLeft);
        foreach (var k in _known) k.Clear();
        _spellAbilities.Clear();
        _formSpells = [];
        _skillsUsable = 0;
        _stateKnown = false;
        _modesOn.Clear();
        (_ac, _gold, _weight, _canLevelUp, _hp, _maxHp, _tempHp) = (0, 0, 0, false, 0, 0, 0);
        (_race, _gender, _goodEvil, _lawChaos, _subrace, _deity, _portrait, _name) = (-1, 0, 50, 50, "", "", "", "");
        (_walkRate, _runRate) = (0, 0);
        _skillRanks.Clear();
        _queue.Clear();
        QueueChanged?.Invoke([]);
        _items.Clear();
        _equipped.Clear();
        _quickbar = [];
        _quickbarSent = _resting = _timingIsRest = false;
        _preparable.Clear();
        ResetSheet();
    }

    /// <summary>A new area: the action queue and a rest don't survive the transition.</summary>
    public void AreaChanged()
    {
        _queue.Clear();
        QueueChanged?.Invoke([]);
        if (_resting) RestEnded("cancelled");
    }

    // -- server to client ---------------------------------------------------

    /// <summary>The object list's entries about the player: player info, the creature's own update and GUI panels.</summary>
    public void ObjectList(GameObjUpdateMessages.GameObjUpdateObjList list)
    {
        if (_player == 0)
        {
            if (_early.Count < 64) _early.Add(list);
            return;
        }
        bool spells = false, abilities = false, sheet = false;
        foreach (var entry in list.Entries)
        {
            // Anyone's spell: its conjuring, then its casting at the target.
            if (entry is GameObjUpdateObjListUpdateCreature { AnimTypeData.Spell: { } cast } caster) Casting(caster.ObjectId, cast, caster.AnimTypeData.CastTarget);
            switch (entry)
            {
                case GameObjUpdateObjListPlayerInfo p when p.ObjectId == _player:
                    PlayerInfo(p, ref spells, ref abilities, ref sheet);
                    break;
                case GameObjUpdateObjListUpdateCreature u when u.ObjectId == _player:
                    sheet |= Creature(u, ref spells);
                    break;
                case GameObjUpdateObjListAppearanceCreature a when a.ObjectId == _player:
                    abilities |= Appearance(a);
                    sheet = true;
                    break;
                case GameObjUpdateMessages.ObjListGuiEntry { Value: GuiCharacterSheet s } when s.ObjectId == _player:
                    CharacterSheet(s);
                    sheet = true;
                    break;
                case GameObjUpdateMessages.ObjListGuiEntry { Value: GuiQuickbarUseCount q }:
                    abilities |= UseCounts(q);
                    break;
                case GameObjUpdateMessages.ObjListGuiEntry { Value: GuiInventorySlotAdd a }:
                    _items[a.ItemId] = a.Item;
                    abilities = true;
                    break;
                case GameObjUpdateMessages.ObjListGuiEntry { Element: 'R' or 'r', Value: GuiRepositoryItemAdd a }:
                    _items[a.ItemId] = a.Item;
                    abilities = true;
                    break;
                case GameObjUpdateMessages.ObjListGuiEntry { Element: 'R' or 'r', Value: GuiRepositoryItemUpdate u } when _items.TryGetValue(u.ItemId, out var stack):
                    stack.StackSize = u.StackSize;
                    abilities = true;
                    break;
                case GameObjUpdateMessages.ObjListGuiEntry { Element: 'R' or 'r', Value: GuiRepositoryItemDelete d }:
                    // Gone from the backpack: kept only while it is still equipped.
                    abilities |= !_equipped.ContainsKey(d.ItemId) && _items.Remove(d.ItemId);
                    break;
            }
        }
        if (spells) SendSpellbook();
        if (abilities || spells) SendAbilities();
        // The sheet goes out only when the client asks for it (its polls while the sheet is open).
        if (sheet) _sheetDirty = true;
    }

    void PlayerInfo(GameObjUpdateObjListPlayerInfo p, ref bool spells, ref bool abilities, ref bool sheet)
    {
        var f = p.Flags;
        bool Has(UpdatePlayerCreatureFlags flag) => (f & flag) != 0;
        if (Has(UpdatePlayerCreatureFlags.AcGoldWeightLevelup) && p.AcGoldWeightLevelup is { } agw)
        {
            (_ac, _gold, _weight, _canLevelUp) = (agw.Ac, agw.Gold, agw.Weight, agw.CanLevelUp);
            sheet = true;
        }
        if (Has(UpdatePlayerCreatureFlags.Guiskill))
        {
            _skillsUsable = p.GuiSkillUseable;
            abilities = true;
        }
        if (Has(UpdatePlayerCreatureFlags.State)) State(p.PlayerState);
        if (Has(UpdatePlayerCreatureFlags.Polymorphspell) && p.PolymorphSpells is { } poly)
        {
            _formSpells = new[] { poly.SpellId1, poly.SpellId2, poly.SpellId3 }.All(s => s < 0) ? [] : [poly.SpellId1, poly.SpellId2, poly.SpellId3];
            spells = true;
        }
        if (Has(UpdatePlayerCreatureFlags.Guinumbermemorizedspells))
            foreach (var c in p.NumMemorizedSpellSlots ?? []) spells |= Levels(c, _slots);
        if (Has(UpdatePlayerCreatureFlags.Guiknownspelluses))
            foreach (var c in p.KnownSpellUses ?? []) spells |= Levels(c, _usesLeft);
        if (Has(UpdatePlayerCreatureFlags.Guiknownspell) && p.KnownSpells is { } known)
            for (int ci = 0; ci < Math.Min(ClassSlots, known.Count); ci++)
            {
                foreach (var s in known[ci].SpellsToDelete) _known[ci].Remove((int)s);
                foreach (var s in known[ci].SpellsToAdd) _known[ci].Add((int)s);
                spells |= known[ci].SpellsToDelete.Count + known[ci].SpellsToAdd.Count > 0;
            }
        if (Has(UpdatePlayerCreatureFlags.Guimemorizedspell) && p.MemorizedSpells is { } memorized)
            for (int ci = 0; ci < Math.Min(ClassSlots, memorized.Count); ci++)
            {
                foreach (var d in memorized[ci].SpellsToDelete) _prepared.Remove((ci, d.SpellLevel, d.SpellSlot));
                foreach (var m in memorized[ci].SpellsToAdd)
                {
                    _prepared[(ci, m.SpellLevel, m.SpellSlot)] = new Prepared((int)m.SpellId, m.MetaType, m.Readied, m.DomainSpell);
                    if (m.DomainSpell) _domainSlots.Add((ci, m.SpellLevel, m.SpellSlot));
                }
                spells |= memorized[ci].SpellsToDelete.Count + memorized[ci].SpellsToAdd.Count > 0;
            }
        if (Has(UpdatePlayerCreatureFlags.Feat) && p.Feats is { } feats)
        {
            foreach (var c in feats.Changes)
            {
                switch ((FeatChangeType)(byte)c.ChangeType)
                {
                    case FeatChangeType.Add: _feats.TryAdd(c.FeatId, null); break;
                    case FeatChangeType.BonusAdd: _feats.TryAdd(c.FeatId, null); _bonusFeats.Add(c.FeatId); break;
                    case FeatChangeType.Delete or FeatChangeType.BonusDelete: _feats.Remove(c.FeatId); _bonusFeats.Remove(c.FeatId); break;
                }
            }
            foreach (var u in feats.Uses)
            {
                _feats[u.FeatId] = u.UsesRemaining;
                _featMax[u.FeatId] = Math.Max(_featMax.GetValueOrDefault(u.FeatId), u.UsesRemaining);
            }
            abilities = sheet = true;
        }
        if (Has(UpdatePlayerCreatureFlags.ActionQueue) && p.ActionQueue is { } queue) Queue(queue);
        if (Has(UpdatePlayerCreatureFlags.AbilityScores) && p.AbilityScores?.Values is { } a)
        {
            int[] b = [a.StrBase, a.DexBase, a.ConBase, a.IntBase, a.WisBase, a.ChaBase];
            int[] v = [a.StrFinal, a.DexFinal, a.ConFinal, a.IntFinal, a.WisFinal, a.ChaFinal];
            b.CopyTo(_abilityBase, 0);
            v.CopyTo(_abilityFinal, 0);
            spells = sheet = true;
        }
        if (Has(UpdatePlayerCreatureFlags.Spelllikeability))
        {
            foreach (var change in p.SpellLikeAbilities ?? [])
            {
                switch (change.Value)
                {
                    case SpellLikeAbilityResize r:
                        _spellAbilities.RemoveAll(s => s.Index >= r.NewSize);
                        break;
                    case SpellLikeAbilityEntryUpdate u:
                        _spellAbilities.RemoveAll(s => s.Index == u.Index);
                        _spellAbilities.Add((u.Index, u.SpellId, u.CasterLevel, u.Readied));
                        break;
                }
                spells = true;
            }
        }
    }

    /// <summary>A per-level count update (slots or uses left) for one class.</summary>
    static bool Levels(SpellSlotClassUpdate c, int[,] into)
    {
        if (c.ClassIndex >= ClassSlots) return false;
        byte?[] levels = [c.Level0, c.Level1, c.Level2, c.Level3, c.Level4, c.Level5, c.Level6, c.Level7, c.Level8, c.Level9];
        bool changed = false;
        for (int l = 0; l < SpellLevels; l++)
            if (levels[l] is { } n) (into[c.ClassIndex, l], changed) = (n, true);
        return changed;
    }

    /// <summary>The player's own creature update: hit points, portrait, race, alignment and classes.</summary>
    bool Creature(GameObjUpdateObjListUpdateCreature u, ref bool spells)
    {
        bool changed = false;
        if (u.HpCurrent is { } hp) (_hp, changed) = (hp, true);
        if (u.HpMax is { } max) _maxHp = max;
        if (u.HpTemp is { } temp) _tempHp = temp;
        if (u.WalkRate is { } walk && walk > 0) (_walkRate, changed) = (walk / 1000f, true);
        if (u.RunRate is { } run && run > 0) _runRate = run / 1000f;
        if (u.PortraitId is { } portrait) (_portrait, changed) = (NwnWorldMirror.PortraitResRef(data, portrait, u.CustomPortrait), true);
        if (u.PrimaryAttributes is { } a)
        {
            (_race, _subrace, _deity, _gender, _goodEvil, _lawChaos) = (a.Race, a.Subrace, a.Deity, a.Gender, a.AlignmentGoodEvil, a.AlignmentLawChaos);
            _classes.Clear();
            foreach (var c in a.Classes)
                _classes.Add(new ClassEntry { Class = c.ClassId, Level = c.Level, Domain1 = c.Domain1 ?? -1, Domain2 = c.Domain2 ?? -1, School = c.School ?? -1 });
            spells = changed = true;
        }
        return changed;
    }

    /// <summary>The player's name and equipped items (slot bits: 1 &lt;&lt; INVENTORY_SLOT_*).</summary>
    bool Appearance(GameObjUpdateObjListAppearanceCreature a)
    {
        if ((a.Flags & UpdateCreatureAppearanceFlags.Name) != 0 && a.Name?.Value is { } name)
            _name = name switch
            {
                OriginalCreatureName o => $"{Text(o.First)} {Text(o.Last)}".Trim(),
                DisplayCreatureName d => d.Name,
                _ => _name,
            };
        if ((a.Flags & UpdateCreatureAppearanceFlags.ItemInfo) == 0 || a.EquippedItems is not { } items) return false;
        foreach (var tagged in items)
        {
            switch (tagged.Value)
            {
                case EquippedItemAdd add:
                    int slot = System.Numerics.BitOperations.Log2(add.Slot);
                    foreach (var old in _equipped.Where(kv => kv.Value == slot).Select(kv => kv.Key).ToArray()) _equipped.Remove(old);
                    _equipped[add.Oid] = slot;
                    _items[add.Oid] = add.Item;
                    break;
                case EquippedItemDelete del:
                    int gone = System.Numerics.BitOperations.Log2(del.Slot);
                    foreach (var old in _equipped.Where(kv => kv.Value == gone).Select(kv => kv.Key).ToArray()) _equipped.Remove(old);
                    break;
            }
        }
        return true;
    }

    /// <summary>Uses left on quickbar items' properties.</summary>
    bool UseCounts(GuiQuickbarUseCount q)
    {
        foreach (var b in q.Buttons)
        {
            if (b.ObjectType != (byte)GuiQuickbarMessages.ButtonObjectType.Item || !_items.TryGetValue(b.ItemId, out var item)) continue;
            while (item.UsesLeft.Uses.Count <= b.PropertyIndex) item.UsesLeft.Uses.Add(null);
            item.UsesLeft.Uses[b.PropertyIndex] = (byte)Math.Min(255, (int)b.Uses);
        }
        return q.Buttons.Count > 0;
    }

    void Queue(ActionQueueChanges changes)
    {
        foreach (var e in changes.Entries)
        {
            switch (e.Value)
            {
                case ActionQueueAdd a: _queue.Add((a.ActionId, a.GroupId, a.SpellId is { } s ? (int)s : -1)); break;
                case ActionQueueDelete d: _queue.RemoveAll(q => q.Group == d.GroupId && q.Action == d.Id); break;
            }
        }
        var shown = new JsonArray();
        foreach (var q in _queue)
            shown.Add(new JsonObject
            {
                ["id"] = QueueId((q.Action, q.Group)), ["action"] = q.Action, ["spell"] = q.Spell,
                ["icon"] = q.Spell >= 0 ? Lower(data.TwoDA("spells", q.Spell, "IconResRef")) : "",
                ["name"] = q.Spell >= 0 ? Tlk("spells", q.Spell, "Name") : "",
            });
        QueueChanged?.Invoke(shown);
    }

    /// <summary>Messages the abilities watch without taking: the rest's progress bar and items' uses left.</summary>
    public void ToClient(NwnMessage message)
    {
        switch (message)
        {
            case GuiTimingEventMessages.GuiTimingEventInfo timing: TimingEvent(timing); break;
            case ItemPropertyMessages.ItemPropertyUpdateUses uses: ItemUses(uses); break;
        }
    }

    /// <summary>
    /// A timed action's progress bar: resting (event type 6) is the client's "rest" notify. The bar's end names no
    /// event, so it ends a rest only when the rest's own bar was the one showing.
    /// </summary>
    public void TimingEvent(GuiTimingEventMessages.GuiTimingEventInfo m)
    {
        if (m.Starting)
        {
            _timingIsRest = m.EventType == TimingRest;
            if (!_timingIsRest) return;
            _resting = true;
            Notify("rest", new JsonObject { ["state"] = "started", ["duration"] = (m.Duration ?? 0) / 1000f, ["nwnxAnimation"] = -1 });
        }
        else
        {
            if (_timingIsRest && _resting) RestEnded("cancelled");
            _timingIsRest = false;
        }
    }

    const byte TimingRest = 6;

    /// <summary>Feedback about this area: a rest that finished or was cancelled.</summary>
    public void Feedback(ClientSideMessageFeedbackMessages.ClientSideMessageFeedback m)
    {
        switch (m.Payload.Value)
        {
            case ClientSideMessageFeedbackMessages.FeedbackRestFinishedRest when _resting: RestEnded("finished"); break;
            case ClientSideMessageFeedbackMessages.FeedbackRestCancelRest when _resting: RestEnded("cancelled"); break;
        }
    }

    void RestEnded(string state)
    {
        _resting = false;
        Notify("rest", new JsonObject { ["state"] = state, ["duration"] = 0f });
    }

    /// <summary>An item's uses left changed (a rest restores them, a use spends one).</summary>
    public void ItemUses(ItemPropertyMessages.ItemPropertyUpdateUses m)
    {
        if (!_items.TryGetValue(m.Item, out var item)) return;
        item.UsesLeft.Uses = [.. m.UsesLeft.Uses];
        SendAbilities();
    }

    /// <summary>
    /// A creature conjuring a spell (its animation carries the spell) or casting it (and the target): the client's
    /// "spell-conjure" and "spell-cast" notifies, with the spell's spells.2da conjure or cast effects and timing.
    /// </summary>
    void Casting(uint caster, GameObjUpdateAnimationMessages.CastAnimData spell, GameObjUpdateAnimationMessages.CastTargetData? target)
    {
        int id = (int)spell.SpellId;
        string Col(string column) => Lower(data.TwoDA("spells", id, column));
        float Seconds(string column) => Cell("spells", id, column, 0) / 1000f;
        var pos = target?.TargetPosition is { } p ? new JsonArray(p.Item1, p.Item2, p.Item3) : null;
        uint item = spell.CastItemId;
        if (target == null)
        {
            NotifyFor(caster, "spell-conjure", new JsonObject
            {
                ["spell"] = id, ["presentation"] = 0, ["target"] = GameObjUpdateMessages.InvalidObjectId, ["pos"] = null,
                ["duration"] = Seconds("ConjTime"), ["animation"] = Col("ConjAnim"), ["headVfx"] = Col("ConjHeadVisual"),
                ["handVfx"] = Col("ConjHandVisual"), ["groundVfx"] = Col("ConjGrndVisual"), ["sound"] = spell.Silent ? "" : Col("ConjSoundVFX"),
                ["voice"] = "", ["metaMagic"] = 0, ["fake"] = false,
            });
            return;
        }
        NotifyFor(caster, "spell-cast", new JsonObject
        {
            ["spell"] = id, ["presentation"] = 0, ["target"] = target.TargetOid ?? GameObjUpdateMessages.InvalidObjectId, ["pos"] = pos,
            ["duration"] = Seconds("CastTime"), ["recovery"] = 0f, ["animation"] = Col("CastAnim"), ["attackAnimation"] = "",
            ["headVfx"] = Col("CastHeadVisual"), ["handVfx"] = Col("CastHandVisual"), ["groundVfx"] = Col("CastGrndVisual"),
            ["sound"] = Col("CastSound"), ["item"] = item, ["fake"] = false,
        });
    }

    void NotifyFor(uint subject, string kind, JsonNode payload) =>
        emit(new NotifyMessage { Kind = kind, Subject = subject, Json = payload.ToJsonString() }.Write());

    // -- notifies -----------------------------------------------------------

    void Notify(string kind, JsonNode payload) =>
        emit(new NotifyMessage { Kind = kind, Subject = _player, Json = payload.ToJsonString() }.Write());

    // -- client to server ---------------------------------------------------

    /// <summary>Handles a client request in this area; false when it isn't one.</summary>
    public bool FromClient(string kind, string json)
    {
        JsonObject d;
        try { d = JsonNode.Parse(json.Length == 0 ? "{}" : json) as JsonObject ?? []; }
        catch (JsonException) { d = []; }
        try { return Handle(kind, d); }
        // A malformed request is dropped; the session goes on.
        catch (Exception e) when (e is InvalidOperationException or FormatException or JsonException) { return true; }
    }

    bool Handle(string kind, JsonObject d)
    {
        switch (kind)
        {
            case "quickbar":
                _quickbarWanted = true;
                if (_quickbar.Length > 0) SendQuickbar();
                return true;
            case "quickbar-set":
                QuickbarSet(d);
                return true;
            case "spellbook":
                SendSpellbook();
                return true;
            case "abilities":
                _abilitiesWanted = true;
                SendAbilities(Id(d, "target"));
                return true;
            case "character":
                Sheet(d);
                return true;
            case "cast-spell":
                CastSpell(d);
                return true;
            case "use-feat":
                UseFeat(d);
                return true;
            case "use-skill":
                UseSkill(d);
                return true;
            // Using an item's power (a potion, wand or scroll, on a target) is this area's; anything else about items
            // is the inventory's.
            case "use-item" or "item-use":
                return UseItem(d, kind == "item-use");
            case "action-mode":
                // A mode whose state bit is known is toggled only when it isn't already as asked.
                if (Int(d, "mode", -1) is var mode and >= 0 && (!ModeBits.ContainsKey(mode) || Bool(d, "on") != ModeOn(mode))) ToggleMode(mode);
                return true;
            case "counterspell":
                if (Id(d, "target") is var counter && counter != GameObjUpdateMessages.InvalidObjectId)
                    send(new InputMessages.InputToggleMode { Mode = InputMessages.ToggleMode.Counterspell, Target = counter });
                return true;
            case "rest":
                send(new InputMessages.InputRest());
                return true;
            case "clear-actions":
                ClearActions(d);
                return true;
            case "cancel-polymorph":
                send(new InputMessages.InputCancelPolymorph { Creature = _player });
                return true;
            case "spell-memorize":
                Memorize(d);
                return true;
        }
        return false;
    }

    /// <summary>
    /// clear-actions {id} or {index} (-1 or neither: all of them): a queued action is cancelled by its number and
    /// group, as the action queue named it; its id is both together (action &lt;&lt; 16 | group).
    /// </summary>
    void ClearActions(JsonObject d)
    {
        IEnumerable<(ushort Action, ushort Group, int Spell)> which = d["id"] != null
            ? _queue.Where(q => QueueId((q.Action, q.Group)) == Id(d, "id"))
            : Int(d, "index", -1) is var index and >= 0 ? _queue.Skip(index).Take(1) : _queue;
        foreach (var (action, group, _) in which.ToArray()) send(new InputMessages.InputCancelAction { ActionId = action, GroupId = group });
    }

    public static uint QueueId((ushort Action, ushort Group) q) => (uint)q.Action << 16 | q.Group;

    static int Int(JsonObject d, string key, int fallback = 0) =>
        d[key] is JsonValue v && (v.TryGetValue<int>(out var i) || v.TryGetValue<double>(out var f) && (i = (int)f) == i) ? i : fallback;

    static bool Bool(JsonObject d, string key) => d[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    static uint Id(JsonObject d, string key) =>
        d[key] is JsonValue v && v.TryGetValue<long>(out var l) && l > 0 ? (uint)l : GameObjUpdateMessages.InvalidObjectId;

    static (float, float, float)? Pos(JsonObject d)
    {
        static float? F(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var f) ? (float)f : null;
        return d["pos"] is JsonArray { Count: 3 } p && F(p[0]) is { } x && F(p[1]) is { } y && F(p[2]) is { } z ? (x, y, z) : null;
    }

    string Tlk(string table, int row, string column, string fallback = "") =>
        uint.TryParse(data.TwoDA(table, row, column), out var strRef) && data.Text(strRef, 0) is { Length: > 0 } s ? s : fallback;

    static int Hex(string raw) =>
        int.TryParse(raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw[2..] : raw, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;

    int Cell(string table, int row, string column, int fallback = -1) =>
        int.TryParse(data.TwoDA(table, row, column), out var v) ? v : fallback;

    static string Lower(string s) => s.ToLowerInvariant();

    string Text(NwnLocString l) => l.IsStrRef ? data.Text(l.StrRef, l.Gender) : l.Text;

    string ItemName(ItemAppearanceAndProps item) => NwnItems.Name(data, item);

    /// <summary>The player's items: the inventory's when it has looked, otherwise those seen on the bar and worn.</summary>
    IEnumerable<(uint Id, ItemAppearanceAndProps Look)> KnownItems()
    {
        var carried = Carried?.Invoke().ToList() ?? [];
        return carried.Count > 0 ? carried : _items.Select(kv => (kv.Key, kv.Value));
    }

    ItemAppearanceAndProps? Look(uint id) =>
        Carried?.Invoke().Where(i => i.Id == id).Select(i => i.Look).FirstOrDefault() ?? _items.GetValueOrDefault(id);
}

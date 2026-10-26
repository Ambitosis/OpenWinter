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
public sealed partial class NwnAbilities(INwnBridgeData data, Action<byte[]> emit, Action<NwnMessage> send)
{
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
    readonly int[,] _slots = new int[ClassSlots, SpellLevels];
    readonly int[,] _usesLeft = new int[ClassSlots, SpellLevels];
    readonly HashSet<int>[] _known = Enumerable.Range(0, ClassSlots).Select(_ => new HashSet<int>()).ToArray();
    readonly List<(int Index, int Spell, int CasterLevel, bool Ready)> _spellAbilities = [];
    int[] _formSpells = [];
    uint _skillsUsable, _state;
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
    readonly List<(ushort Action, ushort Group)> _queue = [];
    /// <summary>The player's items the server has described (on the quickbar or in an open inventory), by object id.</summary>
    readonly Dictionary<uint, ItemAppearanceAndProps> _items = [];
    /// <summary>Equipped items and their INVENTORY_SLOT_*.</summary>
    readonly Dictionary<uint, int> _equipped = [];
    string _name = "";
    float _walkRate, _runRate;
    NwnTagged?[] _quickbar = [];
    readonly List<GameObjUpdateMessages.GameObjUpdateObjList> _early = [];
    bool _quickbarWanted, _abilitiesWanted, _sheetOpen;
    int _sheetVersion;
    string _sheetJson = "";

    /// <summary>The creature the player controls; its updates are the ones kept.</summary>
    public uint Player
    {
        get => _player;
        set
        {
            if (value == _player) return;
            _player = value;
            // The player's creature is described in the same frame as, and just before, control of it.
            var early = _early.ToArray();
            _early.Clear();
            foreach (var list in early) ObjectList(list);
            // A quickbar asked for before control arrived goes out now.
            if (_quickbarWanted && _quickbar.Length > 0) SendQuickbar();
        }
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
            }
        }
        if (spells) SendSpellbook();
        if (abilities || spells) SendAbilities();
        if (sheet) SendSheet(force: false);
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
                    _prepared[(ci, m.SpellLevel, m.SpellSlot)] = new Prepared((int)m.SpellId, m.MetaType, m.Readied, m.DomainSpell);
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
        if (u.CustomPortrait is { Length: > 0 } custom) (_portrait, changed) = (custom, true);
        else if (u.PortraitId is { } id && id < 65534) (_portrait, changed) = (data.TwoDA("portraits", id, "BaseResRef") is { Length: > 0 } b ? "po_" + b : "", true);
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
                case ActionQueueAdd a: _queue.Add((a.ActionId, a.GroupId)); break;
                case ActionQueueDelete d: _queue.RemoveAll(q => q.Group == d.GroupId && q.Action == d.Id); break;
            }
        }
    }

    // -- notifies -----------------------------------------------------------

    void Notify(string kind, JsonNode payload) =>
        emit(new NotifyMessage { Kind = kind, Subject = _player, Json = payload.ToJsonString() }.Write());

    // -- client to server ---------------------------------------------------

    /// <summary>Handles a client request in this area; false when it isn't one.</summary>
    public bool FromClient(string kind, string json)
    {
        var d = JsonNode.Parse(json.Length == 0 ? "{}" : json) as JsonObject ?? [];
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
                // The original sends the open sheet's panel to the server, which then keeps it filled: the main
                // panel brings abilities, saves, base attack and combat figures, the skills panel the skills. The
                // skills panel is asked for once the main panel's figures have come (CharacterSheet).
                if (!_sheetOpen && _player != 0) send(new GuiCharacterSheetMessages.GuiCharacterSheetStatus { ActivePanel = 0, Creature = _player });
                _sheetOpen = true;
                // A poll that already has the current sheet gets nothing.
                SendSheet(force: d["version"] is not JsonValue v || v.GetValue<string>() != _sheetVersion.ToString());
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
            case "use-item" or "item-use":
                UseItem(d, kind == "item-use");
                return true;
            case "action-mode":
                if (Int(d, "mode", -1) is var mode and >= 0 && Bool(d, "on") != ModeOn(mode)) ToggleMode(mode);
                return true;
            case "counterspell":
                send(new InputMessages.InputToggleMode { Mode = InputMessages.ToggleMode.Counterspell, Target = Id(d, "target") });
                return true;
            case "rest":
                send(new InputMessages.InputRest());
                return true;
            case "clear-actions":
                // Each queued action is cancelled by its number and group, as the action queue shows them.
                foreach (var (action, group) in _queue.ToArray()) send(new InputMessages.InputCancelAction { ActionId = action, GroupId = group });
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

    static int Int(JsonObject d, string key, int fallback = 0) =>
        d[key] is JsonValue v && (v.TryGetValue<int>(out var i) || v.TryGetValue<double>(out var f) && (i = (int)f) == i) ? i : fallback;

    static bool Bool(JsonObject d, string key) => d[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    static uint Id(JsonObject d, string key) =>
        d[key] is JsonValue v && v.TryGetValue<long>(out var l) && l > 0 ? (uint)l : GameObjUpdateMessages.InvalidObjectId;

    static (float, float, float)? Pos(JsonObject d) =>
        d["pos"] is JsonArray { Count: 3 } p ? ((float)p[0]!.GetValue<double>(), (float)p[1]!.GetValue<double>(), (float)p[2]!.GetValue<double>()) : null;

    string Tlk(string table, int row, string column, string fallback = "") =>
        uint.TryParse(data.TwoDA(table, row, column), out var strRef) && data.Text(strRef, 0) is { Length: > 0 } s ? s : fallback;

    static int Hex(string raw) =>
        int.TryParse(raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw[2..] : raw, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;

    int Cell(string table, int row, string column, int fallback = -1) =>
        int.TryParse(data.TwoDA(table, row, column), out var v) ? v : fallback;

    static string Lower(string s) => s.ToLowerInvariant();

    string Text(NwnLocString l) => l.IsStrRef ? data.Text(l.StrRef, l.Gender) : l.Text;

    string ItemName(ItemAppearanceAndProps item) => item.Name?.Value switch
    {
        OriginalItemName o => Text(o.Name),
        DisplayItemName n => n.Name,
        _ => Tlk("baseitems", (int)item.BaseItemId, "Name"),
    };
}

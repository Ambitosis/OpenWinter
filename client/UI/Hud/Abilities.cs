using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>A sub-choice of a feat or skill (a wild shape, "Arm" for called shot, "Examine Trap").</summary>
public sealed record SubChoice(int Id, string Name, string Icon, int TargetType);

public sealed record FeatAbility(int Feat, string Name, string Icon, int Uses, int Max, int TargetType, bool Hostile, int Master,
    string MasterName, string MasterIcon, SubChoice[] Subs)
{
    public bool Unlimited => Max < 0;
    public bool Ready => Unlimited || Uses > 0;
}

public sealed record ModeAbility(int Mode, string Name, string Icon, bool On, int Feat, bool Combat);

public sealed record SkillAbility(int Skill, string Name, string Icon, int Rank, int TargetType, bool Hostile, SubChoice[] Subs);

public sealed record ItemPower(int Index, string Name, int Spell, string Icon, int TargetType, bool Hostile, int Uses, bool Ready, string Key);

public sealed record ItemAbility(uint Item, string Name, string[] Icon, int Stack, int Charges, bool Identified, bool Equipped, bool Usable, ItemPower[] Powers, string Key);

/// <summary>
/// Everything the player can use, kept current from the server ("abilities", "spellbook", "item-info" and
/// "action-mode" notifies). Shared by the action bar, the radial menu and the abilities window.
/// </summary>
public sealed class AbilityBook
{
    public readonly ClientSession Session;
    public List<FeatAbility> Feats { get; private set; } = new();
    public List<ModeAbility> Modes { get; private set; } = new();
    public List<SkillAbility> Skills { get; private set; } = new();
    public List<ItemAbility> Items { get; private set; } = new();
    public List<BookSpell> Spells { get; private set; } = new();
    public uint SpellbookLoadedFor { get; private set; }
    /// <summary>The controlled creature is polymorphed: its class spells are greyed out and can't be cast.</summary>
    public bool Polymorphed { get; private set; }
    /// <summary>The form's abilities (polymorph.2da SPELL1-3, -1 when empty) the radial's Cast Spell wedge offers.</summary>
    public int[] FormAbilities { get; private set; } = [];
    public readonly Dictionary<int, string> ClassNames = new();
    public readonly Dictionary<int, string> ClassIcons = new();
    /// <summary>Name and icon of items that have no powers (put on the bar from the inventory).</summary>
    public readonly Dictionary<uint, (string Name, string[] Icon, bool Equippable)> ItemInfo = new();
    readonly Dictionary<uint, int> _equipmentSlots = new();
    public bool IsEquipped(uint item, int slot) => _equipmentSlots.GetValueOrDefault(item, -1) == slot;
    readonly Dictionary<uint, int> _equipableSlots = new();
    public int EquipableSlots(uint item)
    {
        if (!_equipableSlots.ContainsKey(item) && _askedInfo.Add(item)) Session.Request("item-info", new { item });
        return _equipableSlots.GetValueOrDefault(item);
    }
    public void RememberInventoryItem(ItemEntry item)
    {
        if (item.Info is not { } info) return;
        ItemInfo[item.Id] = (item.Name, item.Icon, info.Slots != 0);
        _equipableSlots[item.Id] = info.Slots;
    }
    /// <summary>Facts about the radial menu's target (locked, trapped, hostile, ...), by object id.</summary>
    public readonly Dictionary<uint, JsonElement> TargetFacts = new();
    public bool Loaded { get; private set; }
    /// <summary>The controlled creature whose abilities have been parsed.</summary>
    public uint LoadedFor { get; private set; }
    public event Action? Changed;
    public event Action<uint>? TargetFactsArrived;
    readonly HashSet<uint> _askedInfo = new();

    public AbilityBook(ClientSession session)
    {
        Session = session;
        Session.NotifyReceived += OnNotify;
        Session.ControlledChanged += Reset;
    }

    void Reset(uint controlled)
    {
        Loaded = false; LoadedFor = SpellbookLoadedFor = 0;
        Feats.Clear(); Modes.Clear(); Skills.Clear(); Items.Clear(); Spells.Clear();
        ClassNames.Clear(); ClassIcons.Clear(); ItemInfo.Clear(); TargetFacts.Clear();
        _equipmentSlots.Clear(); _equipableSlots.Clear(); _askedInfo.Clear();
        Changed?.Invoke();
    }

    /// <summary>Asks the server for everything (and to keep sending changes); with a target, facts about it too.</summary>
    public void Refresh(uint target = 0)
    {
        if (target != 0) Session.Request("abilities", new { target });
        else Session.Request("abilities");
    }

    public FeatAbility? Feat(int id) => Feats.FirstOrDefault(f => f.Feat == id);
    public ModeAbility? Mode(int id) => Modes.FirstOrDefault(m => m.Mode == id);
    public SkillAbility? Skill(int id) => Skills.FirstOrDefault(s => s.Skill == id);
    public ItemAbility? Item(uint id) => Items.FirstOrDefault(i => i.Item == id);
    // A shortcut follows this inventory object/stack, never another identical bottle.
    public ItemAbility? ResolveItem(BarSlot slot) => Item(slot.Item);

    public ItemPower? ResolvePower(BarSlot slot, ItemAbility item) => slot.Prop < 0
        ? item.Powers.FirstOrDefault(p => p.Ready) ?? item.Powers.FirstOrDefault()
        : slot.ItemPowerKey.Length > 0 ? item.Powers.FirstOrDefault(p => p.Key == slot.ItemPowerKey)
        : item.Powers.FirstOrDefault(p => p.Index == slot.Prop);

    public bool RememberItem(BarSlot slot)
    {
        if (slot.Kind != "item" || ResolveItem(slot) is not { } item) return false;
        bool changed = slot.Item != item.Item || slot.Name != item.Name || slot.ItemKey != item.Key
            || slot.ItemHasPower != (item.Powers.Length > 0) || !slot.ItemIcons.SequenceEqual(item.Icon);
        if (slot.Prop >= 0 && ResolvePower(slot, item) is { } power)
        {
            changed |= slot.Prop != power.Index || slot.ItemPowerKey != power.Key;
            slot.Prop = power.Index; slot.ItemPowerKey = power.Key;
        }
        slot.Item = item.Item; slot.Name = item.Name; slot.ItemKey = item.Key;
        slot.ItemIcons = item.Icon; slot.ItemHasPower = item.Powers.Length > 0;
        return changed;
    }

    public bool ModeOn(int mode) => Mode(mode)?.On == true;

    /// <summary>Name and icon of any carried item; asks the server once for items without powers.</summary>
    public (string Name, string[] Icon)? DescribeItem(uint id)
    {
        if (Item(id) is { } e) return (e.Name, e.Icon);
        if (ItemInfo.TryGetValue(id, out var info)) return (info.Name, info.Icon);
        if (_askedInfo.Add(id)) Session.Request("item-info", new { item = id });
        return null;
    }

    /// <summary>Spell totals across matching memorised copies; META_ANY includes all forms.</summary>
    public QuickbarSpellCounts SpellUses(int spell, int cls, int meta = 255) =>
        QuickbarSpellCounts.Read(Spells, SpellbookLoadedFor != 0 && SpellbookLoadedFor == Session.ControlledId, spell, cls, meta);

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "abilities" when m.Subject == Session.ControlledId:
                Parse(m.Json);
                break;
            case "spellbook" when m.Subject == Session.ControlledId:
                ParseSpellbook(m.Json);
                break;
            case "item-info" when m.Subject == Session.ControlledId:
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                ItemInfo[d.GetProperty("item").GetUInt32()] = (Str(d, "name"), Strings(d, "icon"), d.GetProperty("equippable").GetBoolean());
                if (d.TryGetProperty("equipSlots", out var mask)) _equipableSlots[d.GetProperty("item").GetUInt32()] = mask.GetInt32();
                if (d.TryGetProperty("equipped", out var equipped))
                {
                    uint item = d.GetProperty("item").GetUInt32();
                    if (equipped.GetBoolean() && d.TryGetProperty("slot", out var slot)) _equipmentSlots[item] = slot.GetInt32();
                    else _equipmentSlots.Remove(item);
                }
                Changed?.Invoke();
                break;
            }
            case "creature-equipment" when m.Subject == Session.ControlledId:
            {
                using var doc = JsonDocument.Parse(m.Json);
                uint id = doc.RootElement.GetProperty("item").GetUInt32();
                int slot = doc.RootElement.GetProperty("slot").GetInt32();
                bool equipped = slot >= 0;
                if (equipped) _equipmentSlots[id] = slot; else _equipmentSlots.Remove(id);
                Items = Items.Select(i => i.Item == id ? i with { Equipped = equipped } : i).ToList();
                Changed?.Invoke();
                break;
            }
            case "action-mode":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                if (d.GetProperty("creature").GetUInt32() != Session.ControlledId) return;
                int mode = d.GetProperty("mode").GetInt32();
                bool on = d.GetProperty("on").GetBoolean();
                Modes = Modes.Select(x => x.Mode == mode ? x with { On = on } : x).ToList();
                Changed?.Invoke();
                break;
            }
        }
    }

    void Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var d = doc.RootElement;
        Feats = d.GetProperty("feats").EnumerateArray().Select(f => new FeatAbility(
            f.GetProperty("feat").GetInt32(), Str(f, "name"), Str(f, "icon"), f.GetProperty("uses").GetInt32(), f.GetProperty("max").GetInt32(),
            f.GetProperty("targetType").GetInt32(), f.GetProperty("hostile").GetBoolean(), f.GetProperty("master").GetInt32(),
            Str(f, "masterName"), Str(f, "masterIcon"), Subs(f))).ToList();
        Modes = d.GetProperty("modes").EnumerateArray().Select(x => new ModeAbility(
            x.GetProperty("mode").GetInt32(), Str(x, "name"), Str(x, "icon"), x.GetProperty("on").GetBoolean(), x.GetProperty("feat").GetInt32(),
            x.GetProperty("combat").GetBoolean())).ToList();
        Skills = d.GetProperty("skills").EnumerateArray().Select(s => new SkillAbility(
            s.GetProperty("skill").GetInt32(), Str(s, "name"), Str(s, "icon"), s.GetProperty("rank").GetInt32(), s.GetProperty("targetType").GetInt32(),
            s.GetProperty("hostile").GetBoolean(), Subs(s))).ToList();
        Items = d.GetProperty("items").EnumerateArray().Select(i => new ItemAbility(
            i.GetProperty("item").GetUInt32(), Str(i, "name"), Strings(i, "icon"), i.GetProperty("stack").GetInt32(), i.GetProperty("charges").GetInt32(),
            i.GetProperty("identified").GetBoolean(), i.GetProperty("equipped").GetBoolean(), i.GetProperty("usable").GetBoolean(),
            i.GetProperty("props").EnumerateArray().Select(p => new ItemPower(p.GetProperty("index").GetInt32(), Str(p, "name"), p.GetProperty("spell").GetInt32(),
                Str(p, "icon"), p.GetProperty("targetType").GetInt32(), p.GetProperty("hostile").GetBoolean(), p.GetProperty("uses").GetInt32(),
                p.GetProperty("ready").GetBoolean(), Str(p, "key"))).ToArray(), Str(i, "key"))).ToList();
        foreach (var item in d.GetProperty("items").EnumerateArray())
            if (item.TryGetProperty("slot", out var slot)) _equipmentSlots[item.GetProperty("item").GetUInt32()] = slot.GetInt32();
        Loaded = true;
        LoadedFor = Session.ControlledId;
        if (d.TryGetProperty("target", out var t) && t.ValueKind == JsonValueKind.Object)
        {
            uint id = t.GetProperty("id").GetUInt32();
            TargetFacts[id] = t.Clone();
            TargetFactsArrived?.Invoke(id);
        }
        Changed?.Invoke();
    }

    void ParseSpellbook(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var d = doc.RootElement;
        var list = new List<BookSpell>();
        ClassNames.Clear();
        ClassIcons.Clear();
        foreach (var cls in d.GetProperty("classes").EnumerateArray())
        {
            int classId = cls.GetProperty("class").GetInt32();
            ClassNames[classId] = Str(cls, "name");
            ClassIcons[classId] = Str(cls, "icon");
            foreach (var level in cls.GetProperty("levels").EnumerateArray())
            {
                int lv = level.GetProperty("level").GetInt32();
                foreach (var s in level.GetProperty("spells").EnumerateArray())
                    list.Add(new BookSpell(s.GetProperty("spell").GetInt32(), classId, lv, s.GetProperty("meta").GetInt32(), s.GetProperty("ready").GetInt32(), s.GetProperty("total").GetInt32()));
            }
        }
        if (d.TryGetProperty("abilities", out var abilities))
            foreach (var ability in abilities.EnumerateArray())
                list.Add(new BookSpell(ability.GetProperty("spell").GetInt32(), 255, -1, 0, ability.GetProperty("ready").GetInt32(), ability.GetProperty("total").GetInt32()));
        Spells = list;
        Polymorphed = d.TryGetProperty("polymorphed", out var poly) && poly.ValueKind == JsonValueKind.True;
        FormAbilities = d.TryGetProperty("formAbilities", out var form) && form.ValueKind == JsonValueKind.Array
            ? form.EnumerateArray().Select(x => x.GetInt32()).ToArray() : [];
        SpellbookLoadedFor = Session.ControlledId;
        Changed?.Invoke();
    }

    /// <summary>A string field with NWN colour tokens removed (names show as plain text on the bar and radial).</summary>
    static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? Kit.NwnText.Strip(v.GetString() ?? "") : "";
    static string[] Strings(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : [];
    static SubChoice[] Subs(JsonElement e) => e.TryGetProperty("subs", out var subs)
        ? subs.EnumerateArray().Select(s => new SubChoice(s.GetProperty("id").GetInt32(), Str(s, "name"), Str(s, "icon"),
            s.TryGetProperty("targetType", out var tt) ? tt.GetInt32() : -1)).ToArray()
        : [];
}

/// <summary>
/// One thing on the action bar (or dragged around): a spell, item, feat, skill, mode, emote or command.
/// The drag-and-drop form is a Godot dictionary with "kind" plus the fields that kind uses, e.g.
/// {"kind":"spell","spell":id,"class":cls} from the spellbook or {"kind":"item","item":objectId} from the inventory.
/// </summary>
public sealed class BarSlot : OpenWinter.Content.QuickbarSlot
{
    public BarSlot Clone() => (BarSlot)MemberwiseClone();

    public Godot.Collections.Dictionary ToDrag()
    {
        var d = new Godot.Collections.Dictionary { ["kind"] = Kind };
        switch (Kind)
        {
            case "spell": d["spell"] = Id; d["class"] = Class; d["meta"] = Meta; break;
            case "item":
                d["item"] = (long)Item; d["prop"] = Prop; d["name"] = Name;
                d["paired"] = Paired; d["secondaryItem"] = (long)SecondaryItem;
                d["itemKey"] = ItemKey; d["itemPowerKey"] = ItemPowerKey; d["itemIcons"] = ItemIcons; d["itemHasPower"] = ItemHasPower; break;
            case "feat": d["feat"] = Id; d["sub"] = Sub; break;
            case "skill": d["skill"] = Id; d["sub"] = Sub; break;
            case "mode": d["mode"] = Id; break;
            case "emote": d["anim"] = Arg; d["name"] = Name; break;
            case "command": d["command"] = Arg; break;
            case "macro": d["label"] = Name; d["text"] = Text; break;
        }
        return d;
    }

    /// <summary>Reads drag data from any window; null when it isn't something the bar can hold.</summary>
    public static BarSlot? FromDrag(Variant data)
    {
        if (data.VariantType == Variant.Type.Object && Kit.ItemDragging.From(data) is { } drag)
            return new BarSlot { Kind = "item", Item = drag.Item.Id, Name = drag.Item.Name };
        if (data.VariantType != Variant.Type.Dictionary) return null;
        var d = data.AsGodotDictionary();
        if (!d.ContainsKey("kind")) return null;
        int I(string k, int fallback = 0) => d.ContainsKey(k) ? d[k].VariantType switch
        {
            Variant.Type.Int => (int)d[k].AsInt64(),
            Variant.Type.Float => (int)d[k].AsDouble(),
            Variant.Type.String => int.TryParse(d[k].AsString(), out var v) ? v : fallback,
            _ => fallback,
        } : fallback;
        string S(string k) => d.ContainsKey(k) ? d[k].AsString() : "";
        var kind = S("kind");
        var s = new BarSlot { Kind = kind };
        switch (kind)
        {
            case "spell": s.Id = I("spell", -1); s.Class = I("class", -1); s.Meta = I("meta", 255); break;
            case "item":
                s.Item = d.ContainsKey("item") ? (uint)d["item"].AsInt64() : 0; s.Prop = I("prop", -1); s.Name = S("name");
                s.Paired = d.ContainsKey("paired") && d["paired"].AsBool();
                s.SecondaryItem = d.ContainsKey("secondaryItem") ? (uint)d["secondaryItem"].AsInt64() : 0;
                s.ItemKey = S("itemKey"); s.ItemPowerKey = S("itemPowerKey"); s.ItemHasPower = d.ContainsKey("itemHasPower") && d["itemHasPower"].AsBool();
                s.ItemIcons = d.ContainsKey("itemIcons") ? d["itemIcons"].AsStringArray() : []; break;
            case "feat": s.Id = I("feat", -1); s.Sub = I("sub"); break;
            case "skill": s.Id = I("skill", -1); s.Sub = I("sub"); break;
            case "mode": s.Id = I("mode", -1); break;
            case "emote": s.Arg = S("anim"); s.Name = S("name"); break;
            case "command": s.Arg = S("command"); break;
            case "macro": s.Name = S("label"); s.Text = S("text"); break;
            default: return null;
        }
        return s.Kind == "item" ? (s.Paired || s.Item != 0 || s.ItemHasPower && s.ItemKey.Length > 0 ? s : null) : (s.Id >= 0 || s.Arg.Length > 0 ? s : null);
    }
}

/// <summary>Commands the bar and radial menu offer (emotes are <see cref="OpenWinter.Content.Emotes"/>).</summary>
public static class BarCatalog
{
    public static readonly (string Command, string Name, string Glyph, string Hint)[] Commands =
    {
        ("attack", "Attack", "attack", "Attack a creature or object"),
        ("rest", "Rest", "rest", "Rest to recover hit points, spells and feat uses"),
        ("examine", "Examine", "eye", "Examine a creature or object"),
        ("talk", "Talk", "talk", "Talk to a creature"),
        ("stance", "Combat stance", "combat", "Draw or sheathe your weapons"),
        // Offered by the quickbar slot's radial (docs/QUICKBAR_RADIAL.md).
        ("barter", "Barter", "party", "Barter with another player"),
        ("craft", "Crafting Skills", "use", "Craft a trap, potion, scroll or wand"),
        ("attacknearest", "Attack Nearest", "party", "Your associates attack the nearest enemy"),
        ("follow", "Follow", "party", "Your associates follow you"),
        ("standground", "Stand Your Ground", "party", "Your associates hold their ground"),
        ("guardme", "Guard Me", "party", "Your associates guard you"),
        ("healme", "Heal Me", "party", "Your associates heal you"),
    };

    /// <summary>The voice-chat command each associate order sends (sound-set slots, as the Issue Command ring).</summary>
    public static int AssociateVoice(string command) => command switch
    {
        "attacknearest" => 0, "follow" => 22, "standground" => 10, "guardme" => 9, "healme" => 4, _ => -1,
    };

    public static string EmoteName(string key) => OpenWinter.Content.Emotes.Find(key)?.Name ?? key;
    public static (string Command, string Name, string Glyph, string Hint) Command(string c) => Commands.FirstOrDefault(x => x.Command == c);

    /// <summary>Kit glyph for a mode without a game icon.</summary>
    public static string ModeGlyph(int mode) => mode switch
    {
        0 => "search", 1 => "stealth", 2 => "shield", 10 => "magic", _ => "aura",
    };
}

/// <summary>
/// Uses what the bar, radial menu and abilities window hold: sends the request, entering targeting mode
/// (the same one spells use) when the ability needs a target the player hasn't picked yet.
/// </summary>
public sealed class AbilityUser
{
    public const int TargetSelf = 1, TargetCreature = 2, TargetGround = 4, TargetItem = 8, TargetDoor = 16, TargetPlaceable = 32, TargetTrigger = 64;

    public required ClientSession Session;
    public required SpellCasting Casting;
    public required AbilityBook Book;
    public Interaction Interaction => Casting.Interaction;
    /// <summary>Opens the examine window (set by whoever owns it).</summary>
    public Action<uint>? Examine;
    /// <summary>A short message for the player (chat feedback when wired).</summary>
    public Action<string>? Feedback;
    /// <summary>Opens or closes the abilities window (set by the window; the bar's button and the radial call it).</summary>
    public Action? ToggleAbilitiesWindow;
    /// <summary>Fired when something is used (the bar flashes the slot).</summary>
    public event Action<BarSlot>? Used;
    /// <summary>What the pending targeting is for ("Choose a target for Cure Light Wounds").</summary>
    public string? TargetingFor { get; private set; }

    /// <summary>Uses a slot; <paramref name="target"/> is the object already chosen (radial menu), else the player picks one if needed.</summary>
    public void Use(BarSlot slot, ObjectView? target = null, System.Numerics.Vector3? ground = null)
    {
        var s = slot;
        switch (s.Kind)
        {
            case "spell":
            {
                // Polymorphed, the original's greyed spell slots do nothing (visual scene polymorphpc: F4).
                if (Book.Polymorphed && s.Class != 255) return;
                if (target != null)
                    Session.Request("cast-spell", new { spell = s.Id, @class = s.Class, meta = s.Meta, target = target.ObjectId, form = s.Arg == "form" });
                else
                {
                    var info = Casting.Info.GetValueOrDefault(s.Id);
                    Aim(info?.TargetType ?? TargetSelf, null, info?.Name ?? "the spell", (t, pos) =>
                    {
                        if (pos is { } p) Session.Request("cast-spell", new { spell = s.Id, @class = s.Class, meta = s.Meta, pos = new[] { p.X, p.Y, p.Z }, form = s.Arg == "form" });
                        else if (t is { } id) Session.Request("cast-spell", new { spell = s.Id, @class = s.Class, meta = s.Meta, target = id, form = s.Arg == "form" });
                        else Session.Request("cast-spell", new { spell = s.Id, @class = s.Class, meta = s.Meta, form = s.Arg == "form" });
                    }, ground: ground);
                }
                break;
            }
            case "feat":
            {
                var f = Book.Feat(s.Id);
                if (f == null && Book.Modes.FirstOrDefault(m => m.Feat == s.Id) is { } modeFeat) { ToggleMode(modeFeat.Mode); break; }
                if (f != null && !f.Ready) { Feedback?.Invoke($"No uses of {f.Name} left."); return; }
                int types = f?.TargetType ?? TargetSelf;
                if (s.Sub != 0 && f?.Subs.FirstOrDefault(x => x.Id == s.Sub) is { TargetType: > 0 } sub) types = sub.TargetType;
                Aim(types, target, f?.Name ?? "the feat", (t, pos) => Session.Request("use-feat", Args(new() { ["feat"] = s.Id, ["sub"] = s.Sub }, t, pos)), ground: ground);
                break;
            }
            case "skill":
            {
                var sk = Book.Skill(s.Id);
                if (s.Id is 5 or 8) { ToggleMode(1); break; } // Hide / Move Silently: stealth
                if (s.Id is 14 or 17 or 6) { ToggleMode(0); break; } // Search / Spot / Listen: detect
                Aim(sk?.TargetType ?? TargetCreature, target, sk?.Name ?? "the skill", (t, pos) =>
                    Session.Request("use-skill", Args(new() { ["skill"] = s.Id, ["sub"] = s.Sub }, t, null)), allowSelfDefault: (sk?.TargetType ?? 0) == TargetSelf);
                break;
            }
            case "item":
            {
                if (s.Paired)
                {
                    if (s.Item == 0 || s.SecondaryItem == 0) { Feedback?.Invoke("An item in this equipment pair is missing."); return; }
                    Session.Request("equip-pair", new { item = s.Item, secondaryItem = s.SecondaryItem });
                    break;
                }
                var item = Book.ResolveItem(s);
                if (item == null && (s.ItemHasPower || s.Prop >= 0)) { Feedback?.Invoke($"You have no {(s.Name.Length > 0 ? s.Name : "such item")} left."); return; }
                var power = item == null ? null : Book.ResolvePower(s, item);
                if (power == null && s.Prop >= 0) { Feedback?.Invoke("That item power is no longer available."); return; }
                if (power == null)
                {
                    // No powers: the bar equips (or takes off) the item, like the original's quickbar.
                    if (item?.Equipped == true) Session.Request("unequip", new { item = s.Item });
                    else Session.Request("equip", new { item = s.Item });
                    break;
                }
                if (!power.Ready || item!.Usable == false) { Feedback?.Invoke(item!.Usable ? $"{item.Name} has no uses left." : $"You cannot use {item.Name}."); return; }
                uint itemId = item.Item;
                Aim(power.TargetType, target, $"{item.Name}: {power.Name}", (t, pos) =>
                    Session.Request("use-item", Args(new() { ["item"] = (long)itemId, ["prop"] = power.Index }, t, pos)), ground: ground);
                break;
            }
            case "mode":
                ToggleMode(s.Id);
                break;
            case "emote":
                // The server plays the original's emote by its key (an older slot's name is mapped to it).
                Session.Send(new ActionRequestMessage { Action = ActionKind.Emote, Argument = OpenWinter.Content.Emotes.Find(s.Arg)?.Key ?? s.Arg }.Write());
                break;
            case "command":
                Command(s.Arg, target);
                break;
            case "macro":
                // A text macro sends its line as typed into chat; without a channel prefix it is shouted (quickbargaps).
                var (channel, text) = DM.ChatInput.Parse(s.Text, ChatChannel.Shout);
                if (text.Length > 0) Session.Send(new ChatSendMessage { Channel = channel, Text = text }.Write());
                break;
            default:
                return;
        }
        Used?.Invoke(slot);
    }

    public void ToggleMode(int mode) => Session.Request("action-mode", new { mode, on = !Book.ModeOn(mode) });

    void Command(string command, ObjectView? target)
    {
        switch (command)
        {
            case "rest": Session.Request("rest"); break;
            case "stance": Session.Send(new ActionRequestMessage { Action = ActionKind.ToggleStance }.Write()); break;
            case "attack":
                Aim(TargetCreature | TargetDoor | TargetPlaceable, target, "an attack", (t, _) => { if (t is { } id && id != Session.ControlledId) Session.Request("attack", new { target = id }); }, allowSelfDefault: false);
                break;
            case "examine":
                Aim(TargetSelf | TargetCreature | TargetDoor | TargetPlaceable | TargetItem, target, "examining", (t, _) => Examine?.Invoke(t ?? Session.ControlledId), allowSelfDefault: false);
                break;
            case "talk":
                Aim(TargetCreature, target, "talking", (t, _) => { if (t is { } id) Session.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = id }.Write()); }, allowSelfDefault: false);
                break;
            case "barter":
                Aim(TargetCreature, target, "bartering", (t, _) => { if (t is { } id && id != Session.ControlledId) Session.Request("barter-request", new { target = id }); }, allowSelfDefault: false);
                break;
            case "craft": Session.Request("craft"); break;
            default:
                if (BarCatalog.AssociateVoice(command) is var voice and >= 0) Session.Request("voice-chat", new { id = voice });
                break;
        }
    }

    static Dictionary<string, object> Args(Dictionary<string, object> d, uint? target, System.Numerics.Vector3? pos)
    {
        if (pos is { } p) d["pos"] = new[] { p.X, p.Y, p.Z };
        else if (target is { } t) d["target"] = (long)t;
        return d;
    }

    /// <summary>
    /// Calls <paramref name="send"/> with the target: the one given, the player for self-only abilities, else the
    /// object or ground point the player clicks next (right-click cancels). Clicks on things the ability can't
    /// target are refused with a message.
    /// </summary>
    internal void ResetControl(uint controlled)
    {
        Interaction.Targeting = null;
        TargetingFor = null;
    }

    public void Aim(int types, ObjectView? given, string what, Action<uint?, System.Numerics.Vector3?> send, bool allowSelfDefault = true, System.Numerics.Vector3? ground = null)
    {
        if (ground is { } point && (types & TargetGround) != 0) { send(null, point); return; }
        if (given != null) { send(given.ObjectId, null); return; }
        if (allowSelfDefault && (types == 0 || types == TargetSelf)) { send(null, null); return; }
        TargetingFor = what;
        Interaction.Targeting = (t, pickedGround) =>
        {
            Interaction.Targeting = null;
            TargetingFor = null;
            if (t != null)
            {
                if (Fits(types, t)) send(t.ObjectId, null);
                else Feedback?.Invoke($"That is not a valid target for {what}.");
            }
            else if (pickedGround is { } g && (types & TargetGround) != 0) send(null, g);
        };
        if ((types & TargetItem) != 0)
            Interaction.ItemTargeting = item => { TargetingFor = null; send(item, null); };
    }

    /// <summary>Whether target-type bits allow this object (the player counts as self and as a creature).</summary>
    public bool Fits(int types, ObjectView v)
    {
        if (v.ObjectId == Session.ControlledId) return (types & (TargetSelf | TargetCreature)) != 0;
        return v.Kind switch
        {
            ObjectKind.Creature => (types & TargetCreature) != 0,
            ObjectKind.Door => (types & TargetDoor) != 0,
            ObjectKind.Placeable => (types & TargetPlaceable) != 0,
            ObjectKind.Item => (types & TargetItem) != 0,
            ObjectKind.Trigger => (types & TargetTrigger) != 0,
            _ => false,
        };
    }

    // ------------------------------------------------------------------ presentation shared by the bar, radial and window

    /// <summary>What a slot shows: name, game icon layers (or a kit glyph), uses badge, toggled, usable, kind line for the tooltip.</summary>
    public SlotLook Look(BarSlot s)
    {
        switch (s.Kind)
        {
            case "spell" when s.Arg == "form":
            {
                // A polymorphed player's form ability (radial Cast Spell): no slot and no count.
                var info = Casting.Info.GetValueOrDefault(s.Id);
                return new SlotLook(info != null ? Kit.NwnText.Strip(info.Name) : $"Spell {s.Id}", info?.Icon is { Length: > 0 } ic ? [ic] : [], "magic",
                    -1, false, Book.FormAbilities.Contains(s.Id), "Form ability", "");
            }
            case "spell":
            {
                var info = Casting.Info.GetValueOrDefault(s.Id);
                var counts = Book.SpellUses(s.Id, s.Class, s.Meta);
                string cls = Book.ClassNames.GetValueOrDefault(s.Class, "");
                return new SlotLook(info != null ? Kit.NwnText.Strip(info.Name) : $"Spell {s.Id}", info?.Icon is { Length: > 0 } ic ? [ic] : [], "magic",
                    counts.Badge, false, counts.Usable && !(Book.Polymorphed && s.Class != 255), cls.Length > 0 ? $"{cls} spell" : "Spell",
                    Book.Polymorphed && s.Class != 255 ? "You cannot cast spells while polymorphed."
                        : counts.Loaded ? $"{counts.Ready} of {counts.Total} ready" : "");
            }
            case "feat":
            {
                if (Book.Feat(s.Id) is { } f)
                {
                    var sub = s.Sub != 0 ? f.Subs.FirstOrDefault(x => x.Id == s.Sub) : null;
                    string name = sub != null ? $"{f.Name}: {sub.Name}" : f.Name;
                    string icon = sub?.Icon is { Length: > 0 } si ? si : f.Icon;
                    return new SlotLook(name, [icon], "feat", f.Unlimited ? -1 : f.Uses, false, f.Ready, "Feat",
                        f.Unlimited ? "" : $"{f.Uses} use{(f.Uses == 1 ? "" : "s")} left today");
                }
                if (Book.Modes.FirstOrDefault(m => m.Feat == s.Id) is { } mf)
                    return new SlotLook(mf.Name, [mf.Icon], BarCatalog.ModeGlyph(mf.Mode), -1, mf.On, true, "Mode", mf.On ? "On" : "Off");
                return new SlotLook($"Feat {s.Id}", [], "feat", -1, false, !Book.Loaded, "Feat", Book.Loaded ? "You can't use this feat" : "");
            }
            case "skill":
            {
                var sk = Book.Skill(s.Id);
                var sub = sk?.Subs.FirstOrDefault(x => x.Id == s.Sub);
                string name = s.Sub != 0 && sub?.Name is { Length: > 0 } subName ? subName : sk?.Name ?? $"Skill {s.Id}";
                string icon = sub?.Icon is { Length: > 0 } subIcon ? subIcon : sk?.Icon ?? "";
                return new SlotLook(name, icon.Length > 0 ? [icon] : [], "skill", -1, false,
                    sk != null || !Book.Loaded, "Skill", sk != null ? $"Rank {sk.Rank}" : "");
            }
            case "item":
            {
                var item = Book.ResolveItem(s);
                if (s.Item != 0) Book.EquipableSlots(s.Item);
                if (s.Paired)
                {
                    var mainDescription = s.Item != 0 ? Book.DescribeItem(s.Item) : null;
                    var secondary = Book.Item(s.SecondaryItem);
                    var description = secondary == null && s.SecondaryItem != 0 ? Book.DescribeItem(s.SecondaryItem) : null;
                    bool equipped = Book.IsEquipped(s.Item, 4) && Book.IsEquipped(s.SecondaryItem, 5);
                    bool available = s.Item != 0 && s.SecondaryItem != 0;
                    return new SlotLook((item?.Name ?? mainDescription?.Name ?? s.Name) + " / " + (secondary?.Name ?? description?.Name ?? "Item"),
                        item?.Icon ?? mainDescription?.Icon ?? s.ItemIcons, "bag", -1, equipped, available, "Paired equipment",
                        !available ? "An item in the pair is missing" : equipped ? "Equipped · click to take off both" : "Click to equip both")
                        { SecondaryIcons = secondary?.Icon ?? description?.Icon ?? [] };
                }
                if (item == null && (s.ItemHasPower || s.Prop >= 0) && Book.Loaded)
                    return new SlotLook(s.Name.Length > 0 ? s.Name : "Item", s.ItemIcons, "potion", 0, false, false, "Item", "None left");
                if (item == null)
                {
                    var d = Book.DescribeItem(s.Item);
                    return new SlotLook(d?.Name ?? (s.Name.Length > 0 ? s.Name : "Item"), d?.Icon ?? [], "bag", -1, false, d != null, "Item", "Click to equip");
                }
                var power = Book.ResolvePower(s, item);
                if (power == null && s.Prop >= 0)
                    return new SlotLook(item.Name, item.Icon, "potion", 0, false, false, "Item", "Power no longer available");
                if (power == null)
                    return new SlotLook(item.Name, item.Icon, "bag", item.Stack > 1 ? item.Stack : -1, item.Equipped, true, "Item", item.Equipped ? "Equipped" : "Click to equip");
                int count = power.Uses;
                bool ready = item.Usable && power.Ready;
                string uses = count < 0 ? "Unlimited uses" : $"{count} use{(count == 1 ? "" : "s")} left";
                return new SlotLook(item.Name, item.Icon, "potion", count, false, ready, power.Name, item.Usable ? uses : "You cannot use this item");
            }
            case "mode":
            {
                var m = Book.Mode(s.Id);
                return new SlotLook(m?.Name ?? "Mode", m?.Icon is { Length: > 0 } mi ? [mi] : [], BarCatalog.ModeGlyph(s.Id), -1, m?.On == true,
                    m != null || !Book.Loaded, "Mode", m?.On == true ? "On · click to turn off" : "Off · click to turn on");
            }
            case "emote":
            {
                // The original's own label and icon (its emote icons are stars, quickbargaps).
                var emote = OpenWinter.Content.Emotes.Find(s.Arg);
                string name = emote == null ? s.Arg : Session.Content?.Resources.TlkString(emote.StrRef) is { Length: > 0 } t ? t : emote.Name;
                return new SlotLook(name, emote != null ? [emote.Icon] : [], "emote", -1, false, emote != null, "Emote", "");
            }
            case "macro":
                // The label shows on the slot, the line in its tooltip ("F10 : Hail: Hail and well met").
                return new SlotLook(s.Name, [], "talk", -1, false, true, "Text macro", s.Text);
            case "command":
            {
                var c = BarCatalog.Command(s.Arg);
                return new SlotLook(c.Name ?? s.Arg, [], c.Glyph ?? "cog", -1, false, true, "Action", c.Hint ?? "");
            }
        }
        return new SlotLook("", [], "cog", -1, false, false, "", "");
    }
}

/// <summary>How a slot looks: game icon layers (empty → <see cref="Glyph"/>), uses badge (-1 none), toggled, usable.</summary>
public sealed record SlotLook(string Name, string[] Icons, string Glyph, int Uses, bool Toggled, bool Usable, string Kind, string Detail)
{
    public string[] SecondaryIcons { get; init; } = [];
}

/// <summary>One shared <see cref="AbilityBook"/> and <see cref="AbilityUser"/> per session.</summary>
public static class AbilityServices
{
    static readonly Dictionary<ClientSession, (AbilityBook Book, AbilityUser User)> All = new();

    public static (AbilityBook Book, AbilityUser User) For(ClientSession session, SpellCasting casting)
    {
        if (All.TryGetValue(session, out var s)) return s;
        var book = new AbilityBook(session);
        var user = new AbilityUser { Session = session, Casting = casting, Book = book };
        session.ControlledChanged += user.ResetControl;
        All[session] = (book, user);
        session.TreeExiting += () => All.Remove(session);
        return (book, user);
    }
}

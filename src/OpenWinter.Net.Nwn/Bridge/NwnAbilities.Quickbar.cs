using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Content;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GuiQuickbarMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The quickbar the server keeps with the character: its 36 buttons become the client's "quickbar" notify, and the
/// client's edits ("quickbar-set") go back one button at a time.
/// </summary>
public sealed partial class NwnAbilities
{
    const int QuickbarSlots = 36;

    /// <summary>The quickbar's property field for an item's first usable property, as the server sends it.</summary>
    const int FirstProperty = 0xFF0000;

    public void Quickbar(GuiQuickbarSetAllButtons all)
    {
        _quickbar = new NwnTagged?[QuickbarSlots];
        for (int i = 0; i < Math.Min(QuickbarSlots, all.Slots.Count); i++) Button(i, all.Slots[i]);
        if (_quickbarWanted) SendQuickbar();
        SendAbilities();
    }

    public void QuickbarButton(GuiQuickbarSetButton one)
    {
        if (one.Button >= QuickbarSlots) return;
        if (_quickbar.Length == 0) _quickbar = new NwnTagged?[QuickbarSlots];
        var was = _quickbar[one.Button]?.Value as ButtonItem;
        Button(one.Button, one.Slot);
        // The server empties an item's buttons when the item is used up or leaves the player.
        if (was?.Primary is { } gone && one.Slot.Value is ButtonEmpty && !_equipped.ContainsKey(gone.ObjectId)
            && !_quickbar.Any(b => b?.Value is ButtonItem { Primary: { } p } && p.ObjectId == gone.ObjectId))
        {
            _items.Remove(gone.ObjectId);
            SendAbilities();
        }
        if (_quickbarWanted) SendQuickbar();
    }

    void Button(int index, NwnTagged button)
    {
        _quickbar[index] = button;
        // Items on the bar carry their whole description: they count as the player's for using them.
        if (button.Value is ButtonItem item)
        {
            if (item.Primary is { } p) _items[p.ObjectId] = p.Item;
            if (item.Secondary is { } s) _items[s.ObjectId] = s.Item;
        }
    }

    void SendQuickbar()
    {
        if (_player == 0) return;
        var slots = new JsonArray();
        foreach (var b in _quickbar) slots.Add(b == null || Slot(b) is not { } slot ? null : JsonSerializer.SerializeToNode(slot));
        // The server's bar is the character's: it replaces any copy the client keeps.
        Notify("quickbar", new JsonObject { ["present"] = true, ["slots"] = slots, ["authoritative"] = true, ["localCache"] = false });
    }

    /// <summary>A quickbar button as an OpenWinter quickbar slot; null for empty and unsupported buttons.</summary>
    QuickbarSlot? Slot(NwnTagged button) => button.Value switch
    {
        ButtonItem { Primary: { } p, Secondary: { } s } => new QuickbarSlot { Kind = "item", Paired = true, Item = p.ObjectId, SecondaryItem = s.ObjectId, Name = ItemName(p.Item) },
        ButtonItem { Primary: { } p } => new QuickbarSlot { Kind = "item", Item = p.ObjectId, Prop = ItemProperty(p), Name = ItemName(p.Item) },
        ButtonSpell s => new QuickbarSlot { Kind = "spell", Id = (int)s.SpellId, Class = s.MultiClass < _classes.Count ? _classes[s.MultiClass].Class : -1, Meta = s.MetaType },
        ButtonSpellLikeAbility s => new QuickbarSlot { Kind = "spell", Id = (int)s.SpellId, Class = 255, Meta = 0 },
        ButtonSkill s when s.SkillId is SkillHide or SkillMoveSilently => new QuickbarSlot { Kind = "mode", Id = ModeStealth },
        ButtonSkill s when s.SkillId is SkillSearch or SkillSpot or SkillListen => new QuickbarSlot { Kind = "mode", Id = ModeDetect },
        ButtonSkill s when s.SkillId == SkillParry => new QuickbarSlot { Kind = "mode", Id = ModeParry },
        ButtonSkill s => new QuickbarSlot { Kind = "skill", Id = s.SkillId },
        ButtonFeat f => new QuickbarSlot { Kind = "feat", Id = f.FeatId },
        ButtonModeToggle m => new QuickbarSlot { Kind = "mode", Id = m.Mode },
        ButtonDialog => new QuickbarSlot { Kind = "command", Arg = "talk" },
        ButtonAttack => new QuickbarSlot { Kind = "command", Arg = "attack" },
        ButtonExamine => new QuickbarSlot { Kind = "command", Arg = "examine" },
        ButtonBarter => new QuickbarSlot { Kind = "command", Arg = "barter" },
        ButtonEmote e when Emotes.FromId(e.EmoteId) is { } emote => new QuickbarSlot { Kind = "emote", Arg = emote.Key, Name = emote.Name },
        ButtonCommandLine c => new QuickbarSlot { Kind = "macro", Name = c.Label, Text = c.Cmd },
        _ => null,
    };

    /// <summary>An item button's property: the index of the item's first usable property (or -1 to equip it).</summary>
    static int ItemProperty(ButtonItem.PrimaryItem p)
    {
        int active = p.PropertyId & 0xFF;
        int seen = 0;
        for (int i = 0; i < p.Item.Properties.Count; i++)
            if (Activatable(p.Item.Properties[i]) && seen++ == active) return i;
        return -1;
    }

    /// <summary>quickbar-set {creature, index, slot}: the slot as the button the original would set.</summary>
    void QuickbarSet(JsonObject d)
    {
        int index = Int(d, "index", -1);
        if (index is < 0 or >= QuickbarSlots || (d["creature"] != null && Id(d, "creature") != _player)) return;
        QuickbarSlot? slot;
        try { slot = d["slot"] is JsonObject o ? o.Deserialize<QuickbarSlot>() : null; }
        catch (JsonException) { return; }
        if (Button(slot) is not { } button) return;
        if (_quickbar.Length == 0) _quickbar = new NwnTagged?[QuickbarSlots];
        _quickbar[index] = button;
        // An item is named by its id and property field only; the server fills in the rest.
        send(button.Value is ButtonItem { Primary: { } item }
            ? new GuiQuickbarSetItemButton { Button = (byte)index, Item = item.ObjectId, PropertyId = item.PropertyId }
            : new GuiQuickbarSetButton { Button = (byte)index, Slot = button });
    }

    /// <summary>A quickbar slot as an NWN button; null when it can't be one (an item the server hasn't described).</summary>
    NwnTagged? Button(QuickbarSlot? s)
    {
        NwnTagged Tag(ButtonObjectType type, INwnStruct value) => new((long)type, value);
        if (s == null) return Tag(ButtonObjectType.Empty, new ButtonEmpty());
        switch (s.Kind)
        {
            case "item" when _items.TryGetValue(s.Item, out var item):
            {
                int active = s.Prop < 0 ? 0 : item.Properties.Take(s.Prop).Count(Activatable);
                var button = new ButtonItem { Primary = new ButtonItem.PrimaryItem { ObjectId = s.Item, PropertyId = FirstProperty | active, Item = item } };
                if (s.Paired && _items.TryGetValue(s.SecondaryItem, out var second))
                    button.Secondary = new ButtonItem.SecondaryItem { ObjectId = s.SecondaryItem, Item = second };
                return Tag(ButtonObjectType.Item, button);
            }
            case "spell" when s.Class == 255:
                return Tag(ButtonObjectType.Spelllikeability, new ButtonSpellLikeAbility { SpellId = (uint)s.Id, CasterLevel = (byte)(_spellAbilities.FirstOrDefault(a => a.Spell == s.Id).CasterLevel) });
            case "spell":
            {
                int ci = s.Class < 0 ? CasterClasses().Select(c => c.Index).DefaultIfEmpty(0).First() : _classes.FindIndex(c => c.Class == s.Class);
                return ci < 0 ? null : Tag(ButtonObjectType.Spell, new ButtonSpell { MultiClass = (byte)ci, SpellId = (uint)s.Id, MetaType = (byte)(s.Meta == 255 ? 0 : s.Meta) });
            }
            case "skill":
                return Tag(ButtonObjectType.Skill, new ButtonSkill { SkillId = s.Id });
            case "feat":
                return Tag(ButtonObjectType.Feat, new ButtonFeat { FeatId = s.Id });
            case "mode":
                return Tag(ButtonObjectType.ModeToggle, new ButtonModeToggle { Mode = s.Id });
            case "command" when s.Arg == "talk":
                return Tag(ButtonObjectType.Dialog, new ButtonDialog());
            case "command" when s.Arg == "attack":
                return Tag(ButtonObjectType.Attack, new ButtonAttack());
            case "command" when s.Arg == "examine":
                return Tag(ButtonObjectType.Examine, new ButtonExamine());
            case "command" when s.Arg == "barter":
                return Tag(ButtonObjectType.Barter, new ButtonBarter());
            case "emote" when Emotes.Find(s.Arg) is { Id: >= 0 } emote:
                return Tag(ButtonObjectType.Emote, new ButtonEmote { EmoteId = emote.Id });
            case "macro":
                return Tag(ButtonObjectType.CommandLine, new ButtonCommandLine { Label = s.Name, Cmd = s.Text });
        }
        return null;
    }
}

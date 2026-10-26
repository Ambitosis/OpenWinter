using OpenWinter.Aurora.Formats;
using OpenWinter.Content;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Skills;
using System.Text.Json;

namespace OpenWinter.Game.Requests;

/// <summary>The character's quickbar and validated client edits, persisted with the character.</summary>
public static class QuickbarRequests
{
    /// <summary>The most a text macro's label and line hold (the original's boxes; docs/QUICKBAR_RADIAL.md).</summary>
    public const int MacroLabelLimit = 512, MacroTextLimit = 1024;

    /// <summary>Native paired shortcuts are cleared when either bound item leaves possession.</summary>
    public static void LostPairItem(World world, Creature character, Item item)
    {
        var original = BicQuickbar.Read(character.Template);
        bool changed = false;
        foreach (int index in character.QuickbarItems.Where(p => p.Value == item).Select(p => p.Key)
            .Concat(character.QuickbarSecondaryItems.Where(p => p.Value == item).Select(p => p.Key)).Distinct().ToArray())
        {
            bool paired = character.QuickbarEdits.TryGetValue(index, out var edited) ? edited?.Paired == true
                : original != null && index < original.Length && (original[index].Paired || original[index].SavedAction?.Paired == true);
            if (!paired) continue;
            character.QuickbarEdits[index] = null;
            character.QuickbarItems.Remove(index);
            character.QuickbarSecondaryItems.Remove(index);
            changed = true;
        }
        if (changed) world.Output?.Notify("quickbar-pair-lost", character, new { item = item.Id }, character);
    }

    [ClientRequest("equip-pair")]
    static void EquipPair(Request r)
    {
        if (r.Obj<Item>("item") is not { } main || r.Obj<Item>("secondaryItem") is not { } secondary
            || main == secondary || !r.Owns(main) || !r.Owns(secondary) || !ActionQueues.Accepts(r.Player)) return;
        ActionQueues.EnqueuePlayerTask(r.World, r.Player, new Inventory.EquipPairAction { MainItem = main.Id, SecondaryItem = secondary.Id });
    }

    [ClientRequest("quickbar")]
    static void Quickbar(Request r)
    {
        var slots = Read(r.World, r.Player, message =>
        {
            if (r.World.Scripts is { } scripts) scripts.Log(message);
            else Console.WriteLine(message);
        });
        bool saved = BicQuickbar.HasSavedEdits(r.Player.Template) || r.Player.QuickbarEdits.Count > 0;
        // Saved character state owns item references and deliberate clears in every vault mode.
        // Only an unedited native character still permits migration of an older client-only layout.
        bool localCache = r.Player.Player?.FamiliarMaster == null;
        // The native possessed familiar starts with an empty bar, not generated player defaults.
        // Its edits belong to that creature and must never migrate a named player's disk cache.
        if (!localCache) slots ??= new QuickbarSlot?[BicQuickbar.Slots];
        bool authoritative = saved || !localCache;
        r.World.Output?.Notify("quickbar", r.Player, new { present = slots != null, slots, authoritative, localCache }, r.Player);
    }

    [ClientRequest("quickbar-set")]
    static void Set(Request r)
    {
        // Reject an edit queued before control moved to another creature. Legacy clients omit this field.
        if (r.Element("creature") != null && r.Id("creature") != r.Player.Id) return;
        int index = r.Int("index", -1);
        if (index is < 0 or >= BicQuickbar.Slots || r.Element("slot") is not { } data) return;
        QuickbarSlot? slot;
        try { slot = data.ValueKind == JsonValueKind.Null ? null : data.Deserialize<QuickbarSlot>(); }
        catch (JsonException) { return; }
        if (slot != null && !Valid(slot)) return;
        // NWNX_ON_QUICKBAR_SET_BUTTON (TYPE as NWNX_PLAYER_QBS_TYPE_*), before anything is remembered; a skip keeps the
        // button as it was server side.
        var ev = Nwnx.NwnxEvent.Begin(r.World, "NWNX_ON_QUICKBAR_SET_BUTTON", r.Player)?.Add("BUTTON", index).Add("TYPE", NwnxButtonType(slot));
        if (ev?.Before() == true) { ev.After(); return; }
        using var after = new Nwnx.NwnxEvent.AfterScope(ev);
        Item? item = null;
        Item? secondary = null;
        if (slot?.Kind == "item")
        {
            if (slot.Paired)
            {
                secondary = r.World.Get<Item>(slot.SecondaryItem);
                if (secondary == null || secondary.IsDestroyed || !r.Owns(secondary) || slot.Item == slot.SecondaryItem || slot.Item == 0) return;
            }
            item = r.World.Get<Item>(slot.Item);
            if (item == null && slot.Item == 0 && slot.ItemHasPower && slot.ItemKey.Length == 64)
            {
                // A retained empty shortcut is presentation only. Any future use
                // still requires an owned real item and server-validated property.
            }
            else
            {
                if (item == null || item.IsDestroyed || !r.Owns(item) || slot.Prop >= item.Properties.Count) return;
                QuickbarItemState.Remember(r.World, r.Player, item, slot);
            }
        }
        r.Player.QuickbarEdits[index] = slot;
        r.Player.QuickbarItems.Remove(index);
        r.Player.QuickbarSecondaryItems.Remove(index);
        if (item != null) r.Player.QuickbarItems[index] = item;
        if (secondary != null) r.Player.QuickbarSecondaryItems[index] = secondary;
    }

    /// <summary>NWNX_PLAYER_QBS_TYPE_* of a button.</summary>
    static int NwnxButtonType(QuickbarSlot? s) => s?.Kind switch
    {
        null => 0,
        "item" => s.Prop >= 0 ? 9 : 1,
        "spell" => 2,
        "skill" => 3,
        "feat" => 4,
        "mode" => 10,
        "emote" => 8,
        "macro" => 18,
        _ => 0,
    };

    static bool Valid(QuickbarSlot s) => s.Name is { Length: <= 512 } && s.Arg is { Length: <= 64 } && s.Text is { Length: <= MacroTextLimit }
        && (s.Kind == "macro" || s.Text.Length == 0)
        && (s.Paired ? s.Kind == "item" && s.Prop == -1 : s.SecondaryItem == 0)
        && s.ItemPowerKey is { Length: <= 64 } && s.ItemKey is { Length: <= 64 } && s.ItemIcons is { Length: <= 16 } && s.ItemIcons.All(i => i is { Length: <= 64 })
        && s.Id is >= 0 and <= ushort.MaxValue && s.Class is >= -1 and <= 255 && s.Meta is >= 0 and <= 255
        && s.Sub is >= 0 and <= ushort.MaxValue && s.Prop is >= -1 and < 255 && s.Kind switch
        {
            "spell" or "item" or "feat" or "skill" => true,
            // A text macro: its label (Name) and the line it sends (Text), as the original's QBObjectType 18.
            "macro" => s.Name.Length <= MacroLabelLimit,
            "mode" => s.Id <= AbilityRequests.ModeDirtyFighting,
            "command" => s.Arg is "attack" or "talk" or "examine" or "rest" or "stance"
                // The quickbar slot radial's other actions, crafting and associate orders (docs/QUICKBAR_RADIAL.md).
                or "barter" or "craft" or "attacknearest" or "follow" or "standground" or "guardme" or "healme",
            // The original's emotes (Emotes.All), also under OpenWinter's earlier names.
            "emote" => Emotes.Find(s.Arg) != null,
            _ => false,
        };

    public static QuickbarSlot?[]? Read(World world, Creature character, Action<string> log)
    {
        var entries = BicQuickbar.Read(character.Template);
        if (entries == null && character.QuickbarEdits.Count == 0) return null;
        entries ??= [];
        var slots = new QuickbarSlot?[BicQuickbar.Slots];
        var items = world.Inventory.AllItems(character).Concat(Rules.EffectSystem.OwnEquipment(character).Select(kv => kv.Value))
            .Where(item => !item.IsDestroyed).ToHashSet();
        for (int i = 0; i < slots.Length; i++)
        {
            if (character.QuickbarEdits.TryGetValue(i, out var edited))
            {
                slots[i] = ResolveItem(edited?.Copy(), i);
                continue;
            }
            if (i >= entries.Length) continue;
            var e = entries[i];
            if (e.SavedAction is { } saved)
            {
                if (Valid(saved)) slots[i] = ResolveItem(saved, i);
                else log($"[quickbar] Skipping QBList slot {i + 1}: invalid OpenWinter action.");
                continue;
            }
            if (e.Type == 0) continue;
            var slot = e.Type switch
            {
                1 when !e.UnsupportedItemOptions && e.Paired
                    => new QuickbarSlot { Kind = "item", Paired = true, Name = "Paired equipment" },
                1 when !e.UnsupportedItemOptions && character.QuickbarItems.GetValueOrDefault(i) is { } item
                    => new QuickbarSlot { Kind = "item", Item = item.Id, Paired = e.Paired, Prop = e.Property, Name = item.Name.Resolve(world.Resources.Talk) },
                2 when e.Parameter >= 0 => new QuickbarSlot { Kind = "spell", Id = e.Parameter, Class = e.Class, Meta = e.Meta },
                3 => Skill(e.Parameter),
                // Packed feat/sub-radial parameters have not been measured yet.
                4 when e.Parameter is >= 0 and <= ushort.MaxValue => new QuickbarSlot { Kind = "feat", Id = e.Parameter },
                6 => new QuickbarSlot { Kind = "command", Arg = "talk" },
                7 => new QuickbarSlot { Kind = "command", Arg = "attack" },
                10 when e.Parameter is 0 or 1 => new QuickbarSlot { Kind = "mode", Id = e.Parameter },
                40 => new QuickbarSlot { Kind = "command", Arg = "examine" },
                BicQuickbar.TextMacro => new QuickbarSlot { Kind = "macro", Name = e.Label, Text = e.Command },
                Emotes.QuickbarType when Emotes.FromId(e.Parameter) is { } emote => new QuickbarSlot { Kind = "emote", Arg = emote.Key, Name = emote.Name },
                _ => null,
            };
            slots[i] = ResolveItem(slot, i);
            if (slots[i] == null)
                log($"[quickbar] Skipping QBList slot {i + 1}: type {e.Type}, parameter {e.Parameter} (unsupported action/options or missing item).");
        }
        return slots;

        QuickbarSlot? ResolveItem(QuickbarSlot? slot, int index)
        {
            if (slot?.Kind != "item") return slot;
            character.QuickbarItems.TryGetValue(index, out var item);
            if (slot.Paired)
            {
                var secondary = character.QuickbarSecondaryItems.GetValueOrDefault(index);
                if (item != null && items.Contains(item)) QuickbarItemState.Remember(world, character, item, slot);
                slot.SecondaryItem = secondary != null && items.Contains(secondary) ? secondary.Id : 0;
                // Keep the compound identity intact; unavailable members never become a single-item action.
                slot.Item = item != null && items.Contains(item) ? item.Id : 0;
                return slot;
            }
            if (item != null) QuickbarItemState.Remember(world, character, item, slot);
            if (item == null || !items.Contains(item))
            {
                if (slot.ItemHasPower && slot.ItemKey.Length > 0) { slot.Item = 0; return slot; }
                else { log($"[quickbar] Skipping QBList slot {index + 1}: missing item."); return null; }
            }
            QuickbarItemState.Remember(world, character, item, slot);
            slot.Item = item.Id;
            return slot;
        }
    }

    static QuickbarSlot? Skill(int skill) => skill switch
    {
        SkillIds.Parry => new() { Kind = "mode", Id = AbilityRequests.ModeParry },
        SkillIds.Hide or SkillIds.MoveSilently => new() { Kind = "mode", Id = AbilityRequests.ModeStealth },
        SkillIds.Search or SkillIds.Spot or SkillIds.Listen => new() { Kind = "mode", Id = AbilityRequests.ModeDetect },
        SkillIds.AnimalEmpathy or SkillIds.DisableTrap or SkillIds.Heal or SkillIds.OpenLock or SkillIds.PickPocket
            or SkillIds.SetTrap or SkillIds.Taunt or SkillIds.Lore => new() { Kind = "skill", Id = skill },
        _ => null,
    };
}

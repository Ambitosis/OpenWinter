using System.Numerics;
using System.Text.Json;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Player: per-player client tweaks. Visual-only ones are forwarded to
/// that player's client as "nwnx-player" notifies ({op, ...}).
/// </summary>
static class PlayerPlugin
{
    const string P = "Player";
    public const int QuickbarSlots = 36;

    static Creature? Pc(NwnxCall n) => n.PopObject<Creature>() is { IsPC: true } pc ? pc : null;

    static void Tell(NwnxCall n, Creature? pc, object data)
    {
        if (pc != null) n.World.Output?.Notify("nwnx-player", pc, data, pc);
    }

    // ---- Quickbar: 36 slots of 13 values each, in the wrapper's push order -------------
    // oItem, oSecondaryItem, nObjectType, nMultiClass, sResRef, sCommandLabel, sCommandLine,
    // sToolTip, nINTParam1, nMetaType, nDomainLevel, nAssociateType, oAssociate.

    static readonly NwnxType[] SlotLayout =
    {
        NwnxType.Object, NwnxType.Object, NwnxType.Int, NwnxType.Int, NwnxType.String, NwnxType.String, NwnxType.String,
        NwnxType.String, NwnxType.Int, NwnxType.Int, NwnxType.Int, NwnxType.Int, NwnxType.Object,
    };

    static NwnxValue Empty(NwnxType t) => t switch
    {
        NwnxType.Object => NwnxValue.Object(GameObject.InvalidId),
        NwnxType.String => NwnxValue.String(""),
        _ => NwnxValue.Int(0),
    };

    public static NwnxValue[] Slot(NwnxCall n, Creature c, int slot) =>
        n.State(c).TryGetValue("qb:" + slot, out var v) && v.Value is NwnxValue[] values ? values : SlotLayout.Select(Empty).ToArray();

    [NwnxFunc(P, "SetQuickBarSlot")] static void SetQuickBarSlot(NwnxCall n)
    {
        var pc = n.PopObject<Creature>();
        int slot = n.PopInt();
        var values = new NwnxValue[SlotLayout.Length];
        for (int i = values.Length - 1; i >= 0; i--) values[i] = n.PopValue() ?? Empty(SlotLayout[i]);
        if (pc == null || slot is < 0 or >= QuickbarSlots) return;
        n.State(pc)["qb:" + slot] = new NwnxValue(NwnxType.Object, values);
        if (pc.IsPC) Tell(n, pc, new { op = "quickbar", slot, type = values[2].Value, resref = values[4].Value, label = values[5].Value });
    }

    [NwnxFunc(P, "GetQuickBarSlot")] static void GetQuickBarSlot(NwnxCall n)
    {
        var pc = n.PopObject<Creature>();
        int slot = n.PopInt();
        var values = pc != null && slot is >= 0 and < QuickbarSlots ? Slot(n, pc, slot) : SlotLayout.Select(Empty).ToArray();
        foreach (var v in values) n.Push(v);
    }

    public static string SerializeQuickbar(NwnxCall n, Creature c)
    {
        var slots = new List<object?[]>();
        for (int s = 0; s < QuickbarSlots; s++)
            slots.Add(Slot(n, c, s).Select(v => v.Value).ToArray());
        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(slots));
    }

    public static bool DeserializeQuickbar(NwnxCall n, Creature c, string data)
    {
        try
        {
            var slots = JsonSerializer.Deserialize<JsonElement[][]>(Convert.FromBase64String(data));
            if (slots == null) return false;
            for (int s = 0; s < Math.Min(slots.Length, QuickbarSlots); s++)
            {
                var values = new NwnxValue[SlotLayout.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    var e = i < slots[s].Length ? slots[s][i] : default;
                    values[i] = SlotLayout[i] switch
                    {
                        NwnxType.Object => NwnxValue.Object(e.ValueKind == JsonValueKind.Number ? e.GetUInt32() : GameObject.InvalidId),
                        NwnxType.String => NwnxValue.String(e.ValueKind == JsonValueKind.String ? e.GetString()! : ""),
                        _ => NwnxValue.Int(e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 0),
                    };
                }
                n.State(c)["qb:" + s] = new NwnxValue(NwnxType.Object, values);
            }
            return true;
        }
        catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    // ---- Client-side effects -----------------------------------------------------

    [NwnxFunc(P, "FloatingTextStringOnCreature")] static void FloatingText(NwnxCall n)
    {
        var pc = Pc(n); var creature = n.PopObjectId(); var text = n.PopString();
        Tell(n, pc, new { op = "floating-text", creature, text });
    }
    [NwnxFunc(P, "SetPlaceableUsable")] static void SetPlaceableUsable(NwnxCall n)
    {
        var pc = Pc(n); var placeable = n.PopObjectId(); bool usable = n.PopInt() != 0;
        Tell(n, pc, new { op = "placeable-usable", placeable, usable });
    }
    [NwnxFunc(P, "SetCreatureNameOverride")] static void SetCreatureNameOverride(NwnxCall n)
    {
        var pc = Pc(n); var creature = n.PopObjectId(); var name = n.PopString();
        Tell(n, pc, new { op = "name-override", target = creature, name });
    }
    [NwnxFunc(P, "SetPlaceableNameOverride")] static void SetPlaceableNameOverride(NwnxCall n)
    {
        var pc = Pc(n); var placeable = n.PopObjectId(); var name = n.PopString();
        Tell(n, pc, new { op = "name-override", target = placeable, name });
    }
    [NwnxFunc(P, "SetObjectHiliteColorOverride")] static void SetHilite(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObjectId(); int color = n.PopInt();
        Tell(n, pc, new { op = "hilite-color", target, color });
    }
    [NwnxFunc(P, "SetObjectMouseCursorOverride")] static void SetCursor(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObjectId(); int cursor = n.PopInt();
        Tell(n, pc, new { op = "mouse-cursor", target, cursor });
    }
    [NwnxFunc(P, "SetObjectVisualTransformOverride")] static void SetTransform(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObjectId(); int transform = n.PopInt(); float value = n.PopFloat();
        Tell(n, pc, new { op = "visual-transform", target, transform, value });
    }
    [NwnxFunc(P, "PlaySound")] static void PlaySound(NwnxCall n)
    {
        var pc = Pc(n); var sound = n.PopString(); var target = n.PopObjectId();
        Tell(n, pc, new { op = "sound", sound, target });
    }
    [NwnxFunc(P, "ShowVisualEffect")] static void ShowVfx(NwnxCall n)
    {
        var pc = Pc(n); int effect = n.PopInt();
        float z = n.PopFloat(), y = n.PopFloat(), x = n.PopFloat();
        Tell(n, pc, new { op = "vfx", effect, x, y, z });
    }
    [NwnxFunc(P, "ApplyInstantVisualEffectToObject")] static void InstantVfx(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObjectId(); int effect = n.PopInt();
        Tell(n, pc, new { op = "vfx", effect, target });
    }
    [NwnxFunc(P, "ApplyLoopingVisualEffectToObject")] static void LoopingVfx(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObjectId(); int effect = n.PopInt();
        Tell(n, pc, new { op = "vfx-loop", effect, target });
    }
    [NwnxFunc(P, "ChangeBackgroundMusic")] static void ChangeMusic(NwnxCall n)
    {
        var pc = Pc(n); bool day = n.PopInt() != 0; int track = n.PopInt();
        Tell(n, pc, new { op = "music", day, track });
    }
    [NwnxFunc(P, "PlayBackgroundMusic")] static void PlayMusic(NwnxCall n)
    {
        var pc = Pc(n); bool play = n.PopInt() != 0;
        Tell(n, pc, new { op = "music-play", play });
    }
    [NwnxFunc(P, "ChangeBattleMusic")] static void ChangeBattleMusic(NwnxCall n)
    {
        var pc = Pc(n); int track = n.PopInt();
        Tell(n, pc, new { op = "battle-music", track });
    }
    [NwnxFunc(P, "PlayBattleMusic")] static void PlayBattleMusic(NwnxCall n)
    {
        var pc = Pc(n); bool play = n.PopInt() != 0;
        Tell(n, pc, new { op = "battle-music-play", play });
    }
    [NwnxFunc(P, "StartGuiTimingBar")] static void StartTimingBar(NwnxCall n)
    {
        var pc = Pc(n); float seconds = n.PopFloat(); int type = n.PopInt();
        Tell(n, pc, new { op = "timing-bar", seconds, type });
    }
    [NwnxFunc(P, "StopGuiTimingBar")] static void StopTimingBar(NwnxCall n) => Tell(n, Pc(n), new { op = "timing-bar-stop" });
    [NwnxFunc(P, "SetAlwaysWalk")] static void AlwaysWalk(NwnxCall n)
    {
        var pc = Pc(n); bool walk = n.PopInt() != 0;
        if (pc != null) n.State(pc)["alwaysWalk"] = NwnxValue.Int(walk ? 1 : 0);
        Tell(n, pc, new { op = "always-walk", walk });
    }
    [NwnxFunc(P, "SetRestDuration")] static void RestDuration(NwnxCall n)
    {
        var pc = Pc(n); int duration = n.PopInt();
        if (pc != null) n.State(pc)["restDuration"] = NwnxValue.Int(duration);
    }
    [NwnxFunc(P, "SetRestAnimation")] static void RestAnimation(NwnxCall n)
    {
        var pc = Pc(n); int animation = n.PopInt();
        if (pc != null) n.State(pc)["restAnimation"] = NwnxValue.Int(animation);
    }
    [NwnxFunc(P, "UpdateCharacterSheet")] static void UpdateSheet(NwnxCall n) => Tell(n, Pc(n), new { op = "character-sheet" });
    [NwnxFunc(P, "OpenInventory")] static void OpenInventory(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObjectId(); bool open = n.PopInt() != 0;
        Tell(n, pc, new { op = "inventory", target, open });
    }
    [NwnxFunc(P, "ForcePlaceableExamineWindow")] static void ExamineWindow(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObjectId();
        Tell(n, pc, new { op = "examine", target });
    }
    [NwnxFunc(P, "ForcePlaceableInventoryWindow")] static void InventoryWindow(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObjectId();
        Tell(n, pc, new { op = "inventory", target, open = true });
    }
    [NwnxFunc(P, "SetTlkOverride")] static void TlkOverride(NwnxCall n)
    {
        var pc = Pc(n); int strref = n.PopInt(); var text = n.PopString(); n.PopInt();
        Tell(n, pc, new { op = "tlk-override", strref, text });
    }
    [NwnxFunc(P, "SetCustomToken", UnsupportedReason = "per-player token is stored but dialogue does not consume it")] static void CustomToken(NwnxCall n)
    {
        var pc = Pc(n); int token = n.PopInt(); var value = n.PopString();
        // TODO(conversation): per-player custom tokens should override SetCustomToken in dialogue text.
        if (pc != null) n.State(pc)["token:" + token] = NwnxValue.String(value);
    }

    // ---- Player identity -----------------------------------------------------------

    [NwnxFunc(P, "ToggleDM")] static void ToggleDM(NwnxCall n)
    {
        var pc = Pc(n); bool dm = n.PopInt() != 0;
        if (pc?.Player == null) return;
        pc.Player.IsDM = dm;
        pc.IsPlayerDM = dm;
        Tell(n, pc, new { op = "dm", enabled = dm });
    }
    [NwnxFunc(P, "GetPlatformId")] static void PlatformId(NwnxCall n) => n.Push(Pc(n)?.Client.Platform ?? 0);
    [NwnxFunc(P, "GetLanguage")] static void Language(NwnxCall n) => n.Push(Pc(n)?.Client.Language ?? -1);
    [NwnxFunc(P, "GetBicFileName")] static void BicFileName(NwnxCall n)
    {
        var pc = Pc(n);
        n.Push(pc != null && n.State(pc).TryGetValue("bic", out var v) && v.Value is string s ? s : "");
    }

    [NwnxFunc(P, "GetJournalEntry")] static void JournalEntry(NwnxCall n)
    {
        var pc = Pc(n); var tag = n.PopString();
        var journal = n.World.System<JournalService>();
        if (pc == null || !pc.Journal.TryGetValue(tag, out var state) || !journal.Categories.TryGetValue(tag, out var cat))
        {
            n.Push(-1);
            return;
        }
        var tlk = n.World.Resources.Talk;
        cat.Entries.TryGetValue(state.State, out var entry);
        n.Push(entry?.Text.Resolve(tlk) ?? "");
        n.Push(cat.Name.Resolve(tlk));
        n.Push(state.Day);
        n.Push(state.Time);
        n.Push(state.State);
        n.Push(cat.Priority);
        n.Push(entry?.End == true);
        n.Push(1);
        n.Push(1);
    }

    [NwnxFunc(P, "GetAreaExplorationState")] static void GetExploration(NwnxCall n)
    {
        var pc = Pc(n); var area = n.PopObjectId();
        n.Push(pc != null && n.State(pc).TryGetValue("explore:" + area, out var v) && v.Value is string s ? s : "");
    }
    [NwnxFunc(P, "SetAreaExplorationState")] static void SetExploration(NwnxCall n)
    {
        var pc = Pc(n); var area = n.PopObjectId(); var data = n.PopString();
        if (pc == null) return;
        n.State(pc)["explore:" + area] = NwnxValue.String(data);
        Tell(n, pc, new { op = "exploration", area, data });
    }

    [NwnxFunc(P, "PossessCreature", UnsupportedReason = "switching the controlled creature is not implemented")] static void Possess(NwnxCall n)
    {
        // TODO(society): possession needs the server to switch the controlled object.
        Pc(n); n.PopObjectId(); n.PopInt(); n.PopInt();
        n.Push(0);
    }
}

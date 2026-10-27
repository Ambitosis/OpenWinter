using System.Numerics;
using System.Text.Json;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Player: per-player views and commands. What only that player's client shows (effects, sounds, music, fog,
/// sky, wind, highlights, cursors) goes to that client alone with the notify kinds the area-wide versions use; the
/// rest changes server state for that player. Calls given anything but a player do nothing and return the empty
/// result (parity suite nwnx_player).
/// </summary>
public static class PlayerPlugin
{
    const string P = "Player";
    public const int QuickbarSlots = 36;

    static Creature? Pc(NwnxCall n) => n.PopObject<Creature>() is { IsPC: true } pc ? pc : null;

    /// <summary>A view only this player gets that the client has no general notify for ("nwnx-player" {op, ...}).</summary>
    static void Tell(NwnxCall n, Creature? pc, object data)
    {
        if (pc != null) n.World.Output?.Notify("nwnx-player", pc, data, pc);
    }

    static float[] Vec(Vector3 v) => new[] { v.X, v.Y, v.Z };

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

    [NwnxFunc(P, "SetQuickBarSlot")]
    static void SetQuickBarSlot(NwnxCall n)
    {
        var pc = n.PopObject<Creature>();
        int slot = n.PopInt();
        var values = new NwnxValue[SlotLayout.Length];
        for (int i = values.Length - 1; i >= 0; i--) values[i] = n.PopValue() ?? Empty(SlotLayout[i]);
        if (pc == null || slot is < 0 or >= QuickbarSlots) return;
        n.State(pc)["qb:" + slot] = new NwnxValue(NwnxType.Object, values);
        if (pc.IsPC) Tell(n, pc, new { op = "quickbar", slot, type = values[2].Value, resref = values[4].Value, label = values[5].Value });
    }

    [NwnxFunc(P, "GetQuickBarSlot")]
    static void GetQuickBarSlot(NwnxCall n)
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

    // ---- What only this player sees ---------------------------------------------

    /// <summary>
    /// Floating text over a creature for one player, whatever faction it is in; with bChatWindow it also goes to her
    /// chat window, as FloatingTextStringOnCreature's lines do.
    /// </summary>
    [NwnxFunc(P, "FloatingTextStringOnCreature")]
    static void FloatingText(NwnxCall n)
    {
        var pc = Pc(n); var creature = n.PopObject(); var text = n.PopString(); bool chat = n.PopInt() != 0;
        if (pc == null || creature == null) return;
        if (chat && FamiliarPossession.SessionOf(pc) is { } session)
            session.Send(new Net.ChatMessage { Speaker = creature.Id, Channel = Net.ChatChannel.Server, SpeakerName = "", Text = text }.Write());
        else n.World.Output?.Notify("float-text", creature, new { text }, pc);
    }

    [NwnxFunc(P, "SetPlaceableUsable")]
    static void SetPlaceableUsable(NwnxCall n)
    {
        var pc = Pc(n); var placeable = n.PopObject<Placeable>(); bool usable = n.PopInt() != 0;
        if (pc == null || placeable == null) return;
        n.State(pc)["usable:" + placeable.Id] = NwnxValue.Int(usable ? 1 : 0);
        Tell(n, pc, new { op = "placeable-usable", placeable = placeable.Id, usable });
    }

    [NwnxFunc(P, "SetCreatureNameOverride")]
    static void SetCreatureNameOverride(NwnxCall n)
    {
        var pc = Pc(n); var creature = n.PopObject<Creature>(); var name = n.PopString();
        if (pc != null && creature != null) SetNameOverride(n, pc, creature, name);
    }

    [NwnxFunc(P, "SetPlaceableNameOverride")]
    static void SetPlaceableNameOverride(NwnxCall n)
    {
        var pc = Pc(n); var placeable = n.PopObject<Placeable>(); var name = n.PopString();
        if (pc != null && placeable != null) SetNameOverride(n, pc, placeable, name);
    }

    static void SetNameOverride(NwnxCall n, Creature pc, GameObject target, string name)
    {
        var key = "name:" + target.Id;
        if (name.Length == 0) n.State(pc).Remove(key);
        else n.State(pc)[key] = NwnxValue.String(name);
        Tell(n, pc, new { op = "name-override", target = target.Id, name });
    }

    /// <summary>The name <paramref name="observer"/> sees for <paramref name="target"/>: her name override, else <paramref name="name"/>.</summary>
    public static string ShownName(World world, GameObject target, Creature observer, string name) =>
        world.System<NwnxRegistry>().ObjectState(observer.Id).TryGetValue("name:" + target.Id, out var v) && v.Value is string s ? s : name;

    [NwnxFunc(P, "SetObjectHiliteColorOverride")]
    static void SetHilite(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject(); int color = n.PopInt();
        if (pc == null || target == null) return;
        n.World.Output?.Notify("hilite-color", target, new { color = color < 0 ? (target as Creature)?.HiliteColor ?? -1 : color }, pc);
    }

    [NwnxFunc(P, "SetObjectMouseCursorOverride")]
    static void SetCursor(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject(); int cursor = n.PopInt();
        if (pc == null || target == null) return;
        n.World.Output?.Notify("mouse-cursor", target, new { cursor = cursor < 0 ? target.MouseCursor : cursor }, pc);
    }

    [NwnxFunc(P, "SetObjectUiDiscoveryMaskOverride")]
    static void SetUiDiscovery(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject(); int mask = n.PopInt();
        if (pc == null || target is not (Creature or Door or Item or Placeable)) return;
        n.World.Output?.Notify("ui-discovery", target, new { mask = mask < 0 ? target.UiDiscoveryMask : mask }, pc);
    }

    /// <summary>A visual transform only this player sees; -1 shows the object's own transforms again.</summary>
    [NwnxFunc(P, "SetObjectVisualTransformOverride")]
    static void SetTransform(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject(); int transform = n.PopInt(); float value = n.PopFloat();
        if (pc == null || target is not (Creature or Door or Item or Placeable)) return;
        var key = "transform:" + target.Id;
        var overridden = n.State(pc).TryGetValue(key, out var v) && v.Value is HashSet<int> set ? set : new HashSet<int>();
        if (transform < 0)
        {
            foreach (int t in overridden)
            {
                float own = target.VisualTransforms?.TryGetValue((0, t), out var vt) == true ? vt.ValueAt(n.World.Time) : WorldObjects.VisualTransform.Default(t);
                SendTransform(n, pc, target, t, own);
            }
            n.State(pc).Remove(key);
            return;
        }
        overridden.Add(transform);
        n.State(pc)[key] = new NwnxValue(NwnxType.Object, overridden);
        SendTransform(n, pc, target, transform, value);
    }

    static void SendTransform(NwnxCall n, Creature pc, GameObject target, int transform, float value) =>
        n.World.Output?.Notify("visual-transform", target, new
        {
            transform, scope = 0, from = value, value, lerp = 0, duration = 0f, pauseWithGame = true, behavior = 0, repeats = 0,
        }, pc);

    [NwnxFunc(P, "PlaySound")]
    static void PlaySound(NwnxCall n)
    {
        var pc = Pc(n); var sound = n.PopString(); var target = n.PopObject();
        if (pc == null) return;
        var source = target is { Area: not null } ? target : pc;
        n.World.Output?.Notify("play-sound", source, new { resref = sound, position = Vec(source.Position), strref = -1 }, pc);
    }

    static object VfxData(int vfx, float scale, Vector3 translate, Vector3 rotate, float duration, long id) =>
        new { vfx, miss = false, scale, translate = Vec(translate), rotate = Vec(rotate), duration, id };

    [NwnxFunc(P, "ShowVisualEffect")]
    static void ShowVfx(NwnxCall n)
    {
        var pc = Pc(n); int effect = n.PopInt(); var position = n.PopVector(); float scale = n.PopFloat();
        var translate = n.PopVector(); var rotate = n.PopVector();
        if (pc?.Area is not { } area) return;
        n.World.Output?.Notify("vfx", area, new
        {
            vfx = effect, miss = false, scale, pos = Vec(position), facing = 0f, translate = Vec(translate), rotate = Vec(rotate),
            duration = 0f, id = 0L,
        }, pc);
    }

    [NwnxFunc(P, "ApplyInstantVisualEffectToObject")]
    static void InstantVfx(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject(); int effect = n.PopInt(); float scale = n.PopFloat();
        var translate = n.PopVector(); var rotate = n.PopVector();
        if (pc == null || target == null) return;
        n.World.Output?.Notify("vfx", target, VfxData(effect, scale, translate, rotate, 0f, 0L), pc);
    }

    static long _nextLoopingId = -1_000_000;

    /// <summary>A looping effect only this player sees; the same call again removes it, -1 removes all of them.</summary>
    [NwnxFunc(P, "ApplyLoopingVisualEffectToObject")]
    static void LoopingVfx(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject(); int effect = n.PopInt();
        if (pc == null || target == null) return;
        var key = "loop:" + target.Id;
        var loops = n.State(pc).TryGetValue(key, out var v) && v.Value is Dictionary<int, long> d ? d : new Dictionary<int, long>();
        void Remove(long id) => n.World.Output?.Notify("effect-removed", target, new { id, type = 0, linkId = "", icon = 0 }, pc);
        if (effect < 0)
        {
            foreach (var id in loops.Values) Remove(id);
            loops.Clear();
        }
        else if (loops.Remove(effect, out var existing)) Remove(existing);
        else
        {
            long id = Interlocked.Decrement(ref _nextLoopingId);
            loops[effect] = id;
            n.World.Output?.Notify("vfx", target, VfxData(effect, 1f, Vector3.Zero, Vector3.Zero, -1f, id), pc);
        }
        n.State(pc)[key] = new NwnxValue(NwnxType.Object, loops);
    }

    // Music for one player: the area's own settings with this player's changes on top.

    static Dictionary<string, object> MusicOverrides(NwnxCall n, Creature pc)
    {
        if (n.State(pc).TryGetValue("music", out var v) && v.Value is Dictionary<string, object> d) return d;
        d = new Dictionary<string, object>();
        n.State(pc)["music"] = new NwnxValue(NwnxType.Object, d);
        return d;
    }

    static void SendMusic(NwnxCall n, Creature pc, string field, object value)
    {
        if (pc.Area is not { } area) return;
        var overrides = MusicOverrides(n, pc);
        overrides[field] = value;
        var env = WorldObjects.AreaSettings.Of(n.World, area);
        T Get<T>(string name, T own) => overrides.TryGetValue(name, out var o) && o is T t ? t : own;
        n.World.Output?.Notify("area-audio", area, new
        {
            musicDay = Get("musicDay", env.MusicDay), musicNight = Get("musicNight", env.MusicNight), musicBattle = Get("musicBattle", env.MusicBattle),
            musicDelay = env.MusicDelay, musicPlaying = Get("musicPlaying", env.MusicPlaying), battlePlaying = Get("battlePlaying", env.BattlePlaying),
            ambientDay = env.AmbientDay, ambientNight = env.AmbientNight, ambientDayVolume = env.AmbientDayVolume,
            ambientNightVolume = env.AmbientNightVolume, ambientPlaying = env.AmbientPlaying,
        }, pc);
    }

    [NwnxFunc(P, "ChangeBackgroundMusic")]
    static void ChangeMusic(NwnxCall n)
    {
        var pc = Pc(n); bool day = n.PopInt() != 0; int track = n.PopInt();
        if (pc != null) SendMusic(n, pc, day ? "musicDay" : "musicNight", track);
    }
    [NwnxFunc(P, "PlayBackgroundMusic")]
    static void PlayMusic(NwnxCall n)
    {
        var pc = Pc(n); bool play = n.PopInt() != 0;
        if (pc != null) SendMusic(n, pc, "musicPlaying", play);
    }
    [NwnxFunc(P, "ChangeBattleMusic")]
    static void ChangeBattleMusic(NwnxCall n)
    {
        var pc = Pc(n); int track = n.PopInt();
        if (pc != null) SendMusic(n, pc, "musicBattle", track);
    }
    [NwnxFunc(P, "PlayBattleMusic")]
    static void PlayBattleMusic(NwnxCall n)
    {
        var pc = Pc(n); bool play = n.PopInt() != 0;
        if (pc != null) SendMusic(n, pc, "battlePlaying", play);
    }

    [NwnxFunc(P, "UpdateWind")]
    static void UpdateWind(NwnxCall n)
    {
        var pc = Pc(n); var direction = n.PopVector(); float magnitude = n.PopFloat(), yaw = n.PopFloat(), pitch = n.PopFloat();
        if (pc?.Area is { } area) n.World.Output?.Notify("area-wind", area, new { direction = Vec(direction), magnitude, yaw, pitch, scripted = true }, pc);
    }

    [NwnxFunc(P, "UpdateSkyBox")]
    static void UpdateSkyBox(NwnxCall n)
    {
        var pc = Pc(n); int skybox = n.PopInt();
        if (pc?.Area is { } area) n.World.Output?.Notify("area-skybox", area, new { skybox }, pc);
    }

    [NwnxFunc(P, "UpdateFogColor")]
    static void UpdateFogColor(NwnxCall n)
    {
        var pc = Pc(n); int sun = n.PopInt(), moon = n.PopInt();
        if (pc?.Area is not { } area) return;
        n.World.Output?.Notify("fog-color", area, new { fogType = 1, color = sun & 0xFFFFFF, fadeTime = 0f }, pc);
        n.World.Output?.Notify("fog-color", area, new { fogType = 2, color = moon & 0xFFFFFF, fadeTime = 0f }, pc);
    }

    [NwnxFunc(P, "UpdateFogAmount")]
    static void UpdateFogAmount(NwnxCall n)
    {
        var pc = Pc(n); int sun = n.PopInt(), moon = n.PopInt();
        if (pc?.Area is { } area) n.World.Output?.Notify("area-fog", area, new { sun = Math.Clamp(sun, 0, 255), moon = Math.Clamp(moon, 0, 255) }, pc);
    }

    [NwnxFunc(P, "StartGuiTimingBar")]
    static void StartTimingBar(NwnxCall n)
    {
        var pc = Pc(n); float seconds = n.PopFloat(); int type = n.PopInt();
        Tell(n, pc, new { op = "timing-bar", seconds, type });
    }
    [NwnxFunc(P, "StopGuiTimingBar")] static void StopTimingBar(NwnxCall n) => Tell(n, Pc(n), new { op = "timing-bar-stop" });

    [NwnxFunc(P, "SetAlwaysWalk")]
    static void AlwaysWalk(NwnxCall n)
    {
        var pc = Pc(n); bool walk = n.PopInt() != 0;
        if (pc == null) return;
        pc.AlwaysWalk = walk;
        n.World.MarkChanged(pc);
    }

    /// <summary>Rest length in milliseconds (at least 10); -1 clears it. RestAction reads it.</summary>
    [NwnxFunc(P, "SetRestDuration")]
    static void RestDuration(NwnxCall n)
    {
        var pc = Pc(n); int duration = n.PopInt();
        if (pc == null) return;
        if (duration < 0) n.State(pc).Remove("restDuration");
        else n.State(pc)["restDuration"] = NwnxValue.Int(Math.Max(10, duration));
    }

    [NwnxFunc(P, "SetRestAnimation")]
    static void RestAnimation(NwnxCall n)
    {
        var pc = Pc(n); int animation = n.PopInt();
        if (pc == null) return;
        if (animation < 0) n.State(pc).Remove("restAnimation");
        else n.State(pc)["restAnimation"] = NwnxValue.Int(animation);
    }

    [NwnxFunc(P, "UpdateCharacterSheet")]
    static void UpdateSheet(NwnxCall n)
    {
        if (Pc(n) is { } pc) n.World.Output?.Notify("character-sheet", pc, Requests.CharacterRequests.Sheet(n.World, pc), pc);
    }

    /// <summary>Opens a creature's inventory (a creature or another player in her area) to the player; FALSE closes it.</summary>
    [NwnxFunc(P, "OpenInventory")]
    static void OpenInventory(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject<Creature>(); bool open = n.PopInt() != 0;
        if (pc == null || target == null || target.Area != pc.Area) return;
        if (open) n.World.Output?.Notify("inventory-open", target, n.World.Inventory.InventoryView(target), pc);
        else Tell(n, pc, new { op = "inventory-close", target = target.Id });
    }

    [NwnxFunc(P, "ForcePlaceableExamineWindow")]
    static void ExamineWindow(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject<Placeable>();
        if (pc != null && target != null) n.World.Output?.Notify("examine", target, Requests.Examine.Describe(n.World, target, pc), pc);
    }

    [NwnxFunc(P, "ForcePlaceableInventoryWindow")]
    static void InventoryWindow(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject<Placeable>();
        if (pc != null && target is { HasInventory: true }) n.World.System<Requests.ContainerService>().Show(pc, target);
    }

    [NwnxFunc(P, "SetTlkOverride")]
    static void TlkOverride(NwnxCall n)
    {
        var pc = Pc(n); int strref = n.PopInt(); var text = n.PopString(); bool restoreGlobal = n.PopInt() != 0;
        if (pc == null) return;
        var restored = text.Length == 0 && restoreGlobal && n.World.TlkOverrides.TryGetValue(strref, out var global) ? global : text;
        Tell(n, pc, new { op = "tlk-override", strref, text = restored });
    }

    [NwnxFunc(P, "ReloadTlk")] static void ReloadTlk(NwnxCall n) => Tell(n, Pc(n), new { op = "reload-tlk" });
    [NwnxFunc(P, "ReloadColorPalettes")] static void ReloadPalettes(NwnxCall n) => Tell(n, Pc(n), new { op = "reload-color-palettes" });

    /// <summary>
    /// One player's client reads the new resource wherever it would read the old one (an empty name clears it). The
    /// client applies it to resources it loads from then on.
    /// </summary>
    [NwnxFunc(P, "SetResManOverride")]
    static void ResManOverride(NwnxCall n)
    {
        var pc = Pc(n); int type = n.PopInt(); var from = n.PopString(); var to = n.PopString();
        if (pc == null || from.Length is 0 or > 16 || to.Length > 16) return;
        Tell(n, pc, new { op = "resman-override", type, from, to });
    }

    /// <summary>Sets a conversation token for one player; SetCustomToken for that token replaces it for everyone.</summary>
    [NwnxFunc(P, "SetCustomToken")]
    static void CustomToken(NwnxCall n)
    {
        var pc = Pc(n); int token = n.PopInt(); var value = n.PopString();
        if (pc != null) n.World.System<Conversations.ConversationService>().PlayerTokens[(pc.Id, token)] = value;
    }

    [NwnxFunc(P, "UpdateItemName")]
    static void UpdateItemName(NwnxCall n)
    {
        var pc = Pc(n); var item = n.PopObject<Item>();
        if (pc != null && item != null) n.World.MarkChanged(item);
    }

    // ---- Player identity -----------------------------------------------------------

    [NwnxFunc(P, "ToggleDM")]
    static void ToggleDM(NwnxCall n)
    {
        var pc = Pc(n); bool dm = n.PopInt() != 0;
        if (pc?.Player == null) return;
        pc.Player.IsDM = dm;
        pc.IsPlayerDM = dm;
        Tell(n, pc, new { op = "dm", enabled = dm });
    }
    [NwnxFunc(P, "GetPlatformId")] static void PlatformId(NwnxCall n) => n.Push(Pc(n)?.Client.Platform ?? 0);
    [NwnxFunc(P, "GetLanguage")] static void Language(NwnxCall n) => n.Push(Pc(n) is { } pc ? Math.Max(0, pc.Client.Language) : 0);
    [NwnxFunc(P, "GetBicFileName")] static void BicFileName(NwnxCall n) => n.Push(Pc(n)?.VaultFile ?? "");

    /// <summary>The creature the player controls now (her familiar or a possessed creature while she possesses it).</summary>
    [NwnxFunc(P, "GetGameObject")]
    static void GameObjectOf(NwnxCall n) =>
        n.PushObject(n.PopObject<Creature>() is { } pc && FamiliarPossession.SessionOf(pc)?.Creature is { } c ? c.Id : GameObject.InvalidId);

    // ---- Journal -------------------------------------------------------------------

    [NwnxFunc(P, "GetJournalEntry")]
    static void JournalEntry(NwnxCall n)
    {
        var pc = Pc(n); var tag = n.PopString();
        var journal = n.World.System<JournalService>();
        if (pc != null && journal.CustomEntries(pc).TryGetValue(tag, out var custom))
        {
            n.Push(custom.Text);
            n.Push(custom.Name);
            n.Push(custom.Day);
            n.Push(custom.Time);
            n.Push(custom.State);
            n.Push(custom.Priority);
            n.Push(custom.Completed);
            n.Push(custom.Displayed);
            n.Push(1);
            return;
        }
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

    [NwnxFunc(P, "AddCustomJournalEntry")]
    static void AddCustomJournalEntry(NwnxCall n)
    {
        var pc = Pc(n);
        var name = n.PopString(); var text = n.PopString(); var tag = n.PopString();
        int state = n.PopInt(), priority = n.PopInt(), completed = n.PopInt(), displayed = n.PopInt();
        n.PopInt(); // nUpdated
        int day = n.PopInt(), time = n.PopInt(); bool silent = n.PopInt() != 0;
        if (pc == null || tag.Length == 0) { n.Push(-1); return; }
        var journal = n.World.System<JournalService>();
        n.Push(journal.AddCustom(pc, new JournalService.CustomEntry(tag, name, text, state, priority, completed != 0, displayed != 0, day, time), silent));
    }

    /// <summary>TRUE when the player's entry of the quest is an end entry, FALSE when not, -1 without an entry.</summary>
    [NwnxFunc(P, "GetQuestCompleted")]
    static void QuestCompleted(NwnxCall n)
    {
        var pc = Pc(n); var quest = n.PopString();
        var journal = n.World.System<JournalService>();
        if (pc != null && journal.CustomEntries(pc).TryGetValue(quest, out var custom)) { n.Push(custom.Completed); return; }
        if (pc == null || !pc.Journal.TryGetValue(quest, out var state)) { n.Push(-1); return; }
        n.Push(journal.Categories.TryGetValue(quest, out var cat) && cat.Entries.TryGetValue(state.State, out var entry) && entry.End);
    }

    // ---- Map exploration -----------------------------------------------------------

    [NwnxFunc(P, "GetAreaExplorationState")]
    static void GetExploration(NwnxCall n)
    {
        var pc = Pc(n); var area = n.PopObject<Area>();
        if (pc == null || area == null || !WorldObjects.AreaSettings.Of(n.World, area).Explored.TryGetValue(pc.Id, out var tiles)) { n.Push(""); return; }
        var bytes = new byte[(tiles.Length + 7) / 8];
        for (int i = 0; i < tiles.Length; i++) if (tiles[i]) bytes[i / 8] |= (byte)(1 << (i % 8));
        n.Push(Convert.ToBase64String(bytes));
    }

    [NwnxFunc(P, "SetAreaExplorationState")]
    static void SetExploration(NwnxCall n)
    {
        var pc = Pc(n); var area = n.PopObject<Area>(); var data = n.PopString();
        if (pc == null || area == null) return;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(data); }
        catch (FormatException) { return; }
        var env = WorldObjects.AreaSettings.Of(n.World, area);
        var tiles = new bool[Math.Max(0, area.Width * area.Height)];
        for (int i = 0; i < tiles.Length && i / 8 < bytes.Length; i++) tiles[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
        env.Explored[pc.Id] = tiles;
        for (int i = 0; i < tiles.Length; i++)
            n.World.Output?.Notify("tile-explored", area, new Net.TileExploredNotice(area.Id, i % area.Width, i / area.Width, tiles[i]), pc);
    }

    // ---- Possession ----------------------------------------------------------------

    [NwnxFunc(P, "PossessCreature")]
    static void Possess(NwnxCall n)
    {
        var pc = Pc(n); var target = n.PopObject<Creature>(); n.PopInt(); n.PopInt();
        n.Push(pc?.Player is { } session && target != null && n.World.System<FamiliarPossession>().PossessCreature(session, target));
    }

    // ---- Stores and parties --------------------------------------------------------

    [NwnxFunc(P, "CloseStore")]
    static void CloseStore(NwnxCall n)
    {
        if (Pc(n) is { } pc) n.World.Inventory.CloseStore(pc);
    }

    [NwnxFunc(P, "GetOpenStore")]
    static void GetOpenStore(NwnxCall n) => n.PushObject(Pc(n) is { } pc && n.World.Inventory.SessionOf(pc) is { } s ? s.Store.Id : GameObject.InvalidId);

    /// <summary>Invites a player into the inviter's party as the party menu does.</summary>
    [NwnxFunc(P, "SendPartyInvite")]
    static void SendPartyInvite(NwnxCall n)
    {
        var pc = Pc(n); var inviter = n.PopObject<Creature>(); n.PopInt(); n.PopInt();
        if (pc != null && inviter is { IsPC: true }) Society.PartyService.Of(n.World).Invite(inviter, pc);
    }

    // ---- Logging in ----------------------------------------------------------------

    sealed record PersistentSpot(uint Area, Vector3 Position, float Facing, bool FirstConnectOnly);

    static Dictionary<string, PersistentSpot> PersistentSpots(NwnxRegistry r)
    {
        var state = r.ObjectState(GameObject.InvalidId);
        if (state.TryGetValue("persistentLocations", out var v) && v.Value is Dictionary<string, PersistentSpot> d) return d;
        d = new Dictionary<string, PersistentSpot>(StringComparer.OrdinalIgnoreCase);
        state["persistentLocations"] = new NwnxValue(NwnxType.Object, d);
        return d;
    }

    /// <summary>Where a player with this key or name and character file starts: at the waypoint, at the next login only or at every one.</summary>
    [NwnxFunc(P, "SetPersistentLocation")]
    static void SetPersistentLocation(NwnxCall n)
    {
        var who = n.PopString(); var bic = n.PopString(); var waypoint = n.PopObject(); bool firstOnly = n.PopInt() != 0;
        if (waypoint?.Area is not { } area) return;
        PersistentSpots(n.Registry)[who + "\n" + bic] = new PersistentSpot(area.Id, waypoint.Position, waypoint.Facing, firstOnly);
    }

    /// <summary>Where this player enters this time; read as she enters (OnClientEnter is early enough in OpenWinter).</summary>
    [NwnxFunc(P, "SetSpawnLocation")]
    static void SetSpawnLocation(NwnxCall n)
    {
        var pc = Pc(n); var location = n.PopLocation();
        if (pc == null || n.World.Get(location.Area) is not Area) return;
        n.State(pc)["spawn"] = new NwnxValue(NwnxType.Location, location);
    }

    /// <summary>The spot a script chose for a player entering the server, if any.</summary>
    public static (Area Area, Vector3 Position, float Facing)? SpawnOverride(World world, PlayerSession session, Creature pc)
    {
        var registry = world.System<NwnxRegistry>();
        if (registry.ObjectState(pc.Id).Remove("spawn", out var spawn) && spawn.Value is Location l && world.Get(l.Area) is Area spawnArea)
            return (spawnArea, l.Position, l.FacingDegrees * MathF.PI / 180f);
        if (!registry.ObjectState(GameObject.InvalidId).ContainsKey("persistentLocations") || pc.VaultFile.Length == 0) return null;
        var spots = PersistentSpots(registry);
        foreach (var who in new[] { session.PublicKey, session.PlayerName })
        {
            var key = who + "\n" + pc.VaultFile;
            if (who.Length == 0 || !spots.TryGetValue(key, out var spot) || world.Get(spot.Area) is not Area area) continue;
            if (spot.FirstConnectOnly) spots.Remove(key);
            return (area, spot.Position, spot.Facing);
        }
        return null;
    }

    // ---- TURDs and DM palettes -----------------------------------------------------

    [NwnxFunc(P, "GetTURD", UnsupportedReason = "OpenWinter keeps no TURD for players who leave")]
    static void GetTurd(NwnxCall n)
    {
        Pc(n);
        n.PushObject(GameObject.InvalidId);
    }

    [NwnxFunc(P, "RemoveEffectFromTURD", UnsupportedReason = "OpenWinter keeps no TURD for players who leave")]
    static void RemoveEffectFromTurd(NwnxCall n)
    {
        Pc(n); n.PopString();
    }

    /// <summary>Sends a DM every creator palette again.</summary>
    [NwnxFunc(P, "SendDMAllCreatorLists")]
    static void SendDMAllCreatorLists(NwnxCall n)
    {
        if (Pc(n) is not { Player.IsDM: true } dm) return;
        var palettes = n.World.System<DungeonMaster.DungeonMasterSystem>().Palettes;
        object Node(DungeonMaster.PaletteNode p) => p.IsBlueprint
            ? new { n = p.Name, r = p.ResRef, cr = p.CR, f = p.Faction }
            : new { n = p.Name, c = p.Children.Select(Node).ToList() };
        foreach (var kind in DungeonMaster.Palettes.Kinds)
            n.World.Output?.Notify("dm-palette", dm, new { kind = kind.Key, title = kind.Title, tree = palettes.Tree(kind).Select(Node).ToList() }, dm);
    }
}

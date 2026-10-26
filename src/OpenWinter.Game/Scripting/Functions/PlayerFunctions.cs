using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>
/// Player-client functions: camera, fades, cutscenes, GUI panels, targeting
/// mode, audio streams, client info, journal, booting and per-object UI hints.
/// Anything visual is sent to that player's client as a notify.
/// </summary>
static class PlayerFunctions
{
    static Creature? Pc(Call c, int i) => c.Obj<Creature>(i) is { IsPC: true } pc ? pc : null;
    static Creature? SelfPc(Call c) => c.Self is Creature { IsPC: true } pc ? pc : null;

    static void Tell(Call c, Creature? pc, string kind, object data)
    {
        if (pc != null) c.World.Output?.Notify(kind, pc, data, pc);
    }

    // ---- Camera --------------------------------------------------------------

    [NwFunc("SetCameraFacing")]
    static object? SetCameraFacing(Call c)
    {
        Tell(c, SelfPc(c), "camera", new { op = "facing", direction = c.F(0), distance = c.F(1, -1), pitch = c.F(2, -1), transition = c.I(3) });
        return null;
    }
    [NwFunc("RestoreCameraFacing")]
    static object? RestoreCameraFacing(Call c)
    {
        if (SelfPc(c) is { } pc)
        {
            Tell(c, pc, "camera", new { op = "restore" });
            pc.StoredCamera = null;
        }
        return null;
    }
    [NwFunc("SetCameraMode")]
    static object? SetCameraMode(Call c)
    {
        int mode = c.I(1);
        if (Pc(c, 0) is { } pc && mode is >= 0 and <= 2)
        {
            pc.CameraMode = mode;
            Tell(c, pc, "camera", new { op = "mode", mode });
        }
        return null;
    }
    [NwFunc("SetCameraHeight")]
    static object? SetCameraHeight(Call c)
    {
        if (Pc(c, 0) is { } pc)
        {
            pc.CameraHeight = c.F(1);
            Tell(c, pc, "camera", new { op = "height", height = pc.CameraHeight });
        }
        return null;
    }
    [NwFunc("SetCameraLimits")]
    static object? SetCameraLimits(Call c)
    {
        Tell(c, Pc(c, 0), "camera", new { op = "limits", minPitch = c.F(1, -1), maxPitch = c.F(2, -1), minDistance = c.F(3, -1), maxDistance = c.F(4, -1) });
        return null;
    }
    [NwFunc("SetCameraFlags")]
    static object? SetCameraFlags(Call c)
    {
        if (Pc(c, 0) is { } pc)
        {
            pc.CameraFlags = c.I(1);
            Tell(c, pc, "camera", new { op = "flags", flags = pc.CameraFlags });
        }
        return null;
    }
    [NwFunc("AttachCamera")]
    static object? AttachCamera(Call c)
    {
        if (Pc(c, 0) is not { } pc) return null;
        var target = c.Obj(1);
        pc.CameraAttachedTo = target is Creature or Placeable ? target.Id : GameObject.InvalidId;
        Tell(c, pc, "camera", new { op = "attach", target = pc.CameraAttachedTo, findClearView = c.I(2) != 0 });
        return null;
    }

    // ---- Cutscenes and fades -------------------------------------------------

    [NwFunc("SetCutsceneMode")]
    static object? SetCutsceneMode(Call c)
    {
        if (c.Obj<Creature>(0) is not { } cr) return null;
        bool on = c.I(1, 1) != 0;
        if (on)
        {
            if (!cr.InCutscene) cr.PlotBeforeCutscene = cr.Plot;
            cr.InCutscene = true;
            cr.CutsceneLeftClick = c.I(2) != 0;
            cr.Plot = true;
        }
        else if (cr.InCutscene)
        {
            cr.InCutscene = false;
            cr.CutsceneLeftClick = false;
            cr.Plot = cr.PlotBeforeCutscene;
        }
        if (cr.IsPC) Tell(c, cr, "cutscene", new { enabled = cr.InCutscene, leftClick = cr.CutsceneLeftClick });
        return null;
    }
    [NwFunc("GetCutsceneMode")] static object? GetCutsceneMode(Call c) => c.Obj<Creature>(0)?.InCutscene == true ? 1 : 0;
    [NwFunc("GetCutsceneCameraMoveRate")] static object? GetCameraMoveRate(Call c) => c.Obj<Creature>(0)?.CutsceneCameraMoveRate ?? 0f;
    [NwFunc("SetCutsceneCameraMoveRate")]
    static object? SetCameraMoveRate(Call c)
    {
        if (c.Obj<Creature>(0) is { } cr)
        {
            cr.CutsceneCameraMoveRate = Math.Clamp(c.F(1), 0.1f, 2.0f);
            if (cr.IsPC) Tell(c, cr, "camera", new { op = "move-rate", rate = cr.CutsceneCameraMoveRate });
        }
        return null;
    }

    static object? Fade(Call c, string op, float speed = 0)
    {
        Tell(c, Pc(c, 0), "fade", new { op, speed });
        return null;
    }
    [NwFunc("FadeFromBlack")] static object? FadeFromBlack(Call c) => Fade(c, "from-black", c.F(1, 0.01f));
    [NwFunc("FadeToBlack")] static object? FadeToBlack(Call c) => Fade(c, "to-black", c.F(1, 0.01f));
    [NwFunc("StopFade")] static object? StopFade(Call c) => Fade(c, "stop");
    [NwFunc("BlackScreen")] static object? BlackScreen(Call c) => Fade(c, "black");

    // ---- GUI panels ---------------------------------------------------------

    [NwFunc("PopUpGUIPanel")]
    static object? PopUpGUIPanel(Call c)
    {
        int panel = c.I(1);
        // Not for the compass, level-up, gold or examine panels.
        if (panel is 3 or 9 or 10 or 11 or 12 or 13 or 14 or 15 || panel < 0 || panel > 22) return null;
        if (panel == 0 && Pc(c, 0)?.Player is { } session) session.DeathPanelRespawn = true;
        Tell(c, Pc(c, 0), "gui-panel", new { panel });
        return null;
    }
    [NwFunc("PopUpDeathGUIPanel")]
    static object? PopUpDeathGUIPanel(Call c)
    {
        var help = c.S(4).Length > 0 ? c.S(4) : c.I(3) != 0 ? c.World.TlkStringOverridden(c.I(3)) : "";
        if (Pc(c, 0)?.Player is { } session) session.DeathPanelRespawn = c.I(1, 1) != 0;
        Tell(c, Pc(c, 0), "gui-death", new { respawn = c.I(1, 1) != 0, waitForHelp = c.I(2, 1) != 0, help });
        return null;
    }
    [NwFunc("SetPanelButtonFlash")]
    static object? SetPanelButtonFlash(Call c)
    {
        Tell(c, Pc(c, 0), "panel-flash", new { button = c.I(1), flash = c.I(2) != 0 });
        return null;
    }
    [NwFunc("SetGuiPanelDisabled")]
    static object? SetGuiPanelDisabled(Call c)
    {
        if (Pc(c, 0) is not { } pc) return null;
        int panel = c.I(1);
        bool disabled = c.I(2) != 0;
        if (disabled) pc.DisabledGuiPanels[panel] = c.O(3);
        else pc.DisabledGuiPanels.Remove(panel);
        Tell(c, pc, "gui-disable", new { panel, disabled, target = c.O(3) });
        return null;
    }

    [NwFunc("GetLastGuiEventPlayer")] static object? GuiPlayer(Call c) => c.World.LastGuiEvent?.Player ?? GameObject.InvalidId;
    [NwFunc("GetLastGuiEventType")] static object? GuiType(Call c) => c.World.LastGuiEvent?.Type ?? 0;
    [NwFunc("GetLastGuiEventInteger")] static object? GuiInteger(Call c) => c.World.LastGuiEvent?.Integer ?? 0;
    [NwFunc("GetLastGuiEventObject")]
    static object? GuiObject(Call c) =>
        c.World.LastGuiEvent is { } e && c.World.Get(e.Object) != null ? e.Object : GameObject.InvalidId;
    [NwFunc("GetLastGuiEventVector")] static object? GuiVector(Call c) => c.World.LastGuiEvent?.Vector ?? Vector3.Zero;

    [NwFunc("PostString")]
    static object? PostString(Call c)
    {
        Tell(c, Pc(c, 0), "post-string", new
        {
            text = c.S(1), x = c.I(2), y = c.I(3), anchor = c.I(4), life = c.F(5, 10f),
            rgba = unchecked((uint)c.I(6)), rgba2 = unchecked((uint)c.I(7)), id = c.I(8), font = c.S(9),
        });
        return null;
    }

    // ---- Targeting mode -----------------------------------------------------

    [NwFunc("EnterTargetingMode")]
    static object? EnterTargetingMode(Call c)
    {
        if (Pc(c, 0) is not { } pc) return null;
        var data = pc.PendingTargetingData;
        pc.PendingTargetingData = null;
        pc.AwaitingTarget = true;
        Tell(c, pc, "targeting-mode", new
        {
            types = c.I(1, ObjectFunctions.TypeAll), cursor = c.I(2, 29), badCursor = c.I(3, 31),
            shape = data?.Shape ?? 0, sizeX = data?.SizeX ?? 0, sizeY = data?.SizeY ?? 0, flags = data?.Flags ?? 0,
            range = data?.Range ?? 0, spell = data?.Spell ?? -1, feat = data?.Feat ?? -1,
        });
        return null;
    }
    [NwFunc("SetEnterTargetingModeData")]
    static object? SetTargetingData(Call c)
    {
        if (Pc(c, 0) is { } pc)
            pc.PendingTargetingData = new SpellTargetingData
            {
                Shape = c.I(1), SizeX = c.F(2), SizeY = c.F(3), Flags = c.I(4), Range = c.F(5), Spell = c.I(6, -1), Feat = c.I(7, -1),
            };
        return null;
    }
    [NwFunc("GetLastPlayerToSelectTarget")] static object? TargetPlayer(Call c) => c.World.LastTarget?.Player ?? GameObject.InvalidId;
    [NwFunc("GetTargetingModeSelectedObject")]
    static object? TargetObject(Call c) =>
        c.World.LastTarget is { } t && c.World.Get(t.Target) != null ? t.Target : GameObject.InvalidId;
    [NwFunc("GetTargetingModeSelectedPosition")] static object? TargetPosition(Call c) => c.World.LastTarget?.Position ?? Vector3.Zero;

    // ---- Audio streams ------------------------------------------------------

    [NwFunc("StartAudioStream")]
    static object? StartAudioStream(Call c)
    {
        Tell(c, Pc(c, 0), "audio-stream", new
        {
            op = "start", stream = c.I(1), resref = c.S(2), looping = c.I(3) != 0, fade = c.F(4), seek = c.F(5, -1), volume = c.F(6, 1),
        });
        return null;
    }
    [NwFunc("StopAudioStream")]
    static object? StopAudioStream(Call c)
    {
        Tell(c, Pc(c, 0), "audio-stream", new { op = "stop", stream = c.I(1), fade = c.F(2) });
        return null;
    }
    [NwFunc("SetAudioStreamPaused")]
    static object? PauseAudioStream(Call c)
    {
        Tell(c, Pc(c, 0), "audio-stream", new { op = "pause", stream = c.I(1), paused = c.I(2) != 0, fade = c.F(3) });
        return null;
    }
    [NwFunc("SetAudioStreamVolume")]
    static object? AudioStreamVolume(Call c)
    {
        Tell(c, Pc(c, 0), "audio-stream", new { op = "volume", stream = c.I(1), volume = Math.Clamp(c.F(2, 1), 0, 1), fade = c.F(3) });
        return null;
    }
    [NwFunc("SeekAudioStream")]
    static object? SeekAudioStream(Call c)
    {
        Tell(c, Pc(c, 0), "audio-stream", new { op = "seek", stream = c.I(1), seconds = c.F(2) });
        return null;
    }

    // ---- Client info ---------------------------------------------------------

    [NwFunc("GetPlayerBuildVersionMajor")] static object? BuildMajor(Call c) => Pc(c, 0)?.Client.BuildMajor ?? 0;
    [NwFunc("GetPlayerBuildVersionMinor")] static object? BuildMinor(Call c) => Pc(c, 0)?.Client.BuildMinor ?? 0;
    [NwFunc("GetPlayerBuildVersionPostfix")] static object? BuildPostfix(Call c) => Pc(c, 0)?.Client.BuildPostfix ?? 0;
    [NwFunc("GetPlayerBuildVersionCommitSha1")] static object? BuildSha1(Call c) => Pc(c, 0)?.Client.BuildSha1 ?? "";
    [NwFunc("GetPlayerLanguage")] static object? Language(Call c) => Pc(c, 0)?.Client.Language ?? -1;
    [NwFunc("GetPlayerDevicePlatform")] static object? Platform(Call c) => Pc(c, 0)?.Client.Platform ?? 0;
    [NwFunc("GetPlayerDeviceProperty")]
    static object? DeviceProperty(Call c) =>
        Pc(c, 0)?.Client.DeviceProperties.GetValueOrDefault(c.S(1), -1) ?? -1;
    [NwFunc("GetPlayerNetworkLatency")]
    static object? Latency(Call c) =>
        Pc(c, 0) is { } pc ? (c.I(1, 1) != 0 ? pc.Client.LatencySmoothedMs : pc.Client.LatencyMs) : 0;
    [NwFunc("GetIsPlayerConnectionRelayed")] static object? Relayed(Call c) => Pc(c, 0)?.Client.Relayed == true ? 1 : 0;
    [NwFunc("GetIsPlayerDM")] static object? IsPlayerDM(Call c) => Pc(c, 0)?.IsPlayerDM == true ? 1 : 0;

    [NwFunc("BootPC")]
    static object? BootPC(Call c)
    {
        // A player controlling its familiar is booted through its character too.
        if (c.Obj<Creature>(0) is not { } target || FamiliarPossession.SessionOf(target) is not { } session) return null;
        Tell(c, session.Creature, "boot", new { reason = c.S(1) });
        // The server notices the closed connection on its next tick and runs OnClientLeave.
        session.Connection.Disconnect();
        return null;
    }

    [NwFunc("Vibrate")]
    static object? Vibrate(Call c)
    {
        Tell(c, Pc(c, 0), "vibrate", new { motor = c.I(1), strength = Math.Clamp(c.F(2), 0, 1), seconds = c.F(3) });
        return null;
    }
    [NwFunc("UnlockAchievement")]
    static object? UnlockAchievement(Call c)
    {
        Tell(c, Pc(c, 0), "achievement", new { id = c.S(1), last = c.I(2), current = c.I(3), max = c.I(4) });
        return null;
    }

    // ---- Party, reputation and control ------------------------------------

    [NwFunc("SetPCLike")]
    static object? SetPCLike(Call c)
    {
        if (Pc(c, 0) is { } a && Pc(c, 1) is { } b) Society.PlayerRelations.SetLike(c.World, a, b, like: true);
        return null;
    }
    [NwFunc("SetPCDislike")]
    static object? SetPCDislike(Call c)
    {
        if (Pc(c, 0) is { } a && Pc(c, 1) is { } b) Society.PlayerRelations.SetLike(c.World, a, b, like: false);
        return null;
    }
    [NwFunc("SetCommandingPlayer")]
    static object? SetCommandingPlayer(Call c)
    {
        // Consulted through World.CommandingPlayer and AssociateService.CommandableBy, only with player party control on.
        if (c.Obj<Creature>(0) is { } cr) cr.CommandingPlayer = c.World.Get(c.O(1)) != null ? c.O(1) : GameObject.InvalidId;
        return null;
    }
    [NwFunc("PlayVoiceChat")]
    static object? PlayVoiceChat(Call c)
    {
        if (c.ObjOrSelf(1) is Creature cr && cr.Area != null)
            OpenWinter.Game.WorldObjects.VoiceService.Play(c.World, cr, c.I(0));
        return null;
    }

    // ---- Per-object UI hints -------------------------------------------------

    static void TellArea(Call c, GameObject o, string kind, object data) => c.World.Output?.Notify(kind, o, data);

    [NwFunc("SetObjectMouseCursor")]
    static object? SetObjectMouseCursor(Call c)
    {
        if (c.Obj(0) is { } o)
        {
            o.MouseCursor = c.I(1, -1);
            TellArea(c, o, "mouse-cursor", new { cursor = o.MouseCursor });
        }
        return null;
    }
    // Measured (nwnx_area_objects): an object left at the default mask (or set back to -1) reads 15, waypoints too.
    [NwFunc("GetObjectUiDiscoveryMask")]
    static object? GetUiDiscovery(Call c) =>
        c.Obj(0) is { } o and not (Areas.Area or Module) ? (o.UiDiscoveryMask == -1 ? 15 : o.UiDiscoveryMask) : -1;
    [NwFunc("SetObjectUiDiscoveryMask")]
    static object? SetUiDiscovery(Call c)
    {
        if (c.Obj(0) is Creature or Door or Item or Placeable && c.Obj(0) is { } o)
        {
            o.UiDiscoveryMask = c.I(1, -1);
            TellArea(c, o, "ui-discovery", new { mask = o.UiDiscoveryMask });
        }
        return null;
    }
    [NwFunc("SetObjectTextBubbleOverride")]
    static object? SetTextBubble(Call c)
    {
        if (c.Obj(0) is { } o)
        {
            o.TextBubbleMode = c.I(1);
            o.TextBubbleText = c.S(2);
            TellArea(c, o, "text-bubble", new { mode = o.TextBubbleMode, text = o.TextBubbleText });
        }
        return null;
    }
    [NwFunc("SetCreatureExploresMinimap")]
    static object? SetExploresMinimap(Call c)
    {
        if (c.Obj<Creature>(0) is not { } cr) return -1;
        int before = cr.ExploresMinimap ? 1 : 0;
        cr.ExploresMinimap = c.I(1) != 0;
        return before;
    }
    [NwFunc("GetCreatureExploresMinimap")] static object? GetExploresMinimap(Call c) => c.Obj<Creature>(0)?.ExploresMinimap == true ? 1 : 0;

    // ---- Journal ------------------------------------------------------------

    [NwFunc("AddJournalQuestEntry")]
    static object? AddJournal(Call c)
    {
        c.World.System<JournalService>().Add(c.S(0), c.I(1), c.Obj<Creature>(2), c.I(3, 1) != 0, c.I(4) != 0, c.I(5) != 0);
        return null;
    }
    [NwFunc("RemoveJournalQuestEntry")]
    static object? RemoveJournal(Call c)
    {
        c.World.System<JournalService>().Remove(c.S(0), c.Obj<Creature>(1), c.I(2, 1) != 0, c.I(3) != 0);
        return null;
    }
    [NwFunc("GetJournalQuestExperience")] static object? JournalXP(Call c) => c.World.System<JournalService>().Experience(c.S(0));
}

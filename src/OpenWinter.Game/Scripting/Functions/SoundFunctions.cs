using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>One-shot sounds, sound objects, background/battle music and ambient sound.</summary>
static class SoundFunctions
{
    /// <summary>Objects with a running action queue get PlaySound as an action; areas and the module play at once.</summary>
    static void PlayOrQueue(Call c, string resref, int strRef, bool asAction)
    {
        var self = c.Self;
        if (self == null) return;
        if (asAction && self is not (Area or Module) && self.Area != null)
        {
            if (self.Commandable) self.Actions.Add(new PlaySoundAction { Sound = resref, StrRef = strRef });
            return;
        }
        SoundService.Play(c.World, self, resref, strRef);
    }

    [NwFunc("PlaySound")] static object? PlaySound(Call c)
    {
        if (c.World.System<Nwnx.NwnxRegistry>().TryLegacyCall(c.S(0), c.Host, c.Thread)) return null; // legacy NWNX call
        PlayOrQueue(c, c.S(0), -1, true);
        return null;
    }
    [NwFunc("PlaySoundByStrRef")] static object? PlaySoundByStrRef(Call c) { PlayOrQueue(c, "", c.I(0), c.I(1, 1) != 0); return null; }

    [NwFunc("GetStrRefSoundDuration")] static object? StrRefDuration(Call c) =>
        SoundService.WavDuration(c.World.Resources, c.World.Resources.Talk.Sound(unchecked((uint)c.I(0))));
    [NwFunc("GetDialogSoundLength")] static object? DialogSoundLength(Call c) =>
        SoundService.WavDuration(c.World.Resources, c.World.Resources.Talk.Sound(unchecked((uint)c.I(0))));

    [NwFunc("SoundObjectPlay")] static object? SoundPlay(Call c)
    {
        if (c.Obj(0) is Sound s) { s.Active = true; SoundService.NotifySoundObject(c.World, s, "play"); }
        return null;
    }
    [NwFunc("SoundObjectStop")] static object? SoundStop(Call c)
    {
        if (c.Obj(0) is Sound s) { s.Active = false; SoundService.NotifySoundObject(c.World, s, "stop"); }
        return null;
    }
    [NwFunc("SoundObjectSetVolume")] static object? SoundVolume(Call c)
    {
        if (c.Obj(0) is Sound s) { s.Volume = Math.Clamp(c.I(1), 0, 127); SoundService.NotifySoundObject(c.World, s, "volume"); }
        return null;
    }
    [NwFunc("SoundObjectSetPosition")] static object? SoundPosition(Call c)
    {
        if (c.Obj(0) is Sound s)
        {
            s.Position = c.V(1);
            c.World.MarkChanged(s);
            SoundService.NotifySoundObject(c.World, s, "position");
        }
        return null;
    }

    static AreaEnvironment? Env(Call c, int i, out Area? area)
    {
        area = c.Obj(i) as Area;
        return area == null ? null : AreaSettings.Of(c.World, area);
    }

    static object? Audio(Call c, Action<AreaEnvironment> change)
    {
        if (Env(c, 0, out var area) is { } env)
        {
            change(env);
            SoundService.NotifyAudio(c.World, area!);
        }
        return null;
    }

    [NwFunc("MusicBackgroundPlay")] static object? MusicPlay(Call c) => Audio(c, e => e.MusicPlaying = true);
    [NwFunc("MusicBackgroundStop")] static object? MusicStop(Call c) => Audio(c, e => e.MusicPlaying = false);
    [NwFunc("MusicBackgroundSetDelay")] static object? MusicDelay(Call c) => Audio(c, e => e.MusicDelay = Math.Max(0, c.I(1)));
    [NwFunc("MusicBackgroundChangeDay")] static object? MusicDay(Call c) => Audio(c, e => e.MusicDay = c.I(1));
    [NwFunc("MusicBackgroundChangeNight")] static object? MusicNight(Call c) => Audio(c, e => e.MusicNight = c.I(1));
    [NwFunc("MusicBattlePlay")] static object? BattlePlay(Call c) => Audio(c, e => e.BattlePlaying = true);
    [NwFunc("MusicBattleStop")] static object? BattleStop(Call c) => Audio(c, e => e.BattlePlaying = false);
    [NwFunc("MusicBattleChange")] static object? BattleChange(Call c) => Audio(c, e => e.MusicBattle = c.I(1));
    [NwFunc("AmbientSoundPlay")] static object? AmbientPlay(Call c) => Audio(c, e => e.AmbientPlaying = true);
    [NwFunc("AmbientSoundStop")] static object? AmbientStop(Call c) => Audio(c, e => e.AmbientPlaying = false);
    [NwFunc("AmbientSoundChangeDay")] static object? AmbientDay(Call c) => Audio(c, e => e.AmbientDay = c.I(1));
    [NwFunc("AmbientSoundChangeNight")] static object? AmbientNight(Call c) => Audio(c, e => e.AmbientNight = c.I(1));
    [NwFunc("AmbientSoundSetDayVolume")] static object? AmbientDayVolume(Call c) => Audio(c, e => e.AmbientDayVolume = Math.Clamp(c.I(1), 0, 100));
    [NwFunc("AmbientSoundSetNightVolume")] static object? AmbientNightVolume(Call c) => Audio(c, e => e.AmbientNightVolume = Math.Clamp(c.I(1), 0, 100));

    // Anything but an area reads 0 (audio_scripts).
    [NwFunc("MusicBackgroundGetDayTrack")] static object? GetDayTrack(Call c) => Env(c, 0, out _)?.MusicDay ?? 0;
    [NwFunc("MusicBackgroundGetNightTrack")] static object? GetNightTrack(Call c) => Env(c, 0, out _)?.MusicNight ?? 0;
    [NwFunc("MusicBackgroundGetBattleTrack")] static object? GetBattleTrack(Call c) => Env(c, 0, out _)?.MusicBattle ?? 0;
}

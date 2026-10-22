using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;

namespace OpenWinter.Game.WorldObjects;

/// <summary>
/// When a player arrives in an area, re-sends the script-made changes the area
/// description doesn't carry (music, weather, fog, sky, wind, light, grass, explored
/// tiles, object visual overrides) using the same notify kinds as the live changes.
/// Runs one step after arrival so the client has loaded the area and its objects.
/// </summary>
public sealed class AreaStateSync : IWorldSystem
{
    readonly World _world;
    public int Order => 40;

    public AreaStateSync(World world)
    {
        _world = world;
        world.ObjectAdded += o =>
        {
            if (o is Creature { IsPC: true } pc && pc.Area is { } area) world.After(0, () => Send(pc, area));
        };
    }

    public void Step(float dt) { }

    void Send(Creature pc, Area area)
    {
        if (pc.IsDestroyed || pc.Area != area || _world.Output is not { } output) return;
        if (area.Environment is { } env)
        {
            output.Notify("area-audio", area, SoundService.AudioData(env), pc);
            output.Notify("weather", area, WeatherSystem.Data(area, env), pc);
            output.Notify("area-fog", area, new { sun = env.SunFog, moon = env.MoonFog }, pc);
            output.Notify("area-skybox", area, new { skybox = env.SkyBox }, pc);
            output.Notify("area-wind", area, new { direction = SoundService.Vec(env.WindDirection), magnitude = env.WindMagnitude, yaw = env.WindYaw, pitch = env.WindPitch }, pc);
            if (env.SunDirectionSet) output.Notify("area-light-direction", area, new { type = 1, direction = SoundService.Vec(env.SunDirection), fade = 0f }, pc);
            if (env.MoonDirectionSet) output.Notify("area-light-direction", area, new { type = 0, direction = SoundService.Vec(env.MoonDirection), fade = 0f }, pc);
            if (env.DefaultGrassDisabled) output.Notify("area-grass", area, new { defaultDisabled = true }, pc);
            foreach (var grass in env.GrassOverrides.Values) output.Notify("area-grass", area, grass, pc);
            if (env.Explored.TryGetValue(pc.Id, out var tiles))
                for (int i = 0; i < tiles.Length; i++)
                    if (tiles[i]) output.Notify("tile-explored", area, new TileExploredNotice(area.Id, i % area.Width, i / area.Width, true), pc);
        }
        foreach (var o in area.Objects)
        {
            if (o.VisualTransforms != null)
                foreach (var ((scope, transform), vt) in o.VisualTransforms)
                    output.Notify("visual-transform", o, new
                    {
                        transform, scope, from = vt.From, value = vt.To, lerp = vt.LerpType, duration = vt.LerpDuration,
                        pauseWithGame = vt.PauseWithGame, behavior = vt.Behavior, repeats = vt.Repeats,
                        // How far the lerp has run, so the arriving client joins it mid-way.
                        elapsed = _world.Time - vt.StartTime,
                    }, pc);
            if (o.TextureReplacements != null)
                foreach (var (old, replacement) in o.TextureReplacements) output.Notify("object-texture", o, new { old, @new = replacement }, pc);
            if (o.AnimationReplacements != null)
                foreach (var (old, replacement) in o.AnimationReplacements) output.Notify("object-animation", o, new { old, @new = replacement }, pc);
            // Traps this player sees: detected, flagged, or its own party's.
            if (o is ITrappable && TrapSystem.Shows(_world, o, pc))
                output.Notify("trap-detected", o, TrapSystem.DetectedData(_world, o, pc, true), pc);
            if (o is Waypoint { HasMapNote: true, MapNoteEnabled: false } w)
                output.Notify("map-pin", w, new { enabled = false, note = w.MapNote.Resolve(_world.Resources.Talk), position = SoundService.Vec(w.Position) }, pc);
        }
    }
}

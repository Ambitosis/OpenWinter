using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    // Explicit capture-only regression: the normal scene and network feed establish visibility.
    // Temporary effects then verify independent reset and appearance rebuild composition.
    void CheckVisualVisibility(string dir, string shot)
    {
        var world = _server!.World;
        var pc = world.Get<Creature>(_session.ControlledId)!;
        var readings = new List<object>();
        foreach (var view in _session.Objects.Values.OfType<CreatureView>())
        {
            if (world.Get<Creature>(view.ObjectId) is not { } actor) continue;
            var state = CreatureVisibilityFeed.Read(world, pc, actor);
            if (state == null) continue;
            bool expectedVisible = state.seen || state.heard;
            float expectedAlpha = !state.seen && state.heard ? .5f : 1;
            void Check(string stage, float alpha)
            {
                var meshes = view.FindChildren("*", "GeometryInstance3D", true, false).OfType<GeometryInstance3D>().ToArray();
                bool ok = view.Visible == expectedVisible && Math.Abs(view.PresentationAlpha - alpha) < .001f
                    && meshes.Length > 0 && meshes.All(g => Math.Abs(g.Transparency - (1 - alpha)) < .001f);
                readings.Add(new
                {
                    shot, id = actor.Id, actor.Tag, stage, expectedVisible, visible = view.Visible,
                    state.seen, state.heard, alpha = view.PresentationAlpha, meshes = meshes.Length, ok
                });
                if (!ok) throw new InvalidOperationException($"Visibility capture {shot}/{actor.Tag}/{stage}");
            }
            Check("stream", expectedAlpha);
            view.SetEffectAlpha(long.MinValue, .25f);
            view.SetStealth(true);
            Check("effect-and-stealth", .25f);
            view.SetStealth(false);
            Check("stealth-cleared", .25f);
            view.SetEffectAlpha(long.MinValue, null);
            Check("effect-cleared", expectedAlpha);
            if (view.CurrentAppearance is { } appearance)
            {
                view.Rebuild(appearance, _session.Content!);
                Check("rebuilt", expectedAlpha);
            }
        }
        System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "visibility.jsonl"),
            System.Text.Json.JsonSerializer.Serialize(new { shot, readings }) + "\n");
    }
}

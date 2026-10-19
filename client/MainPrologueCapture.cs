using OpenWinter.Client.UI;
using OpenWinter.Client.Views;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    async Task CapturePrologue(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        var checks = new List<object>();
        bool passed = false;
        void Check(string name, bool ok) { checks.Add(new { name, ok }); GD.Print($"[prologue] {name}: {ok}"); if (!ok) throw new InvalidOperationException(name); }
        async Task Until(Func<bool> done) { for (int i = 0; i < 600 && !done(); i++) await Wait(.1); Check("wait completed", done()); }
        void Mouse(Vector2 at, MouseButton button, bool down) => Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = down });
        void Move(Vector2 at) { _interaction.PointerOverride = at; Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at }); }
        try
        {
            await Until(() => _session.Controlled != null && !_session.PresentationLoading && !_loading.Showing);
            var pc = _server!.World.Get<Creature>(_session.ControlledId)!;
            var session = pc.Player!;
            var area = pc.Area!;
            _server.EnterArea(session, area, pc.Position, pc.Facing);
            await Wait(.1);
            Check("loading excludes player from world", session.IsAreaLoading && pc.Area == null);
            Check("loading suspends presentation", _session.PresentationLoading && _session.Space.ProcessMode == ProcessModeEnum.Disabled);
            Check("loading has no area audio", !Descendants<AudioStreamPlayer>(_session).Any(p => p.Playing) && !Descendants<AudioStreamPlayer3D>(_session).Any(p => p.Playing));
            await Shot(dir, "a_loading");
            await Until(() => !_session.PresentationLoading && !_loading.Showing && pc.Area != null);
            Check("ready resumes scene", _session.Space.ProcessMode == ProcessModeEnum.Inherit && !session.IsAreaLoading);
            foreach (float progress in new[] { 0f, .5f, 1f })
            {
                _loading.Present(_session.ModuleName, area.Name.FirstString, _session.Content, _session.Area!.Info.LoadScreen, area.Tileset, false, "Loading test");
                _loading.SetStatus("Loading test", progress);
                await Wait(.8); await Shot(dir, "bar_" + (int)(progress * 100));
                _loading.Finish(0); await Wait(.7);
            }
            var radial = Descendants<RadialMenu>(this).Single();
            var point = _camera.UnprojectPosition(_session.Controlled!.GlobalPosition + Vector3.Up);
            Move(point); Mouse(point, MouseButton.Right, true); await Wait(.2);
            Check("world radial opens on right down", radial.IsOpen);
            Mouse(point, MouseButton.Right, false); await Wait(.1);
            Check("opening release keeps radial open", radial.IsOpen);
            radial.Close();
            Move(point); Mouse(point, MouseButton.Right, true); await Wait(.1);
            var choice = radial.WedgePoint("Character Sheet")!.Value;
            Move(choice); await Wait(.1); Mouse(choice, MouseButton.Right, false); await Wait(.2);
            Check("held right release selects", !radial.IsOpen);
            await Shot(dir, "b_held_selection");
            WindowStack.CloseAll(); foreach (var window in Descendants<UI.Classic.ClassicWindow>(this).Where(w => w.IsOpen).ToArray()) window.Close(); await Wait(.2);
            // A stationary right-click also leaves the ring available for ordinary left clicks.
            Move(point); Mouse(point, MouseButton.Right, true); await Wait(.1);
            Mouse(point, MouseButton.Right, false); await Wait(.1);
            choice = radial.WedgePoint("Character Sheet")!.Value;
            Move(choice); Mouse(choice, MouseButton.Left, true); Mouse(choice, MouseButton.Left, false); await Wait(.2);
            Check("left click selects after right release", !radial.IsOpen);
            WindowStack.CloseAll(); foreach (var window in Descendants<UI.Classic.ClassicWindow>(this).Where(w => w.IsOpen).ToArray()) window.Close(); await Wait(.2);
            var door = _session.Objects.Values.Single(v => v.Kind == ObjectKind.Door);
            point = _camera.UnprojectPosition(door.GlobalPosition + Vector3.Up * 1.2f);
            Move(point); Mouse(point, MouseButton.Right, true); await Wait(.2);
            Check("door radial opens while held", radial.IsOpen && radial.Target == door);
            choice = radial.WedgePoint("Examine")!.Value;
            Move(choice); Mouse(choice, MouseButton.Right, false); await Wait(.2);
            Check("door held release selects", !radial.IsOpen);
            WindowStack.CloseAll(); foreach (var window in Descendants<UI.Classic.ClassicWindow>(this).Where(w => w.IsOpen).ToArray()) window.Close(); await Wait(.2);
            point = _camera.UnprojectPosition(_session.Controlled!.GlobalPosition + new Vector3(2, 0, 2));
            Move(point); Mouse(point, MouseButton.Right, true); await Wait(.2);
            Check("ground radial opens while held", radial.IsOpen && radial.Target == null && radial.WedgePoint("Move To").HasValue);
            choice = radial.WedgePoint("Move To")!.Value;
            Move(choice); Mouse(choice, MouseButton.Right, false); await Wait(.2);
            Check("ground held release selects", !radial.IsOpen);
            passed = true;
        }
        catch (Exception e) { GD.PrintErr("[prologue] " + e); await Shot(dir, "failed"); }
        finally
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "checks.json"), System.Text.Json.JsonSerializer.Serialize(new { passed, checks }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}

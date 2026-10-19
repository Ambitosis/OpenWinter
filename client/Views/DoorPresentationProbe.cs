using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using OpenWinter.Client.UI;
using OpenWinter.Client.UI.HudParts;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>Installed-model checks in Godot: posed picking, transition geometry and decoded sound resources.</summary>
public static class DoorPresentationProbe
{
    public static async Task<int> Run(Node owner, string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks.Add(label);
        }
        try
        {
            var cfg = LocalConfig.Find(ProjectSettings.GlobalizePath("res://"))!;
            using var resources = ResMan.ForClient(new GamePaths(cfg.GameRoot, cfg.UserRoot, cfg.Language), [], "");
            var content = new ClientContent(resources);
            var flags = ObjectFlags.Selectable;
            var spawn = new SpawnMessage { ObjectId = 1, Kind = ObjectKind.Door, Flags = flags, Appearance = new Appearance { GenericDoorType = 0 } };
            var door = StaticObjectView.Create(spawn, content)!;
            owner.AddChild(door);
            var animation = door.GetNode<AnimationPlayer>("AnimationPlayer");
            async Task Pose(ObjectFlags state)
            {
                door.ApplyUpdate(new UpdateMessage { Flags = state });
                animation.Advance(2);
                animation.Advance(.1);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var model = content.Models.Get("t_door01")!;
            int sam = model.FindPart("sam");
            var plane = door.GetNode<MeshInstance3D>("fx_door_transition_" + sam);
            var mesh = model.Parts[sam].Mesh!;
            var rest = Rendering.Convert.T(model.RestGlobals()[sam]);
            var centre = mesh.Vertices.Select(Rendering.Convert.V).Aggregate(Vector3.Zero, (a, b) => a + b) / mesh.Vertices.Length;
            var a = Rendering.Convert.V(mesh.Vertices[0]);
            var normal = rest.Basis * (Rendering.Convert.V(mesh.Vertices[1]) - a).Cross(Rendering.Convert.V(mesh.Vertices[2]) - a).Normalized();
            var from = rest * centre + normal * 5;
            var direction = -normal;
            await Pose(flags);
            Check(door.PickDoor(from, direction) != null, "closed door covers doorway");
            Check(!plane.Visible, "closed transition mesh hidden");
            foreach (var side in new[] { ObjectFlags.None, ObjectFlags.OpenSide2 })
            {
                await Pose(flags | ObjectFlags.Open | side);
                Check(door.PickDoor(from, direction) == null, $"open side {side}: empty doorway passes through");
                // Aim at an actual triangle on the swung leaf, away from the old doorway plane.
                var skeleton = door.GetNode<Skeleton3D>("Skeleton3D");
                var leaf = model.Parts.FindIndex(p => p.Mesh is { Texture: "DR_01", Indices.Length: > 30 });
                var leafMesh = model.Parts[leaf].Mesh!;
                var pose = skeleton.GetBoneGlobalPose(leaf);
                var points = leafMesh.Indices.Take(3).Select(i => pose * Rendering.Convert.V(leafMesh.Vertices[i])).ToArray();
                var middle = (points[0] + points[1] + points[2]) / 3;
                var n = (points[1] - points[0]).Cross(points[2] - points[0]).Normalized();
                Check(door.PickDoor(middle + n, -n) != null, $"open side {side}: posed leaf is pickable");
                Check(RadialMenu.DoorMenuAction(door) == ActionKind.CloseDoor, "open radial uses explicit CloseDoor");
                Check(Cursors.DoorCursor(door.Flags) == Cursors.Original.Door, "plain open door cursor");
                await Pose(flags | ObjectFlags.Open | ObjectFlags.Transition | side);
                Check(door.PickDoor(from, direction, out bool isTransition) != null && isTransition, "linked opening picks authored plane");
                Check(!plane.Visible, "unhovered transition stays invisible but pickable");
                door.SetDoorHovered(true);
                Check(plane.Visible, "hover reveals transition plane");
                Check(Math.Abs(((StandardMaterial3D)plane.Mesh.SurfaceGetMaterial(0)).AlbedoColor.A - .25f) < .001f, "authored trans alpha is retained");
                Check(Cursors.DoorCursor(door.Flags) == Cursors.Original.Transition, "linked open door transition cursor");
                Check(RadialMenu.DoorMenuAction(door) == ActionKind.CloseDoor, "linked radial closes rather than uses transition");
                door.SetDoorHovered(false);
                Check(!plane.Visible, "leaving doorway hides plane");
            }
            await Pose(flags | ObjectFlags.Transition);
            Check(!plane.Visible && Cursors.DoorCursor(door.Flags) == Cursors.Original.Door, "closing restores door cursor and hides transition");
            Check(RadialMenu.DoorMenuAction(door) == ActionKind.OpenDoor, "closed radial uses explicit OpenDoor");
            // A killed transition door (docs/INTER_AREA_MOVEMENT.md, "Killed doors"): the server sends it open and dead.
            await Pose(flags | ObjectFlags.Open | ObjectFlags.Transition | ObjectFlags.Dead);
            Check(animation.CurrentAnimation == "dead" || animation.AssignedAnimation == "dead", "killed door holds its dead pose");
            Check(door.PickDoor(from, direction, out bool killedTransition) != null && killedTransition, "killed linked doorway picks the transition plane");
            Check(Cursors.DoorCursor(door.Flags) == Cursors.Original.Transition, "killed linked door shows the transition cursor");
            door.SetDoorHovered(true);
            Check(plane.Visible, "hovering a killed linked doorway reveals the transition plane");
            door.SetDoorHovered(false);
            await Pose(flags | ObjectFlags.Transition);
            Check(door.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().All(m => !Rendering.Keyhole.IsKeyholed(m)), "doors have no keyhole material or mask cut");
            foreach (float rotation in new[] { 0f, MathF.PI / 2, MathF.PI })
            {
                door.Rotation = new Vector3(0, 0, rotation);
                foreach (var state in new[] { flags, flags | ObjectFlags.Open, flags | ObjectFlags.Open | ObjectFlags.Transition })
                {
                    await Pose(state);
                    void Camera(float eye, float actor) => door.UpdateDoorCamera(door.ToGlobal(new Vector3(4, eye, 4)), door.ToGlobal(new Vector3(0, actor, 0)));
                    Camera(8, -3);
                    Check(door.CameraFaded && door.PresentationAlpha == .25f, "opposite sides fade independent of lateral offset, rotation and open pose");
                    Check(door.PickDoor(from, direction) == null, "faded leaf and transition cannot intercept picks");
                    door.SetDoorHovered(true); Check(!plane.Visible, "faded transition cannot be revealed by stale hover");
                    door.SetEffectAlpha(99, .1f); Check(door.PresentationAlpha == .1f, "effect alpha composes with camera fade");
                    door.SetEffectAlpha(99, null); Check(door.PresentationAlpha == .25f, "removing effect retains camera fade");
                    Camera(-8, -3); Check(!door.CameraFaded && door.PresentationAlpha == 1, "same side restores opacity");
                    Camera(-8, 3); Check(door.CameraFaded, "reverse actor side also fades");
                    Camera(8, 3); Check(!door.CameraFaded, "reverse same side restores picking eligibility");
                    Camera(8, .1f); Check(door.CameraFaded, "an actor 0.1 m onto the camera side still fades (doorfaredge)");
                    Camera(8, .2f); Check(!door.CameraFaded, "an actor 0.2 m onto the camera side is solid (doorfaredge)");
                    Camera(-8, -.1f); Check(door.CameraFaded, "the band applies from the other side too");
                    Camera(-8, -.2f); Check(!door.CameraFaded, "and clears at 0.2 m there");
                }
            }
            door.Rotation = Vector3.Zero;
            door.SetUnexplored(true); door.SetEffectAlpha(99, .5f); door.SetEffectAlpha(99, null);
            Check(!door.Visible, "look updates cannot reveal unexplored geometry");
            door.SetDisappeared(true); door.SetUnexplored(false);
            Check(!door.Visible, "discovery cannot reveal disappeared geometry");
            door.SetUnexplored(true); door.SetDisappeared(false);
            Check(door.Visible, "a door already shown stays drawn when its tiles are unexplored again (unexploredkinds)");
            door.SetDisappeared(false); Check(door.Visible, "independent visibility blockers clear correctly");
            var area = AreaView.Build(new LoadAreaMessage { Width = 2, Height = 1, IsInterior = true, Tileset = "tde01", Tiles = [new TileInfo { Id = 4 }, new TileInfo { Id = 4 }], FogClipDistance = 45 }, content, out var environment, out var sun);
            owner.AddChild(area);
            Check(!area.IsExplored(new Vector3(5, 5, 0)) && !area.GetNode<Node3D>("Tiles").GetChildren().OfType<Node3D>().Any(t => t.GetNode<Node3D>("Details").Visible), "fresh interior geometry starts undiscovered");
            area.SetExplored(0, 0, true);
            Check(area.IsExplored(new Vector3(5, 5, 0)) && !area.IsExplored(new Vector3(15, 5, 0)), "discovery reveals only its tile");
            door.Position = new Vector3(10, 5, 0); door.Rotation = new Vector3(0, 0, MathF.PI / 2);
            Check(area.IsExplored(door), "boundary door remains visible from the discovered side");
            area.SetAllExplored(true); Check(area.IsExplored(new Vector3(15, 5, 0)), "script exploration reveals room geometry");
            area.SetAllExplored(false); Check(area.IsExplored(door) && area.IsExplored(new Vector3(15, 5, 0)), "a reset after a script reveal keeps drawing it (unexploredmemory)");
            area.QueueFree(); sun.Free(); environment.Dispose(); door.Position = Vector3.Zero; door.Rotation = Vector3.Zero;
            // A newly arriving observer must see the replicated second swing, not default to the first.
            spawn.Flags = flags | ObjectFlags.Open | ObjectFlags.OpenSide2;
            var late = StaticObjectView.Create(spawn, content)!; owner.AddChild(late);
            late.GetNode<AnimationPlayer>("AnimationPlayer").Advance(.1);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(late.GetNode<AnimationPlayer>("AnimationPlayer").AssignedAnimation == "opened2", "late spawn retains opened2");
            Check(late.PickDoor(from, direction) == null, "late spawn opening remains clear");
            // A barred cell door (the tutorial menagerie's TIN_UDoor_03, doortypes row 43) is picked between its
            // bars while closed: its invisible sam plane spans the frame. The bars alone leave gaps.
            var cell = StaticObjectView.Create(new SpawnMessage { ObjectId = 2, Kind = ObjectKind.Door, Flags = flags, Appearance = new Appearance { Type = 43 } }, content)!;
            owner.AddChild(cell);
            cell.GetNode<AnimationPlayer>("AnimationPlayer").Advance(.1);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            var cellModel = content.Models.Get("tin_udoor_03")!;
            var cellRest = cellModel.RestGlobals();
            int cellSam = cellModel.FindPart("sam");
            var samPoints = cellModel.Parts[cellSam].Mesh!.Vertices.Select(v => Rendering.Convert.V(System.Numerics.Vector3.Transform(v, cellRest[cellSam]))).ToArray();
            float x0 = samPoints.Min(v => v.X), x1 = samPoints.Max(v => v.X), z0 = samPoints.Min(v => v.Z), z1 = samPoints.Max(v => v.Z);
            bool HitsBars(Vector3 origin, Vector3 dir)
            {
                for (int i = 0; i < cellModel.Parts.Count; i++)
                {
                    if (cellModel.Parts[i].Mesh is not { Render: true, Alpha: > 0.01f } m) continue;
                    var g = Rendering.Convert.T(cellRest[i]);
                    for (int f = 0; f + 2 < m.Indices.Length; f += 3)
                        if (Geometry3D.RayIntersectsTriangle(origin, dir, g * Rendering.Convert.V(m.Vertices[m.Indices[f]]),
                                g * Rendering.Convert.V(m.Vertices[m.Indices[f + 1]]), g * Rendering.Convert.V(m.Vertices[m.Indices[f + 2]])).VariantType != Variant.Type.Nil)
                            return true;
                }
                return false;
            }
            int samples = 0, picked = 0, gaps = 0;
            for (int ix = 1; ix < 12; ix++)
                for (int iz = 1; iz < 6; iz++)
                {
                    var origin = new Vector3(x0 + (x1 - x0) * ix / 12f, 5, z0 + (z1 - z0) * iz / 6f);
                    samples++;
                    if (cell.PickDoor(origin, new Vector3(0, -1, 0)) != null) picked++;
                    if (!HitsBars(origin, new Vector3(0, -1, 0))) gaps++;
                }
            Check(gaps > 0, $"cell door bars leave gaps ({gaps} of {samples} rays miss them)");
            Check(picked == samples, $"closed cell door is picked between its bars ({picked} of {samples})");
            cell.QueueFree();
            foreach (int row in Enumerable.Range(0, 12))
                foreach (string state in new[] { "Opened", "Closed", "Locked", "Destroyed" })
                {
                    string sound = resources.TwoDA("placeableobjsnds").Get(row, state);
                    if (sound == "as_dr_metlmedcl2" && resources.Read(sound, ResType.Wav) == null)
                    {
                        Check(content.Audio.Get(sound) == null, "installed sliding metal close cue is absent; no invented replacement");
                        continue;
                    }
                    Check(content.Audio.Get(sound)?.GetLength() > 0, $"door sound {row}/{state}: {sound} decodes");
                }
            late.QueueFree(); door.QueueFree();
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[door-presentation] {checks.Count} checks passed"); return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "failure.txt"), e.ToString()); GD.PrintErr(e); return 1;
        }
    }
}

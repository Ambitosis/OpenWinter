using OpenWinter.Client.Rendering;
using OpenWinter.Content;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// Placeables, doors and items on the ground. Objects whose model has
/// animations get a skeleton and play their state animations: doors
/// closed/opening1/opened1/closing1, containers close/close2open/open/open2close,
/// lights off/off2on/on/on2off, plus "default" idle loops (flags, mills).
/// </summary>
public partial class StaticObjectView : ObjectView
{
    AnimationPlayer? _player;
    string _state = "";

    public static StaticObjectView? Create(SpawnMessage spawn, ClientContent content)
    {
        string model = spawn.Kind switch
        {
            ObjectKind.Placeable => content.Resources.TwoDA("placeables").Get(spawn.Appearance.Type, "ModelName"),
            ObjectKind.Door => spawn.Appearance.Type > 0
                ? content.Resources.TwoDA("doortypes").Get(spawn.Appearance.Type, "Model")
                : content.Resources.TwoDA("genericdoors").Get(spawn.Appearance.GenericDoorType, "ModelName"),
            _ => "",
        };
        var data = spawn.Kind == ObjectKind.Item ? PartBasedBuilder.GroundItem(GroundVisual(spawn), content) : content.Models.Get(model);
        if (data == null)
        {
            if (!string.IsNullOrEmpty(model)) content.WarnOnce($"{spawn.Kind} model {model} not found; object not drawn");
            return null;
        }
        var view = new StaticObjectView();
        view.Init(spawn);
        if (spawn.Kind == ObjectKind.Door) view.InitDoor(data, content);
        var anims = spawn.Kind == ObjectKind.Item ? new List<(string, ModelAnimation, bool)>() : content.Models.AllAnimations(data).ToList();
        if (anims.Any(a => a.Item2.Length > 0.05f || a.Item1 is "opened1" or "open" or "on"))
        {
            var skeleton = ModelBuilder.BuildSkinned(data, content.Materials, part => content.Materials.Get(part.Mesh!, MaterialKind.World),
                anims.Select(a => a.Item2).ToList(), warm: true, includeMesh: part => !view.IsTransitionPart(part));
            view.AddChild(skeleton);
            view._doorSkeleton = spawn.Kind == ObjectKind.Door ? skeleton : null;
            // Lights come from the static builder (meshes and emitters are already on the skeleton).
            var lights = ModelBuilder.BuildStatic(data, content.Materials, meshes: false, emitters: false);
            if (lights.Lights.Count > 0) view.AddChild(lights.Root);
            var library = new AnimationLibrary();
            foreach (var (name, anim, _) in anims)
                library.AddAnimation(name, ModelBuilder.BuildAnimation(anim, skeleton, "Skeleton3D", 1f));
            view._player = new AnimationPlayer { Name = "AnimationPlayer" };
            view.AddChild(view._player);
            view._player.AddAnimationLibrary("", library);
            view.SetState(spawn.Flags, animate: false);
        }
        else
        {
            var built = ModelBuilder.BuildStatic(data, content.Materials, shareMeshes: true);
            view.AddChild(built.Root);
            if (spawn.Kind == ObjectKind.Placeable) AreaView.AddCameraCollision(built.Root, data, built.Faces);
        }
        if (spawn.Kind == ObjectKind.Placeable) AddLight(view, spawn.Appearance.Type, (spawn.Flags & ObjectFlags.Static) != 0, content);
        // Doors use a whole-model camera-side fade; the keyhole never cuts them.
        if (spawn.Kind == ObjectKind.Placeable) Keyhole.Apply(view);
        return view;
    }

    /// <summary>
    /// Range (metres), distance falloff and strength of placeable lights; see <see cref="AddLight"/>. Measured on
    /// the original (parity/visual): a placeable made by script (scene placelight) lights the floor about as the
    /// inverse square of the distance, still a tenth of its strength 10 m away. Static placeables (scene throne,
    /// the HotU throne room's braziers) are part of the original's static lighting, baked per vertex into the
    /// tiles: far less light near them, so a room ringed with braziers is evenly dim rather than blazing.
    /// </summary>
    public static float PlaceableLightRange = 24f, PlaceableLightAttenuation = 2.0f, PlaceableLightEnergy = 2.8f;
    public static float StaticLightRange = 24f, StaticLightAttenuation = 1.5f, StaticLightEnergy = 0.44f;

    /// <summary>
    /// Light-giving placeables (braziers, lamp posts, candelabra) have no light in their model: placeables.2da
    /// gives its colour (LightColor, a lightcolor.2da row) and where it sits (LightOffsetX/Y/Z). Toggled by
    /// SetPlaceableIllumination + RecomputeStaticLighting. Range and falloff are measured on the original
    /// (parity/visual, scene placelight).
    /// </summary>
    static void AddLight(StaticObjectView view, int appearance, bool isStatic, ClientContent content)
    {
        var table = content.Resources.TwoDA("placeables");
        if (table.IsEmpty(appearance, "LightColor")) return;
        int row = table.GetInt(appearance, "LightColor");
        var colors = content.Resources.TwoDA("lightcolor");
        var c = new Color(colors.GetFloat(row, "RED"), colors.GetFloat(row, "GREEN"), colors.GetFloat(row, "BLUE"));
        if (c.R + c.G + c.B < 0.01f) return;
        view.AddChild(new OmniLight3D
        {
            Name = "PlaceableLight",
            LightColor = c,
            LightEnergy = isStatic ? StaticLightEnergy : PlaceableLightEnergy,
            OmniRange = isStatic ? StaticLightRange : PlaceableLightRange,
            OmniAttenuation = isStatic ? StaticLightAttenuation : PlaceableLightAttenuation,
            ShadowEnabled = false,
            // The view lives in area (Z-up) space, turned with the placeable.
            Position = new Vector3(table.GetFloat(appearance, "LightOffsetX"), table.GetFloat(appearance, "LightOffsetY"), table.GetFloat(appearance, "LightOffsetZ")),
        });
    }

    /// <summary>
    /// Placeables and doors use bearing directly (no +Y model convention). Items on the ground face +Y as creatures
    /// do: a longsword made at facing 0 points its blade east, at 90 north (parity/visual, scene grounditems).
    /// </summary>
    protected override float FacingToModel(float facing) => Kind == ObjectKind.Item ? facing - MathF.PI / 2f : facing;

    public override void ApplyUpdate(UpdateMessage u)
    {
        var before = Flags;
        base.ApplyUpdate(u);
        UpdateDoorTransition();
        if (_player != null && (before & (ObjectFlags.Open | ObjectFlags.Activated | ObjectFlags.Dead)) != (Flags & (ObjectFlags.Open | ObjectFlags.Activated | ObjectFlags.Dead)))
            SetState(Flags, animate: true);
    }

    public override void PlayAnimation(PlayAnimationMessage m)
    {
        if (_player == null) return;
        bool door = Kind == ObjectKind.Door;
        string state = m.Animation, transition = "";
        switch (state)
        {
            case "open":
                state = Has("open") ? "open" : "opened1";
                transition = Has("close2open") ? "close2open" : "opening1";
                break;
            case "opened1": transition = "opening1"; break;
            case "opened2": transition = "opening2"; break;
            case "close":
                state = door ? "closed" : Has("close") ? "close" : "closed";
                transition = door ? _state == "opened2" ? "closing2" : "closing1"
                    : Has("open2close") ? "open2close" : "closing1";
                break;
            case "on": transition = "off2on"; break;
            case "off": transition = "on2off"; break;
            case "die": state = "dead"; transition = "die"; break;
        }
        if (!Has(state) && !Has(transition)) return;
        PlayState(state, transition, true, m.Speed);
    }

    bool Has(string clip) => _player!.HasAnimation(clip);

    void SetState(ObjectFlags flags, bool animate)
    {
        bool door = Kind == ObjectKind.Door;
        string state, transition = "";
        if ((flags & ObjectFlags.Dead) != 0) { state = "dead"; transition = "die"; }
        else if ((flags & ObjectFlags.Open) != 0)
        {
            state = door ? (flags & ObjectFlags.OpenSide2) != 0 ? "opened2" : "opened1" : Has("open") ? "open" : "opened1";
            transition = door ? state == "opened2" ? "opening2" : "opening1" : Has("close2open") ? "close2open" : "opening1";
        }
        else if ((flags & ObjectFlags.Activated) != 0) { state = "on"; transition = "off2on"; }
        else if (_state is "on" or "off") { state = "off"; transition = "on2off"; }
        else
        {
            state = door ? "closed" : _state == "close" ? "close" : Has("default") ? "default" : Has("close") ? "close" : "closed";
            transition = _state is "open" or "opened1" or "opened2"
                ? (door ? _state == "opened2" ? "closing2" : "closing1" : Has("open2close") ? "open2close" : "closing1") : "";
        }
        PlayState(state, transition, animate);
    }

    void PlayState(string state, string transition, bool animate, float speed = 1)
    {
        if (!Has(state)) state = Has("default") ? "default" : "";
        if (state == _state) return;
        _state = state;
        if (state.Length == 0) return;
        _player!.ClearQueue();
        _player.SpeedScale = speed > 0 ? speed : 1;
        var clip = _player!.GetAnimation(state);
        clip.LoopMode = clip.Length > 0.1f ? Godot.Animation.LoopModeEnum.Linear : Godot.Animation.LoopModeEnum.None;
        if (animate && transition.Length > 0 && Has(transition))
        {
            _player.Play(transition);
            _player.Queue(state);
        }
        else _player.Play(state);
    }

    /// <summary>The item's parts and colours (the server sends them as its one equipment entry).</summary>
    static ItemVisual GroundVisual(SpawnMessage spawn) =>
        spawn.Appearance.Equipment.FirstOrDefault() ?? new ItemVisual { BaseItem = spawn.Appearance.Type };
}

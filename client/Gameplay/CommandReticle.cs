using System.Text.Json;
using OpenWinter.Client.Rendering;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Gameplay;

/// <summary>The original's animated ground markers, loaded from the player's data in both UI modes.</summary>
public partial class CommandReticle : Node3D
{
    public ClientSession Session = null!;
    readonly Dictionary<string, (Node3D Root, AnimationPlayer Player)> _models = new();
    Node3D? _shown;
    uint _target, _owner;
    public bool IsShowing => _shown?.Visible == true;

    public override void _Ready()
    {
        Session.NotifyReceived += OnNotify;
        Session.AreaLoading += OnAreaLoading;
        Session.AreaLoaded += Clear;
    }

    public override void _ExitTree()
    {
        Session.NotifyReceived -= OnNotify;
        Session.AreaLoading -= OnAreaLoading;
        Session.AreaLoaded -= Clear;
    }

    void OnAreaLoading(LoadAreaMessage _) => Clear();

    void Clear()
    {
        if (_shown != null) _shown.Visible = false;
        _shown = null;
        _target = GameObjectInvalid;
    }

    const uint GameObjectInvalid = 0x7f000000;

    void OnNotify(NotifyMessage message)
    {
        if (message.Kind != "command-reticle" || message.Subject != Session.ControlledId) return;
        Clear();
        using var doc = JsonDocument.Parse(message.Json);
        var data = doc.RootElement;
        string? kind = data.GetProperty("kind").GetString();
        if (kind is not ("move" or "attack")) return;
        _owner = message.Subject;
        _target = kind == "attack" ? data.GetProperty("target").GetUInt32() : GameObjectInvalid;
        string size = Session.Objects.GetValueOrDefault(_target) is CreatureView creature ? creature.SizeLetter.ToLowerInvariant() : "m";
        var model = Model(kind == "attack" ? "gui_hostile" + size : "gui_walkto");
        if (model == null) return;
        Position = new Vector3(data.GetProperty("x").GetSingle(), data.GetProperty("y").GetSingle(), data.GetProperty("z").GetSingle());
        _shown = model.Value.Root;
        _shown.Visible = true;
        model.Value.Player.Stop();
        model.Value.Player.Play("default");
        model.Value.Player.Advance(0);
    }

    (Node3D Root, AnimationPlayer Player)? Model(string name)
    {
        if (_models.TryGetValue(name, out var cached)) return cached;
        var content = Session.Content;
        var data = content?.Models.Get(name);
        if (content == null || data == null || !data.Animations.TryGetValue("default", out var animation)) return null;
        var root = new Node3D { Name = name, Visible = false };
        var skeleton = ModelBuilder.BuildSkinned(data, content.Materials, part =>
        {
            var material = (StandardMaterial3D)content.Materials.Get(part.Mesh!, MaterialKind.World, moduleShaders: false)!.Duplicate();
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            material.DisableReceiveShadows = true;
            return material;
        });
        root.AddChild(skeleton);
        foreach (var mesh in skeleton.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var clip = ModelBuilder.BuildAnimation(animation, skeleton, "Skeleton3D", 1);
        clip.LoopMode = Godot.Animation.LoopModeEnum.Linear;
        var library = new AnimationLibrary();
        library.AddAnimation("default", clip);
        var player = new AnimationPlayer();
        root.AddChild(player);
        player.AddAnimationLibrary("", library);
        AddChild(root);
        _models[name] = (root, player);
        return (root, player);
    }

    public override void _Process(double delta)
    {
        if (_shown == null) return;
        if (Session.ControlledId != _owner || Session.Controlled == null || (Session.Controlled.Flags & ObjectFlags.Dead) != 0)
        {
            Clear();
            return;
        }
        if (_target == GameObjectInvalid) return;
        if (!Session.Objects.TryGetValue(_target, out var target) || !target.Visible || (target.Flags & ObjectFlags.Dead) != 0)
        {
            Clear();
            return;
        }
        Position = target.Position;
    }
}

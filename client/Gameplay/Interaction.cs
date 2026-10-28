using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Gameplay;

/// <summary>
/// Mouse interaction: hover shows an object's name; left-click talks to
/// creatures, uses placeables and doors, or walks to the clicked ground;
/// right-click on an object or yourself opens the radial menu (<see cref="OpenRadial"/>).
/// </summary>
public partial class Interaction : Node
{
    public ClientSession Session = null!;
    public OrbitCamera Camera = null!;
    public UI.HudParts.ObjectLabel HoverLabel = null!;
    public bool ShowPlayerNames;
    /// <summary>Viewport-local pointer for visual captures, without moving the operating system cursor.</summary>
    public Vector2? PointerOverride;
    public ObjectView? Hovered { get; private set; }
    public bool HoveredDoorTransition => Hovered is { Kind: ObjectKind.Door } door
        && (door.Flags & (ObjectFlags.Open | ObjectFlags.Transition)) == (ObjectFlags.Open | ObjectFlags.Transition);
    public bool GroundWalkable { get; private set; }
    public Vector3? GroundPosition { get; private set; }
    /// <summary>
    /// Cutscene mode: the mouse picks and highlights nothing (as in the original), so a pointer resting over the
    /// window never lights a creature up in a cinematic.
    /// </summary>
    public bool Suspended;
    /// <summary>Objects lit up while the highlight key (Tab) is held, as in the original; empty otherwise.</summary>
    public IReadOnlyList<ObjectView> HighlightedAll => _all;
    /// <summary>Opens the examine window for an object.</summary>
    public Action<uint>? Examine;
    /// <summary>When inventory is open, carries ground item art until an inventory destination is chosen.</summary>
    public Func<uint, bool>? CarryGroundItem;
    /// <summary>Set while a script's targeting mode is active: the next click picks (object, ground point); right-click cancels with nothing.</summary>
    public Action<ObjectView?, System.Numerics.Vector3?>? Targeting
    {
        get => _targeting;
        set { _targeting = value; ItemTargeting = null; }
    }
    Action<ObjectView?, System.Numerics.Vector3?>? _targeting;
    /// <summary>
    /// Set after <see cref="Targeting"/> by a spell or ability that can target items: a click on an item in the
    /// inventory picks it (casting at a blank scroll, potion or wand is how Brew Potion, Scribe Scroll and Craft Wand work).
    /// </summary>
    public Action<uint>? ItemTargeting;
    /// <summary>An inventory item was clicked: resolves a pending item target; false when none is pending.</summary>
    public bool TargetItem(uint itemId)
    {
        if (_targeting == null || ItemTargeting is not { } pick) return false;
        Targeting = null;
        pick(itemId);
        return true;
    }
    /// <summary>Portraits resolve the same pending ability target as a world click.</summary>
    public bool TargetPortrait(uint id)
    {
        if (Targeting == null || !Session.Objects.TryGetValue(id, out var target)) return false;
        Targeting(target, null);
        return true;
    }
    /// <summary>
    /// Adds entries to an object's context menu (DM tools): fill the given menu; the radial menu shows them as a
    /// sub-ring and sends picks (their ids) to <see cref="MenuChosen"/>.
    /// </summary>
    public Action<PopupMenu, ObjectView>? ExtendMenu;
    public Action<long, ObjectView>? MenuChosen;
    /// <summary>Opens the radial (context) menu for an object at a screen point; set by the radial menu.</summary>
    public Action<ObjectView, Vector2>? OpenRadial;
    /// <summary>Closes the radial menu if it is open.</summary>
    public Action? CloseRadial;
    /// <summary>Whether the radial menu is showing (the hover name is hidden meanwhile).</summary>
    public Func<bool>? RadialOpen;
    /// <summary>The shown trap trigger under an area point (set by the world effects), or null.</summary>
    public Func<System.Numerics.Vector3, Views.WorldFx.TrapFx.Shown?>? TrapUnder;
    /// <summary>Opens the trap commands for a trap trigger at a screen point (set by the radial menu).</summary>
    public Action<Views.WorldFx.TrapFx.Shown, Vector2>? TrapMenu;
    /// <summary>Right-click on open ground (screen position, area point); when set, used instead of nothing.</summary>
    public Action<Vector2, System.Numerics.Vector3>? GroundMenu;
    /// <summary>Opens a radial menu of the caller's own entries (root: title and centre icon; screen point; entries).</summary>
    public Action<UI.RadialEntry, Vector2, Func<List<UI.RadialEntry>>>? OpenMenu;
    /// <summary>Opens a quickbar slot's radial (screen point, what the slot holds, how to change it; set by the radial menu).</summary>
    public Action<Vector2, UI.BarSlot?, Action<UI.BarSlot?>>? QuickbarMenu;
    Vector2 _pressAt;
    bool _pressed;
    readonly List<ObjectView> _all = new();
    /// <summary>The tooltip key: hold it, or double-tap it to keep highlights and creature names up until the next press.</summary>
    readonly TapLatch _tooltips = new();
    readonly List<UI.HudParts.ObjectLabel> _allLabels = new();
    readonly Dictionary<uint, (string Name, string Player, int Damage)> _labels = new();
    readonly Dictionary<ulong, (double Next, Aabb Box, bool Ok)> _bounds = new();
    double _clock;
    /// <summary>How creatures react to this player (server "reactions" notify): 1 party/faction ally, 2 hostile; absent is neutral.</summary>
    readonly Dictionary<uint, int> _reactions = new();
    public const int Friendly = 1, Hostile = 2;

    public override void _Ready() => Session.NotifyReceived += OnNotify;
    public override void _ExitTree() => Session.NotifyReceived -= OnNotify;

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind == "creature-labels")
        {
            using var labels = System.Text.Json.JsonDocument.Parse(m.Json);
            if (labels.RootElement.TryGetProperty("reset", out _)) _labels.Clear();
            foreach (var p in labels.RootElement.EnumerateObject())
                if (uint.TryParse(p.Name, out var id))
                {
                    if (p.Value.ValueKind == System.Text.Json.JsonValueKind.Null) { _labels.Remove(id); continue; }
                    string name = p.Value.GetProperty("name").GetString() ?? "";
                    _labels[id] = (name, p.Value.GetProperty("playerName").GetString() ?? "", p.Value.GetProperty("damageLevel").GetInt32());
                    if (Session.Objects.TryGetValue(id, out var view)) view.DisplayName = UI.Kit.NwnText.Strip(name);
                }
            return;
        }
        if (m.Kind != "reactions") return;
        using var doc = System.Text.Json.JsonDocument.Parse(m.Json);
        if (doc.RootElement.TryGetProperty("reset", out _)) _reactions.Clear();
        foreach (var p in doc.RootElement.EnumerateObject())
            if (uint.TryParse(p.Name, out var id))
            {
                if (p.Value.GetInt32() == 0) _reactions.Remove(id);
                else _reactions[id] = p.Value.GetInt32();
            }
    }

    /// <summary>The HP bar (damagelevels.2da row) the server last sent for a creature's label; -1 when none.</summary>
    public int LabelDamage(uint id) => _labels.TryGetValue(id, out var label) ? label.Damage : -1;

    /// <summary>How a creature reacts to the player: 0 neutral (and every non-creature), 1 party/faction ally, 2 hostile.</summary>
    public int Reaction(ObjectView v) => _reactions.GetValueOrDefault(v.ObjectId);

    void ConfigureLabel(UI.HudParts.ObjectLabel label, ObjectView v)
    {
        if (Session.Content is not { } content) return;
        bool creature = v.Kind == ObjectKind.Creature;
        var info = _labels.GetValueOrDefault(v.ObjectId, (v.DisplayName, "", -1));
        label.Set(content, info.Item1, ShowPlayerNames ? info.Item2 : "", creature ? info.Item3 : -1, v.Portrait,
            creature ? v.ObjectId == Session.ControlledId ? Friendly : Reaction(v) : -1);
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        var mouse = PointerOverride ?? GetViewport().GetMousePosition();
        var oldHover = Hovered;
        Hovered = Suspended ? null : Pick(mouse, hover: true);
        if (oldHover != Hovered && oldHover is StaticObjectView oldDoor && IsInstanceValid(oldDoor)) oldDoor.SetDoorHovered(false);
        if (oldHover != Hovered && oldHover is TriggerView oldTrigger && IsInstanceValid(oldTrigger)) oldTrigger.SetHovered(false);
        if (Hovered is TriggerView trigger) trigger.SetHovered(true);
        if (Hovered is StaticObjectView { Kind: ObjectKind.Door } door)
            door.SetDoorHovered(HoveredDoorTransition);
        UpdateGround(mouse);
        UpdateMouseDriving();
        bool highlightAll = UpdateHighlightAll();
        if (Settings.Current.Interface.TextBubbleMode != 0 && Hovered != null && Hovered.Kind is not (ObjectKind.Door or ObjectKind.Trigger)
            && (!highlightAll || Hovered.Kind == ObjectKind.Creature) && Hovered.DisplayName.Length > 0 && RadialOpen?.Invoke() != true)
        {
            HoverLabel.Visible = true;
            ConfigureLabel(HoverLabel, Hovered);
            HoverLabel.Position = LabelAnchor(Hovered) - HoverLabel.AnchorOffset;
        }
        else HoverLabel.Visible = false;
    }

    /// <summary>Screen point just above an object's model (where its name goes).</summary>
    Vector2 LabelAnchor(ObjectView v)
        => Camera.UnprojectPosition(HeadTop(v));

    /// <summary>Overhead text follows the posed head instead of equipment's bind-pose bounds.</summary>
    public Vector3 HeadTop(ObjectView v)
    {
        if (v is CreatureView { Skeleton: { } skeleton, CurrentAppearance: { } a } && Session.Content is { } content)
        {
            // Head height follows the posed body. Bind-pose mesh bounds include equipment and can
            // sit well above a part-based PC. Keep the horizontal anchor on the creature's centre.
            string head = content.Resources.TwoDA("appearance").Get(a.Type, "HEAD_NAME");
            int bone = skeleton.FindBone(head);
            if (bone < 0) bone = skeleton.FindBone("head_g");
            // Some stock rows have stale HEAD_NAMEs (Rat names Rat_head, its model has Badger_head).
            if (bone < 0) bone = skeleton.FindBone("head");
            if (bone >= 0)
            {
                var point = v.GlobalPosition;
                point.Y = (skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone).Origin).Y + 0.5f * v.Scale.X;
                return point;
            }
        }
        return LabelTop(v);
    }

    /// <summary>The world point just above an object's model, where names and spoken text go.</summary>
    public Vector3 LabelTop(ObjectView v) => Bounds(v) is { } b
        ? v.GlobalTransform * new Vector3(b.GetCenter().X, b.GetCenter().Y, b.End.Z) + Vector3.Up * 0.25f
        : v.GlobalPosition + Vector3.Up * 2.1f;

    /// <summary>
    /// Holding the tooltip key (Tab) lights up every usable object nearby. Only creatures get names;
    /// placeables, doors and ground items only glow. A quick double tap keeps the highlights up.
    /// </summary>
    /// <summary>Holds the highlight key down from a script (the visual harness's VISUAL|key|9 lines).</summary>
    public static bool ForceHighlightAll;

    bool UpdateHighlightAll()
    {
        _all.Clear();
        var focus = GetViewport().GuiGetFocusOwner();
        bool held = _tooltips.Active(InputMap.HasAction(Preferences.ActionCatalog.HighlightObjects) && Input.IsActionPressed(Preferences.ActionCatalog.HighlightObjects))
            && focus is not (LineEdit or TextEdit) && RadialOpen?.Invoke() != true || ForceHighlightAll;
        if (held && !Suspended)
        {
            var rect = GetViewport().GetVisibleRect();
            var centre = Session.Objects.GetValueOrDefault(Session.ControlledId) is { } me ? me.GlobalPosition : Camera.GlobalPosition;
            foreach (var v in Session.Objects.Values)
            {
                // Transitions light up only under the pointer (visual scene triggerhover).
                if (!Highlightable(v) || v.Kind == ObjectKind.Trigger) continue;
                if (v.GlobalPosition.DistanceTo(centre) > 40 || Camera.IsPositionBehind(v.GlobalPosition)) continue;
                if (!rect.HasPoint(Camera.UnprojectPosition(v.GlobalPosition))) continue;
                _all.Add(v);
            }
            // Nearest first, so their names win the space when labels would overlap.
            var cam = Camera.GlobalPosition;
            _all.Sort((a, b) => a.GlobalPosition.DistanceSquaredTo(cam).CompareTo(b.GlobalPosition.DistanceSquaredTo(cam)));
        }
        var placed = new List<Rect2>();
        int used = 0;
        foreach (var v in _all)
        {
            if (Settings.Current.Interface.TextBubbleMode == 0 || v.Kind != ObjectKind.Creature || v.DisplayName.Length == 0 || v == Hovered) continue;
            if (used == _allLabels.Count)
            {
                var copy = new UI.HudParts.ObjectLabel();
                HoverLabel.GetParent().AddChild(copy);
                HoverLabel.GetParent().MoveChild(copy, HoverLabel.GetIndex());
                _allLabels.Add(copy);
            }
            var label = _allLabels[used++];
            ConfigureLabel(label, v);
            label.ResetSize();
            var size = label.Size;
            var home = new Rect2(LabelAnchor(v) - label.AnchorOffset, size);
            // A label that would cover a nearer one's moves to the closest free row above or below.
            var r = home;
            for (int step = 1; step <= 8 && placed.Any(p => p.Grow(2).Intersects(r)); step++)
                r = home with { Position = home.Position + new Vector2(0, (step % 2 == 1 ? -1 : 1) * ((step + 1) / 2) * size.Y) };
            placed.Add(r);
            label.Position = r.Position;
            label.Visible = true;
        }
        for (int i = used; i < _allLabels.Count; i++) _allLabels[i].Visible = false;
        return held;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } && Preferences.InputBindings.Pressed(e, Preferences.ActionCatalog.HighlightObjects))
        {
            _tooltips.Press(Time.GetTicksMsec() / 1000.0);
            return;
        }
        // Open on RMB-down so a held press can select a radial command on release.
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right } rb)
        {
            if (!rb.Pressed || Suspended || PlayerInput.Blocked) return;
            if (Targeting != null) Targeting(null, null);
            else if (Pick(rb.Position) is { } picked) ShowMenu(picked, rb.Position);
            // A detected trap trigger on the ground opens the four trap commands straight away (traplook g_radial_root).
            else if (TrapMenu != null && GroundPoint(rb.Position) is { } trapPoint && TrapUnder?.Invoke(Rendering.Convert.N(trapPoint)) is { } trap)
                TrapMenu(trap, rb.Position);
            else if (GroundMenu != null && GroundPoint(rb.Position) is { } ground)
                GroundMenu(rb.Position, Rendering.Convert.N(ground));
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mb) return;
        if (mb.Pressed)
        {
            _pressed = !Suspended && !PlayerInput.Blocked; _pressAt = mb.Position; _leftDownAt = _clock; _leftReleased = false;
            var picked = Pick(mb.Position);
            _leftGround = picked == null || picked.ObjectId == Session.ControlledId ? GroundPoint(mb.Position) : null;
            return;
        }
        // Short clicks retain the press position even if the pointer moved before release.
        if (!_pressed) return;
        _pressed = false;
        var at = _pressAt;
        var target = Pick(at);
        if (Targeting != null)
        {
            var ground = GroundPoint(at);
            Targeting(target, ground is { } g ? Rendering.Convert.N(g) : null);
            return;
        }
        if (target != null && target.ObjectId != Session.ControlledId)
        {
            if (target.Kind == ObjectKind.Item && CarryGroundItem?.Invoke(target.ObjectId) == true) return;
            Session.Send(new ActionRequestMessage
            {
                Action = target.Kind == ObjectKind.Creature ? ActionKind.Talk : ActionKind.Use,
                Target = target.ObjectId,
            }.Write());
            return;
        }
        if ((_leftGround ?? GroundPoint(at, out _)) is { } p)
        {
            // A left click on a detected trap trigger does nothing (traplook e_after_click: the player stayed put).
            if (TrapUnder?.Invoke(Rendering.Convert.N(p)) != null) return;
            Session.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = Rendering.Convert.N(p), Run = !PlayerInput.WalkAtPointer(mb) }.Write());
        }
    }

    /// <summary>Opens the radial menu for an object (or the player) at a screen point.</summary>
    public void ShowMenu(ObjectView target, Vector2 at) => OpenRadial?.Invoke(target, at);

    static bool Pickable(ObjectView v) => Shown(v) && PickRules.Selectable(v.Kind, v.Flags);

    /// <summary>Lit and named under the pointer and the highlight key: anything a click can pick, and every corpse (<see cref="PickRules"/>).</summary>
    static bool Highlightable(ObjectView v) => Shown(v) && PickRules.Highlightable(v.Kind, v.Flags);

    static bool Shown(ObjectView v) => IsInstanceValid(v) && v.Visible && v is not StaticObjectView { CameraFaded: true };

    /// <summary>The hovered object can be clicked (a corpse that isn't selectable when dead lights up but can't).</summary>
    public bool HoveredSelectable => Hovered is { } h && (h.ObjectId == Session.ControlledId || Pickable(h));

    /// <summary>
    /// The object under the cursor. Like the original, the whole model counts: the mouse ray is tested
    /// against each object's bounding box (padded, so thin and small things are easy to hit), the
    /// nearest hit wins, and an object inside another's box (an item on a table) beats the larger one.
    /// Objects without a model fall back to a small radius around their position on screen. Your own
    /// character is only picked when nothing else is under the cursor. <paramref name="hover"/> also takes
    /// corpses that can't be selected (the pointer lights them up; a click goes through them).
    /// </summary>
    ObjectView? Pick(Vector2 mouse, bool includeTransition = true, bool hover = false)
    {
        var camPos = Camera.GlobalPosition;
        var from = Camera.ProjectRayOrigin(mouse);
        var dir = Camera.ProjectRayNormal(mouse);
        var hits = new List<(ObjectView View, float T, float Volume)>();
        ObjectView? self = null, near = null;
        List<TriggerView>? triggers = null;
        float nearDist = 28f;
        foreach (var v in Session.Objects.Values)
        {
            bool isSelf = v.ObjectId == Session.ControlledId;
            if (isSelf ? !IsInstanceValid(v) || !v.Visible : hover ? !Highlightable(v) : !Pickable(v)) continue;
            if (v is TriggerView trigger)
            {
                (triggers ??= new()).Add(trigger);
                continue;
            }
            if (camPos.DistanceTo(v.GlobalPosition) > 70) continue;
            if (v is StaticObjectView { Kind: ObjectKind.Door } door)
            {
                if (door.PickDoor(from, dir, includeTransition) is { } hit) hits.Add((v, hit, 1f));
                continue;
            }
            if (Bounds(v) is { } box)
            {
                var inv = v.GlobalTransform.AffineInverse();
                if (RayHit(box, inv * from, inv.Basis * dir) is not { } local) continue;
                if (isSelf) { self = v; continue; }
                hits.Add((v, from.DistanceTo(v.GlobalTransform * local), box.Volume));
                continue;
            }
            var world = v.GlobalPosition + Vector3.Up * 0.6f;
            if (Camera.IsPositionBehind(world)) continue;
            float d = Camera.UnprojectPosition(world).DistanceTo(mouse);
            if (d >= nearDist) continue;
            if (isSelf) self ??= v;
            else { nearDist = d; near = v; }
        }
        if (hits.Count > 0)
        {
            float first = hits.Min(h => h.T);
            return hits.Where(h => h.T <= first + 1.5f).MinBy(h => h.Volume).View;
        }
        if (near != null) return near;
        // A transition trigger is picked by the ground under the pointer.
        if (triggers != null && GroundPoint(mouse, out _) is { } ground && triggers.FirstOrDefault(t => t.Contains(ground)) is { } under)
            return under;
        return self;
    }

    /// <summary>
    /// An object's pick box in its own (Aurora, Z-up) space: its visible meshes' bounds, padded and grown
    /// to a minimum size by kind. Cached and refreshed about once a second (models load and change).
    /// </summary>
    Aabb? Bounds(ObjectView v)
    {
        ulong key = v.GetInstanceId();
        if (_bounds.TryGetValue(key, out var c) && c.Next > _clock) return c.Ok ? c.Box : null;
        var inv = v.GlobalTransform.AffineInverse();
        Aabb box = default;
        bool any = false;
        foreach (var mi in v.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (mi.Mesh == null || !mi.IsVisibleInTree() || mi.CastShadow == GeometryInstance3D.ShadowCastingSetting.ShadowsOnly) continue;
            if (UnderEffect(mi, v)) continue;
            var b = (inv * mi.GlobalTransform) * mi.GetAabb();
            box = any ? box.Merge(b) : b;
            any = true;
        }
        // A model the size of a building is not something to point at (a stray ground plane): use the fallback.
        if (any && (box.Size.X > 30 || box.Size.Y > 30 || box.Size.Z > 30)) any = false;
        if (any)
        {
            var (min, pad) = v.Kind switch
            {
                ObjectKind.Creature => (new Vector3(0.7f, 0.7f, 1.2f), 0.15f),
                ObjectKind.Item => (new Vector3(0.5f, 0.5f, 0.4f), 0.1f),
                _ => (new Vector3(0.4f, 0.4f, 0.4f), 0.1f),
            };
            box = box.Grow(pad);
            var centre = box.GetCenter();
            var size = box.Size.Max(min);
            box = new Aabb(new Vector3(centre.X - size.X / 2, centre.Y - size.Y / 2, Math.Min(box.Position.Z, centre.Z - size.Z / 2)), size);
        }
        // Spread refreshes out so they don't all land on one frame.
        if (_bounds.Count > 4000) _bounds.Clear();
        _bounds[key] = (_clock + 0.8 + (key % 7) * 0.1, box, any);
        return any ? box : null;
    }

    /// <summary>Where a ray first enters a box (slab test), or null when it misses or the box is behind.</summary>
    static Vector3? RayHit(Aabb box, Vector3 from, Vector3 dir)
    {
        float near = 0, far = float.MaxValue;
        for (int axis = 0; axis < 3; axis++)
        {
            float o = from[axis], d = dir[axis], lo = box.Position[axis], hi = box.End[axis];
            if (Math.Abs(d) < 1e-8f) { if (o < lo || o > hi) return null; continue; }
            float t1 = (lo - o) / d, t2 = (hi - o) / d;
            if (t1 > t2) (t1, t2) = (t2, t1);
            near = Math.Max(near, t1);
            far = Math.Min(far, t2);
            if (near > far) return null;
        }
        return from + dir * near;
    }

    /// <summary>Attached effect models (fx_*) don't count toward an object's shape.</summary>
    static bool UnderEffect(Node n, Node root)
    {
        for (var p = n; p != null && p != root; p = p.GetParent())
            if (p.Name.ToString().StartsWith("fx_", StringComparison.Ordinal)) return true;
        return false;
    }

    // The no-walk cursor's ground probe, skipped while nothing changed or the cursor doesn't need it.
    (Vector2 Mouse, Transform3D Camera, AreaView? Area, int Tris) _groundKey;

    void UpdateGround(Vector2 mouse)
    {
        bool needed = !Suspended && Targeting == null && (_pressed || Hovered == null || Hovered.ObjectId == Session.ControlledId);
        if (!needed) { GroundWalkable = true; GroundPosition = null; _groundKey = default; return; }
        var key = (mouse, Camera.GlobalTransform, Session.Area, Session.Area?.WalkTriangles.Count ?? 0);
        if (key == _groundKey) return;
        _groundKey = key;
        GroundPosition = GroundPoint(mouse, out bool walkable);
        GroundWalkable = walkable;
    }

    /// <summary>Where the mouse ray hits walkable ground, in area (Z-up) coordinates; null off the walkmesh.</summary>
    /// <summary>Pick walkable area-space terrain for inventory placement using the same ray as movement.</summary>
    public System.Numerics.Vector3? PickGround(Vector2 mouse) => GroundPoint(mouse) is { } p ? Rendering.Convert.N(p) : null;

    Vector3? GroundPoint(Vector2 mouse) => GroundPoint(mouse, out bool walkable) is { } p && walkable ? p : null;

    /// <summary>Pick the requested point without snapping invalid orders onto valid ground.</summary>
    Vector3? GroundPoint(Vector2 mouse, out bool walkable)
    {
        walkable = false;
        if (Session.Area == null) return null;
        var toArea = Session.Space.GlobalTransform.AffineInverse();
        var from = toArea * Camera.ProjectRayOrigin(mouse);
        var dir = (toArea.Basis * Camera.ProjectRayNormal(mouse)).Normalized();
        var tris = Session.Area.WalkTriangles;
        float bestT = float.MaxValue;
        // AreaView excludes wall and cap faces: pick walkable ground behind rendered walls.
        for (int i = 0; i + 2 < tris.Count; i += 3)
            if (RayTriangle(from, dir, tris[i], tris[i + 1], tris[i + 2]) is { } t && t < bestT) { bestT = t; walkable = true; }
        if (bestT < float.MaxValue) return from + dir * bestT;
        // Outside the tile mesh, still let the server attempt a closest-reachable move.
        float z = Session.Controlled?.Position.Z ?? 0;
        float planeT = Math.Abs(dir.Z) > 1e-6f ? (z - from.Z) / dir.Z : -1;
        return planeT >= 0 && planeT < 1000 ? from + dir * planeT : null;
    }

    /// <summary>Möller–Trumbore, double-sided, in managed code: this runs over every walk triangle each probe.</summary>
    static float? RayTriangle(Vector3 from, Vector3 dir, Vector3 a, Vector3 b, Vector3 c)
    {
        var e1 = b - a;
        var e2 = c - a;
        var p = dir.Cross(e2);
        float det = e1.Dot(p);
        if (Math.Abs(det) < 1e-8f) return null;
        float inv = 1f / det;
        var s = from - a;
        float u = s.Dot(p) * inv;
        if (u < 0 || u > 1) return null;
        var q = s.Cross(e1);
        float v = dir.Dot(q) * inv;
        if (v < 0 || u + v > 1) return null;
        float t = e2.Dot(q) * inv;
        return t > 0 ? t : null;
    }
}

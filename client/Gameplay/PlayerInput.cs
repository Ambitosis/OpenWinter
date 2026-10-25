using OpenWinter.Client.Preferences;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Gameplay;

/// <summary>
/// Turns key presses (InputMap actions from <see cref="ActionCatalog"/>, rebindable in Options) into server
/// requests. The server decides what actually happens.
/// </summary>
public partial class PlayerInput : Node
{
    public ClientSession Session = null!;
    public OrbitCamera Camera = null!;
    /// <summary>The window keys (I, C, J, M, B): toggles set by Main for whichever interface is in use.</summary>
    public Action? ToggleInventory, ToggleCharacter, ToggleJournal, ToggleMap, ToggleSpellbook;
    /// <summary>Current stealth / detect state of the player's creature (from "action-mode" notifies).</summary>
    public bool Stealth, Detect;
    /// <summary>When set, replaces keyboard movement (scripted demos): (x right, y forward), walk.</summary>
    public (Vector2 Dir, bool Walk)? Scripted;
    /// <summary>Esc with no window to close: open the game menu.</summary>
    public Action? OpenGameMenu;
    /// <summary>The Options key (O).</summary>
    public Action? OpenOptions;
    /// <summary>The player list (P), where players like or dislike each other (PvP).</summary>
    public Action? OpenPlayerList;
    public Action? OpenQuickChat;
    public Func<bool>? KeyboardMenuOpen;
    /// <summary>Hide / show the whole interface (H).</summary>
    public Action? ToggleGui;
    /// <summary>Previous / next page of the inventory ([ and ]): -1 or +1.</summary>
    public Action<int>? CycleInventory;
    /// <summary>Feedback line in the chat window (camera mode changes, keys that do nothing yet).</summary>
    public Action<string>? Feedback;
    /// <summary>True in single player, where the pause key pauses.</summary>
    public bool SinglePlayer;
    public UI.HudParts.HudState? HudState;
    public Interaction? Interaction;
    /// <summary>Movement keys this physics frame (x right, y forward); the chase cameras read it.</summary>
    public static Vector2 KeyInput { get; private set; }
    readonly QuickGroups _groups = new();
    /// <summary>True while a full-screen menu (game menu, options, loading) has the keyboard: no movement or camera keys.</summary>
    public static bool Blocked { get; set; }

    static readonly string[] Gestures = { "greeting", "salute", "tlklaugh", "drink", "taunt" };
    static readonly string[] Attacks = { "nwslashl", "nwkickr", "1hslashr" };
    int _gesture, _attack;
    Vector3 _lastDir;
    bool _lastWalk;
    double _resend;
    float _lastTurn;
    bool _lastMouse, _lastActive;

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not (InputEventKey { Pressed: true, Echo: false } or InputEventMouseButton { Pressed: true })) return;
        bool P(string action) => InputBindings.Pressed(e, action);
        if (P(ActionCatalog.GameMenu))
        {
            // Esc closes the most recent window; with none open it brings up the game menu.
            if (!UI.WindowStack.CloseTop()) OpenGameMenu?.Invoke();
        }
        else if (Blocked) return;
        else if (P(ActionCatalog.TurnLeft) || P(ActionCatalog.TurnRight)
            || P(ActionCatalog.MoveForward) || P(ActionCatalog.MoveBack) || P(ActionCatalog.MoveLeft) || P(ActionCatalog.MoveRight))
        {
            // Send the press even if a tap is released before the next physics frame.
            float turn = (Input.IsActionPressed(ActionCatalog.TurnLeft) ? 1 : 0) - (Input.IsActionPressed(ActionCatalog.TurnRight) ? 1 : 0);
            var axes = new Vector3(
                (Input.IsActionPressed(ActionCatalog.MoveRight) ? 1 : 0) - (Input.IsActionPressed(ActionCatalog.MoveLeft) ? 1 : 0),
                (Input.IsActionPressed(ActionCatalog.MoveForward) ? 1 : 0) - (Input.IsActionPressed(ActionCatalog.MoveBack) ? 1 : 0), 0);
            _lastActive = turn != 0 || axes != Vector3.Zero;
            Session.Send(new DriveInputMessage { Direction = Rendering.Convert.N(axes), Turn = turn, Walk = WalkHeld, Active = _lastActive }.Write());
            _resend = 0;
        }
        else if (P(ActionCatalog.ToggleStance)) ToggleStance();
        else if (P(ActionCatalog.Character)) ToggleCharacter?.Invoke();
        else if (P(ActionCatalog.Journal)) ToggleJournal?.Invoke();
        else if (P(ActionCatalog.Map)) ToggleMap?.Invoke();
        else if (P(ActionCatalog.Spellbook)) ToggleSpellbook?.Invoke();
        else if (P(ActionCatalog.Rest)) Session.Request("rest");
        else if (P(ActionCatalog.Stealth)) Session.Request("action-mode", new { mode = 1, on = !Stealth });
        else if (P(ActionCatalog.Detect)) Session.Request("action-mode", new { mode = 0, on = !Detect });
        else if (P(ActionCatalog.Gesture)) Gesture();
        else if (P(ActionCatalog.Attack)) Attack();
        else if (P(ActionCatalog.Inventory)) ToggleInventory?.Invoke();
        else if (P(ActionCatalog.Options)) OpenOptions?.Invoke();
        else if (P(ActionCatalog.PlayerList)) OpenPlayerList?.Invoke();
        else if (P(ActionCatalog.ToggleGui)) ToggleGui?.Invoke();
        else if (P(ActionCatalog.InventoryPrevious)) CycleInventory?.Invoke(-1);
        else if (P(ActionCatalog.InventoryNext)) CycleInventory?.Invoke(1);
        else if (P(ActionCatalog.CameraMode)) NextCameraMode();
        else if (P(ActionCatalog.Pause)) Pause();
        else if (P(ActionCatalog.RadialSelf)) RadialOnSelf();
        else if (P(ActionCatalog.QuickChat)) OpenQuickChat?.Invoke();
        // TODO(render): the key does not toggle keyholing yet.
        else if (P(ActionCatalog.ToggleKeyhole)) Feedback?.Invoke("Keyhole dissolve is not available yet.");
        else if (QuickGroupKey(e)) { }
        else return;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Walking rather than running: the walk key, inverted by Options, Gameplay, Always walk.</summary>
    public static bool WalkHeld => Input.IsActionPressed(ActionCatalog.Walk) != Settings.Current.Gameplay.AlwaysWalk;

    /// <summary>Each ground click chooses its own gait from the current pointer modifiers.</summary>
    public static bool WalkAtPointer(InputEventWithModifiers pointer) =>
        InputBindings.HeldAtPointer(ActionCatalog.Walk, pointer) != Settings.Current.Gameplay.AlwaysWalk;

    public void ToggleStance() => Session.Send(new ActionRequestMessage { Action = ActionKind.ToggleStance }.Write());
    public void Gesture() => Session.Send(new ActionRequestMessage { Action = ActionKind.Emote, Argument = Gestures[_gesture++ % Gestures.Length] }.Write());
    /// <summary>The pause key: the server pauses the world for everyone if this player may (see the "pause" request).</summary>
    void Pause() => Session.Request("pause");

    /// <summary>Num *: top down, chase, driving, and round again (saved like the Options choice).</summary>
    void NextCameraMode()
    {
        var next = CameraModes.Next(Camera.Mode);
        Settings.Update(s => s.Gameplay.CameraMode = (int)next);
        Camera.Mode = next;
        Feedback?.Invoke(CameraModes.Name(next));
    }

    /// <summary>Num 0: the radial menu on yourself, centred on your character.</summary>
    void RadialOnSelf()
    {
        if (Session.Controlled is not { } me || Interaction?.OpenRadial is not { } open) return;
        if (Interaction.RadialOpen?.Invoke() == true) { Interaction.CloseRadial?.Invoke(); return; }
        var at = Camera.IsPositionBehind(me.GlobalPosition) ? GetViewport().GetVisibleRect().Size / 2 : Camera.UnprojectPosition(me.GlobalPosition + Vector3.Up * 1.2f);
        open(me, at);
    }

    /// <summary>
    /// Quick groups: "set" remembers the selected object, "select" selects it again. The original's groups hold
    /// several creatures; our client selects one object at a time, so a group is that one object for now.
    /// </summary>
    bool QuickGroupKey(InputEvent e)
    {
        for (int i = 0; i < _groups.Count; i++)
        {
            if (InputBindings.Pressed(e, ActionCatalog.QuickGroupSet(i)))
            {
                uint selected = HudState?.Selected ?? 0;
                _groups.Set(i, [selected]);
                Feedback?.Invoke(selected != 0 ? $"Quick group {i + 1} set." : $"Quick group {i + 1} cleared (nothing was selected).");
                return true;
            }
            if (InputBindings.Pressed(e, ActionCatalog.QuickGroupPick(i)))
            {
                var ids = _groups.Pick(i, id => Session.Objects.ContainsKey(id));
                if (ids.Count > 0) HudState?.Select(ids[0]);
                return true;
            }
        }
        return false;
    }

    public void Attack() => Session.Send(new ActionRequestMessage { Action = ActionKind.Attack, Argument = Attacks[_attack++ % Attacks.Length] }.Write());

    public override void _PhysicsProcess(double delta)
    {
        Vector2 input;
        float turn = 0;
        bool walk;
        if (Scripted is { } s) { input = s.Dir; walk = s.Walk; }
        else if (UI.DM.ChatInput.Typing || Blocked || KeyboardMenuOpen?.Invoke() == true) { input = Vector2.Zero; walk = false; }
        else
        {
            input = new Vector2(
                (Input.IsActionPressed(ActionCatalog.MoveRight) ? 1 : 0) - (Input.IsActionPressed(ActionCatalog.MoveLeft) ? 1 : 0),
                (Input.IsActionPressed(ActionCatalog.MoveForward) ? 1 : 0) - (Input.IsActionPressed(ActionCatalog.MoveBack) ? 1 : 0));
            walk = WalkHeld;
            turn = (Input.IsActionPressed(ActionCatalog.TurnLeft) ? 1 : 0) - (Input.IsActionPressed(ActionCatalog.TurnRight) ? 1 : 0);
        }
        KeyInput = Scripted != null ? Vector2.Zero : input;
        if (Scripted != null)
        {
            var scriptedDir = input.LengthSquared() > 0.01f ? CameraRelative(input.Normalized()) : Vector3.Zero;
            Session.Send(new MoveInputMessage { Direction = Rendering.Convert.N(scriptedDir), Walk = walk }.Write(), reliable: false);
            return;
        }
        bool mouse = input == Vector2.Zero && turn == 0 && Interaction?.MouseDriveTarget != null && !Blocked && !UI.DM.ChatInput.Typing && KeyboardMenuOpen?.Invoke() != true;
        var dir = mouse ? Interaction!.MouseDriveTarget!.Value : new Vector3(input.X, input.Y, 0);
        if (mouse) walk = Interaction!.MouseDriveWalk;
        bool active = mouse || input != Vector2.Zero || turn != 0;
        _resend -= delta;
        if (dir.DistanceSquaredTo(_lastDir) > 0.0004f || walk != _lastWalk || turn != _lastTurn || mouse != _lastMouse || active != _lastActive || _resend <= 0)
        {
            Session.Send(new DriveInputMessage { Direction = Rendering.Convert.N(dir), Walk = walk, Turn = turn, Mouse = mouse, Active = active }.Write(), reliable: active != _lastActive);
            _lastDir = dir;
            _lastWalk = walk;
            _lastTurn = turn;
            _lastMouse = mouse;
            _lastActive = active;
            _resend = 0.25;
        }
    }

    /// <summary>Key direction (x right, y forward) → Aurora world XY relative to the camera.</summary>
    Vector3 CameraRelative(Vector2 input)
    {
        var basis = Camera.GlobalTransform.Basis;
        var fwd = -basis.Z; fwd.Y = 0;
        var right = basis.X; right.Y = 0;
        var world = fwd.Normalized() * input.Y + right.Normalized() * input.X;
        var local = Session.Space.GlobalTransform.Basis.Inverse() * world;
        return new Vector3(local.X, local.Y, 0).Normalized();
    }
}

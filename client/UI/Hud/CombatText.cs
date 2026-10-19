using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>
/// Floating combat text over creatures: weapon hits (bigger and orange for criticals), misses and
/// parries, spell and effect damage coloured by damage type, and healing in green. Drawn in screen
/// space so it stays crisp, following the creature as it moves.
/// </summary>
public partial class CombatText : Control
{
    public ClientSession Session = null!;
    public Camera3D Camera = null!;
    public static bool Enabled = true;

    sealed class Floater
    {
        public required Label Label;
        public uint Target;
        public Vector3 Anchor;
        public Vector2 Drift;
        public double Age, Life;
        public float Pop;
    }

    readonly List<Floater> _floaters = new();
    readonly Dictionary<uint, int> _stack = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorRight = AnchorBottom = 1;
        Session.NotifyReceived += OnNotify;
    }

    void OnNotify(NotifyMessage m)
    {
        if (!Enabled || !Settings.Current.Interface.CombatText || m.Kind is not ("attack" or "attack-impact" or "hp-change")) return;
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        // A round attack's numbers float when its blow lands (attack-impact), not at the swing.
        if (m.Kind == "attack" && Flag(d, "pending")) return;
        if (m.Kind is "attack" or "attack-impact") Attack(d);
        else HpChange(d);
    }

    void Attack(JsonElement d)
    {
        uint target = d.GetProperty("target").GetUInt32();
        string? word = Flag(d, "parried") ? "Parried" : Flag(d, "deflected") ? "Deflected" : Flag(d, "concealed") ? "Concealed"
            : Flag(d, "epicDodge") ? "Dodged" : null;
        // A coup de grace kills without damage; the attacker's player gets "*Coup De Grace!*" over her ("float-text").
        if (Flag(d, "coupDeGrace")) return;
        if (d.TryGetProperty("feedback", out var fb) && fb.ValueKind == JsonValueKind.Array && fb.GetArrayLength() > 0)
            Spawn(target, string.Join(" · ", fb.EnumerateArray().Select(x => x.GetString())), Palette.AccentBright, 15, 1.8, bold: false);
        if (word != null) { Spawn(target, word, Palette.TextSecondary, 16, 1.0); return; }
        if (!d.GetProperty("hit").GetBoolean()) { Spawn(target, "Miss", Palette.TextSecondary, 16, 0.9); return; }
        int damage = d.TryGetProperty("dealt", out var dealt) ? dealt.GetInt32() : d.GetProperty("damage").GetInt32();
        bool crit = d.GetProperty("critical").GetBoolean();
        bool mine = target == Session.ControlledId;
        var color = mine ? Palette.HealthBright : crit ? new Color("ff9a3c") : Palette.Text;
        if (damage <= 0) { Spawn(target, "Absorbed", Palette.TextSecondary, 15, 1.0); return; }
        Spawn(target, crit ? damage + "!" : damage.ToString(), color, crit ? 34 : 24, crit ? 1.4 : 1.1, pop: crit ? 0.5f : 0.25f);
    }

    void HpChange(JsonElement d)
    {
        if (d.TryGetProperty("combat", out var c) && c.GetBoolean()) return; // shown by the attack notify
        uint target = d.GetProperty("target").GetUInt32();
        int amount = d.GetProperty("amount").GetInt32();
        if (amount == 0) return;
        if (amount > 0) { Spawn(target, "+" + amount, Palette.Success.Lightened(0.15f), 22, 1.2); return; }
        Spawn(target, (-amount).ToString(), TypeColor(d.GetProperty("type").GetInt32()), 24, 1.2, pop: 0.3f);
    }

    static bool Flag(JsonElement d, string name) => d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    /// <summary>DAMAGE_TYPE_* bits to colours.</summary>
    static Color TypeColor(int type) => type switch
    {
        16 => new Color("9be35a"),   // acid
        32 => new Color("8fd8ff"),   // cold
        64 => new Color("fff2a8"),   // divine
        128 => new Color("b9b4ff"),  // electrical
        256 => new Color("ff8a3d"),  // fire
        512 => new Color("b67cff"),  // negative
        1024 => new Color("fffbe0"), // positive
        2048 => new Color("7ff0e0"), // sonic
        8 => Palette.Magic,          // magical
        _ => Palette.Text,
    };

    void Spawn(uint target, string text, Color color, int size, double life, float pop = 0.15f, bool bold = true)
    {
        if (!Session.Objects.TryGetValue(target, out var view)) return;
        var label = Ui.Overlay(text, size, color, bold ? Fonts.UiBold : Fonts.UiMedium);
        label.AddThemeConstantOverride("outline_size", size >= 30 ? 8 : 6);
        AddChild(label);
        // Successive numbers on the same creature fan out so they don't overlap.
        int n = _stack.GetValueOrDefault(target);
        _stack[target] = n + 1;
        float side = (n % 3 - 1) * 26f;
        _floaters.Add(new Floater
        {
            Label = label, Target = target, Anchor = view.GlobalPosition + Vector3.Up * 2.05f,
            Drift = new Vector2(side + (float)GD.RandRange(-6, 6), 0), Life = life, Pop = pop,
        });
        GetTree().CreateTimer(0.6).Timeout += () => { if (_stack.TryGetValue(target, out var k) && k > 0) _stack[target] = k - 1; };
    }

    public override void _Process(double delta)
    {
        for (int i = _floaters.Count - 1; i >= 0; i--)
        {
            var f = _floaters[i];
            f.Age += delta;
            if (f.Age >= f.Life || !IsInstanceValid(f.Label)) { if (IsInstanceValid(f.Label)) f.Label.QueueFree(); _floaters.RemoveAt(i); continue; }
            if (Session.Objects.TryGetValue(f.Target, out var view)) f.Anchor = view.GlobalPosition + Vector3.Up * 2.05f;
            if (Camera.IsPositionBehind(f.Anchor)) { f.Label.Visible = false; continue; }
            f.Label.Visible = true;
            float t = (float)(f.Age / f.Life);
            var screen = Camera.UnprojectPosition(f.Anchor);
            // Rise quickly then hang; pop in at the start; fade over the last third.
            float rise = 46 * (1 - MathF.Pow(1 - t, 3));
            float scale = 1 + f.Pop * MathF.Max(0, 1 - t * 6);
            f.Label.PivotOffset = f.Label.Size / 2;
            f.Label.Scale = new Vector2(scale, scale);
            f.Label.Position = screen + f.Drift * t - new Vector2(f.Label.Size.X / 2, rise + f.Label.Size.Y / 2);
            f.Label.Modulate = f.Label.Modulate with { A = t < 0.66f ? 1 : 1 - (t - 0.66f) / 0.34f };
        }
    }
}

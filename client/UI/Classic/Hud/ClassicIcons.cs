using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's icons for what the modern interface draws with its own glyphs: commands, emotes and modes
/// get the radial menu's ir_* icons from the game data (attack, rest, examine, stealth ...).
/// </summary>
public static class ClassicIcons
{
    static readonly Dictionary<string, string> ByGlyph = new()
    {
        ["character"] = "ir_charsheet", ["command"] = "ir_command",
        ["attack"] = "ir_attack", ["rest"] = "ir_rest", ["eye"] = "ir_examine", ["talk"] = "ir_dialog", ["combat"] = "ir_xattack",
        ["search"] = "ir_search", ["stealth"] = "ir_stealth", ["shield"] = "ir_parry", ["magic"] = "ir_cast", ["aura"] = "ir_genaura",
        ["emote"] = "ir_emotes", ["taunt"] = "ir_taunt", ["book"] = "ir_read", ["pickup"] = "ir_pickup", ["meditate"] = "ir_meditate",
        ["feat"] = "ir_xability", ["skill"] = "ir_skillself", ["bag"] = "ir_inventory", ["potion"] = "ir_drink", ["menu"] = "ir_more_sp_ab",
        ["cog"] = "ir_action", ["lock"] = "ir_lock", ["unlock"] = "ir_unlock", ["open"] = "ir_open", ["trap"] = "ir_trap",
    };

    static readonly Dictionary<string, string> ByCommand = new()
    {
        ["attack"] = "ir_attack", ["rest"] = "ir_rest", ["examine"] = "ir_examine", ["talk"] = "ir_dialog", ["stance"] = "ir_xattack",
        ["barter"] = "ir_barter", ["craft"] = "ir_craft", ["attacknearest"] = "ir_attacknearest", ["follow"] = "ir_follow",
        ["standground"] = "ir_standgrnd", ["guardme"] = "ir_guard", ["healme"] = "ir_healme",
    };

    static readonly Dictionary<int, string> ByMode = new() { [0] = "ir_search", [1] = "ir_stealth", [2] = "ir_parry", [10] = "ir_defcast" };

    /// <summary>The icon layers to draw for a slot: its own game icons, else the original's icon for its kind.</summary>
    public static string[] For(ClientContent? content, SlotLook look, BarSlot? slot = null)
    {
        if (content != null && look.Icons.Any(i => i.Length > 0 && content.Textures.Get(i) != null)) return look.Icons;
        string? ir = slot?.Kind switch
        {
            "emote" => OpenWinter.Content.Emotes.Find(slot.Arg)?.Icon,
            "command" => ByCommand.GetValueOrDefault(slot.Arg),
            "mode" => ByMode.GetValueOrDefault(slot.Id),
            _ => null,
        };
        ir ??= ByGlyph.GetValueOrDefault(look.Glyph);
        return ir != null && content?.Textures.Get(ir) != null ? [ir] : look.Icons;
    }

    /// <summary>The original's icon for a kit glyph (radial wedges without a game icon); null when none.</summary>
    public static string? ForGlyph(ClientContent? content, string glyph) =>
        ByGlyph.TryGetValue(glyph, out var ir) && content?.Textures.Get(ir) != null ? ir : null;

    /// <summary>
    /// Draws icon layers fitted inside a box (keeping their shape: tall item icons stay tall), centred; falls back to
    /// the modern glyph when the game has no icon at all.
    /// </summary>
    public static void Draw(CanvasItem ci, ClientContent? content, SlotLook look, string[] icons, Rect2 box, Color modulate)
    {
        if (look.SecondaryIcons.Length > 0 && content != null) { SlotArt.DrawPair(ci, content, look, box, modulate); return; }
        var first = content == null ? null : icons.Select(i => i.Length > 0 ? content.Textures.Get(i) : null).FirstOrDefault(t => t != null);
        var rect = box;
        if (first != null)
        {
            var size = SlotArt.IconCanvas(content!, icons) ?? first.Texture.GetSize();
            float scale = Math.Min(box.Size.X / size.X, box.Size.Y / size.Y);
            var s = (size * scale).Round();
            rect = new Rect2((box.Position + (box.Size - s) / 2).Round(), s);
        }
        SlotArt.DrawIcon(ci, content, look with { Icons = icons }, rect, modulate);
    }
}

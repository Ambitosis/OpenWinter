using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// Design tokens: every colour, size and spacing the interface uses. Obsidian panels with brass
/// accents; text in three strengths; semantic colours for health, mana, attitude and rarity.
/// </summary>
public static class Palette
{
    // Surfaces, darkest to lightest.
    public static readonly Color Bg0 = new("0a0b0fef");
    public static readonly Color Bg1 = new("111319");
    public static readonly Color Bg2 = new("181b23");
    public static readonly Color Bg3 = new("222632");
    public static readonly Color Bg4 = new("2c3140");
    public static readonly Color Scrim = new(0, 0, 0, 0.55f);

    // Lines.
    public static readonly Color Line = new(1, 1, 1, 0.07f);
    public static readonly Color LineStrong = new(1, 1, 1, 0.14f);

    // Brass accent.
    public static readonly Color Accent = new("d4a657");
    public static readonly Color AccentBright = new("f2cf86");
    public static readonly Color AccentDim = new("8c6c36");
    public static readonly Color AccentGlow = new(0.83f, 0.65f, 0.34f, 0.18f);

    // Text.
    public static readonly Color Text = new("ece7dc");
    public static readonly Color TextSecondary = new("aba596");
    public static readonly Color TextMuted = new("6f6b63");
    public static readonly Color TextOnAccent = new("1a1408");

    // Semantic.
    public static readonly Color Health = new("c8463c");
    public static readonly Color HealthBright = new("e8685a");
    public static readonly Color HealthTrail = new("f0c89a");
    public static readonly Color TempHealth = new("6fa8dc");
    public static readonly Color Mana = new("4a8fd8");
    public static readonly Color Xp = new("b48cf0");
    public static readonly Color Success = new("6fbf73");
    public static readonly Color Warning = new("e0a040");
    public static readonly Color Danger = new("e05a4f");
    public static readonly Color Info = new("7fb0e8");
    public static readonly Color Hostile = new("e05a4f");
    public static readonly Color Friendly = new("6fbf73");
    public static readonly Color Neutral = new("e0c060");
    public static readonly Color Magic = new("8fb4ff");
    public static readonly Color Plot = new("e7a6ff");

    // Chat channels.
    public static readonly Color ChatTalk = new("ece7dc");
    public static readonly Color ChatShout = new("f0c060");
    public static readonly Color ChatWhisper = new("a8a8cc");
    public static readonly Color ChatServer = new("b0e0b0");
    public static readonly Color ChatParty = new("80c0ff");
    public static readonly Color ChatDm = new("e090ff");
    public static readonly Color ChatTell = new("6fe0d0");

    // Metrics.
    public const int Radius = 10, RadiusSmall = 6, RadiusLarge = 14;
    public const int Space1 = 4, Space2 = 8, Space3 = 12, Space4 = 16, Space5 = 24, Space6 = 32;
    public const int FontCaption = 12, FontSmall = 13, FontBody = 15, FontLarge = 17, FontTitle = 20, FontHeading = 26, FontDisplay = 40;

    public static string Hex(Color c) => "#" + c.ToHtml(false);
}

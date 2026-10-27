namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// NWN's animation codes in object updates and what OpenWinter plays for them. Script animations
/// (PlayAnimation, ActionPlayAnimation) arrive as codes of their own, recorded against each ANIMATION_* constant
/// (parity/visual/scenes/bridgefx); they become the model animations OpenWinter's server names for those constants.
/// A looping one plays until the next code; the server sends a standing code when its time is up.
/// </summary>
public static class NwnAnimations
{
    /// <summary>Standing: the plain pause and the pose a creature arrives in.</summary>
    public const ushort Pause = 0, Ready = 1;
    /// <summary>
    /// Lying dead: ANIMATION_LOOPING_DEAD_FRONT and DEAD_BACK, which a creature killed also shows. Walking (2, 4),
    /// attacks (9) and hit reactions (77, 99, 100) are the world mirror's and combat's.
    /// </summary>
    public const ushort DeadFront = 6, DeadBack = 8;
    public const ushort Conjure1 = 15, Conjure2 = 16;
    /// <summary>The cast animations a spell's release plays: spells.2da CastAnim "out", "self", "up", "area" and "touch".</summary>
    public static readonly ushort[] Casts = [17, 18, 19, 20, 61];

    /// <summary>Placeable states: destroyed (doors too; a door's open and closed codes are the mirror's), on, off, open, closed.</summary>
    public const ushort Destroyed = 72, On = 73, Off = 74, Open = 75, Close = 76;

    /// <summary>A creature animation: the model animation, upper body only, and whether it loops.</summary>
    public readonly record struct Clip(string Name, bool Gesture, bool Looping);

    static readonly Dictionary<ushort, Clip> Creature = Build();

    static Dictionary<ushort, Clip> Build()
    {
        Clip Loop(string name, bool gesture = false) => new(name, gesture, true);
        Clip Once(string name, bool gesture = false) => new(name, gesture, false);
        var map = new Dictionary<ushort, Clip>
        {
            // ANIMATION_LOOPING_*
            [52] = Loop("pause2"), [30] = Loop("listen"), [32] = Loop("meditate"), [33] = Loop("worship"),
            [48] = Loop("lookfar"), [36] = Loop("sit"), [47] = Loop("sitcross"), [38] = Loop("tlknorm", true),
            [39] = Loop("tlkplead", true), [40] = Loop("tlkforce", true), [41] = Loop("tlklaugh", true),
            [59] = Loop("getlowlp"), [60] = Loop("getmidlp"), [57] = Loop("pausetrd"), [58] = Loop("pausedrnk"),
            [93] = Loop("spasm"), [97] = Loop("custom1"), [98] = Loop("custom2"),
            [119] = Loop("mount1"), [120] = Loop("dismount1"),
            // ANIMATION_FIREFORGET_*
            [53] = Once("hturnl", true), [54] = Once("hturnr", true), [55] = Once("pausesh"), [56] = Once("pausebrd"),
            [34] = Once("salute", true), [35] = Once("bow"), [37] = Once("steal"), [29] = Once("greeting", true),
            [28] = Once("taunt", true), [44] = Once("victoryfr"), [45] = Once("victorymg"), [46] = Once("victoryth"),
            [71] = Once("read", true), [70] = Once("drink", true), [90] = Once("dodges"), [91] = Once("dodgelr"),
            [23] = Once("spasm"),
        };
        // ANIMATION_LOOPING_CUSTOM3-20 and CUSTOM21-57 run on in one block.
        for (int i = 3; i <= 20; i++) map[(ushort)(98 + i)] = Loop("custom" + i);
        for (int i = 21; i <= 57; i++) map[(ushort)(100 + i)] = Loop("custom" + i);
        return map;
    }

    /// <summary>The clip a creature's animation code plays; null for standing, moving, combat, death and casting codes.</summary>
    public static Clip? For(ushort code) => Creature.TryGetValue(code, out var clip) ? clip : null;

    /// <summary>The conjure animation for spells.2da ConjAnim ("hand", "head") or a script's conjure code.</summary>
    public static string ConjureClip(string conjAnim, ushort code) =>
        conjAnim.ToLowerInvariant() switch { "hand" => "conjure1", "head" => "conjure2", _ => code == Conjure2 ? "conjure2" : "conjure1" };

    /// <summary>The cast animation for spells.2da CastAnim, or for the code when the spell doesn't say.</summary>
    public static string CastClip(string castAnim, ushort code) => castAnim.ToLowerInvariant() switch
    {
        "out" => "castout",
        "touch" or "point" => "castpoint",
        "up" => "castup",
        "self" => "castself",
        "area" => "castarea",
        _ => code switch { 18 => "castself", 19 => "castup", 20 => "castarea", 61 => "castpoint", _ => "castout" },
    };

    /// <summary>The animation code a script animation (an ANIMATION_* constant) is sent as; null when it has none.</summary>
    public static ushort? FromScript(int animation) => animation switch
    {
        0 => Pause,
        1 => 52,
        2 => 30,
        3 => 32,
        4 => 33,
        5 => 48,
        6 => 36,
        7 => 47,
        8 => 38,
        9 => 39,
        10 => 40,
        11 => 41,
        12 => 59,
        13 => 60,
        14 => 57,
        15 => 58,
        16 => DeadFront,
        17 => DeadBack,
        18 => Conjure1,
        19 => Conjure2,
        20 => 93,
        21 => 97,
        22 => 98,
        >= 23 and <= 31 => (ushort)(animation + 78),
        >= 32 and <= 40 => (ushort)(animation + 78),
        41 => 119,
        42 => 120,
        >= 43 and <= 79 => (ushort)(animation + 78),
        100 => 53,
        101 => 54,
        102 => 55,
        103 => 56,
        104 => 34,
        105 => 35,
        106 => 37,
        107 => 29,
        108 => 28,
        109 => 44,
        110 => 45,
        111 => 46,
        112 => 71,
        113 => 70,
        114 => 90,
        115 => 91,
        116 => 23,
        _ => null,
    };
}

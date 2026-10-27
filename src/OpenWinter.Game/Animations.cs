namespace OpenWinter.Game;

/// <summary>ANIMATION_* constants → model animation names.</summary>
public static class Animations
{
    static readonly Dictionary<int, (string Name, bool Gesture)> Map = new()
    {
        [0] = ("pause1", false), [1] = ("pause2", false), [2] = ("listen", false), [3] = ("meditate", false),
        [4] = ("worship", false), [5] = ("lookfar", false), [6] = ("sit", false), [7] = ("sitcross", false),
        [8] = ("tlknorm", true), [9] = ("tlkplead", true), [10] = ("tlkforce", true), [11] = ("tlklaugh", true),
        [12] = ("getlowlp", false), [13] = ("getmidlp", false), [14] = ("pausetrd", false), [15] = ("pausedrnk", false),
        [16] = ("deadfnt", false), [17] = ("deadbck", false), [18] = ("conjure1", false), [19] = ("conjure2", false),
        [20] = ("spasm", false), [21] = ("custom1", false), [22] = ("custom2", false),
        [41] = ("mount1", false), [42] = ("dismount1", false),
        [100] = ("hturnl", true), [101] = ("hturnr", true), [102] = ("pausesh", false), [103] = ("pausebrd", false),
        [104] = ("salute", true), [105] = ("bow", false), [106] = ("steal", false), [107] = ("greeting", true),
        [108] = ("taunt", true), [109] = ("victoryfr", false), [110] = ("victorymg", false), [111] = ("victoryth", false),
        [112] = ("read", true), [113] = ("drink", true), [114] = ("dodges", false), [115] = ("dodgelr", false),
        [116] = ("spasm", false),
        [200] = ("on", false), [201] = ("off", false), [202] = ("open", false), [203] = ("close", false),
        [204] = ("close", false), [205] = ("opened1", false), [206] = ("opened2", false), [207] = ("die", false),
    };

    static Animations()
    {
        // ANIMATION_LOOPING_CUSTOM3-20 are 23-40, CUSTOM21-57 are 43-79.
        for (int i = 23; i <= 40; i++) Map[i] = ("custom" + (i - 20), false);
        for (int i = 43; i <= 79; i++) Map[i] = ("custom" + (i - 22), false);
    }

    public static (string Name, bool Gesture) Name(int animation) => Map.TryGetValue(animation, out var v) ? v : ("", false);

    /// <summary>
    /// What a creature plays for ANIMATION_LOOPING_CUSTOM3-11 when its model has no clips of that name: the engine's
    /// own casting, knockdown and sitting animations (visual scene camanim: CUSTOM6 on a model without custom6 clips).
    /// Null for any other name.
    /// </summary>
    public static string? CustomFallback(string clip) => clip.ToLowerInvariant() switch
    {
        "custom3" => "castout",
        "custom4" => "castpoint",
        "custom5" => "castup",
        "custom6" => "castself",
        "custom7" => "castarea",
        "custom8" => "closeh",
        "custom9" => "kdbck",
        "custom10" => "kdfnt",
        "custom11" => "sitdown",
        _ => null,
    };

    /// <summary>
    /// A conversation node's Animation field (.dlg) as the ANIMATION_* constant the original plays for it, and
    /// whether it keeps playing (a looping one holds until a later line of that speaker or the end of the
    /// conversation). Measured on the original (parity/visual/scenes/talkanim: each value beside a creature
    /// playing the constant); 0, the default, plays nothing, nor do 31, 42, 43, 49-60, 72, 73 and 88.
    /// </summary>
    public static (int Animation, bool Looping) ConversationLine(int value) => value switch
    {
        28 => (108, false), // taunt
        29 => (107, false), // greeting
        30 => (2, true),    // listen
        32 => (3, true),    // meditate
        33 => (4, true),    // worship
        34 => (104, false), // salute
        35 => (105, false), // bow
        36 => (106, false), // steal
        37 => (13, false),  // a reach at waist height (get mid, once)
        38 => (8, true),    // talk normal
        39 => (9, true),    // talk pleading
        40 => (10, true),   // talk forceful
        41 => (11, true),   // talk laughing
        44 => (109, false), // victory 1
        45 => (110, false), // victory 2
        46 => (111, false), // victory 3
        47 => (12, true),   // get low
        48 => (5, true),    // look far
        70 => (113, false), // drink
        71 => (112, false), // read
        _ => (-1, false),
    };
}

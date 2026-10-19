namespace OpenWinter.Content;

/// <summary>
/// The original's emotes as its radial offers them (the self radial's Emotes and the quickbar slot ring's Emote; visual
/// scenes quickbargaps, quickbaremote and quickbarexport, docs/QUICKBAR_RADIAL.md): three rings of fixed places, the
/// next ring at north-west ("More Emotes"). Each plays an ANIMATION_* constant (looping ones for <see
/// cref="Emote.Seconds"/>) and some speak a voice-set line. On the quickbar the original saves an emote as
/// QBObjectType 8 with its <see cref="Emote.Id"/> in QBINTParam1.
/// </summary>
public static class Emotes
{
    /// <param name="Key">OpenWinter's name for it (a quickbar slot's Arg).</param>
    /// <param name="Ring">0 the Emote ring, 1 and 2 its More Emotes.</param>
    /// <param name="Direction">Its place, clockwise from north (0) in eighths.</param>
    /// <param name="Animation">The ANIMATION_* constant it plays.</param>
    /// <param name="Seconds">How long a looping animation plays (0 for a single play).</param>
    /// <param name="Voice">The sound-set slot it speaks, or -1.</param>
    /// <param name="Id">The original's quickbar id (QBINTParam1 of a QBObjectType 8 entry); -1 where not measured yet.</param>
    public sealed record Emote(string Key, uint StrRef, string Name, string Icon, int Ring, int Direction,
        int Animation, float Seconds, int Voice, int Id);

    public const uint MoreStrRef = 702;
    public const string MoreIcon = "ir_moreemotes";
    /// <summary>The quickbar entry type of an emote.</summary>
    public const int QuickbarType = 8;

    public static readonly Emote[] All =
    [
        new("bow", 699, "Bow", "ir_bow", 0, 0, 105, 0, -1, 7),
        new("threaten", 66859, "Threaten", "ir_threaten", 0, 1, 108, 0, -1, 8),
        new("greet", 1069, "Greet", "ir_greet", 0, 2, 107, 0, 34, 6),
        new("laugh", 66856, "Laugh", "ir_laugh", 0, 3, 11, 2, 42, 4),
        new("salute", 703, "Salute", "ir_salute", 0, 4, 104, 0, -1, 3),
        new("cheer", 1010, "Cheer", "ir_cheer", 0, 5, 109, 0, 44, 1),
        new("goodbye", 66857, "Goodbye", "ir_goodbye", 0, 6, 107, 0, 40, -1),
        new("nod", 66864, "Nod", "ir_nod", 1, 0, 2, 2, -1, -1),
        new("tired", 66863, "Tired", "ir_tired", 1, 1, 14, 2.833f, 38, -1),
        new("beg", 985, "Beg", "ir_beg", 1, 2, 9, 2, -1, -1),
        new("poisoned", 4723, "Poisoned", "ir_woosey", 1, 3, 15, 4, 19, -1),
        new("bored", 66861, "Bored", "ir_bored", 1, 4, 103, 0, -1, -1),
        new("lookfar", 66860, "Look Far", "ir_lookfar", 1, 5, 5, 2, -1, -1),
        new("scratch", 984, "Scratch Head", "ir_scratch", 1, 6, 102, 0, 47, -1),
        new("fallforward", 110530, "Fall down forwards", "ir_fallfront", 2, 0, 16, 3, -1, -1),
        new("sit", 110528, "Sit on ground", "ir_sit", 2, 2, 7, 10, -1, -1),
        new("meditate", 110526, "Meditate", "ir_meditate", 2, 3, 3, 10, -1, -1),
        new("drink", 110525, "Drink potion", "ir_drink", 2, 4, 113, 0, -1, -1),
        new("read", 110524, "Read", "ir_read", 2, 5, 112, 0, -1, -1),
        new("worship", 110527, "Worship", "ir_worship", 2, 6, 4, 7, -1, -1),
        new("fallback", 110529, "Fall down backwards", "ir_fallback", 2, 7, 17, 3, -1, -1),
    ];

    /// <summary>OpenWinter's earlier emote names (animation names, kept in older saves) and what they became.</summary>
    static readonly Dictionary<string, string> Legacy = new()
    {
        ["greeting"] = "greet", ["tlklaugh"] = "laugh", ["taunt"] = "threaten", ["victoryfr"] = "cheer", ["tlkplead"] = "beg",
    };

    /// <summary>An emote by its key (or an earlier OpenWinter name); null when there is none.</summary>
    public static Emote? Find(string key)
    {
        if (Legacy.TryGetValue(key, out var renamed)) key = renamed;
        return All.FirstOrDefault(e => e.Key == key);
    }

    /// <summary>An emote by the original's quickbar id; null when unknown.</summary>
    public static Emote? FromId(int id) => id < 0 ? null : All.FirstOrDefault(e => e.Id == id);
}

using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.Combat;

/// <summary>
/// How a humanoid holds what is in its hands, from baseitems.2da (WeaponWield, WeaponSize, WeaponType, BaseAC) and
/// the creature's size: the clip prefix of its ready stance and swings, and the arm clips the original lays over
/// idling, walking and running. Measured black-box on the original with public ReplaceObjectAnimation (parity/visual
/// scenes weaponstance1..3 and weaponready1..6):
/// <list type="bullet">
/// <item>Unarmed: nw (nwreadyr, nwslashl ...). A weapon in each hand: 2w.</item>
/// <item>WeaponWield 4 (halberd, scythe, spear, quarterstaff, magic staff) and 8 (two-bladed sword, dire mace,
/// double axe): pl, the polearm set, ready stance and swings alike.</item>
/// <item>A weapon larger than the creature (WeaponSize above its size: greatsword, greataxe, heavy flail and trident
/// for a human, a club or a morning star for a halfling): 2h. Otherwise 1h (bastard sword for a human).</item>
/// <item>Bows bowrdy/bowshot, crossbows xbowrdy/xbowshot; slings and thrown weapons stand in 1hreadyr and throw
/// (throwr: the sling model animates throwr and 1hready).</item>
/// <item>Arm clips: a one-handed weapon in the right hand (thrown ones too) walk_swordr / run_swordr; a weapon in the
/// left hand walk_swordl / run_swordl; a shield walk_shieldl / run_shieldl; a bow walk_bowl / run_bowl; a crossbow
/// xbowr; a two-handed or polearm weapon plpause1 over idling, walking and running alike, except the flails, whose
/// chain the model animates itself (heavy flail for a human, morning star for a halfling: none).</item>
/// </list>
/// </summary>
public static class WeaponStances
{
    public const string Unarmed = "nw", OneHanded = "1h", TwoHanded = "2h", Polearm = "pl", Dual = "2w";

    /// <param name="Prefix">nw, 1h, 2h, pl or 2w; empty for bows and crossbows.</param>
    /// <param name="Ready">The combat ready stance (1hreadyr, bowrdy ...).</param>
    /// <param name="Shot">The ranged attack clip (bowshot, xbowshot, throwr); empty for melee.</param>
    /// <param name="Idle">Arm clips over pause1.</param>
    /// <param name="Walk">Arm clips over walk.</param>
    /// <param name="Run">Arm clips over run.</param>
    public sealed record Stance(string Prefix, string Ready, string Shot, string[] Idle, string[] Walk, string[] Run);

    const int SizeMedium = 3;

    /// <summary>
    /// The stance for a creature of <paramref name="creatureSize"/> (CREATURE_SIZE_*, appearance.2da SIZECATEGORY)
    /// holding <paramref name="right"/> and <paramref name="left"/> (base item rows, -1 for an empty hand).
    /// <paramref name="chainAnimated"/> tells whether a base item's model animates itself (the flails' chain).
    /// </summary>
    public static Stance For(TwoDA baseItems, int creatureSize, int right, int left, Func<int, bool>? chainAnimated = null)
    {
        bool Valid(int row) => row >= 0 && row < baseItems.RowCount;
        int Wield(int row) => Valid(row) ? baseItems.GetInt(row, "WeaponWield", 0) : 0;
        bool Weapon(int row) => Valid(row) && baseItems.GetInt(row, "WeaponType", 0) > 0 && Wield(row) is not (1 or 7 or 9);
        bool Shield(int row) => Valid(row) && Wield(row) == 7;
        if (creatureSize <= 0) creatureSize = SizeMedium;

        var idle = new List<string>();
        var walk = new List<string>();
        var run = new List<string>();
        if (Shield(left)) { walk.Add("walk_shieldl"); run.Add("run_shieldl"); }
        else if (Weapon(left)) { walk.Add("walk_swordl"); run.Add("run_swordl"); }

        string prefix, ready, shot = "";
        int wield = Wield(right);
        if (!Weapon(right))
        {
            prefix = Unarmed;
            ready = "nwreadyr";
        }
        else if (wield == CombatK.WieldBow)
        {
            prefix = "";
            ready = "bowrdy";
            shot = "bowshot";
            walk.Add("walk_bowl"); run.Add("run_bowl");
        }
        else if (wield == CombatK.WieldCrossbow)
        {
            prefix = "";
            ready = "xbowrdy";
            shot = "xbowshot";
            idle.Add("xbowr");
        }
        else if (wield is CombatK.WieldSling or CombatK.WieldThrown)
        {
            prefix = OneHanded;
            ready = "1hreadyr";
            shot = "throwr";
            if (wield == CombatK.WieldThrown) { walk.Add("walk_swordr"); run.Add("run_swordr"); }
        }
        else
        {
            bool large = baseItems.GetInt(right, "WeaponSize", 0) > creatureSize;
            prefix = Weapon(left) ? Dual
                : wield is CombatK.WieldTwoHanded or CombatK.WieldDouble ? Polearm
                : large ? TwoHanded : OneHanded;
            ready = prefix + "readyr";
            if (prefix is Polearm or TwoHanded)
            {
                if (chainAnimated?.Invoke(right) != true) { idle.Add("plpause1"); walk.Add("plpause1"); run.Add("plpause1"); }
            }
            else { walk.Add("walk_swordr"); run.Add("run_swordr"); }
        }
        return new Stance(prefix, ready, shot, idle.ToArray(), walk.ToArray(), run.ToArray());
    }

    /// <summary>A melee swing of the stance: slashl, slashr or stab picked by the base item's %AnimSlash weights.</summary>
    public static string Swing(Stance stance, TwoDA baseItems, int weapon, int roll) => Swing(stance.Prefix, baseItems, weapon, roll);

    /// <summary>
    /// Whether an appearance.2da MODELTYPE is a creature model with the creature animation set (cpause1, creadyr,
    /// ca1slashl, ccloseh ...): S (simple) and L (large) models, with or without the W / T wing and tail letters.
    /// P (part-based) and F (whole-body) models use the humanoid clips. Checked against the installed models: every
    /// S and L model with attack clips has ca1slashl, every P and F one nwslashl (a few EE models have both sets).
    /// </summary>
    public static bool CreatureSet(string modelType) =>
        modelType.Length > 0 && char.ToUpperInvariant(modelType[0]) is 'S' or 'L';

    /// <summary>
    /// A non-humanoid creature's melee attack clip, measured on the original with ReplaceObjectAnimation markers
    /// (parity/visual scenes creatureswing, creatureswing2..7 and creaturereach; docs/WEAPON_STANCES.md "Creature
    /// models"): a target within the
    /// attacker's appearance.2da PREFATCKDIST (centre to centre) gets ccloseh; farther away, as where a creature
    /// closing in from a distance stops (PREFATCKDIST plus the target's HITDIST), ca1slashl, ca1slashr or ca1stab
    /// picked by the creature weapon's %AnimSlash weights (the creature weapon base items have 50 / 50 / 50:
    /// a third each), whichever creature weapon slot the attack comes from. ca2*, creach and cclosel were never
    /// played.
    /// </summary>
    public static string CreatureSwing(TwoDA baseItems, int weapon, int roll, float distance, float preferredDistance) =>
        distance <= preferredDistance + CloseSlack ? "ccloseh" : Swing(CreatureAttack, baseItems, weapon, roll);

    /// <summary>The creature set's first attack prefix (ca1slashl, ca1slashr, ca1stab).</summary>
    public const string CreatureAttack = "ca1";

    // A dire badger (PREFATCKDIST 2.6) rooted 2.6 m away played ccloseh, 2.7 m away ca1; a wolf (2.3) ccloseh at
    // 2.1 m, ca1 at 2.5 m. The slack only absorbs float rounding of a creature placed at exactly its distance.
    const float CloseSlack = 0.01f;

    static readonly string[] SwingSuffixes = { "slashl", "slashr", "stab", "closeh", "closel", "reach" };
    static readonly string[] HumanoidPrefixes = { Unarmed, OneHanded, TwoHanded, Polearm, Dual };

    /// <summary>
    /// The same swing in the other animation set, for a model that lacks the one asked for: a humanoid swing
    /// (nwslashl, 1hstab, 2hcloseh ...) as the creature clip (ca1slashl, ca1stab, ccloseh), and a creature clip
    /// (ca1slashl, ca2stab, ccloseh, creach) as the unarmed humanoid one (nwslashl, nwstab, nwcloseh, nwreach).
    /// Null when <paramref name="clip"/> is no melee swing.
    /// </summary>
    public static string? SwingAlias(string clip)
    {
        clip = clip.ToLowerInvariant();
        foreach (var suffix in SwingSuffixes)
        {
            if (!clip.EndsWith(suffix, StringComparison.Ordinal)) continue;
            string prefix = clip[..^suffix.Length];
            bool basic = suffix is "slashl" or "slashr" or "stab";
            if (HumanoidPrefixes.Contains(prefix)) return basic ? CreatureAttack + suffix : "c" + suffix;
            if (basic ? prefix is "ca1" or "ca2" : prefix == "c") return Unarmed + suffix;
        }
        return null;
    }

    static string Swing(string prefix, TwoDA baseItems, int weapon, int roll)
    {
        int l = 34, r = 33, s = 33;
        if (weapon >= 0 && weapon < baseItems.RowCount && baseItems.ColumnIndex("%AnimSlashL") >= 0)
        {
            l = baseItems.GetInt(weapon, "%AnimSlashL", 34);
            r = baseItems.GetInt(weapon, "%AnimSlashR", 33);
            s = baseItems.GetInt(weapon, "%AnimSlashS", 33);
        }
        int total = Math.Max(1, l + r + s);
        int pick = ((roll % total) + total) % total;
        return prefix + (pick < l ? "slashl" : pick < l + r ? "slashr" : "stab");
    }
}

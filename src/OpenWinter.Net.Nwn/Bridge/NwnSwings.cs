namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The swing clip and weapon sounds of an attack the server reports. The server names neither: as the original
/// client does, they follow from what the attacker holds and what the blow lands on. The rules are those of
/// OpenWinter's own server (the weapon stance scenes weaponstance1..3 and the creature swing scenes creatureswing,
/// creatureswing2..7 and creaturereach), read from the same 2DAs.
/// </summary>
public static class NwnSwings
{
    /// <summary>NWN's equipment slot bits for the hands.</summary>
    public const int SlotRight = 0x10, SlotLeft = 0x20;

    const int WieldShield = 7, WieldBow = 5, WieldCrossbow = 6, WieldSling = 10, WieldThrown = 11, WieldTwoHanded = 4, WieldDouble = 8;

    /// <summary>
    /// The clip of a swing by a creature of appearance <paramref name="appearance"/> holding <paramref name="right"/>
    /// and <paramref name="left"/> (base item rows, -1 for an empty hand), <paramref name="distance"/> metres from its
    /// target.
    /// </summary>
    public static string Swing(INwnBridgeData data, int appearance, int right, int left, bool ranged, bool offHand, float distance, int roll)
    {
        int Int(string table, int row, string column, int fallback) =>
            row >= 0 && int.TryParse(data.TwoDA(table, row, column), out var v) ? v : fallback;
        bool Valid(int row) => row >= 0 && row < data.TwoDARowCount("baseitems");
        int Wield(int row) => Valid(row) ? Int("baseitems", row, "WeaponWield", 0) : 0;
        bool Weapon(int row) => Valid(row) && Int("baseitems", row, "WeaponType", 0) > 0 && Wield(row) is not (1 or WieldShield or 9);

        // Creature models (MODELTYPE S or L) have their own set: ccloseh within PREFATCKDIST, else ca1 swings.
        var modelType = data.TwoDA("appearance", appearance, "MODELTYPE");
        if (modelType.Length > 0 && char.ToUpperInvariant(modelType[0]) is 'S' or 'L')
        {
            float preferred = float.TryParse(data.TwoDA("appearance", appearance, "PREFATCKDIST"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 1f;
            return distance <= preferred + 0.01f ? "ccloseh" : Pick(data, "ca1", right, roll);
        }
        if (Weapon(right))
        {
            int wield = Wield(right);
            if (wield == WieldBow) return "bowshot";
            if (wield == WieldCrossbow) return "xbowshot";
            if (wield is WieldSling or WieldThrown) return "throwr";
        }
        else if (ranged) return "bowshot";
        string prefix;
        if (!Weapon(right)) prefix = "nw";
        else
        {
            int size = Int("appearance", appearance, "SIZECATEGORY", 3);
            if (size <= 0) size = 3;
            int wield = Wield(right);
            prefix = Weapon(left) ? "2w"
                : wield is WieldTwoHanded or WieldDouble ? "pl"
                : Int("baseitems", right, "WeaponSize", 0) > size ? "2h" : "1h";
        }
        if (offHand) return prefix + "slasho";
        return Pick(data, prefix, right, roll);
    }

    /// <summary>slashl, slashr or stab, by the weapon's %AnimSlash weights.</summary>
    static string Pick(INwnBridgeData data, string prefix, int weapon, int roll)
    {
        int Weight(string column, int fallback) =>
            weapon >= 0 && int.TryParse(data.TwoDA("baseitems", weapon, column), out var v) ? v : fallback;
        int l = Weight("%AnimSlashL", 34), r = Weight("%AnimSlashR", 33), s = Weight("%AnimSlashS", 33);
        int total = Math.Max(1, l + r + s);
        int pick = ((roll % total) + total) % total;
        return prefix + (pick < l ? "slashl" : pick < l + r ? "slashr" : "stab");
    }

    /// <summary>
    /// The weaponsounds.2da sounds of a blow: the weapon's row (its WeaponMatType, or the attacker's sound set for
    /// the clip without one), the hit sounds of what the target wears, the critical sound, or the parry or miss.
    /// </summary>
    public static string[] Sounds(INwnBridgeData data, int weapon, int attackerAppearance, string clip, int targetAppearance, int targetArmour,
        bool hit, bool critical, bool parried, bool ranged, int roll)
    {
        int Int(string table, int row, string column, int fallback) =>
            row >= 0 && int.TryParse(data.TwoDA(table, row, column), out var v) ? v : fallback;
        int row;
        if (weapon >= 0) row = Int("baseitems", weapon, "WeaponMatType", 0);
        else
        {
            string column = clip switch
            {
                var s when s.EndsWith("slashl") => "WeapTypeL",
                var s when s.EndsWith("slashr") => "WeapTypeR",
                var s when s.EndsWith("closel") => "WeapTypeClsLw",
                var s when s.EndsWith("closeh") => "WeapTypeClsH",
                var s when s.EndsWith("reach") => "WeapTypeRch",
                _ => "WeapTypeS",
            };
            row = Int("appearancesndset", Int("appearance", attackerAppearance, "SOUNDAPPTYPE", 0), column, 0);
        }
        string Cell(string column) => data.TwoDA("weaponsounds", row, column) is { Length: > 0 } s && s != "****" ? s : "";
        string Either(string a, string b) => roll % 2 == 0 ? Cell(a) : Cell(b);
        var played = new List<string>();
        if (ranged) played.Add(Either("Miss0", "Miss1"));
        if (hit)
        {
            string armour = Armour(data, targetAppearance, targetArmour);
            played.Add(Cell(armour + "0"));
            played.Add(Cell(armour + "1"));
            if (critical) played.Add(Cell("Critical0"));
        }
        else if (parried) played.Add(Cell("Parry0"));
        else if (!ranged) played.Add(Either("Miss0", "Miss1"));
        return played.Where(s => s.Length > 0).Distinct().ToArray();
    }

    /// <summary>What a blow lands on: the appearance's sound set armour, or the armour worn (its base AC).</summary>
    static string Armour(INwnBridgeData data, int appearance, int armourAc)
    {
        int set = int.TryParse(data.TwoDA("appearance", appearance, "SOUNDAPPTYPE"), out var s) ? s : 0;
        return data.TwoDA("appearancesndset", set, "ArmorType").ToLowerInvariant() switch
        {
            "" or "****" => armourAc >= 6 ? "Plate" : armourAc >= 4 ? "Chain" : "Leather",
            "chain" => "Chain",
            "plate" => "Plate",
            "stone" or "crystal" => "Stone",
            "wood" => "Wood",
            "chitin" => "Chitin",
            "scale" => "Scale",
            "ethereal" => "Ethereal",
            _ => "Leather",
        };
    }
}

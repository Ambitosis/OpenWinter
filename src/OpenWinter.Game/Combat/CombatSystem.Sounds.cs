using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Combat;

public sealed partial class CombatSystem
{
    /// <summary>
    /// The sounds an attack makes, from weaponsounds.2da, as the original plays them (parity/visual/scenes
    /// combatsound and combatarmor, recorded with tools/visual/combat_audio.py):
    /// <list type="bullet">
    /// <item>The row is the weapon's baseitems WeaponMatType, or for a creature fighting without one its
    /// appearancesndset WeapType for the swing (Unarmed when it has none).</item>
    /// <item>A hit plays both of the row's sounds for what it lands on (Leather0 and Leather1 together), plus
    /// Critical0 on a critical hit. The column is the target's appearancesndset ArmorType, or for a humanoid its
    /// body armour's base AC: 0-3 leather (also unarmoured), 4-5 chain, 6-8 plate.</item>
    /// <item>A miss plays one of Miss0 and Miss1 at random; a parried blow plays Parry0.</item>
    /// </list>
    /// Not measured: ranged attacks (the bow rows have only their shot sounds, played here on every shot), doors and
    /// placeables (taken as wood) and crystal creatures (taken as stone).
    /// </summary>
    public string[] AttackSounds(AttackOutcome o)
    {
        var a = o.Context;
        var sounds = _world.Resources.TwoDA("weaponsounds");
        int row = WeaponSoundRow(a, o.Animation);
        string Cell(string column) => sounds.Get(row, column) is { Length: > 0 } s && s != "****" ? s : "";
        string Either(string a0, string a1) => Random.Shared.Next(2) == 0 ? Cell(a0) : Cell(a1);
        var played = new List<string>();
        if (a.Ranged) played.Add(Either("Miss0", "Miss1"));
        if (o.Hit)
        {
            string column = ArmourSoundColumn(a.Target);
            played.Add(Cell(column + "0"));
            played.Add(Cell(column + "1"));
            if (o.Critical) played.Add(Cell("Critical0"));
        }
        else if (o.Parried) played.Add(Cell("Parry0"));
        else if (!a.Ranged) played.Add(Either("Miss0", "Miss1"));
        return played.Where(s => s.Length > 0).Distinct().ToArray();
    }

    int WeaponSoundRow(AttackContext a, string animation)
    {
        if (a.Weapon != null)
            return _world.Resources.TwoDA("baseitems").GetInt(a.Weapon.BaseItem, "WeaponMatType", 0);
        int set = _world.Resources.TwoDA("appearance").GetInt(a.Attacker.AppearanceType, "SOUNDAPPTYPE", 0);
        string column = animation switch
        {
            var s when s.EndsWith("slashl") => "WeapTypeL",
            var s when s.EndsWith("slashr") => "WeapTypeR",
            var s when s.EndsWith("closel") => "WeapTypeClsLw",
            var s when s.EndsWith("closeh") => "WeapTypeClsH",
            var s when s.EndsWith("reach") => "WeapTypeRch",
            _ => "WeapTypeS",
        };
        return _world.Resources.TwoDA("appearancesndset").GetInt(set, column, 0);
    }

    /// <summary>The weaponsounds column prefix for what a blow lands on (see <see cref="AttackSounds"/>).</summary>
    string ArmourSoundColumn(GameObject target)
    {
        if (target is not Creature c) return "Wood";
        int set = _world.Resources.TwoDA("appearance").GetInt(c.AppearanceType, "SOUNDAPPTYPE", 0);
        var type = _world.Resources.TwoDA("appearancesndset").Get(set, "ArmorType").ToLowerInvariant();
        if (type is "" or "****")
        {
            int ac = CreatureRules.ArmourBaseAC(_world, c);
            return ac >= 6 ? "Plate" : ac >= 4 ? "Chain" : "Leather";
        }
        return type switch
        {
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

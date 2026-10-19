using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Spells;

/// <summary>
/// The chat lines a spell cast prints, measured on the original with the castlines and castlines2 scenes
/// (docs/PARTY.md, "Spell-casting lines"):
/// <list type="bullet">
/// <item>"&lt;caster&gt; casting &lt;spell&gt;" (dialog.tlk 8127) when the cast starts and "&lt;caster&gt; casts &lt;spell&gt;"
/// (1501) at its release, about two seconds later for Barkskin.</item>
/// <item>The caster's own player always reads the spell's name. Every other player rolls Spellcraft once per cast:
/// d20 + her Spellcraft rank (a specialist wizard +2 for her school, -5 for her opposition school; not measured)
/// against 15 + the spell's innate level names it, otherwise both lines say "unknown spell"
/// (1502). That holds for her own henchman's casts too. (Rank 7 named 63% of Barkskins, 12 named 7 of 10, 17 and 57
/// every one: DC 15 + level fits, 10 + level does not.)</item>
/// <item>"casting" reaches players up to <see cref="CastingRange"/> away, "casts" up to <see cref="CastsRange"/>.</item>
/// <item>Instant casts (bInstantSpell), fake casts, items, creature abilities and feats print none; cheat casts do.
/// Items say "&lt;user&gt; uses &lt;item&gt;" instead, abilities and feats their AltMessage (castlines3, castlines4).</item>
/// </list>
/// Not measured: creatures' spells at a target (they never started in the original's scenes).
/// </summary>
public static class CastingLines
{
    /// <summary>"casting" was printed for casters 35 m away and not 36 m.</summary>
    public const float CastingRange = 35.5f;
    /// <summary>"casts" was printed for casters 28 m away and not 30 m.</summary>
    public const float CastsRange = 29f;

    public const int CastingStrref = 8127, CastsStrref = 1501, UnknownStrref = 1502;

    /// <summary>
    /// How far an ability's AltMessage and an item's "uses" line carry (<see cref="AltMessage"/>, <see cref="Uses"/>):
    /// both were read 30 m away and not 35 m (a potion) or 40 m (an ability), castlines4.
    /// </summary>
    public const float AltMessageRange = 32.5f, UsesRange = 32.5f;

    /// <summary>
    /// The "casting" line to the players near a caster; returns whether each of them identified the spell (kept for the
    /// "casts" line at release).
    /// </summary>
    public static Dictionary<uint, bool>? Start(World w, GameObject caster, int spell, Func<int, int>? random = null)
    {
        if (w.Output == null || caster.Area == null) return null;
        var known = new Dictionary<uint, bool>();
        foreach (var pc in Players(w, caster, CastingRange))
        {
            bool named = Identifies(w, pc, caster, spell, random);
            known[pc.Id] = named;
            w.Output.SendMessage(pc, Line(w, caster, spell, CastingStrref, named));
        }
        return known;
    }

    /// <summary>The "casts" line at release, with each player's identification from the start (rolled now for a newcomer).</summary>
    public static void Release(World w, GameObject caster, int spell, Dictionary<uint, bool>? known, Func<int, int>? random = null)
    {
        if (w.Output == null || caster.Area == null || known == null) return;
        foreach (var pc in Players(w, caster, CastsRange))
        {
            if (!known.TryGetValue(pc.Id, out bool named)) known[pc.Id] = named = Identifies(w, pc, caster, spell, random);
            w.Output.SendMessage(pc, Line(w, caster, spell, CastsStrref, named));
        }
    }

    /// <summary>
    /// A creature ability's or feat's spells.2da AltMessage ("&lt;CUSTOM0&gt; is surrounded by an aura.") to the players
    /// within <see cref="AltMessageRange"/> of the caster (her own player wherever she stands).
    /// </summary>
    public static void AltMessage(World w, GameObject caster, int spell)
    {
        if (w.Output == null || caster.Area == null) return;
        if (SpellTable.Table(w).GetInt(spell, "AltMessage", -1) is not (>= 0 and var alt)
            || w.TlkStringOverridden(alt) is not { Length: > 0 } line) return;
        string text = line.Replace("<CUSTOM0>", Name(w, caster));
        foreach (var pc in Players(w, caster, AltMessageRange)) w.Output.SendMessage(pc, text);
    }

    /// <summary>
    /// "&lt;user&gt; uses &lt;item&gt;" (dialog.tlk 63264) when a creature uses a potion, scroll or wand, to the players
    /// within <see cref="UsesRange"/> of it (her own player wherever she stands). castlines3 and castlines4: an NPC's
    /// potion was read at 5, 20, 25 and 30 m and not at 35 or 40 m; a wizard's wand and the player's scroll and wand
    /// printed it too (no "casting" / "casts" lines for any item).
    /// </summary>
    public static void Uses(World w, Creature user, Item item)
    {
        if (w.Output == null || user.Area == null) return;
        string text = $"{Name(w, user)} {w.TlkStringOverridden(63264)} {item.Name.Resolve(w.Resources.Talk)}";
        foreach (var pc in Players(w, user, UsesRange)) w.Output.SendMessage(pc, text);
    }

    static string Name(World w, GameObject o) => o.DisplayName(w.Resources.Talk) is { Length: > 0 } n ? n : o.Tag;

    /// <summary>The players in the caster's area within a range of it (the caster's own player wherever she stands).</summary>
    static IEnumerable<Creature> Players(World w, GameObject caster, float range) =>
        w.ConnectedPCs.Where(pc => pc.Player?.FamiliarMaster == null && pc.Player != null
            && (pc == caster || pc.Area == caster.Area && Vector3.Distance(pc.Position, caster.Position) <= range)).ToList();

    /// <summary>Whether a player reads the spell's name: her own casts always; anyone else's on a Spellcraft roll.</summary>
    public static bool Identifies(World w, Creature pc, GameObject caster, int spell, Func<int, int>? random = null)
    {
        if (pc == caster) return true;
        int rank = CreatureRules.SkillRank(w, pc, Skills.SkillIds.Spellcraft);
        if (rank < 0 || !CreatureRules.HasSkill(w, pc, Skills.SkillIds.Spellcraft)) return false;
        int roll = (random ?? (n => Random.Shared.Next(n)))(20) + 1;
        return roll + rank + SchoolBonus(w, pc, spell) >= 15 + SpellTable.Innate(w, spell);
    }

    /// <summary>
    /// A specialist wizard identifies spells of her own school at +2 and of her opposition school (spellschools.2da
    /// Opposition) at -5 (not measured: the pregenerated players aren't specialists).
    /// </summary>
    static int SchoolBonus(World w, Creature pc, int spell)
    {
        int school = SpellTable.School(w, spell), bonus = 0;
        foreach (var cc in pc.Classes)
        {
            if (cc.Class != WizardClass || cc.School <= 0) continue;
            if (school == cc.School) bonus += 2;
            if (int.TryParse(w.Resources.TwoDA("spellschools").Get(cc.School, "Opposition"), out int opposed) && school == opposed) bonus -= 5;
        }
        return bonus;
    }

    const int WizardClass = 10;

    static string Line(World w, GameObject caster, int spell, int verb, bool named)
    {
        string name = caster.DisplayName(w.Resources.Talk) is { Length: > 0 } n ? n : caster.Tag;
        string what = named ? SpellName(w, spell) : w.TlkStringOverridden(UnknownStrref);
        return $"{name} {w.TlkStringOverridden(verb)} {what}";
    }

    static string SpellName(World w, int spell) =>
        int.TryParse(SpellTable.Table(w).Get(spell, "Name"), out var strref) && w.TlkStringOverridden(strref) is { Length: > 0 } s
            ? s : SpellTable.Table(w).Get(spell, "Label");
}

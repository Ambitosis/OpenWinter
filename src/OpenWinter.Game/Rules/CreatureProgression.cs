using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>Experience, level loss and alignment.</summary>
public static partial class CreatureRules
{
    /// <summary>
    /// The highest character level: 40, or 41-60 from NWNX_MAXLEVEL_MAX as the NWNX_MaxLevel plugin reads it (the
    /// levels' XP then comes from exptable.2da rows beyond 40, and feats and ability increases follow their usual
    /// progression).
    /// </summary>
    public static readonly int MaxLevel = MaxLevelFrom(Environment.GetEnvironmentVariable("NWNX_MAXLEVEL_MAX"));

    public const int StandardMaxLevel = 40;

    /// <summary>The level cap a NWNX_MAXLEVEL_MAX value gives: 41 to 60 raise it; anything else leaves 40.</summary>
    public static int MaxLevelFrom(string? value) =>
        int.TryParse(value?.Trim(), out int n) && n > StandardMaxLevel && n <= 60 ? n : StandardMaxLevel;

    /// <summary>XP needed to reach a character level (exptable.2da); a level the table has no row for can't be reached.</summary>
    public static int XPForLevel(World w, int level)
    {
        if (level <= 1) return 0;
        var t = w.Resources.TwoDA("exptable");
        int row = level - 1;
        return row >= t.RowCount ? int.MaxValue : t.GetInt(row, "XP", int.MaxValue);
    }

    /// <summary>The highest level an amount of XP allows.</summary>
    public static int LevelForXP(World w, int xp)
    {
        int level = 1;
        while (level < MaxLevel && XPForLevel(w, level + 1) <= xp) level++;
        return level;
    }

    /// <summary>True when the creature has enough XP for its next level (the level-up itself is the player's choice).</summary>
    public static bool CanLevelUp(World w, Creature c) => c.TotalLevel < MaxLevel && c.Experience >= XPForLevel(w, c.TotalLevel + 1);

    /// <summary>
    /// SetXP: sets experience. Raising it only makes level-ups available; lowering it below the
    /// current level's requirement removes levels (using the character's level history).
    /// A player is told the change as the original does (levelup scene): "Experience Points Gained:  N" or
    /// "Experience Points Lost:  N" (TLK 8263 / 8264), and, when a level first becomes available, TLK 8302 in
    /// the chat with "*Level Up!*" (TLK 53242) over the head and gui_level_up (both client side).
    /// </summary>
    public static void SetXP(World w, Creature c, int xp)
    {
        EnsureInit(w, c);
        xp = Math.Max(0, xp);
        // NWNX_ON_SET_EXPERIENCE: a skip keeps the experience, or sets its result instead (nwnx_events_creature).
        if (Nwnx.NwnxEvent.Begin(w, "NWNX_ON_SET_EXPERIENCE", c)?.Add("XP", xp) is { } ev)
        {
            bool skipped = ev.Before();
            if (!skipped) SetXPNow(w, c, xp);
            else if (ev.HasResult && int.TryParse(ev.Result, out var r)) SetXPNow(w, c, Math.Max(0, r));
            ev.After();
            return;
        }
        SetXPNow(w, c, xp);
    }

    static void SetXPNow(World w, Creature c, int xp)
    {
        bool couldLevel = CanLevelUp(w, c);
        int before = c.Experience;
        c.Experience = xp;
        int allowed = LevelForXP(w, xp);
        bool lost = false;
        while (c.TotalLevel > allowed && c.TotalLevel > 1 && LevelDown(w, c)) lost = true;
        if (lost) SyncHitPoints(w, c);
        if (c.IsPC)
        {
            if (xp != before)
                w.Output?.SendFeedback(c, xp > before ? $"{Tlk(w, 8263, "Experience Points Gained: ")} {xp - before}" : $"{Tlk(w, 8264, "Experience Points Lost: ")} {before - xp}", FeedbackKind.Message, xp > before ? 182 : 183);
            bool canLevel = CanLevelUp(w, c);
            bool ready = canLevel && !couldLevel;
            w.Output?.Notify("xp-changed", c, new
            {
                xp,
                level = c.TotalLevel,
                nextLevelXp = c.TotalLevel < MaxLevel ? XPForLevel(w, c.TotalLevel + 1) : -1,
                canLevelUp = canLevel,
                levelLost = lost,
                // A level just became available: the client floats this over the player and plays gui_level_up.
                levelUpReady = ready,
                creature = c.Id,
                feedback = ready ? Tlk(w, 53242, "Level Up!") : "",
            }, c);
            if (ready) w.Output?.SendFeedback(c, Tlk(w, 8302, "Your character has gained enough experience to advance a level."), FeedbackKind.Message, 11);
        }
        if (lost) w.MarkChanged(c);
    }

    // Module TLK overrides (SetTlkOverride) apply, as to every engine string.
    static string Tlk(World w, int strref, string fallback) => w.TlkStringOverridden(strref) is { Length: > 0 } s ? s : fallback;

    /// <summary>
    /// Adds XP (non-positive amounts are ignored). Not scaled by the module XP scale, like the original. GiveXPToCreature
    /// and kill experience pass <paramref name="multiclassPenalty"/>: the original takes 20% off for each class out of
    /// step (killxp2: a wizard 1 / rogue 1 / fighter 3 elf given 100 got 80, given 7 got 5).
    /// </summary>
    public static void GiveXP(World w, Creature c, int amount, bool multiclassPenalty = false)
    {
        if (multiclassPenalty) amount = (int)decimal.Truncate(amount * MulticlassXPFactor(w, c));
        if (amount <= 0) return;
        long total = (long)c.Experience + amount;
        SetXP(w, c, (int)Math.Min(int.MaxValue, total));
    }

    /// <summary>
    /// The multiclass experience factor: 20% off for each class more than one level below the highest, counting only
    /// classes with classes.2da XPPenalty (not prestige classes) and leaving out the race's favoured class
    /// (racialtypes.2da Favored; "****" means the highest class). Measured once: an elf wizard 1 / rogue 1 /
    /// fighter 3 gets 80% (killxp, killxp2).
    /// </summary>
    public static decimal MulticlassXPFactor(World w, Creature c)
    {
        var classes = w.Resources.TwoDA("classes");
        var races = w.Resources.TwoDA("racialtypes");
        int favored = c.Race < races.RowCount ? races.GetInt(c.Race, "Favored", -1) : -1;
        var counted = c.Classes.Where(cc => cc.Level > 0 && cc.Class != favored && classes.GetInt(cc.Class, "XPPenalty", 0) != 0)
            .Select(cc => cc.Level).OrderByDescending(l => l).ToList();
        // With no fixed favoured class, the highest class is the favoured one.
        if (favored < 0 && counted.Count > 0) counted.RemoveAt(0);
        if (counted.Count < 2) return 1m;
        int highest = counted[0];
        int behind = counted.Count(l => highest - l > 1);
        return Math.Max(0m, 1m - 0.2m * behind);
    }

    /// <summary>Loses the most recent level between NWNX_ON_LEVEL_DOWN_BEFORE and _AFTER.</summary>
    public static bool LevelDown(World w, Creature c)
    {
        var ev = LevelDownBefore(w, c);
        bool ok = RemoveLastLevel(w, c);
        ev?.After();
        return ok;
    }

    /// <summary>NWNX_ON_LEVEL_DOWN's _BEFORE half, for level losses made some other way (the caller runs After).</summary>
    public static Nwnx.NwnxEvent? LevelDownBefore(World w, Creature c)
    {
        var ev = Nwnx.NwnxEvent.Begin(w, "NWNX_ON_LEVEL_DOWN", c);
        ev?.Before();
        return ev;
    }

    /// <summary>Undoes the most recent level from the level history (LvlStatList). False if there is no history to undo.</summary>
    public static bool RemoveLastLevel(World w, Creature c)
    {
        var history = c.Template.GetList("LvlStatList");
        if (history.Count == 0 || history.Count != c.TotalLevel) return false;
        var entry = history[^1];
        int cls = (int)entry.GetInt("LvlStatClass");
        int ci = ClassIndex(c, cls);
        if (ci < 0) return false;

        var cc = c.Classes[ci];
        cc.Level--;
        if (cc.Level <= 0) c.Classes.RemoveAt(ci);
        c.BaseHitPoints -= (int)entry.GetInt("LvlStatHitDie");
        var skills = entry.GetList("SkillList");
        for (int i = 0; i < skills.Count && i < c.Skills.Length; i++)
            c.Skills[i] = Math.Max(0, c.Skills[i] - (sbyte)skills[i].GetInt("Rank"));
        foreach (var f in entry.GetList("FeatList"))
        {
            int feat = (int)f.GetInt("Feat");
            c.Feats.Remove(feat);
            c.FeatUses.Remove(feat);
        }
        if (entry.Has("LvlStatAbility"))
        {
            int ab = (int)entry.GetInt("LvlStatAbility");
            if (ab >= 0 && ab < 6) c.Abilities[ab]--;
        }
        if (cc.Level > 0)
            for (int lvl = 0; lvl < 10; lvl++)
                foreach (var k in entry.GetList($"KnownList{lvl}"))
                    cc.KnownSpells[lvl].Remove((int)k.GetInt("Spell"));
        history.RemoveAt(history.Count - 1);
        return true;
    }

    /// <summary>
    /// The benefits of a full rest (ForceRest): effects end (all but permanent supernatural and unyielding
    /// ones), then hit points, spells, spell-like abilities and feat uses are restored.
    /// </summary>
    public static void Rest(World w, Creature c)
    {
        EnsureInit(w, c);
        var k = Scripting.EffectConstants.Of(w);
        w.System<EffectSystem>().RemoveWhere(c, e => e.SubType != k.Unyielding && !(e.SubType == k.Supernatural && e.DurationType == k.Permanent));
        // A polymorph that resting ended changes the creature back at once.
        w.System<EffectSystem>().FinishPolymorphEnd(c);
        SyncHitPoints(w, c);
        // Full hit points: the maximum (a creature over it, as a polymorph can leave one, comes down to it).
        c.CurrentHitPoints = c.MaxHitPoints;
        RestoreSpells(w, c);
        RestoreFeatUses(w, c);
        w.MarkChanged(c);
    }

    // ------------------------------------------------------------------ alignment

    /// <summary>ALIGNMENT_LAWFUL/NEUTRAL/CHAOTIC for a law/chaos value (70+ lawful, 30- chaotic).</summary>
    public static int LawChaos(int value) => value >= 70 ? NwRules.AlignLawful : value <= 30 ? NwRules.AlignChaotic : NwRules.AlignNeutral;
    public static int GoodEvil(int value) => value >= 70 ? NwRules.AlignGood : value <= 30 ? NwRules.AlignEvil : NwRules.AlignNeutral;

    /// <summary>
    /// AdjustAlignment. With bAllPartyMembers the subject's party (other players in its party
    /// faction) shift by half as much.
    /// </summary>
    public static void AdjustAlignment(World w, Creature c, int alignment, int shift, bool allParty)
    {
        Shift(w, c, alignment, shift);
        if (!allParty || !c.IsPC) return;
        foreach (var other in Society.PartyService.Of(w).PartyOf(c))
            if (other != c) Shift(w, other, alignment, shift / 2);
    }

    static void Shift(World w, Creature c, int alignment, int shift)
    {
        int lc = c.LawfulChaotic, ge = c.GoodEvil;
        switch (alignment)
        {
            case NwRules.AlignAll: lc += shift; ge += shift; break;
            case NwRules.AlignLawful: lc += shift; break;
            case NwRules.AlignChaotic: lc -= shift; break;
            case NwRules.AlignGood: ge += shift; break;
            case NwRules.AlignEvil: ge -= shift; break;
            case NwRules.AlignNeutral:
                lc = TowardNeutral(lc, shift);
                ge = TowardNeutral(ge, shift);
                break;
            default: return;
        }
        lc = Math.Clamp(lc, 0, 100);
        ge = Math.Clamp(ge, 0, 100);
        if (lc == c.LawfulChaotic && ge == c.GoodEvil) return;
        int oldLc = c.LawfulChaotic, oldGe = c.GoodEvil;
        c.LawfulChaotic = lc;
        c.GoodEvil = ge;
        if (!c.IsPC || w.Output == null) return;
        if (ge != oldGe) w.Output.SendMessage(c, $"Your alignment has shifted towards {(ge > oldGe ? "Good" : "Evil")}.");
        if (lc != oldLc) w.Output.SendMessage(c, $"Your alignment has shifted towards {(lc > oldLc ? "Law" : "Chaos")}.");
        if (GoodEvil(ge) != GoodEvil(oldGe) || LawChaos(lc) != LawChaos(oldLc))
            w.Output.Notify("alignment-changed", c, new { lawChaos = lc, goodEvil = ge }, c);
    }

    static int TowardNeutral(int value, int shift)
    {
        shift = Math.Abs(shift);
        return value < 50 ? Math.Min(50, value + shift) : value > 50 ? Math.Max(50, value - shift) : 50;
    }
}

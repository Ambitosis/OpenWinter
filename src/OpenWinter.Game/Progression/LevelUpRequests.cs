using System.Text.Json;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Progression;

/// <summary>
/// Level-up from the character sheet: "levelup-data" (classes the player may take, with reasons
/// for the ones they can't; with a "choice" it answers "levelup-plan" with the options and errors of
/// that choice, completed from the package when "auto" is set) and "levelup-apply".
/// </summary>
public static class LevelUpRequests
{
    [ClientRequest("levelup-data")]
    static void Data(Request r)
    {
        var w = r.World;
        var c = r.Player;
        if (r.Element("choice") is { ValueKind: JsonValueKind.Object } json)
        {
            var ch = LevelChoice.FromJson(json);
            LevelChoice? auto = null;
            if (r.Bool("auto") && ProgressionRules.ClassExists(w, ch.Class)) ch = auto = PackageBuilder.Complete(w, c, ch);
            var plan = ProgressionRules.Plan(w, c, ch);
            w.Output?.Notify("levelup-plan", c, ChargenData.Plan(w, plan, auto), c);
            if (r.Bool("summary") && ProgressionRules.ClassExists(w, ch.Class)) w.Output?.Notify("levelup-summary", c, Summary(w, c, ch, plan), c);
            return;
        }
        // Opening the level-up window: NWNX_ON_CLIENT_LEVEL_UP_BEGIN; a skip keeps it shut.
        var ev = Nwnx.NwnxEvent.Begin(w, "NWNX_ON_CLIENT_LEVEL_UP_BEGIN", c);
        if (ev?.Before() == true) { ev.After(); return; }
        w.Output?.Notify("levelup-data", c, Summary(w, c), c);
        ev?.After();
    }

    [ClientRequest("levelup-apply")]
    static void Apply(Request r)
    {
        var ch = r.Element("choice") is { ValueKind: JsonValueKind.Object } json ? LevelChoice.FromJson(json) : new LevelChoice();
        // NWNX_ON_LEVEL_UP around the player's level-up; a skip refuses it.
        var ev = Nwnx.NwnxEvent.Begin(r.World, "NWNX_ON_LEVEL_UP", r.Player);
        if (ev?.Before() == true) { ev.After(); return; }
        var errors = LevelUp(r.World, r.Player, ch);
        ev?.After();
        r.World.Output?.Notify("levelup-result", r.Player, new
        {
            ok = errors.Count == 0,
            errors = errors.Select(e => new { code = e.Code, message = e.Message }).ToArray(),
            level = r.Player.TotalLevel,
        }, r.Player);
        if (errors.Count == 0) r.World.Output?.Notify("character-sheet", r.Player, CharacterRequests.Sheet(r.World, r.Player), r.Player);
    }

    sealed class PendingRoll { public int Class, Level, Die; }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Creature, PendingRoll> Pending = new();

    /// <summary>The hit die a summary rolled for this class and character level, if it showed one.</summary>
    public static int? HeldHitDie(Creature c, int cls, int level) =>
        Pending.TryGetValue(c, out var held) && held.Class == cls && held.Level == level ? held.Die : null;

    /// <summary>
    /// The level-up summary (the classic window's last step): the hit points the level gives, rolled now and kept for
    /// the level when it is taken (the original shows the figure before the level is confirmed).
    /// </summary>
    static object Summary(World w, Creature c, LevelChoice ch, LevelPlan plan)
    {
        var opts = w.System<ProgressionSystem>();
        // One roll per class and level: asking for the summary again shows the same figure rather than rolling anew.
        int die;
        if (Pending.TryGetValue(c, out var held) && held.Class == ch.Class && held.Level == plan.NewCharacterLevel) die = held.Die;
        else
        {
            die = LevelApplier.HitPointsForLevel(w, plan.NewCharacterLevel, ProgressionRules.HitDie(w, ch.Class), opts.AlwaysMaxHitPoints, opts.Random);
            Pending.AddOrUpdate(c, new PendingRoll { Class = ch.Class, Level = plan.NewCharacterLevel, Die = die });
        }
        return new
        {
            @class = ch.Class,
            hitPoints = Math.Max(1, die + CreatureRules.Modifier(CreatureRules.AbilityScore(w, plan.After, NwRules.AbilityCon, baseOnly: true))),
        };
    }

    /// <summary>What the level-up window opens with: level, XP and every class with whether it can be taken.</summary>
    public static object Summary(World w, Creature c)
    {
        var classes = w.Resources.TwoDA("classes");
        var rows = Enumerable.Range(0, classes.RowCount).Where(cls => ProgressionRules.IsPlayerClass(w, cls) || CreatureRules.ClassLevel(c, cls) > 0)
            .Select(cls =>
            {
                var errors = ProgressionRules.ClassErrors(w, c, cls, player: true);
                return new
                {
                    id = cls,
                    level = CreatureRules.ClassLevel(c, cls),
                    available = errors.Count == 0,
                    reasons = errors.Select(e => e.Message).ToArray(),
                };
            }).ToArray();
        return new
        {
            canLevelUp = CreatureRules.CanLevelUp(w, c),
            level = c.TotalLevel,
            xp = c.Experience,
            nextLevelXp = c.TotalLevel < CreatureRules.MaxLevel ? CreatureRules.XPForLevel(w, c.TotalLevel + 1) : -1,
            abilityIncrease = (c.TotalLevel + 1) % 4 == 0,
            abilities = Enumerable.Range(0, 6).Select(i => CreatureRules.AbilityScore(w, c, i, baseOnly: true)).ToArray(),
            skillPoints = (int)c.Template.GetInt("SkillPoints"),
            classes = rows,
            // For the classic level-up window: the title's name and classes, the class taken last (selected when the
            // class list opens: l20), the race, the scores before racial adjustments, the ranks the character has and
            // the associates it has (offered again at the levels that grant them).
            name = c.Name.Resolve(w.Resources.Talk),
            race = c.Race,
            classLevels = c.Classes.Select(k => new { id = k.Class, level = k.Level }).ToArray(),
            lastClass = LastClass(c),
            baseAbilities = c.Abilities.ToArray(),
            ranks = c.Skills.ToArray(),
            familiar = c.FamiliarType, familiarName = c.FamiliarName,
            companion = c.CompanionType, companionName = c.CompanionName,
        };
    }

    /// <summary>The class of the character's last level (its level history), else its first class.</summary>
    static int LastClass(Creature c)
    {
        var history = c.Template.GetList("LvlStatList");
        if (history.Count > 0 && history[^1].Has("LvlStatClass")) return (int)history[^1].GetInt("LvlStatClass");
        return c.Classes.Count > 0 ? c.Classes[^1].Class : -1;
    }

    /// <summary>
    /// A player takes a level: requires the experience for it, validates every choice, applies it and
    /// runs the module's OnPlayerLevelUp (GetPCLevellingUp). Returns the broken rules (empty on success).
    /// </summary>
    public static List<RuleError> LevelUp(World w, Creature c, LevelChoice ch)
    {
        if (!CreatureRules.CanLevelUp(w, c)) return new List<RuleError> { new("xp", "You don't have enough experience to gain a level.") };
        // A polymorphed player can't level up (TLK 112227). LevelUpHenchman still levels a polymorphed NPC
        // (parity suite fx_poly_abilities: level.bear).
        if (c.PolymorphForm != null)
            return new List<RuleError> { new("polymorph", w.Resources.TlkString(112227) is { Length: > 0 } t ? t : "You cannot level up while polymorphed.") };
        var plan = ProgressionRules.Plan(w, c, ch);
        if (!plan.Ok) return plan.Errors;
        // The roll a summary showed for this level (none: rolled now).
        int? die = Pending.TryGetValue(c, out var roll) && roll.Class == ch.Class && roll.Level == plan.NewCharacterLevel ? roll.Die : null;
        Pending.Remove(c);
        LevelApplier.Apply(w, c, ch, plan, die);
        // The original writes nothing in the chat for a level taken (levelup scene).
        if (c.IsPC)
        {
            w.Output?.Notify("xp-changed", c, new
            {
                xp = c.Experience,
                level = c.TotalLevel,
                nextLevelXp = c.TotalLevel < CreatureRules.MaxLevel ? CreatureRules.XPForLevel(w, c.TotalLevel + 1) : -1,
                canLevelUp = CreatureRules.CanLevelUp(w, c),
                levelLost = false,
            }, c);
        }
        w.RunEvent(w.Module, "Mod_OnPlrLvlUp", new Scripting.EventContext().Set("LevellingUp", c.Id));
        return new List<RuleError>();
    }
}

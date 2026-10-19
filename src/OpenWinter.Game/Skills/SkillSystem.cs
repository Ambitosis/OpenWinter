using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Skills;

/// <summary>One skill check: d20 (or a fixed 20) plus the creature's bonus against a DC.</summary>
public readonly record struct SkillRoll(int Skill, int Roll, bool Take20, int Bonus, int Dc, bool Success)
{
    /// <summary>Roll (20 when taking 20) plus bonus.</summary>
    public int Total => (Take20 ? 20 : Roll) + Bonus;
    /// <summary>How far the total beat (positive) or missed (negative) the DC.</summary>
    public int Margin => Total - Dc;
}

/// <summary>
/// The skills package: skill checks with combat-log feedback, the actions behind ActionUseSkill
/// (Open Lock, trap handling, Pick Pocket, Taunt, Heal, Animal Empathy), stealth and detect modes and
/// their opposed Hide/Move Silently vs Spot/Listen checks, passive trap searching, Lore identification,
/// and hooks other packages call (Use Magic Device, Parry, Appraise).
/// <para>
/// Behaviour follows the game's own skill descriptions (dialog.tlk) and ruleset.2da: Open Lock, Disable
/// Trap and Set Trap take 20 out of combat (Skill Mastery: always); opposed stealth checks are rolled once
/// per round; passive Search uses half the skill (Keen Sense: full); only rogues detect traps above DC 35
/// or disarm traps of DC 35 and more.
/// </para>
/// </summary>
public sealed partial class SkillSystem : IWorldSystem
{
    readonly World _world;

    /// <summary>Opposed and passive checks are re-rolled once per combat round.</summary>
    public const float RoundSeconds = 6f;

    /// <summary>Rolls a die: returns 1..<c>sides</c>. Tests replace this for deterministic results.</summary>
    public Func<int, int> Dice = sides => Random.Shared.Next(1, Math.Max(1, sides) + 1);

    /// <summary>Open Lock, Disable/Recover Trap and Set Trap take 20 when the user isn't in combat.</summary>
    public bool TakeTwentyOutOfCombat = true;

    public SkillSystem(World world)
    {
        _world = world;
        world.ObjectRemoved += Forget;
    }

    /// <summary>After perception (10) and traps (15): stealth visibility goes out once perception has settled.</summary>
    public int Order => 20;

    public void Step(float dt)
    {
        StepSearch();
        StepStealthVisibility();
        StepDomination();
    }

    void Forget(GameObject o)
    {
        ForgetStealth(o.Id);
        _nextSearch.Remove(o.Id);
        _dominated.Remove(o.Id);
    }

    // ------------------------------------------------------------------ checks

    /// <summary>GetSkillRank (ranks + ability + feats + items/effects - armour check penalty), or -1 if the creature can't use the skill.</summary>
    public int Rank(Creature c, int skill) => CreatureRules.SkillRank(_world, c, skill);

    /// <summary>The skill modifier a check adds: <see cref="Rank"/>, with an unusable skill counting as 0 (penalties stay negative).</summary>
    public int Bonus(Creature c, int skill)
    {
        int r = Rank(c, skill);
        return r == -1 && !CanUse(c, skill) ? 0 : r;
    }

    /// <summary>Base ranks only (synergy and the trained-only test look at these).</summary>
    public static int Ranks(Creature c, int skill) => CreatureRules.SkillRankBase(c, skill);

    /// <summary>Whether the creature may use the skill at all (trained-only skills need a rank).</summary>
    public bool CanUse(Creature c, int skill) => CreatureRules.HasSkill(_world, c, skill);

    /// <summary>In combat for skill purposes: attacking or flagged as fighting.</summary>
    public bool InCombat(Creature c) => c.InCombat || _world.System<CombatSystem>().IsInCombat(c);

    /// <summary>Open Lock, Disable Trap and Set Trap: take 20 out of combat, always with Skill Mastery.</summary>
    public bool TakesTwenty(Creature c) =>
        CreatureRules.HasFeat(_world, c, SkillIds.FeatSkillMastery, ignoreUses: true) || (TakeTwentyOutOfCombat && !InCombat(c));

    /// <summary>
    /// A skill check: d20 (or 20 when <paramref name="take20"/>) + skill rank + <paramref name="bonus"/> against
    /// <paramref name="dc"/>. Players involved (the user and <paramref name="versus"/>) get the combat-log line
    /// and a <c>skill-check</c> notify.
    /// </summary>
    public SkillRoll Check(Creature c, int skill, int dc, int bonus = 0, bool take20 = false, GameObject? versus = null, bool feedback = true)
    {
        int rank = Bonus(c, skill) + bonus;
        int roll = take20 ? 20 : Dice(20);
        var r = new SkillRoll(skill, roll, take20, rank, dc, (take20 ? 20 : roll) + rank >= dc);
        if (feedback) Report(c, r, versus);
        return r;
    }

    /// <summary>A roll without a DC yet (the opposing side of an opposed check).</summary>
    public SkillRoll Roll(Creature c, int skill, int bonus = 0) => Check(c, skill, 0, bonus, feedback: false);

    /// <summary>
    /// Sends the combat-log line and the structured notify for a finished check. Measured lines (traprules, traplook):
    /// "Take 20 : Name : Disable Trap : *success* : (20 + 15 = 35 vs. DC: 10)", a taken 20 short of the DC is
    /// "*success not possible*", a botched disarm "*critical failure*". <paramref name="label"/> replaces the skill name
    /// (the trap subskills print "Examine Trap", "Flag Trap", "Recover Trap").
    /// </summary>
    public void Report(Creature c, SkillRoll r, GameObject? versus = null, string? label = null, bool critical = false)
    {
        var output = _world.Output;
        if (output == null) return;
        string result = r.Success ? "success" : r.Take20 ? "success not possible" : critical ? "critical failure" : "failure";
        string who = r.Take20 ? Tlk(SkillIds.StrTake20, "Take 20") + " : " + Name(c) : Name(c);
        string text = Format(SkillIds.StrSkillCheck, "<CUSTOM0> : <CUSTOM1> : *<CUSTOM2>* : (<CUSTOM3> <CUSTOM4> <CUSTOM5> = <CUSTOM6> vs. DC: <CUSTOM7>)",
            who, label ?? SkillName(r.Skill), result,
            (r.Take20 ? 20 : r.Roll).ToString(), r.Bonus < 0 ? "-" : "+", Math.Abs(r.Bonus).ToString(),
            r.Total.ToString(), r.Dc.ToString());
        var data = new
        {
            creature = c.Id, skill = r.Skill, roll = r.Take20 ? 20 : r.Roll, take20 = r.Take20, bonus = r.Bonus, total = r.Total,
            dc = r.Dc, success = r.Success, target = versus?.Id ?? GameObject.InvalidId, text,
        };
        if (c.IsPC)
        {
            output.SendMessage(c, text);
            output.Notify("skill-check", c, data, c);
        }
        if (versus is Creature { IsPC: true } other && other != c)
        {
            output.SendMessage(other, text);
            output.Notify("skill-check", c, data, other);
        }
    }

    /// <summary>A line for a player's combat log.</summary>
    void Tell(Creature? pc, string text)
    {
        if (pc is { IsPC: true }) _world.Output?.SendMessage(pc, text);
    }

    // ------------------------------------------------------------------ text

    string Name(GameObject o) => o.Name.Resolve(_world.Resources.Talk);

    /// <summary>The skill's name from skills.2da (Name strref), else its label.</summary>
    public string SkillName(int skill)
    {
        var skills = _world.Resources.TwoDA("skills");
        string label = skills.Get(skill, "Label");
        return uint.TryParse(skills.Get(skill, "Name"), out var strref) && Tlk(strref, "") is { Length: > 0 } name ? name : label;
    }

    string Tlk(uint strref, string fallback)
    {
        var s = _world.Resources.TlkString(strref);
        return string.IsNullOrEmpty(s) ? fallback : s;
    }

    /// <summary>A dialog.tlk line with &lt;CUSTOMn&gt; tokens filled in (the fallback text when the talk table lacks it).</summary>
    string Format(uint strref, string fallback, params string[] custom)
    {
        var text = Tlk(strref, fallback);
        for (int i = custom.Length - 1; i >= 0; i--) text = text.Replace("<CUSTOM" + i + ">", custom[i]);
        return text;
    }

    // ------------------------------------------------------------------ helpers

    int Rule(string label, int fallback) => Ruleset.Int(_world, label, fallback);
    float RuleF(string label, float fallback) => Ruleset.Float(_world, label, fallback);

    bool HasFeat(Creature c, int feat) => CreatureRules.HasFeat(_world, c, feat, ignoreUses: true);

    static float Distance2D(GameObject a, GameObject b) =>
        System.Numerics.Vector2.Distance(new(a.Position.X, a.Position.Y), new(b.Position.X, b.Position.Y));

    /// <summary>The PC that should hear about something a creature did: itself, or its master.</summary>
    Creature? PlayerOf(Creature c) => c.IsPC ? c : _world.Get<Creature>(c.Master) is { IsPC: true } m ? m : null;
}

using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.ClientSideMessageCombatMessages;
using static OpenWinter.Net.Nwn.Messages.ClientSideMessageFeedbackMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The combat log lines the original prints for the server's combat messages and combat feedback, built from the
/// talk table's templates ("&lt;CUSTOM0&gt; attacks &lt;CUSTOM1&gt; : *&lt;CUSTOM2&gt;* : (...)") in the forms OpenWinter's own
/// server prints them.
/// </summary>
public static class NwnCombatLog
{
    // Talk table templates: attack, special attack, saving throw, damage, kill, initiative, threat roll, the
    // concealment and miss chance results, touch attacks and spell resistance.
    const uint Attack = 10470, Attempt = 10471, Save = 10484, Damages = 10466, Killed = 10474, Initiative = 53174,
        ThreatRoll = 61953, TargetConcealed = 62337, MissChance = 62336, ResistSpell = 8342, SpellResisted = 8343,
        Countered = 10483;

    // Defences and feedback: damage immunity, resistance and reduction (with what remains), immunity to a spell, a
    // heal, experience, a weapon that can't hurt, epic dodge, massive damage, spell level absorption, and the first
    // of the "Immune to ..." lines, which follow the feedback numbers 125 to 146 in order.
    const uint ImmunityAbsorbs = 52961, ResistanceAbsorbs = 52962, ReductionAbsorbs = 52963, ResistanceRemaining = 52969,
        ReductionRemaining = 52970, ImmuneTo = 52971, Healed = 62504, XpGained = 8263, XpLost = 8264, NotEffective = 61939,
        EpicDodge = 1772, MassiveDamage = 2384, LevelAbsorbs = 52972, LevelAbsorbsRemaining = 52973, FirstImmunity = 62453;

    // Words: the attack results, the attack kinds named before a line, save names and results.
    const uint Hit = 511, Miss = 514, Parried = 1463, CriticalHit = 5224, Resisted = 53241, AutomaticHit = 61628,
        CoupDeGrace = 61629, SneakAttack = 981, DeathAttack = 3952, Opportunity = 6626, Someone = 8349,
        Fortitude = 520, Reflex = 519, Will = 745, Success = 5352, Failure = 5353;

    /// <summary>
    /// Saving throw types (SAVING_THROW_TYPE_*) as the original names them after "vs.": talk table lines where it
    /// has them, the English word where it doesn't (no line reads just "Positive").
    /// </summary>
    static readonly (uint StrRef, string Word)[] SaveTypeNames =
    [
        (0, ""), (5157, "Mind Affecting"), (879, "Poison"), (1006, "Disease"), (804, "Fear"), (2202, "Sonic"), (1027, "Acid"),
        (1028, "Fire"), (8114, "Electricity"), (0, "Positive"), (6620, "Negative"), (5154, "Death"), (1029, "Cold"),
        (5155, "Divine"), (5563, "Traps"), (2295, "Spells"), (265, "Good"), (266, "Evil"), (4943, "Law"), (4944, "Chaos"),
        (5161, "Paralysis"),
    ];

    /// <summary>Attack results, as the server numbers them.</summary>
    public const int ResultHit = 1, ResultParried = 2, ResultCritical = 3, ResultMiss = 4, ResultResisted = 5,
        ResultAutomatic = 7, ResultConcealed = 8, ResultMissChance = 9, ResultDevastating = 10;

    public static bool IsHit(int result) => result is ResultHit or ResultCritical or ResultAutomatic or ResultDevastating;

    /// <summary>The line for a combat message, or null when it has none.</summary>
    public static string? Line(NwnMessage message, INwnBridgeData data, Func<uint, string> name)
    {
        string T(uint strRef) => data.Text(strRef, 0);
        string Fill(uint strRef, params string[] custom)
        {
            var text = T(strRef);
            for (int i = 0; i < custom.Length; i++) text = text.Replace($"<CUSTOM{i}>", custom[i]);
            return text;
        }
        (string Sign, string Amount) Mod(int mod) => mod < 0 ? ("-", (-mod).ToString()) : ("+", mod.ToString());
        switch (message)
        {
            case ClientSideMessageComplexAttack a:
            {
                string prefix = a.CoupDeGrace ? T(CoupDeGrace) : a.AttackOfOpportunity ? T(Opportunity)
                    : a.DeathAttack ? T(DeathAttack) : a.SneakAttack ? T(SneakAttack) : "";
                return AttackLine(prefix, a.Effector, a.Target, a.ToHitRoll, a.ToHitMod, a.CoupDeGrace ? ResultAutomatic : a.AttackResult,
                    a.CriticalThreat ? a.ThreatRoll : null, a.Concealment);
            }
            case ClientSideMessageSpecialAttack a:
            {
                // The special attack's feat (Knockdown, Disarm ...) names it.
                string prefix = uint.TryParse(data.TwoDA("feat", a.AttackType, "FEAT"), out var feat) ? T(feat)
                    : a.DeathAttack ? T(DeathAttack) : a.SneakAttack ? T(SneakAttack) : "";
                return AttackLine(prefix, a.Effector, a.Target, a.ToHitRoll, a.ToHitMod, a.AttackResult, a.CriticalThreat ? a.ThreatRoll : null, a.Concealment);
            }
            case ClientSideMessageTouchAttack t:
            {
                var (sign, amount) = Mod(t.ToHitMod);
                return Fill(Attempt, Who(t.Attacker), t.IsMelee ? "Touch Attack (Melee)" : "Touch Attack (Ranged)", Who(t.Target),
                    Result(t.AttackResult, t.Concealment), t.ToHitRoll.ToString(), sign, amount, (t.ToHitRoll + t.ToHitMod).ToString(), "");
            }
            case ClientSideMessageComplexDamage d:
            {
                var (total, parts) = DamageParts(d.Damages, data);
                return Fill(Damages, Who(d.Effector), Who(d.Target), $"{total} ({parts})");
            }
            case ClientSideMessageComplexDeath d:
                return Fill(Killed, Who(d.Killer), Who(d.Victim));
            case ClientSideMessageInitiative i:
            {
                var (sign, amount) = Mod(i.Modifier);
                int total = i.DieRoll + i.Modifier;
                return Fill(Initiative, Who(i.Target), total.ToString(), i.DieRoll.ToString(), sign, amount, total.ToString());
            }
            case ClientSideMessageSavingThrow s:
            {
                string vs = s.SaveSpecificType > 0 && s.SaveSpecificType < SaveTypeNames.Length
                    && SaveTypeNames[s.SaveSpecificType] is var (strRef, word) ? " vs. " + (strRef != 0 && T(strRef) is { Length: > 0 } t ? t : word) : "";
                string label = (s.SaveType switch { 1 => T(Fortitude), 2 => T(Reflex), 3 => T(Will), _ => "" }) + vs;
                int total = s.DieRoll + s.Modifier;
                // A natural 1 fails only when the server says so (auto_fail_on_1, sent with a roll of 1); a 20 succeeds.
                bool ok = s.DieRoll == 20 || (!(s.DieRoll == 1 && s.AutoFailOn1 != false) && total >= s.DifficultyClass);
                var (sign, amount) = Mod(s.Modifier);
                var line = Fill(Save, Who(s.Target), label, T(ok ? Success : Failure), s.DieRoll.ToString(), sign, amount, total.ToString(),
                    s.DifficultyClass.ToString());
                // A feat that bore on the save (has_feat) is named before the line, as the attack kinds are.
                return s.HasFeat && s.Feat is { } feat && uint.TryParse(data.TwoDA("feat", feat, "FEAT"), out var featRef) && T(featRef) is { Length: > 0 } featName
                    ? featName + " : " + line : line;
            }
            // Measured: 1 is a resisted spell (no damage followed), 0 one that got through, which prints nothing.
            case ClientSideMessageSpellResistance { Result: 1 } r:
                return Fill(ResistSpell, Who(r.Target), T(SpellResisted));
            case ClientSideMessageCounterspell c when c.Success:
                return Fill(Countered, Who(c.Target), Spell(c.CounteredSpellId), Who(c.Caster), Spell(c.SpellId));
        }
        return null;

        string Who(uint id) => name(id) is { Length: > 0 } n ? n : T(Someone);
        string Spell(uint id) => uint.TryParse(data.TwoDA("spells", (int)id, "Name"), out var s) ? T(s) : "";

        string Result(int result, byte? concealment) => result switch
        {
            ResultHit => T(Hit),
            ResultParried => T(Parried),
            ResultCritical or ResultDevastating => T(CriticalHit),
            ResultResisted => T(Resisted),
            ResultAutomatic => T(AutomaticHit),
            ResultConcealed => Fill(TargetConcealed, (concealment ?? 0).ToString()),
            ResultMissChance => Fill(MissChance, (concealment ?? 0).ToString()),
            _ => T(Miss),
        };

        string AttackLine(string prefix, uint attacker, uint target, int roll, int mod, int result, byte? threat, byte? concealment)
        {
            var (sign, amount) = Mod(mod);
            string threatText = threat is { } t ? Fill(ThreatRoll, $"{t} {sign} {amount} = {t + mod}") : "";
            var line = Fill(Attack, Who(attacker), Who(target), Result(result, concealment), roll.ToString(), sign, amount, (roll + mod).ToString(), threatText);
            return prefix.Length > 0 ? prefix + " : " + line : line;
        }
    }

    /// <summary>
    /// A damage message's total and its parts by damage type group in damagetypegroups.2da order ("40 Physical 5
    /// Fire"): entry i is damagetypes.2da row i (the DAMAGE_TYPE_* bits, then the base weapon damage).
    /// </summary>
    public static (int Total, string Parts) DamageParts(IReadOnlyList<DamageEntry> damages, INwnBridgeData data)
    {
        var byGroup = new SortedDictionary<int, int>();
        int total = 0;
        for (int i = 0; i < damages.Count; i++)
        {
            if (!damages[i].HasDamage || damages[i].Damage is not { } amount) continue;
            int group = int.TryParse(data.TwoDA("damagetypes", i, "DamageTypeGroup"), out var g) ? g : 0;
            byGroup[group] = byGroup.GetValueOrDefault(group) + amount;
            total += amount;
        }
        var parts = byGroup.Select(kv => GroupText(data, kv.Key).Replace("<CUSTOM0>", kv.Value.ToString()).Trim());
        return (total, string.Join(" ", parts));
    }

    /// <summary>A damage type group's feedback text ("&lt;CUSTOM0&gt; Fire").</summary>
    static string GroupText(INwnBridgeData data, int group) =>
        uint.TryParse(data.TwoDA("damagetypegroups", group, "FeedbackStrref"), out var s) && data.Text(s, 0) is { Length: > 0 } text
            ? text
            : "<CUSTOM0> " + data.TwoDA("damagetypegroups", group, "Label");

    /// <summary>The damage type bit (DAMAGE_TYPE_*) that dealt most of a damage message: 0 for physical damage.</summary>
    public static int MainType(IReadOnlyList<DamageEntry> damages)
    {
        int best = -1, most = 0;
        for (int i = 0; i < damages.Count; i++)
            if (damages[i].HasDamage && damages[i].Damage is { } amount && amount > most) (best, most) = (i, amount);
        return best is >= 3 and < 12 ? 1 << best : 0;
    }

    /// <summary>The line for a combat feedback message, or null when it isn't one.</summary>
    public static string? Feedback(NwnTagged payload, INwnBridgeData data, Func<uint, string> name)
    {
        string T(uint strRef) => data.Text(strRef, 0);
        string Fill(uint strRef, params string[] custom)
        {
            var text = T(strRef);
            for (int i = 0; i < custom.Length; i++) text = text.Replace($"<CUSTOM{i}>", custom[i]);
            return text;
        }
        string Who(uint id) => name(id) is { Length: > 0 } n ? n : T(Someone);
        if (payload.Tag is >= 125 and <= 146 && payload.Value.GetType().GetProperty("Target")?.GetValue(payload.Value) is uint immune)
            return Fill(FirstImmunity + (uint)(payload.Tag - 125), Who(immune));
        return payload.Value switch
        {
            FeedbackCombatDamageImmunity m => Fill(ImmunityAbsorbs, Who(m.Target), m.AbsorbedAmount.ToString(), TypeNames(m.DamageFlags, data)),
            FeedbackCombatDamageResistance m => Fill(ResistanceAbsorbs, Who(m.Target), m.AbsorbedAmount.ToString()),
            FeedbackCombatDamageResistanceRemaining m => Fill(ResistanceRemaining, Who(m.Target), m.AbsorbedAmount.ToString(), m.RemainingAmount.ToString()),
            FeedbackCombatDamageReduction m => Fill(ReductionAbsorbs, Who(m.Target), m.AbsorbedAmount.ToString()),
            FeedbackCombatDamageReductionRemaining m => Fill(ReductionRemaining, Who(m.Target), m.AbsorbedAmount.ToString(), m.RemainingAmount.ToString()),
            FeedbackCombatSpellImmunity m => Fill(ImmuneTo, Who(m.Target),
                uint.TryParse(data.TwoDA("spells", m.SpellId, "Name"), out var spell) ? T(spell) : ""),
            FeedbackCombatSpellLevelAbsorption m => Fill(LevelAbsorbs, Who(m.Target), m.AbsorbedSpellLevels.ToString()),
            FeedbackCombatSpellLevelAbsorptionRemaining m => Fill(LevelAbsorbsRemaining, Who(m.Target), m.AbsorbedSpellLevels.ToString(), m.RemainingSpellLevels.ToString()),
            FeedbackCombatWeaponNotEffective => T(NotEffective),
            FeedbackCombatEpicDodgeAttackEvaded m => Fill(EpicDodge, Who(m.Target)),
            FeedbackCombatMassiveDamage m => Fill(MassiveDamage, Who(m.Target)),
            FeedbackDamageHealed m => Fill(Healed, Who(m.Target), m.Amount.ToString()),
            // "Experience Points Gained: " and the amount, as OpenWinter's server prints it (two spaces).
            FeedbackExperienceGained m => $"{T(XpGained)} {m.Experience}",
            FeedbackExperienceLost m => $"{T(XpLost)} {m.Experience}",
            _ => null,
        };
    }

    /// <summary>The damage types of a DAMAGE_TYPE_* mask, each as its group's feedback name (" Fire").</summary>
    static string TypeNames(int flags, INwnBridgeData data)
    {
        var names = new List<string>();
        for (int bit = 0; bit < 12; bit++)
            if ((flags & (1 << bit)) != 0 && int.TryParse(data.TwoDA("damagetypes", bit, "DamageTypeGroup"), out var group))
                names.Add(GroupText(data, group).Replace("<CUSTOM0>", "").Trim());
        return names.Count == 0 ? "" : " " + string.Join(" ", names.Distinct());
    }
}

using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Poison and disease, as measured on the original server (parity suites fx_poison and fx_disease):
/// - They only take hold on a failed Fortitude save (poison.2da Save_DC, disease.2da First_Save), and a
///   poisoned creature can't be poisoned again, nor a diseased one diseased (by any disease). Applied as
///   instant they are kept as permanent.
/// - Poison deals its initial ability damage at once (an ability decrease that lasts as long as the poison
///   was applied for); a minute later the poison effect ends and, unless a second save succeeds, the
///   secondary damage follows as a permanent ability decrease.
/// - The poison effect reports [poison, secondary ability, secondary damage, DC] like the original.
/// - A disease incubates for disease.2da Incu_Hours game hours, then the creature makes a Fortitude save against
///   Subs_Save once a game day (24 game hours after the previous check, by the module clock, so SetTime moves it).
///   A failed save deals the Dam_n dice of Dice_n sides to ability Type_n (each a permanent, supernatural ability
///   decrease) and runs 24_Hour_Script on the creature; a failure at the end of incubation deals the damage and
///   runs End_Incu_Script instead (the script first: red slaad eggs kill, and the damage still lands). The second
///   successful save cures it (the end of incubation counts, and a failure in between doesn't start the count
///   over); the damage stays.
/// - The disease effect reports [disease, successful saves, incubated, calendar day and time of the next check].
/// - A row's Script_1 / Script_2 runs on the poisoned creature (as OBJECT_SELF) when the initial / secondary save fails,
///   besides any Default_n damage (parity suite cond_poison).
/// </summary>
public sealed partial class EffectSystem
{
    const float PoisonSecondaryDelay = 60f;

    /// <summary>Dice for poison and disease (returns 0..n-1); tests replace it.</summary>
    public Func<int, int> Dice = n => Random.Shared.Next(n);

    static bool IsAffliction(EffectConstants k, int type) => type == k["EFFECT_TYPE_POISON"] || type == k["EFFECT_TYPE_DISEASE"];

    /// <summary>Whether a poison or disease takes hold; the poison's initial damage goes into <paramref name="extra"/>.</summary>
    bool Afflict(EffectConstants k, Effect e, GameObject target, List<Effect> extra, List<(string Script, Effect Poison)> scripts)
    {
        if (target is not Creature c || c.IsDead) return false;
        bool poison = e.Type == k["EFFECT_TYPE_POISON"];
        var table = _world.Resources.TwoDA(poison ? "poison" : "disease");
        int row = e.Ints[0];
        if (row < 0 || row >= table.RowCount) return false;
        // A poisoned creature can't be poisoned again, nor a diseased one diseased.
        if (c.Effects.Any(x => x.Type == e.Type && !x.Hidden)) return false;
        int dc = table.GetInt(row, poison ? "Save_DC" : "First_Save", 10);
        int saveType = poison ? NwRules.SaveTypePoison : NwRules.SaveTypeDisease;
        if (CreatureRules.RollSave(_world, c, NwRules.SaveFort, dc, saveType, _world.Get(e.Creator), Dice) != CreatureRules.SaveFailed) return false;
        if (!poison)
        {
            e.Ints[1] = 0;
            e.Ints[2] = 0;
            ScheduleDisease(e, _world.Clock.TotalMs + table.GetInt(row, "Incu_Hours", 1) * _world.Clock.HourMs);
            return true;
        }

        int first = NwRules.AbilityFromLabel(table.Get(row, "Default_1"));
        if (first >= 0) extra.Add(PoisonDamage(k, e, first, Roll(table, row, 1)));
        int second = NwRules.AbilityFromLabel(table.Get(row, "Default_2"));
        // A secondary that is only a script reads 2 here on the original (Blue Whinnis, Pit Fiend Ichor; cond_poison).
        e.Ints[1] = second >= 0 ? second : table.Get(row, "Script_2").Length > 0 ? 2 : 0;
        e.Ints[2] = second >= 0 ? Roll(table, row, 2) : 0;
        e.Ints[3] = dc;
        e.NextTick = _world.Time + PoisonSecondaryDelay;
        // Script_1 runs on the poisoned creature as the poison lands (Sassone Leaf: 2d12 acid; Carrion Crawler:
        // paralysis whose creator is the creature itself). A death it causes leaves the poison on the corpse
        // (death_keeps), so Land runs it while the poison is being applied.
        string script = table.Get(row, "Script_1");
        if (script.Length > 0) scripts.Add((script.ToLowerInvariant(), e));
        return true;
    }

    /// <summary>Damage column <paramref name="n"/> of poison.2da: Dam_n dice of Dice_n sides.</summary>
    int Roll(Aurora.Formats.TwoDA table, int row, int n)
    {
        int sides = table.GetInt(row, "Dice_" + n, 0), count = table.GetInt(row, "Dam_" + n, 0), total = 0;
        for (int i = 0; i < count && sides > 0; i++) total += Dice(sides) + 1;
        return total;
    }

    Effect PoisonDamage(EffectConstants k, Effect poison, int ability, int amount, bool disease = false)
    {
        var d = new Effect
        {
            Type = k["EFFECT_TYPE_ABILITY_DECREASE"], SubType = disease ? k.Supernatural : k.Magical, Creator = poison.Creator, SpellId = poison.SpellId,
            CasterLevel = poison.CasterLevel, ScriptId = NextId(),
        };
        d.Ints[0] = ability;
        d.Ints[1] = amount;
        d.Ints[2] = disease ? 2 : 1;
        return d;
    }

    /// <summary>How often a disease looks at the module clock for its next check (the original is that close).</summary>
    const float DiseasePoll = 0.1f;

    /// <summary>The next disease check, as the effect reports it: calendar day number and milliseconds past midnight.</summary>
    void ScheduleDisease(Effect e, long totalMs)
    {
        long dayMs = 24 * _world.Clock.HourMs;
        e.Ints[3] = (int)(totalMs / dayMs);
        e.Ints[4] = (int)(totalMs % dayMs);
        e.NextTick = _world.Time + DiseasePoll;
    }

    /// <summary>Polled while a disease lasts: once the module clock reaches its next check, the creature saves or suffers.</summary>
    void DiseaseTick(EffectConstants k, Effect e, GameObject target)
    {
        e.NextTick = _world.Time + DiseasePoll;
        var clock = _world.Clock;
        if (clock.TotalMs < e.Ints[3] * 24 * clock.HourMs + e.Ints[4]) return;
        if (target is not Creature c || c.IsDead) return;
        var table = _world.Resources.TwoDA("disease");
        int row = e.Ints[0];
        if (row < 0 || row >= table.RowCount) { RemoveLinks(target, new[] { e }); return; }
        bool incubating = e.Ints[2] == 0;
        e.Ints[2] = 1;
        ScheduleDisease(e, clock.TotalMs + 24 * clock.HourMs);
        int dc = table.GetInt(row, "Subs_Save", table.GetInt(row, "First_Save", 10));
        if (CreatureRules.RollSave(_world, c, NwRules.SaveFort, dc, NwRules.SaveTypeDisease, _world.Get(e.Creator), Dice) != CreatureRules.SaveFailed)
        {
            // The second successful save cures it (a failure in between doesn't start the count over).
            if (++e.Ints[1] >= 2) RemoveLinks(target, new[] { e });
            return;
        }
        // The script runs first: the damage still lands on a creature it killed (red slaad eggs).
        var script = table.Get(row, incubating ? "End_Incu_Script" : "24_Hour_Script");
        if (script.Length > 0) _world.Scripts?.Run(script.ToLowerInvariant(), c.Id);
        if (c.IsDestroyed) return;
        for (int n = 1; n <= 3; n++)
        {
            int ability = table.IsEmpty(row, "Type_" + n) ? -1 : table.GetInt(row, "Type_" + n, -1);
            if (ability is < 0 or > 5) continue;
            int amount = Roll(table, row, n);
            if (amount > 0) Land(new List<Effect> { PoisonDamage(k, e, ability, amount, disease: true) }, target, k.Permanent, 0, hidden: false);
        }
    }

    /// <summary>
    /// A minute after it took hold: the poison ends and its secondary damage follows unless the creature saves; a row
    /// whose Script_2 is set runs that script instead (Blue Whinnis: sleep; Pit Fiend Ichor: death).
    /// </summary>
    void PoisonSecondary(EffectConstants k, Effect e, GameObject target)
    {
        RemoveLinks(target, new[] { e });
        if (target is not Creature c || c.IsDead) return;
        var table = _world.Resources.TwoDA("poison");
        string script = e.Ints[0] >= 0 && e.Ints[0] < table.RowCount ? table.Get(e.Ints[0], "Script_2") : "";
        bool damage = NwRules.AbilityFromLabel(e.Ints[0] >= 0 && e.Ints[0] < table.RowCount ? table.Get(e.Ints[0], "Default_2") : "") >= 0 && e.Ints[2] > 0;
        if (!damage && script.Length == 0) return;
        if (CreatureRules.RollSave(_world, c, NwRules.SaveFort, e.Ints[3], NwRules.SaveTypePoison, _world.Get(e.Creator), Dice) != CreatureRules.SaveFailed) return;
        if (damage) Land(new List<Effect> { PoisonDamage(k, e, e.Ints[1], e.Ints[2]) }, target, k.Permanent, 0, hidden: false);
        PoisonScript(script, c);
    }

    /// <summary>A poison.2da Script_2, run on the poisoned creature after the current update (the poison has ended).</summary>
    void PoisonScript(string script, Creature c)
    {
        if (script.Length == 0) return;
        string name = script.ToLowerInvariant();
        _world.After(0, () => { if (!c.IsDestroyed && !c.IsDead) _world.Scripts?.Run(name, c.Id); });
    }
}

using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_NoStack: how bonuses of one kind add up, per NWNX_NOSTACK_ABILITY / _SKILL / _SAVINGTHROW / _ATTACKBONUS
/// (0 vanilla; 1 only the largest; 2 the largest item one plus the largest spell one; 3 items do not stack, spells
/// as usual; 4 by bonus type). Measured on NWNX's server (parity suites nwnx_nostack_off and nwnx_nostack_a to _d):
/// an effect with no spell id (item properties, and effects scripts make outside spells) counts as an item's;
/// bonuses and penalties are each totalled by the rule and the penalty taken from the bonus, unless
/// NWNX_NOSTACK_ALWAYS_STACK_PENALTIES leaves penalties to the usual rule. By type, the largest of each type counts,
/// except circumstance bonuses, which stack as usual.
/// </summary>
public static class NoStackPlugin
{
    public enum Category { Ability, Skill, SavingThrow, AttackBonus }

    const int Circumstance = 1;

    sealed class State
    {
        public readonly Dictionary<int, int> SpellTypes = new();
    }

    static readonly ConditionalWeakTable<NwnxRegistry, State> States = new();

    [NwnxFunc("NoStack", "SetSpellBonusType")]
    static void SetSpellBonusType(NwnxCall n)
    {
        int spell = n.PopInt(); int type = n.PopInt();
        if (spell < 0 || type is < 0 or > 20) return;
        States.GetValue(n.Registry, _ => new State()).SpellTypes[spell] = type;
    }

    /// <summary>The rules of a world, read from its settings once (again if the settings source is replaced).</summary>
    sealed class Settings
    {
        public required NwnxRegistry Registry;
        public required Func<string, string?> Source;
        public readonly Rules?[] Bonus = new Rules?[4], Penalty = new Rules?[4];
    }

    static readonly ConditionalWeakTable<World, Settings> Cache = new();
    static readonly string[] Variables = ["NWNX_NOSTACK_ABILITY", "NWNX_NOSTACK_SKILL", "NWNX_NOSTACK_SAVINGTHROW", "NWNX_NOSTACK_ATTACKBONUS"];

    /// <summary>The rule for a kind of bonus (or penalty), or null when it stacks as usual.</summary>
    public static Rules? For(World w, Category category, bool penalty)
    {
        if (!Cache.TryGetValue(w, out var s) || s.Source != s.Registry.EnvironmentVariable)
        {
            var reg = w.System<NwnxRegistry>();
            s = new Settings { Registry = reg, Source = reg.EnvironmentVariable };
            var types = States.GetValue(reg, _ => new State()).SpellTypes;
            int spellDefault = Math.Clamp(reg.EnvInt("NWNX_NOSTACK_SPELL_DEFAULT_TYPE", Circumstance), 0, 20);
            int itemDefault = Math.Clamp(reg.EnvInt("NWNX_NOSTACK_ITEM_DEFAULT_TYPE", 0), 0, 20);
            bool separate = reg.EnvBool("NWNX_NOSTACK_SEPARATE_INVALID_OID_EFFECTS"), penalties = reg.EnvBool("NWNX_NOSTACK_ALWAYS_STACK_PENALTIES");
            for (int i = 0; i < 4; i++)
            {
                int mode = reg.EnvInt(Variables[i]);
                if (mode is < 1 or > 4) continue;
                s.Bonus[i] = new Rules(mode, spellDefault, itemDefault, separate, types);
                if (!penalties) s.Penalty[i] = s.Bonus[i];
            }
            Cache.AddOrUpdate(w, s);
        }
        return penalty ? s.Penalty[(int)category] : s.Bonus[(int)category];
    }

    /// <summary>One stacking rule: totals a list of bonuses, each with its spell id (-1 for an item's) and creator.</summary>
    public sealed class Rules(int mode, int spellDefault, int itemDefault, bool separateInvalid, Dictionary<int, int> spellTypes)
    {
        public int Total(IReadOnlyList<(int SpellId, uint Creator, int Value)> all)
        {
            int loose = 0;
            var items = new List<int>();
            var spells = new List<(int SpellId, int Value)>();
            foreach (var (spell, creator, value) in all)
            {
                // Scripted effects with no creator and no spell each count on their own when asked.
                if (separateInvalid && spell < 0 && creator == GameObject.InvalidId) loose += value;
                else if (spell < 0) items.Add(value);
                else spells.Add((spell, value));
            }
            int Max(IEnumerable<int> v) => v.DefaultIfEmpty(0).Max();
            return loose + mode switch
            {
                1 => Max(items.Concat(spells.Select(s => s.Value))),
                2 => Max(items) + Max(spells.Select(s => s.Value)),
                3 => Max(items) + Vanilla(spells),
                _ => ByType(items, spells),
            };
        }

        /// <summary>The usual rule: one spell's effects do not stack with each other, different spells' do.</summary>
        static int Vanilla(IEnumerable<(int SpellId, int Value)> spells) =>
            spells.GroupBy(s => s.SpellId).Sum(g => g.Max(s => s.Value));

        int ByType(List<int> items, List<(int SpellId, int Value)> spells)
        {
            var byType = new Dictionary<int, List<(int SpellId, int Value)>>();
            void Add(int type, int spell, int value)
            {
                if (!byType.TryGetValue(type, out var list)) byType[type] = list = new();
                list.Add((spell, value));
            }
            foreach (var v in items) Add(itemDefault, -1, v);
            foreach (var (spell, v) in spells) Add(spellTypes.TryGetValue(spell, out int t) ? t : spellDefault, spell, v);
            return byType.Sum(kv => kv.Key == Circumstance
                ? kv.Value.Where(s => s.SpellId < 0).Sum(s => s.Value) + Vanilla(kv.Value.Where(s => s.SpellId >= 0))
                : kv.Value.Max(s => s.Value));
        }
    }
}

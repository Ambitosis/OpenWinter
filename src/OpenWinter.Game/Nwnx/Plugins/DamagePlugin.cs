using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// Damage by type as NWNX_Damage passes it: one value per DAMAGE_TYPE_* bit (bludgeoning ... sonic, base weapon,
/// custom 1-19), -1 for a type the damage doesn't carry.
/// </summary>
public sealed class NwnxDamageData
{
    public const int Types = 32, BaseIndex = 12;
    public readonly int[] Values = Enumerable.Repeat(-1, Types).ToArray();

    public static int Bit(int index) => unchecked((int)(1u << index));

    public static NwnxDamageData From(IReadOnlyDictionary<int, int> byType)
    {
        var d = new NwnxDamageData();
        foreach (var (type, amount) in byType)
            for (int i = 0; i < Types; i++)
                if ((type & Bit(i)) != 0) { d.Values[i] = Math.Max(0, d.Values[i]) + Math.Max(0, amount); break; }
        return d;
    }

    /// <summary>The types present (value 0 or more), by DAMAGE_TYPE_* bit.</summary>
    public Dictionary<int, int> ToDictionary()
    {
        var map = new Dictionary<int, int>();
        for (int i = 0; i < Types; i++) if (Values[i] >= 0) map[Bit(i)] = Values[i];
        return map;
    }

    public bool SameAs(NwnxDamageData o) => Values.AsSpan().SequenceEqual(o.Values);
    public NwnxDamageData Copy() { var d = new NwnxDamageData(); Values.CopyTo(d.Values, 0); return d; }
}

/// <summary>NWNX_Damage_AttackEventData: one attack as the attack event script sees it.</summary>
public sealed class NwnxAttackEventData
{
    public uint Target = GameObject.InvalidId;
    public NwnxDamageData Damage = new();
    public int AttackNumber, AttackResult, WeaponAttackType, SneakAttack, AttackType, ToHitRoll, ToHitModifier;
    public bool KillingBlow, Ranged;

    // Attack results.
    public const int Hit = 1, Parried = 2, CriticalHit = 3, Miss = 4, Resisted = 5, AutomaticHit = 7, Concealed = 8, MissChance = 9, DevastatingCritical = 10;
    public static bool IsHit(int result) => result is Hit or CriticalHit or AutomaticHit or DevastatingCritical;
}

/// <summary>
/// NWNX_Damage: scripts run for every attack (OBJECT_SELF the attacker, as it swings) and for damage about to be
/// dealt (OBJECT_SELF the damaged object), with event data they may change. A script set for one object replaces
/// the global one for it: the attacker's for attacks, the damaged object's for damage (parity suite nwnx_damage).
/// </summary>
public static class DamagePlugin
{
    const string P = "Damage";
    const string AttackKey = "nwnx:damage:attack", DamageKey = "nwnx:damage:damage", SpellKey = "nwnx:damage:spell", DamagerKey = "nwnx:damage:damager";

    sealed class Scripts
    {
        public readonly Dictionary<uint, string> Attack = new(), Damage = new();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NwnxRegistry, Scripts> _state = new();
    static Scripts State(NwnxRegistry r) => _state.GetValue(r, _ => new Scripts());

    [NwnxFunc(P, "SetEventScript")]
    static void SetEventScript(NwnxCall n)
    {
        var evt = n.PopString(); var script = n.PopString(); uint owner = n.PopObjectId();
        var map = evt.Equals("ATTACK", StringComparison.OrdinalIgnoreCase) ? State(n.Registry).Attack
            : evt.Equals("DAMAGE", StringComparison.OrdinalIgnoreCase) ? State(n.Registry).Damage : null;
        if (map == null) return;
        if (script.Length == 0) map.Remove(owner);
        else map[owner] = script;
    }

    /// <summary>The ATTACK or DAMAGE script for an object: its own, else the global one ("" for none).</summary>
    static string ScriptFor(Dictionary<uint, string> map, uint owner) =>
        map.Count == 0 ? "" : map.TryGetValue(owner, out var own) ? own : map.TryGetValue(GameObject.InvalidId, out var g) ? g : "";

    static NwnxRegistry? Registry(World world) => world.Systems.OfType<NwnxRegistry>().FirstOrDefault();

    /// <summary>
    /// Runs the damage event for damage about to be dealt to <paramref name="target"/>. Returns the amounts the script
    /// left (types it set to -1 dropped), or null when no script ran or it changed nothing.
    /// </summary>
    public static Dictionary<int, int>? RunDamageEvent(World world, GameObject target, uint damager, IReadOnlyDictionary<int, int> byType, int spellId)
    {
        if (world.Scripts == null || Registry(world) is not { } reg) return null;
        var script = ScriptFor(State(reg).Damage, target.Id);
        if (script.Length == 0) return null;
        var data = NwnxDamageData.From(byType);
        var before = data.Copy();
        var ctx = new EventContext().Set(DamageKey, data).Set(DamagerKey, damager).Set(SpellKey, spellId);
        world.Scripts.Run(script, target.Id, ctx);
        return data.SameAs(before) ? null : data.ToDictionary();
    }

    /// <summary>Whether an attack by <paramref name="attacker"/> has an attack event script to run.</summary>
    public static bool HasAttackEvent(World world, uint attacker) =>
        world.Scripts != null && Registry(world) is { } reg && ScriptFor(State(reg).Attack, attacker).Length > 0;

    /// <summary>Runs the attack event for one attack; the script may change <paramref name="data"/>.</summary>
    public static void RunAttackEvent(World world, uint attacker, NwnxAttackEventData data)
    {
        if (world.Scripts == null || Registry(world) is not { } reg) return;
        var script = ScriptFor(State(reg).Attack, attacker);
        if (script.Length == 0) return;
        world.Scripts.Run(script, attacker, new EventContext().Set(AttackKey, data));
    }

    static NwnxDamageData? CurrentDamage(NwnxCall n) => n.Host?.CurrentEvent.Values.GetValueOrDefault(DamageKey) as NwnxDamageData;
    static NwnxAttackEventData? CurrentAttack(NwnxCall n) => n.Host?.CurrentEvent.Values.GetValueOrDefault(AttackKey) as NwnxAttackEventData;

    /// <summary>Pushes damage values so the script pops bludgeoning first.</summary>
    static void PushDamage(NwnxCall n, NwnxDamageData d)
    {
        for (int i = NwnxDamageData.Types - 1; i >= 0; i--) n.Push(d.Values[i]);
    }

    static void PopDamage(NwnxCall n, NwnxDamageData d)
    {
        for (int i = 0; i < NwnxDamageData.Types; i++) d.Values[i] = n.PopInt();
    }

    [NwnxFunc(P, "GetDamageEventData")]
    static void GetDamageEventData(NwnxCall n)
    {
        var d = CurrentDamage(n) ?? new NwnxDamageData();
        n.Push(n.Host?.CurrentEvent.Values.GetValueOrDefault(SpellKey) is int spell ? spell : -1);
        PushDamage(n, d);
        n.PushObject(n.Host?.CurrentEvent.Obj(DamagerKey) ?? GameObject.InvalidId);
    }

    [NwnxFunc(P, "SetDamageEventData")]
    static void SetDamageEventData(NwnxCall n)
    {
        var d = CurrentDamage(n);
        PopDamage(n, d ?? new NwnxDamageData());
    }

    [NwnxFunc(P, "GetAttackEventData")]
    static void GetAttackEventData(NwnxCall n)
    {
        var a = CurrentAttack(n) ?? new NwnxAttackEventData();
        n.Push(a.Ranged);
        n.Push(a.ToHitModifier);
        n.Push(a.ToHitRoll);
        n.Push(a.AttackType);
        n.Push(a.KillingBlow);
        n.Push(a.SneakAttack);
        n.Push(a.WeaponAttackType);
        n.Push(a.AttackResult);
        n.Push(a.AttackNumber);
        for (int i = NwnxDamageData.Types - 1; i >= 0; i--) n.Push(a.Damage.Values[i]);
        n.PushObject(a.Target);
    }

    [NwnxFunc(P, "SetAttackEventData")]
    static void SetAttackEventData(NwnxCall n)
    {
        var a = CurrentAttack(n) ?? new NwnxAttackEventData();
        PopDamage(n, a.Damage);
        a.AttackResult = n.PopInt();
        a.SneakAttack = n.PopInt();
    }

    /// <summary>
    /// DealDamage: damage of several types at once, met by the target's defences (the damage power against damage
    /// reduction) and reported like a weapon hit, physical damage as base weapon damage (parity suite nwnx_damage).
    /// </summary>
    [NwnxFunc(P, "DealDamage")]
    static void DealDamage(NwnxCall n)
    {
        uint source = n.PopObjectId(); var target = n.PopObject();
        var d = new NwnxDamageData();
        PopDamage(n, d);
        int power = n.PopInt(); bool ranged = n.PopInt() != 0;
        if (target == null) return;
        var byType = new Dictionary<int, int>();
        for (int i = 0; i < NwnxDamageData.Types; i++)
            if (i != NwnxDamageData.BaseIndex && d.Values[i] > 0) byType[NwnxDamageData.Bit(i)] = d.Values[i];
        if (byType.Count == 0) return;
        HitPoints.Damage(n.World, target, source, byType, power, ranged: ranged, weaponStyle: true);
    }
}

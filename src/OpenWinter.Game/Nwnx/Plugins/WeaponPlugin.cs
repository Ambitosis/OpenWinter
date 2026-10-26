using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Weapon over the combat rules' <see cref="WeaponRuleOverrides"/>: weapon feats for more base items, finesse
/// sizes, unarmed and monk weapons, the greater focus and specialization bonuses, the devastating critical event,
/// one-and-a-half Strength and ranged attack distances (parity suite nwnx_weapon).
/// </summary>
public static class WeaponPlugin
{
    const string P = "Weapon";
    const string DcKey = "nwnx:weapon:dc";
    const int OptGreaterFocus = 0, OptGreaterSpecialization = 1, DataDevastatingCritical = 0, DataBypass = 0;

    sealed class DevastatingCriticalData
    {
        public uint Weapon = GameObject.InvalidId, Target = GameObject.InvalidId;
        public int Damage;
        public bool Bypass;
    }

    sealed class State
    {
        public string DevastatingCriticalScript = "";
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NwnxRegistry, State> _state = new();
    static State StateOf(NwnxRegistry r) => _state.GetValue(r, _ => new State());

    static WeaponRuleOverrides Rules(NwnxCall n) => n.World.System<CombatSystem>().WeaponOverrides;

    static void Feat(NwnxCall n, WeaponFeat kind)
    {
        int baseItem = n.PopInt(); int feat = n.PopInt();
        if (feat >= 0) Rules(n).AddFeat(baseItem, kind, feat);
    }

    [NwnxFunc(P, "SetWeaponFocusFeat")] static void Focus(NwnxCall n) => Feat(n, WeaponFeat.Focus);
    [NwnxFunc(P, "SetEpicWeaponFocusFeat")] static void EpicFocus(NwnxCall n) => Feat(n, WeaponFeat.EpicFocus);
    [NwnxFunc(P, "SetGreaterWeaponFocusFeat")] static void GreaterFocus(NwnxCall n) => Feat(n, WeaponFeat.GreaterFocus);
    [NwnxFunc(P, "SetWeaponSpecializationFeat")] static void Spec(NwnxCall n) => Feat(n, WeaponFeat.Specialization);
    [NwnxFunc(P, "SetEpicWeaponSpecializationFeat")] static void EpicSpec(NwnxCall n) => Feat(n, WeaponFeat.EpicSpecialization);
    [NwnxFunc(P, "SetGreaterWeaponSpecializationFeat")] static void GreaterSpec(NwnxCall n) => Feat(n, WeaponFeat.GreaterSpecialization);
    [NwnxFunc(P, "SetWeaponImprovedCriticalFeat")] static void ImprovedCritical(NwnxCall n) => Feat(n, WeaponFeat.ImprovedCritical);
    [NwnxFunc(P, "SetEpicWeaponOverwhelmingCriticalFeat")] static void Overwhelming(NwnxCall n) => Feat(n, WeaponFeat.OverwhelmingCritical);
    [NwnxFunc(P, "SetEpicWeaponDevastatingCriticalFeat")] static void Devastating(NwnxCall n) => Feat(n, WeaponFeat.DevastatingCritical);
    [NwnxFunc(P, "SetWeaponOfChoiceFeat")] static void OfChoice(NwnxCall n) => Feat(n, WeaponFeat.WeaponOfChoice);

    [NwnxFunc(P, "SetWeaponFinesseSize")]
    static void SetFinesseSize(NwnxCall n)
    {
        int baseItem = n.PopInt(); int size = n.PopInt();
        Rules(n).FinesseSize[baseItem] = size;
    }

    /// <summary>The smallest creature size that finesses the base item: baseitems.2da's or a module's, -1 for none.</summary>
    [NwnxFunc(P, "GetWeaponFinesseSize")]
    static void GetFinesseSize(NwnxCall n)
    {
        int baseItem = n.PopInt();
        n.Push(n.World.System<CombatSystem>().Rules is CreatureCombatRules r ? r.FinesseSize(baseItem)
            : Rules(n).FinesseSize.TryGetValue(baseItem, out int size) ? size : -1);
    }

    [NwnxFunc(P, "SetWeaponUnarmed")] static void Unarmed(NwnxCall n) => Rules(n).FinesseAsUnarmed.Add(n.PopInt());
    [NwnxFunc(P, "SetWeaponIsMonkWeapon")] static void Monk(NwnxCall n) => Rules(n).MonkWeapons.Add(n.PopInt());

    [NwnxFunc(P, "SetOption")]
    static void SetOption(NwnxCall n)
    {
        int option = n.PopInt(); int value = n.PopInt();
        var o = Rules(n);
        if (option == OptGreaterFocus) o.GreaterFocusAttackBonus = value;
        else if (option == OptGreaterSpecialization) o.GreaterSpecializationDamageBonus = value;
    }

    [NwnxFunc(P, "SetDevastatingCriticalEventScript")]
    static void SetDcScript(NwnxCall n) => StateOf(n.Registry).DevastatingCriticalScript = n.PopString();

    /// <summary>
    /// Runs the devastating critical event script, OBJECT_SELF the attacker, for a critical hit that would bring a
    /// devastating critical. Returns whether the script bypassed it.
    /// </summary>
    public static bool RunDevastatingCriticalEvent(World world, Creature attacker, Item? weapon, GameObject target, int damage)
    {
        if (world.Scripts == null || world.Systems.OfType<NwnxRegistry>().FirstOrDefault() is not { } reg) return false;
        var script = StateOf(reg).DevastatingCriticalScript;
        if (script.Length == 0) return false;
        var data = new DevastatingCriticalData { Weapon = weapon?.Id ?? GameObject.InvalidId, Target = target.Id, Damage = damage };
        world.Scripts.Run(script, attacker.Id, new EventContext().Set(DcKey, data));
        return data.Bypass;
    }

    [NwnxFunc(P, "GetEventData")]
    static void GetEventData(NwnxCall n)
    {
        int type = n.PopInt();
        if (type != DataDevastatingCritical) return;
        var d = n.Host?.CurrentEvent.Values.GetValueOrDefault(DcKey) as DevastatingCriticalData ?? new DevastatingCriticalData();
        n.Push(d.Damage);
        n.PushObject(d.Target);
        n.PushObject(d.Weapon);
    }

    [NwnxFunc(P, "SetEventData")]
    static void SetEventData(NwnxCall n)
    {
        int type = n.PopInt(); int value = n.PopInt();
        if (type == DataBypass && n.Host?.CurrentEvent.Values.GetValueOrDefault(DcKey) is DevastatingCriticalData d) d.Bypass = value != 0;
    }

    /// <summary>One-and-a-half Strength for a melee weapon; persisted ones are saved with the item.</summary>
    [NwnxFunc(P, "SetOneHalfStrength")]
    static void SetOneHalfStrength(NwnxCall n)
    {
        var item = n.PopObject<Item>(); bool enable = n.PopInt() != 0; bool persist = n.PopInt() != 0;
        if (item == null) return;
        var o = Rules(n);
        if (enable) o.OneHalfStrength.Add(item.Id);
        else o.OneHalfStrength.Remove(item.Id);
        if (persist && enable) item.Template.SetByte(CreatureCombatRules.OneHalfStrengthField, 1);
        else item.Template.Remove(CreatureCombatRules.OneHalfStrengthField);
    }

    [NwnxFunc(P, "GetOneHalfStrength")]
    static void GetOneHalfStrength(NwnxCall n)
    {
        var item = n.PopObject<Item>();
        n.Push(item != null && (Rules(n).OneHalfStrength.Contains(item.Id) || item.Template.GetInt(CreatureCombatRules.OneHalfStrengthField) != 0));
    }

    [NwnxFunc(P, "SetMaxRangedAttackDistanceOverride")]
    static void SetRangedDistance(NwnxCall n)
    {
        int baseItem = n.PopInt(); float max = n.PopFloat(); float passive = n.PopFloat(); float preferred = n.PopFloat();
        Rules(n).RangedDistance[baseItem] = (max, passive, preferred);
    }
}

using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Creature's other functions: body sizes, special abilities, restoring uses, associates, encounters,
/// orientation, stores, killers, immunity bypass and equipment (parity suites nwnx_creature_misc, nwnx_creature_uses).
/// </summary>
static partial class CreaturePlugin
{
    // ---- Body sizes (appearance.2da values the creature can have its own of) ---------

    static float Appearance(NwnxCall n, Creature c, string column) =>
        n.World.Resources.TwoDA("appearance").GetFloat(c.AppearanceType, column, 0f);

    [NwnxFunc("Creature", "GetPersonalSpace")]
    static void GetPersonalSpace(NwnxCall n) => n.Push(n.PopObject<Creature>() is { } c ? Game.PersonalSpace.Radius(n.World, c) : 0f);
    [NwnxFunc("Creature", "SetPersonalSpace")]
    static void SetPersonalSpace(NwnxCall n) { var c = n.PopObject<Creature>(); float v = n.PopFloat(); if (c != null) O(c).PersonalSpace = v; }
    [NwnxFunc("Creature", "GetCreaturePersonalSpace")]
    static void GetCreaturePersonalSpace(NwnxCall n) => n.Push(n.PopObject<Creature>() is { } c ? Game.PersonalSpace.CreatureSpace(n.World, c) : 0f);
    [NwnxFunc("Creature", "SetCreaturePersonalSpace")]
    static void SetCreaturePersonalSpace(NwnxCall n) { var c = n.PopObject<Creature>(); float v = n.PopFloat(); if (c != null) O(c).CreaturePersonalSpace = v; }
    [NwnxFunc("Creature", "GetHeight")]
    static void GetHeight(NwnxCall n) => n.Push(n.PopObject<Creature>() is { } c ? c.Overrides?.Height ?? Appearance(n, c, "HEIGHT") : 0f);
    [NwnxFunc("Creature", "SetHeight")]
    static void SetHeight(NwnxCall n) { var c = n.PopObject<Creature>(); float v = n.PopFloat(); if (c != null) O(c).Height = v; }
    [NwnxFunc("Creature", "GetHitDistance")]
    static void GetHitDistance(NwnxCall n) => n.Push(n.PopObject<Creature>() is { } c ? Combat(n).HitDistance(c) : 0f);
    [NwnxFunc("Creature", "SetHitDistance")]
    static void SetHitDistance(NwnxCall n) { var c = n.PopObject<Creature>(); float v = n.PopFloat(); if (c != null) O(c).HitDistance = v; }
    [NwnxFunc("Creature", "GetPreferredAttackDistance")]
    static void GetPreferredAttackDistance(NwnxCall n) => n.Push(n.PopObject<Creature>() is { } c ? Combat(n).PreferredAttackDistance(c) : 0f);
    [NwnxFunc("Creature", "SetPreferredAttackDistance")]
    static void SetPreferredAttackDistance(NwnxCall n) { var c = n.PopObject<Creature>(); float v = n.PopFloat(); if (c != null) O(c).PreferredAttackDistance = v; }

    // ---- Special abilities -----------------------------------------------------
    // The list is the creature's spell-like abilities. Removing one leaves its place with spell -1, which the count
    // leaves out but the indices keep (nwserver nwnx_creature_uses: three of four places counted after a removal).

    static List<SpellAbility> Abilities(NwnxCall n, Creature c)
    {
        CreatureRules.EnsureInit(n.World, c);
        return c.SpellAbilities;
    }

    [NwnxFunc("Creature", "GetSpecialAbilityCount")]
    static void SpecialAbilityCount(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c != null ? Abilities(n, c).Count(a => a.Spell >= 0) : 0);
    }
    [NwnxFunc("Creature", "GetSpecialAbility")]
    static void SpecialAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int index = n.PopInt();
        var list = c != null ? Abilities(n, c) : new List<SpellAbility>();
        if (index >= 0 && index < list.Count) { n.Push(list[index].Spell); n.Push(list[index].Ready ? 1 : 0); n.Push(list[index].CasterLevel); }
        else { n.Push(-1); n.Push(-1); n.Push(-1); }
    }
    [NwnxFunc("Creature", "AddSpecialAbility")]
    static void AddSpecialAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt(); int ready = n.PopInt(); int id = n.PopInt();
        if (c != null) Abilities(n, c).Add(new SpellAbility { Spell = id, Ready = ready != 0, CasterLevel = level });
    }
    [NwnxFunc("Creature", "RemoveSpecialAbility")]
    static void RemoveSpecialAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int index = n.PopInt();
        if (c != null && Abilities(n, c) is var list && index >= 0 && index < list.Count) list[index].Spell = -1;
    }
    [NwnxFunc("Creature", "SetSpecialAbility")]
    static void SetSpecialAbility(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int index = n.PopInt(); int level = n.PopInt(); int ready = n.PopInt(); int id = n.PopInt();
        if (c == null || Abilities(n, c) is not { } list || index < 0 || index >= list.Count) return;
        list[index].Spell = id;
        list[index].Ready = ready != 0;
        list[index].CasterLevel = level;
    }
    [NwnxFunc("Creature", "RestoreSpecialAbilities")]
    static void RestoreSpecialAbilities(NwnxCall n)
    {
        if (n.PopObject<Creature>() is { } c) foreach (var a in Abilities(n, c)) a.Ready = true;
    }

    /// <summary>Every item the creature carries gets its uses per day back; charges stay as they are.</summary>
    [NwnxFunc("Creature", "RestoreItems")]
    static void RestoreItems(NwnxCall n)
    {
        if (n.PopObject<Creature>() is not { } c) return;
        var inv = n.World.Inventory;
        foreach (var item in inv.Items(c).Concat(c.Equipment.Values).Distinct())
        {
            bool changed = false;
            foreach (var p in item.Properties)
                if (Inventory.InventorySystem.MaxUsesPerDay(p) is int max and > 0 && p.UsesPerDay != max) { p.UsesPerDay = max; changed = true; }
            if (changed) n.World.MarkChanged(item);
        }
    }

    // ---- Associates ----------------------------------------------------------

    [NwnxFunc("Creature", "SetAnimalCompanionCreatureType")]
    static void SetCompanionType(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int type = n.PopInt();
        if (c == null) return;
        CreatureRules.EnsureInit(n.World, c);
        c.CompanionType = type;
        c.Template.SetInt("CompanionType", type);
    }
    [NwnxFunc("Creature", "SetFamiliarCreatureType")]
    static void SetFamiliarType(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int type = n.PopInt();
        if (c == null) return;
        CreatureRules.EnsureInit(n.World, c);
        c.FamiliarType = type;
        c.Template.SetInt("FamiliarType", type);
    }
    [NwnxFunc("Creature", "SetAnimalCompanionName")]
    static void SetCompanionName(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var name = n.PopString();
        if (c == null) return;
        CreatureRules.EnsureInit(n.World, c);
        c.CompanionName = name;
        c.Template.SetString("CompanionName", name);
    }
    [NwnxFunc("Creature", "SetFamiliarName")]
    static void SetFamiliarName(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var name = n.PopString();
        if (c == null) return;
        CreatureRules.EnsureInit(n.World, c);
        c.FamiliarName = name;
        c.Template.SetString("FamiliarName", name);
    }

    // ---- Encounters, body bags, barter, perception ------------------------------

    [NwnxFunc("Creature", "GetEncounter")]
    static void GetEncounter(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.PushObject(c != null && n.World.Get(c.SpawnedByEncounter) is Encounter e ? e.Id : GameObject.InvalidId);
    }
    [NwnxFunc("Creature", "SetEncounter")]
    static void SetEncounter(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var e = n.PopObject();
        if (c == null) return;
        c.SpawnedByEncounter = e is Encounter ? e.Id : GameObject.InvalidId;
    }

    /// <summary>The bag a dead creature's loot lies in.</summary>
    [NwnxFunc("Creature", "GetBodyBag")]
    static void GetBodyBag(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        uint bag = c?.Locals.Objects.GetValueOrDefault(CorpseSystem.BagLink, GameObject.InvalidId) ?? GameObject.InvalidId;
        n.PushObject(n.World.Get(bag) is Placeable ? bag : GameObject.InvalidId);
    }

    [NwnxFunc("Creature", "GetIsBartering")]
    static void GetIsBartering(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c is { IsPC: true } && n.World.System<Requests.BarterService>().SessionOf(c) != null);
    }

    [NwnxFunc("Creature", "DoPerceptionUpdateOnCreature")]
    static void DoPerceptionUpdate(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var t = n.PopObject<Creature>();
        if (c != null && t != null && c.Area != null && c.Area == t.Area) n.World.System<Society.PerceptionSystem>().Update(c);
    }

    // ---- Orientation, stores, multiclassing, killers ---------------------------

    [NwnxFunc("Creature", "GetLockOrientationToObject")]
    static void GetLockOrientation(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        uint id = c?.Overrides?.LockOrientationTo ?? GameObject.InvalidId;
        n.PushObject(n.World.Get(id) != null ? id : GameObject.InvalidId);
    }
    [NwnxFunc("Creature", "SetLockOrientationToObject")]
    static void SetLockOrientation(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); uint target = n.PopObjectId();
        if (c == null) return;
        if (n.World.Get(target) == null) { if (c.Overrides != null) c.Overrides.LockOrientationTo = GameObject.InvalidId; }
        else O(c).LockOrientationTo = target;
    }

    /// <summary>-2: no override; -1: no limit; else the most the store pays this creature for an item.</summary>
    [NwnxFunc("Creature", "GetMaxSellToStorePriceOverride")]
    static void GetMaxSellPrice(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); uint store = n.PopObjectId();
        n.Push(c?.Overrides is { } o && o.MaxSellToStorePrice.TryGetValue(store, out int v) ? v : -2);
    }
    [NwnxFunc("Creature", "SetMaxSellToStorePriceOverride")]
    static void SetMaxSellPrice(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var store = n.PopObject<Store>(); int price = n.PopInt();
        if (c == null || store == null) return;
        if (price <= -2) c.Overrides?.MaxSellToStorePrice.Remove(store.Id);
        else O(c).MaxSellToStorePrice[store.Id] = Math.Max(-1, price);
    }

    [NwnxFunc("Creature", "GetMulticlassLimit")]
    static void GetMulticlassLimit(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c is { IsPC: true } ? c.Overrides?.MulticlassLimit ?? 0 : 0);
    }
    /// <summary>Players only; a limit for classes not yet taken (0 lifts it).</summary>
    [NwnxFunc("Creature", "SetMulticlassLimit")]
    static void SetMulticlassLimit(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int limit = n.PopInt(); bool persist = n.PopInt() != 0;
        if (c is not { IsPC: true }) return;
        O(c).MulticlassLimit = Math.Max(0, limit);
        // NWNX keeps this one for players only, which the parity lab (no players) can't show; it lasts the session.
        if (persist) n.ReportUnsupported("bPersist: the multiclass limit is not kept in the character file", "persist");
    }

    [NwnxFunc("Creature", "SetLastKiller")]
    static void SetLastKiller(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); uint killer = n.PopObjectId();
        if (c != null) c.LastKiller = killer;
    }

    // ---- Immunity bypass ---------------------------------------------------------

    /// <summary>IMMUNITY_TYPE_* for the creature's effects on others, its negative for others' on it; 255 for every type.</summary>
    [NwnxFunc("Creature", "SetBypassEffectImmunity")]
    static void SetBypassImmunity(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int type = n.PopInt(); int chance = n.PopInt(); bool persist = n.PopInt() != 0;
        if (c == null) return;
        chance = Math.Clamp(chance, -100, 100);
        if (chance == 0) c.Overrides?.BypassImmunity.Remove(type);
        else O(c).BypassImmunity[type] = chance;
        Persist(c, CreatureOverridesPersistence.BypassKey(type), persist && chance != 0 ? chance : null);
    }
    [NwnxFunc("Creature", "GetBypassEffectImmunity")]
    static void GetBypassImmunity(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int type = n.PopInt();
        n.Push(c?.Overrides?.BypassImmunity.GetValueOrDefault(type) ?? 0);
    }

    // ---- Equipment ---------------------------------------------------------------

    /// <summary>Equips at once; an invalid item is an error (nothing returned), as on nwserver.</summary>
    [NwnxFunc("Creature", "RunEquip")]
    static void RunEquip(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var item = n.PopObject<Item>(); int slot = n.PopInt();
        if (item == null) return;
        n.Push(c != null && n.World.Inventory.Equip(c, item, slot));
    }

    /// <summary>Unequips at once. nwserver reads FALSE back even when the item came off (nwnx_creature_attack).</summary>
    [NwnxFunc("Creature", "RunUnequip")]
    static void RunUnequip(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var item = n.PopObject<Item>();
        if (item == null) return;
        if (c != null) n.World.Inventory.Unequip(c, item);
        n.Push(0);
    }
}

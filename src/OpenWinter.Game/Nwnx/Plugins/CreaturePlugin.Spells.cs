using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Spells;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Creature's spell functions (parity suite nwnx_creature_spells). Remaining slots are the spontaneous casting
/// uses (a class that prepares spells keeps the number but doesn't use it); they are bytes, so going below 0 wraps
/// to 255 as on nwserver.
/// </summary>
static partial class CreaturePlugin
{
    [NwnxFunc("Creature", "GetMemorisedSpell")]
    static void GetMemorised(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int index = n.PopInt();
        var k = ClassOf(c, cls);
        if (k != null && level is >= 0 and < 10 && index >= 0 && index < k.Memorized[level].Count)
        {
            var m = k.Memorized[level][index];
            n.Push(m.Spell); n.Push(m.Ready); n.Push(m.Meta); n.Push(0);
        }
        else { n.Push(-1); n.Push(0); n.Push(0); n.Push(0); }
    }
    [NwnxFunc("Creature", "SetMemorisedSpell")]
    static void SetMemorised(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int index = n.PopInt();
        n.PopInt(); // domain
        int meta = n.PopInt(); bool ready = n.PopInt() != 0; int id = n.PopInt();
        var k = ClassOf(c, cls);
        if (k == null || level is < 0 or >= 10 || index < 0) return;
        var list = k.Memorized[level];
        while (list.Count <= index) list.Add((-1, 0, false));
        list[index] = (id, meta, ready);
    }
    [NwnxFunc("Creature", "ClearMemorisedSpell")]
    static void ClearMemorised(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int index = n.PopInt();
        var k = ClassOf(c, cls);
        if (k != null && level is >= 0 and < 10 && index >= 0 && index < k.Memorized[level].Count) k.Memorized[level][index] = (-1, 0, false);
    }
    [NwnxFunc("Creature", "GetMemorisedSpellCountByLevel")]
    static void MemorisedCount(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        var k = ClassOf(c, cls);
        n.Push(k != null && level is >= 0 and < 10 ? k.Memorized[level].Count : 0);
    }
    [NwnxFunc("Creature", "GetKnownSpell")]
    static void KnownSpell(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int index = n.PopInt();
        var k = ClassOf(c, cls);
        n.Push(k != null && level is >= 0 and < 10 && index >= 0 && index < k.KnownSpells[level].Count ? k.KnownSpells[level][index] : -1);
    }
    [NwnxFunc("Creature", "GetKnownSpellCount")]
    static void KnownSpellCount(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        var k = ClassOf(c, cls);
        n.Push(k != null && level is >= 0 and < 10 ? k.KnownSpells[level].Count : 0);
    }
    [NwnxFunc("Creature", "AddKnownSpell")]
    static void AddKnownSpell(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int spell = n.PopInt();
        var k = ClassOf(c, cls);
        if (k != null && level is >= 0 and < 10 && !k.KnownSpells[level].Contains(spell)) k.KnownSpells[level].Add(spell);
    }
    [NwnxFunc("Creature", "RemoveKnownSpell")]
    static void RemoveKnownSpell(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int spell = n.PopInt();
        var k = ClassOf(c, cls);
        if (k != null && level is >= 0 and < 10) k.KnownSpells[level].Remove(spell);
    }

    // ---- Slots ---------------------------------------------------------------

    /// <summary>The class's position and the slot's current count, or false when the creature lacks the class.</summary>
    static bool Slot(NwnxCall n, Creature? c, int cls, int level, out int position, out int left)
    {
        position = -1; left = 0;
        if (c == null || level is < 0 or > 9) return false;
        CreatureRules.EnsureInit(n.World, c);
        position = CreatureRules.ClassIndex(c, cls);
        if (position < 0) return false;
        if (c.SpellSlotsLeft.TryGetValue((position, level), out left)) return true;
        left = CreatureRules.MemorizesSpells(n.World, cls) || !CreatureRules.IsSpellCaster(n.World, cls) ? 0 : CreatureRules.SlotsLeft(n.World, c, position, level);
        return true;
    }

    [NwnxFunc("Creature", "GetRemainingSpellSlots")]
    static void GetSlots(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        n.Push(Slot(n, c, cls, level, out _, out int left) ? left : 0);
    }
    [NwnxFunc("Creature", "SetRemainingSpellSlots")]
    static void SetSlots(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); int slots = n.PopInt();
        if (Slot(n, c, cls, level, out int pos, out _)) c!.SpellSlotsLeft[(pos, level)] = slots & 0xFF;
    }
    [NwnxFunc("Creature", "DecrementRemainingSpellSlots")]
    static void DecrementSlots(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        if (Slot(n, c, cls, level, out int pos, out int left)) c!.SpellSlotsLeft[(pos, level)] = (left - 1) & 0xFF;
    }
    [NwnxFunc("Creature", "IncrementRemainingSpellSlots")]
    static void IncrementSlots(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        if (Slot(n, c, cls, level, out int pos, out int left)) c!.SpellSlotsLeft[(pos, level)] = (left + 1) & 0xFF;
    }
    /// <summary>Slots per day at a spell level, bonus spells included; -1 when the creature lacks the class.</summary>
    [NwnxFunc("Creature", "GetMaxSpellSlots")]
    static void MaxSlots(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt();
        if (c == null || CreatureRules.ClassIndex(c, cls) < 0) { n.Push(-1); return; }
        n.Push(level is >= 0 and < 10 ? CreatureRules.SpellSlots(n.World, c, cls, level) : 0);
    }

    [NwnxFunc("Creature", "GetNumberOfBonusSpells")]
    static void NumberOfBonusSpells(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int position = n.PopInt(); int level = n.PopInt();
        n.Push(c?.Overrides?.BonusSpells.GetValueOrDefault((position, level)) ?? 0);
    }

    /// <summary>Extra slots for a class position and spell level, on top of the ability's bonus spells (never below 0).</summary>
    [NwnxFunc("Creature", "ModifyNumberBonusSpells")]
    static void ModifyNumberBonusSpells(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int position = n.PopInt(); int level = n.PopInt(); int delta = n.PopInt();
        if (c == null || position < 0 || position >= c.Classes.Count || level is < 0 or > 9) return;
        var d = O(c).BonusSpells;
        d[(position, level)] = Math.Clamp(d.GetValueOrDefault((position, level)) + delta, 0, 255);
    }

    [NwnxFunc("Creature", "SetDomain")]
    static void SetDomain(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int index = n.PopInt(); int domain = n.PopInt();
        var k = ClassOf(c, cls);
        if (k == null) return;
        if (index == 1) k.Domain1 = domain;
        else if (index == 2) k.Domain2 = domain;
    }
    [NwnxFunc("Creature", "SetSpecialization")]
    static void SetSpecialization(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int school = n.PopInt();
        if (ClassOf(c, cls) is { } k) k.School = school;
    }

    // ---- Caster levels ---------------------------------------------------------

    [NwnxFunc("Creature", "SetCasterLevelModifier")]
    static void SetCasterLevelModifier(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int mod = n.PopInt(); bool persist = n.PopInt() != 0;
        if (c == null) return;
        if (mod == 0) c.Overrides?.CasterLevelModifier.Remove(cls);
        else O(c).CasterLevelModifier[cls] = mod;
        Persist(c, $"CASTERLEVEL_MODIFIER{cls}", persist && mod != 0 ? mod : null);
    }
    [NwnxFunc("Creature", "GetCasterLevelModifier")]
    static void GetCasterLevelModifier(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt();
        n.Push(c?.Overrides?.CasterLevelModifier.GetValueOrDefault(cls) ?? 0);
    }
    /// <summary>A caster level for the class's spells (nwserver: a 20th-level wizard set to 5 casts at 5); -1 removes it.</summary>
    [NwnxFunc("Creature", "SetCasterLevelOverride")]
    static void SetCasterLevelOverride(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt(); int level = n.PopInt(); bool persist = n.PopInt() != 0;
        if (c == null) return;
        if (level < 0) c.Overrides?.CasterLevelOverride.Remove(cls);
        else O(c).CasterLevelOverride[cls] = level;
        Persist(c, $"CASTERLEVEL_OVERRIDE{cls}", persist && level >= 0 ? level : null);
    }
    [NwnxFunc("Creature", "GetCasterLevelOverride")]
    static void GetCasterLevelOverride(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int cls = n.PopInt();
        n.Push(c?.Overrides is { } o && o.CasterLevelOverride.TryGetValue(cls, out int v) ? v : -1);
    }

    /// <summary>
    /// The caster level of the creature's last item spell; set from a spell hook it is the level the spell being
    /// cast from an item runs at.
    /// </summary>
    [NwnxFunc("Creature", "SetLastItemCasterLevel")]
    static void SetLastItemCasterLevel(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int level = n.PopInt();
        if (c == null) return;
        c.LastItemCasterLevel = level;
        if (c.LastSpell is { } info && info.CastItem != GameObject.InvalidId) info.CasterLevel = level;
    }
    [NwnxFunc("Creature", "GetLastItemCasterLevel")]
    static void GetLastItemCasterLevel(NwnxCall n) => n.Push(n.PopObject<Creature>()?.LastItemCasterLevel ?? -1);

    /// <summary>
    /// A spell resistance for the creature, standing until its spell resistance effects change (then it is gone, as
    /// the header warns and nwserver showed).
    /// </summary>
    [NwnxFunc("Creature", "SetSpellResistance")]
    static void SetSpellResistance(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int sr = n.PopInt();
        if (c == null) return;
        var o = O(c);
        o.SpellResistance = Math.Max(0, sr);
        o.SpellResistanceEffects = CreatureOverrides.EffectStamp(c, CreatureOverrides.IsSpellResistanceEffect);
    }

    // ---- Casts ---------------------------------------------------------------

    /// <summary>
    /// Casts a spell at once as from an item: no class, caster level as given, no slot, the impact (or the given
    /// script; "****" for none) a moment later, or when the projectile lands.
    /// </summary>
    [NwnxFunc("Creature", "DoItemCastSpell")]
    static void DoItemCastSpell(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var target = n.PopObject(); var loc = n.PopLocation();
        int spell = n.PopInt(), casterLevel = n.PopInt(); float flight = n.PopFloat();
        n.PopInt(); n.PopInt(); // projectile path and its spell: presentation only
        var item = n.PopObject<Item>(); var script = n.PopString();
        if (c == null || spell < 0) return;
        var w = n.World;
        var spells = w.System<SpellSystem>();
        int level = SpellTable.Innate(w, spell);
        var info = new SpellCastInfo
        {
            SpellId = spell,
            CasterLevel = casterLevel,
            MetaMagic = SpellSlots.MetaAny,
            ClassType = NwRules.ClassInvalid,
            SaveDC = 10 + level + level / 2,
            SpellLevel = level,
            CastItem = item?.Id ?? GameObject.InvalidId,
            Target = target?.Id ?? GameObject.InvalidId,
            TargetLocation = target != null ? spells.TargetLocationOf(target) : loc,
        };
        w.After(Math.Max(0, flight), () =>
        {
            if (c.IsDestroyed) return;
            c.LastItemCasterLevel = casterLevel;
            spells.RunImpact(c, info, item, script);
        }, persist: false);
    }

    /// <summary>
    /// Queues a cast as the class in a position (0-2), a cheat cast (255) or a spell-like ability (254). Fails when
    /// the creature has nothing to cast it from (nwserver: a wizard without the spell prepared). With bInstant the
    /// spell goes off without casting time.
    /// </summary>
    [NwnxFunc("Creature", "AddCastSpellActions")]
    static void AddCastSpellActions(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var target = n.PopObject(); var at = n.PopVector();
        int spell = n.PopInt(), position = n.PopInt(), meta = n.PopInt(), domainLevel = n.PopInt(), path = n.PopInt();
        bool instant = n.PopInt() != 0, clear = n.PopInt() != 0, front = n.PopInt() != 0;
        if (c == null || target == null || spell < 0) { n.Push(0); return; }
        var w = n.World;
        int cls;
        bool cheat = false;
        if (position == 255) { cls = -1; cheat = true; }
        else if (position == 254) cls = NwRules.ClassInvalid;
        else if (position >= 0 && position < c.Classes.Count) cls = c.Classes[position].Class;
        else { n.Push(0); return; }
        if (!cheat && SpellSlots.Find(w, c, spell, meta, cls, domainLevel, false) == null) { n.Push(0); return; }
        var action = new CastSpellAction
        {
            Spell = spell, MetaMagic = meta, Cheat = cheat, DomainLevel = domainLevel, ProjectilePathType = path,
            Instant = instant, Class = cls,
        };
        if (target is Areas.Area area) action.TargetLocation = new Location(area.Id, at, 0f);
        else action.Target = target.Id;
        if (clear) ActionQueues.Clear(w, c);
        if (front) c.Actions.Insert(0, action); else c.Actions.Add(action);
        n.Push(1);
    }
}

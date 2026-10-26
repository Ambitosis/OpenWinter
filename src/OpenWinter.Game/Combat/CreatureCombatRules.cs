using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Combat;

/// <summary>What baseitems.2da says about a weapon (or the unarmed strike, BaseItem -1).</summary>
public sealed record WeaponStats
{
    public int BaseItem = -1;
    public int NumDice, Die;
    /// <summary>Threat range size (1 = 20 only, 2 = 19-20, ...).</summary>
    public int CritThreat = 1;
    public int CritMult = 2;
    /// <summary>baseitems WeaponType: 1 piercing, 2 bludgeoning, 3 slashing, 4 piercing+slashing, 5 bludgeoning+piercing.</summary>
    public int Type = 2;
    public int Size;
    public int Wield;
    public bool Ranged, Thrown, Creature, Unarmed;
    /// <summary>baseitems AmmunitionType: 1 arrow, 2 bolt, 3 bullet, 4 dart, 5 shuriken, 6 throwing axe.</summary>
    public int AmmoType;
    /// <summary>Base item of the ammunition a launcher fires (baseitems RangedWeapon).</summary>
    public int AmmoBaseItem = -1;
    public int Focus = -1, EpicFocus = -1, Spec = -1, EpicSpec = -1, ImprovedCritical = -1, Overwhelming = -1, Devastating = -1, OfChoice = -1;
    public float PreferredDistance = 1.0f;
    public int[] ProficiencyFeats = Array.Empty<int>();
    public bool IsCrossbow => Wield == CombatK.WieldCrossbow;
    public bool IsBow => Wield == CombatK.WieldBow;
    public bool IsDouble => Wield == CombatK.WieldDouble;
}

/// <summary>
/// The D&amp;D 3.x rules of the original engine for weapon attacks: attack bonus, armour class against
/// an attacker, concealment, critical hits and damage. Numbers come from baseitems.2da, feat.2da,
/// ruleset.2da and the item property tables, so modules that change those see the change here.
/// </summary>
public sealed partial class CreatureCombatRules : ICombatRules
{
    readonly World _w;
    static readonly ConditionalWeakTable<TwoDA, Dictionary<int, WeaponStats>> StatsCache = new();
    readonly Dictionary<string, Item?> _virtualAmmo = new(StringComparer.OrdinalIgnoreCase);

    public CreatureCombatRules(World world) { _w = world; }

    TwoDA BaseItems => _w.Resources.TwoDA("baseitems");
    int RI(string label, int fallback) => Ruleset.Int(_w, label, fallback);
    float RF(string label, float fallback) => Ruleset.Float(_w, label, fallback);
    bool Has(Creature c, int feat) => feat >= 0 && CreatureRules.HasFeat(_w, c, feat, ignoreUses: true);
    static bool HasEffect(GameObject o, int type) => CombatVision.Has(o, type);
    int Mod(Creature c, int ability) => CreatureRules.AbilityModifier(_w, c, ability);

    // ------------------------------------------------------------------ weapons

    static readonly WeaponStats UnarmedStats = new()
    {
        BaseItem = -1, Unarmed = true, Type = 2, CritThreat = 1, CritMult = 2, Size = 0, PreferredDistance = 1.0f,
        Focus = CombatK.FeatWeaponFocusUnarmed, EpicFocus = CombatK.FeatEpicWeaponFocusUnarmed,
        Spec = CombatK.FeatWeaponSpecializationUnarmed, EpicSpec = CombatK.FeatEpicWeaponSpecializationUnarmed,
        ImprovedCritical = CombatK.FeatImprovedCriticalUnarmed, Overwhelming = CombatK.FeatEpicOverwhelmingCriticalUnarmed,
        Devastating = CombatK.FeatEpicDevastatingCriticalUnarmed,
    };

    /// <summary>baseitems.2da data for a weapon; the unarmed strike for null.</summary>
    public WeaponStats Stats(Item? weapon) => weapon == null ? UnarmedStats : Stats(weapon.BaseItem);

    public WeaponStats Stats(int baseItem)
    {
        if (baseItem < 0) return UnarmedStats;
        var bi = BaseItems;
        var cache = StatsCache.GetValue(bi, _ => new Dictionary<int, WeaponStats>());
        lock (cache)
        {
            if (cache.TryGetValue(baseItem, out var s)) return s;
            int Feat(string col) => bi.ColumnIndex(col) >= 0 && !bi.IsEmpty(baseItem, col) ? bi.GetInt(baseItem, col, -1) : -1;
            int wield = bi.GetInt(baseItem, "WeaponWield", 0);
            bool ranged = !bi.IsEmpty(baseItem, "RangedWeapon");
            var prof = new List<int>();
            for (int i = 0; i < 5; i++) { int f = Feat("ReqFeat" + i); if (f >= 0) prof.Add(f); }
            s = new WeaponStats
            {
                BaseItem = baseItem,
                NumDice = bi.GetInt(baseItem, "NumDice", 0),
                Die = bi.GetInt(baseItem, "DieToRoll", 0),
                CritThreat = Math.Max(1, bi.GetInt(baseItem, "CritThreat", 1)),
                CritMult = Math.Max(1, bi.GetInt(baseItem, "CritHitMult", 2)),
                Type = bi.GetInt(baseItem, "WeaponType", 2),
                Size = bi.GetInt(baseItem, "WeaponSize", 0),
                Wield = wield,
                Ranged = ranged,
                Thrown = ranged && wield == CombatK.WieldThrown,
                Creature = wield == CombatK.WieldCreature,
                AmmoType = bi.GetInt(baseItem, "AmmunitionType", 0),
                AmmoBaseItem = ranged ? bi.GetInt(baseItem, "RangedWeapon", -1) : -1,
                Focus = Feat("WeaponFocusFeat"), EpicFocus = Feat("EpicWeaponFocusFeat"),
                Spec = Feat("WeaponSpecializationFeat"), EpicSpec = Feat("EpicWeaponSpecializationFeat"),
                ImprovedCritical = Feat("WeaponImprovedCriticalFeat"), Overwhelming = Feat("EpicWeaponOverwhelmingCriticalFeat"),
                Devastating = Feat("EpicWeaponDevastatingCriticalFeat"), OfChoice = Feat("WeaponOfChoiceFeat"),
                PreferredDistance = bi.GetFloat(baseItem, "PrefAttackDist", 1.0f),
                ProficiencyFeats = prof.ToArray(),
            };
            cache[baseItem] = s;
            return s;
        }
    }

    /// <summary>A held item that attacks: a ranged weapon or anything with a weapon type (shields and torches have type 0).</summary>
    public bool IsWeapon(Item? item) => item != null && (Stats(item).Ranged || BaseItems.GetInt(item.BaseItem, "WeaponType", 0) > 0);

    /// <summary>The creature's weapons: main hand (null = unarmed), off hand (a second weapon, or a double weapon's other end), creature weapons.</summary>
    public (Item? Main, Item? Off, List<Item> CreatureWeapons) Weapons(Creature c)
    {
        var right = c.Equipment.GetValueOrDefault(EquipSlot.RightHand);
        var left = c.Equipment.GetValueOrDefault(EquipSlot.LeftHand);
        Item? main = IsWeapon(right) ? right : null;
        Item? off = null;
        if (main != null && !Stats(main).Ranged)
        {
            if (Stats(main).IsDouble) off = main;
            else if (IsWeapon(left) && !Stats(left!).Ranged) off = left;
        }
        else if (main == null && IsWeapon(left) && !Stats(left!).Ranged) main = left; // weapon only in the left hand
        var creature = new List<Item>();
        if (main == null)
            foreach (int slot in new[] { EquipSlot.CreatureRight, EquipSlot.CreatureLeft, EquipSlot.CreatureBite })
                if (c.Equipment.TryGetValue(slot, out var cw)) creature.Add(cw);
        return (main, off, creature);
    }

    /// <summary>Monks fighting unarmed (or with a kama) without armour or shield use the monk attack progression.</summary>
    public bool MonkProgression(Creature c, Item? main)
    {
        if (CreatureRules.ClassLevel(c, NwRules.ClassMonk) <= 0) return false;
        if (main != null && main.BaseItem != CombatK.BaseKama) return false;
        return CreatureRules.ArmourBaseAC(_w, c) == 0 && CreatureRules.ShieldBaseAC(_w, c) == 0;
    }

    bool InMode(Creature c, int mode) => c.ActionModes.Contains(mode);

    public bool FlurryActive(Creature c, Item? main) =>
        InMode(c, CombatK.ModeFlurry) && Has(c, CombatK.FeatFlurryOfBlows) && MonkProgression(c, main);

    public bool RapidShotActive(Creature c, Item? main) =>
        InMode(c, CombatK.ModeRapidShot) && Has(c, CombatK.FeatRapidShot) && main != null && Stats(main).Ranged && !Stats(main).IsCrossbow;

    public bool DirtyFightingActive(Creature c) => InMode(c, CombatK.ModeDirtyFighting) && Has(c, CombatK.FeatDirtyFighting);

    /// <summary>Iterative attack penalty per extra attack (ruleset.2da; 0 = none).</summary>
    public int IterativePenalty(Creature c, Item? main) => MonkProgression(c, main)
        ? RI("MULTIPLE_ATTACKS_BAB_PENALTY_MULTIPLIER_MONK", 3)
        : RI("MULTIPLE_ATTACKS_BAB_PENALTY_MULTIPLIER", 5);

    // ------------------------------------------------------------------ the round

    public IReadOnlyList<PlannedAttack> PlanRound(Creature c)
    {
        CreatureRules.EnsureInit(_w, c);
        var list = new List<PlannedAttack>();
        if (InMode(c, CombatK.ModeParry)) return list; // parrying: only ripostes
        var (main, off, cw) = Weapons(c);
        var ms = Stats(main);
        int count = CreatureRules.AttacksPerRound(_w, c);
        int step = Math.Max(0, IterativePenalty(c, main));
        if (ms.IsCrossbow && !Has(c, CombatK.FeatRapidReload)) count = Math.Min(count, Math.Max(1, RI("CROSSBOW_ATTACKS", 1)));

        // Haste (effect or item property) and slow cancel each other out; slowed creatures attack once (SLOWED_ATTACKS is a count).
        bool hasted = CreatureRules.IsHasted(c), slowed = CreatureRules.IsSlowed(c);
        int extra = 0;
        if (hasted) extra += RI("HASTED_BONUS_ATTACKS", 1);
        if (slowed) count = Math.Max(1, RI("SLOWED_ATTACKS", 1));
        foreach (var e in c.Effects) if (e.Type == CombatK.EffectModifyAttacks) extra += e.Ints[0];
        if (RapidShotActive(c, main)) extra++;
        if (FlurryActive(c, main)) extra++;
        bool dirty = DirtyFightingActive(c);
        if (dirty) { count = Math.Max(1, RI("DIRTY_FIGHTING_NUM_ATTACKS_PER_ROUND", 1)); extra = 0; }

        Item? Pick(int i) => cw.Count > 0 ? cw[i % cw.Count] : main;
        for (int i = 0; i < count; i++) list.Add(new PlannedAttack { Weapon = Pick(i), Penalty = i * step });
        // Extra attacks (haste, EffectModifyAttacks, flurry, rapid shot) have their own iterative penalties, as in the original.
        list.InsertRange(Math.Min(1, list.Count), Enumerable.Range(0, Math.Clamp(extra, 0, 5))
            .Select(i => new PlannedAttack { Weapon = Pick(i), Extra = true, Penalty = i * step }));
        if (off != null && !dirty)
        {
            list.Add(new PlannedAttack { Weapon = off, OffHand = true });
            if (Has(c, CombatK.FeatImprovedTwoWeaponFighting))
                list.Add(new PlannedAttack { Weapon = off, OffHand = true, Penalty = Math.Max(step, 5) });
        }
        return list;
    }

    // ------------------------------------------------------------------ context

    public AttackContext Context(Creature attacker, GameObject target, Item? weapon, bool offHand, IDice dice)
    {
        var s = Stats(weapon);
        var a = new AttackContext
        {
            World = _w, Attacker = attacker, Target = target, Dice = dice, Weapon = weapon, OffHand = offHand,
            Ranged = s.Ranged, Thrown = s.Thrown, CreatureWeapon = s.Creature,
        };
        if (a.Ranged && !a.Thrown && weapon != null) a.Ammo = AmmoFor(attacker, weapon);
        Prepare(a);
        return a;
    }

    /// <summary>Equipment slot a launcher's ammunition goes in (0 for thrown weapons, which are their own ammunition).</summary>
    public static int AmmoSlot(int ammoType) => ammoType switch
    {
        1 => EquipSlot.Arrows,
        2 => EquipSlot.Bolts,
        3 => EquipSlot.Bullets,
        _ => 0,
    };

    /// <summary>The ammunition a launcher would fire: the Unlimited Ammunition blueprint, else the equipped stack of the right kind.</summary>
    public Item? AmmoFor(Creature c, Item launcher)
    {
        var s = Stats(launcher);
        if (UnlimitedAmmo(launcher) is { } unlimited) return unlimited;
        int slot = AmmoSlot(s.AmmoType);
        if (slot == 0) return null;
        var ammo = c.Equipment.GetValueOrDefault(slot);
        if (ammo == null || ammo.IsDestroyed) return null;
        return s.AmmoBaseItem < 0 || ammo.BaseItem == s.AmmoBaseItem ? ammo : null;
    }

    /// <summary>Unlimited Ammunition: the iprp_ammocost.2da blueprint for the launcher's ammunition (cached, not in the world).</summary>
    public Item? UnlimitedAmmo(Item launcher)
    {
        var prop = launcher.Properties.FirstOrDefault(p => p.Type == CombatK.IpUnlimitedAmmo);
        if (prop == null) return null;
        var s = Stats(launcher);
        string column = s.AmmoType switch { 1 => "Arrow", 2 => "Bolt", 3 => "Bullet", _ => "" };
        if (column.Length == 0) return null;
        var resref = _w.Resources.TwoDA("iprp_ammocost").Get(Math.Max(1, prop.CostTableValue), column);
        if (resref.Length == 0) resref = _w.Resources.TwoDA("iprp_ammocost").Get(1, column);
        if (resref.Length == 0) return null;
        if (!_virtualAmmo.TryGetValue(resref, out var item)) _virtualAmmo[resref] = item = _w.Items.FromBlueprint(resref.ToLowerInvariant());
        return item;
    }

    public bool HasUnlimitedAmmo(Item launcher) => launcher.Properties.Any(p => p.Type == CombatK.IpUnlimitedAmmo);

    public void Prepare(AttackContext a)
    {
        var att = a.Attacker;
        a.Distance = Vector2.Distance(new(att.Position.X, att.Position.Y), new(a.Target.Position.X, a.Target.Position.Y));
        a.TargetUnseen = !CombatHooks.CanSee(_w, att, a.Target);
        if (a.Target is not Creature t) { a.AttackerUnseen = false; a.TargetDeniedDex = true; a.FlatFooted = false; a.Flanked = false; return; }
        // An attacker in darkness isn't "unseen" for the +2 and the target's Dexterity, as on nwserver.
        a.AttackerUnseen = !CombatHooks.CanSee(_w, t, att) && !(HasEffect(att, CombatK.EffectDarkness) && CombatVision.CanSee(_w, t, att, darkness: false));
        // As in the original, what the target is attacking decides: nothing (flat-footed), someone else (flanked).
        uint theirTarget = _w.System<CombatSystem>().TargetOf(t);
        a.FlatFooted = theirTarget == GameObject.InvalidId;
        a.TargetDeniedDex = DeniedDex(t, a.AttackerUnseen, a.FlatFooted);
        a.Flanked = theirTarget != GameObject.InvalidId && theirTarget != att.Id && IsFlanked(a);
    }

    /// <summary>
    /// The target loses its Dexterity bonus to AC: it can't see the attacker, or it's flat-footed (not attacking
    /// anyone, even while being attacked). Uncanny Dodge keeps it. Being paralysed, stunned or asleep doesn't matter
    /// in the original.
    /// </summary>
    public bool DeniedDex(Creature t, bool attackerUnseen, bool flatFooted)
    {
        if (!attackerUnseen && !flatFooted) return false;
        return !CombatVision.UncannyDodge(_w, t);
    }

    /// <summary>
    /// Flanking, for a target attacking someone else: wherever that is, as in the original. Ranged attacks only
    /// within MAX_RANGED_FLANK_ATTACK_DISTANCE. Improved Uncanny Dodge prevents it unless the attacker has
    /// FLANK_LEVEL_RANGE more rogue levels.
    /// </summary>
    public bool IsFlanked(AttackContext a)
    {
        if (a.Target is not Creature t) return false;
        if (a.Ranged && a.Distance > RF("MAX_RANGED_FLANK_ATTACK_DISTANCE", 10f)) return false;
        if (CombatVision.ImprovedUncannyDodge(_w, t))
        {
            int theirs = CreatureRules.ClassLevel(t, NwRules.ClassRogue) + CreatureRules.ClassLevel(t, NwRules.ClassBarbarian);
            if (CreatureRules.ClassLevel(a.Attacker, NwRules.ClassRogue) < theirs + RI("FLANK_LEVEL_RANGE", 4)) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Numeric value of an item property's cost table row ("Value" column, else the row).</summary>
    int Value(ItemProperty ip) => CreatureRules.PropertyValue(_w, ip);

    static IEnumerable<ItemProperty> Props(Item? item)
    {
        if (item == null) yield break;
        foreach (var p in item.Properties) if (p.Valid) yield return p;
    }

    /// <summary>The item whose weapon properties (enhancement, attack and damage bonuses, keen) an attack uses: the weapon, or gloves for an unarmed strike.</summary>
    public Item? WeaponItem(AttackContext a) => a.Weapon ?? (a.Attacker.Equipment.GetValueOrDefault(EquipSlot.Arms) is { BaseItem: CombatK.BaseGloves } g ? g : null);

    /// <summary>Best enhancement bonus of the attack's weapon against the target (enhancement and enhancement-versus properties) less decreases.</summary>
    public int Enhancement(AttackContext a)
    {
        int best = 0, penalty = 0;
        foreach (var p in Props(WeaponItem(a)))
        {
            switch (p.Type)
            {
                case CombatK.IpEnhancement: best = Math.Max(best, Value(p)); break;
                case CombatK.IpEnhancementVsAlignGroup or CombatK.IpEnhancementVsRace or CombatK.IpEnhancementVsAlign:
                    if (ItemPropertyVersus.Applies(_w, p, a.Target)) best = Math.Max(best, Value(p));
                    break;
                case CombatK.IpDecreasedEnhancement: penalty += Math.Abs(Value(p)); break;
            }
        }
        return best - penalty;
    }

    /// <summary>Highest arcane archer Enchant Arrow bonus (applies to arrows from bows).</summary>
    public int EnchantArrow(AttackContext a)
    {
        if (!Stats(a.Weapon).IsBow) return 0;
        int best = 0;
        foreach (var (from, to, first) in CombatK.EnchantArrowChains)
            for (int f = to; f >= from; f--)
                if (Has(a.Attacker, f)) { best = Math.Max(best, RI($"PRESTIGE_ENCHANT_ARROW_{first + f - from}_BONUS", first + f - from)); break; }
        return best;
    }

    /// <summary>The target is a favoured enemy of the attacker (ranger favoured enemy feats).</summary>
    public bool FavoredEnemy(Creature attacker, GameObject target)
    {
        if (target is not Creature t) return false;
        // A race with a parent race (NWNX_Race) counts as either; races can have their own favoured enemy feat.
        int parent = Nwnx.Plugins.RulesModifiers.ParentRace(_w, t.Race);
        foreach (var (feat, race) in CombatK.FavoredEnemies)
            if ((race == t.Race || race == parent) && Has(attacker, feat)) return true;
        if (Nwnx.Plugins.RulesModifiers.FavoredEnemyFeat(_w, t.Race) is >= 0 and var own && Has(attacker, own)) return true;
        return parent != t.Race && Nwnx.Plugins.RulesModifiers.FavoredEnemyFeat(_w, parent) is >= 0 and var inherited && Has(attacker, inherited);
    }

    /// <summary>The weapon of choice (weapon master) is this weapon.</summary>
    bool WeaponOfChoice(Creature c, WeaponStats s) => s.OfChoice >= 0 && Has(c, s.OfChoice);

    /// <summary>A feat id by its feat.2da LABEL (for feats some content adds), or -1.</summary>
    public int FeatByLabel(string label)
    {
        var feats = _w.Resources.TwoDA("feat");
        return feats.Find("LABEL", label);
    }
}

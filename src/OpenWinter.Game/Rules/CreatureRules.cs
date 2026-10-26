using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Derived creature statistics computed from 2DA data, the creature's base values, its
/// effects and the item properties of its equipped items: ability scores, BAB, saves,
/// armour class, skills, hit points, spell resistance, arcane spell failure, attacks.
/// Everything is computed on demand; nothing here is cached except template-derived state.
/// </summary>
public static partial class CreatureRules
{
    // ------------------------------------------------------------------ effects and item properties

    /// <summary>
    /// Every effect currently on an object, including linked children that weren't
    /// applied as separate entries.
    /// </summary>
    public static IEnumerable<Effect> Effects(GameObject o)
    {
        if (o.Effects.Count == 0) yield break;
        HashSet<Effect>? top = null;
        foreach (var e in o.Effects.ToArray())
        {
            if (e.Type > 0) yield return e;
            if (e.Linked.Count == 0) continue;
            top ??= new HashSet<Effect>(o.Effects, ReferenceEqualityComparer.Instance);
            foreach (var child in Linked(e, top, 0)) yield return child;
        }
    }

    static IEnumerable<Effect> Linked(Effect e, HashSet<Effect> top, int depth)
    {
        if (depth > 16) yield break;
        foreach (var l in e.Linked)
        {
            if (top.Contains(l)) continue;
            if (l.Type > 0) yield return l;
            foreach (var x in Linked(l, top, depth + 1)) yield return x;
        }
    }

    /// <summary>Item properties of every equipped item, with the slot bit the item is in.</summary>
    public static IEnumerable<(Item Item, int Slot, ItemProperty Prop)> EquippedProperties(Creature c)
    {
        // Polymorphed, only the form's items are equipped (the creature's own are put away).
        foreach (var (slot, item) in c.Equipment.ToArray())
            foreach (var p in item.Properties.ToArray())
                if (p.Valid) yield return (item, slot, p);
    }

    /// <summary>The numeric value an item property's cost table row stands for (its "Value" column, else the row number).</summary>
    public static int PropertyValue(World w, ItemProperty ip)
    {
        var table = CostTable(w, ip);
        if (table == null || table.ColumnIndex("Value") < 0) return ip.CostTableValue;
        var v = table.Get(ip.CostTableValue, "Value");
        if (v.Length == 0) return ip.CostTableValue;
        return table.GetInt(ip.CostTableValue, "Value", ip.CostTableValue);
    }

    /// <summary>Float "Value" of an item property's cost table row (weight reductions are fractions).</summary>
    public static float PropertyValueFloat(World w, ItemProperty ip) =>
        CostTable(w, ip)?.GetFloat(ip.CostTableValue, "Value", ip.CostTableValue) ?? ip.CostTableValue;

    static TwoDA? CostTable(World w, ItemProperty ip)
    {
        int table = ip.CostTable;
        if (table <= 0) table = w.Resources.TwoDA("itempropdef").GetInt(ip.Type, "CostTableResRef", -1);
        if (table < 0) return null;
        var name = w.Resources.TwoDA("iprp_costtable").Get(table, "Name");
        return name.Length == 0 ? null : w.Resources.TwoDA(name.ToLowerInvariant());
    }

    static int Clamp(int bonus, int penalty, int bonusLimit, int penaltyLimit) =>
        Math.Min(bonus, bonusLimit) - Math.Min(penalty, penaltyLimit);

    // ------------------------------------------------------------------ template-derived state

    /// <summary>Reads rules state from the creature's template the first time rules touch it.</summary>
    public static void EnsureInit(World w, Creature c)
    {
        if (c.RulesInitialised) return;
        c.RulesInitialised = true;
        var t = c.Template;
        foreach (var f in t.GetList("FeatList"))
            if (f.Has("Uses")) c.FeatUses[(int)f.GetInt("Feat")] = (int)f.GetInt("Uses");
        c.FootstepOverride = (int)t.GetInt("FootstepType", -1);
        if (c.FootstepOverride == 65535) c.FootstepOverride = -1;
        c.StartingPackage = (int)t.GetInt("StartingPackage", 255);
        c.FamiliarType = t.Has("FamiliarType") ? (int)t.GetInt("FamiliarType") : 255;
        c.CompanionType = t.Has("CompanionType") ? (int)t.GetInt("CompanionType") : 255;
        c.FamiliarName = t.GetString("FamiliarName");
        c.CompanionName = t.GetString("CompanionName");
        // A blueprint's Lootable tick also makes the body selectable (and not raiseable, CreatureStats) unless the
        // creature says otherwise, as a saved one does (nwserver corpse_drops: lootf, lootfi).
        c.SelectableWhenDead = t.Has("DeadSelectable") ? t.GetInt("DeadSelectable") != 0 : t.GetInt("Lootable") != 0;
        if (!t.Has("CreatureSize")) c.Size = AppearanceSize(w, c.AppearanceType);
        // OverrideBAB holds the SetBaseAttackBonus attack count (0 = none).
        c.BaseAttackOverride = t.GetInt("OverrideBAB") > 0 ? (int)t.GetInt("OverrideBAB") : -1;
        if (c.StealthMode) c.ActionModes.Add(ActionModeStealth);
        if (c.DetectMode) c.ActionModes.Add(ActionModeDetect);

        foreach (var s in t.GetList("SpecAbilityList"))
            c.SpellAbilities.Add(new SpellAbility
            {
                Spell = (int)s.GetInt("Spell"),
                CasterLevel = (int)s.GetInt("SpellCasterLevel"),
                Ready = !s.Has("SpellFlags") || (s.GetInt("SpellFlags") & 1) != 0,
            });

        var classes = t.GetList("ClassList");
        for (int ci = 0; ci < classes.Count && ci < c.Classes.Count; ci++)
        {
            var perDay = classes[ci].GetList("SpellsPerDayList");
            for (int lvl = 0; lvl < perDay.Count && lvl < 10; lvl++)
                if (perDay[lvl].Has("NumSpellsLeft")) c.SpellSlotsLeft[(ci, lvl)] = (int)perDay[lvl].GetInt("NumSpellsLeft");
            for (int lvl = 0; lvl < 10; lvl++)
            {
                var mem = classes[ci].GetList($"MemorizedList{lvl}");
                for (int k = 0; k < mem.Count; k++)
                    if (mem[k].GetInt("SpellDomain") != 0) c.DomainSlots.Add((ci, lvl, k));
            }
        }

        // Hit points: the rolled hit dice (HitPoints) plus the Constitution and feat bonus; the stored MaxHitPoints is
        // not consulted (parity suite hp_blueprints, 831 blueprints: nw_beggmale stores 4 and reads 3, its 2 rolled
        // plus Toughness; nw_gnthill stores 110 and reads 102). Only a template without HitPoints keeps its stored
        // maximum. Equipment and effects add their Constitution on top (hp_items: boots of +2 Constitution add 8
        // to an 8th-level henchman, current hit points with them).
        int max = c.MaxHitPoints;
        int bonus = HitPointBonus(w, c, effects: false, items: false);
        c.BaseHitPoints = t.Has("HitPoints") ? (int)t.GetInt("HitPoints") : max - bonus;
        c.HitPointOffset = t.Has("HitPoints") ? 0 : max - (c.BaseHitPoints + bonus);
        int full = Math.Max(1, c.BaseHitPoints + HitPointBonus(w, c) + c.HitPointOffset);
        // Templates store current hit points without the Constitution and feat bonus; character
        // files also keep the real value in PregameCurrent.
        // A blueprint's current hit points aren't held to the maximum (tm_cr_dragblksml stores 150 of 104 rolled and
        // reads 202/156); a copy's are (the bandit copied at 14/14 with an effect's Constitution reads 12/12).
        bool stored = c.CurrentHitPoints == (int)t.GetInt("CurrentHitPoints", 1);
        if (stored && !t.Has("PregameCurrent")) c.CurrentHitPoints += full - c.BaseHitPoints - c.HitPointOffset;
        else
        {
            if (stored) c.CurrentHitPoints = (int)t.GetInt("PregameCurrent");
            if (!c.IsDead) c.CurrentHitPoints = Math.Min(c.CurrentHitPoints, full);
        }
        if (!c.IsDead) c.MaxHitPoints = full;
    }

    /// <summary>
    /// The maximum hit points a blueprint, copy or character file stores: without the Constitution that equipment
    /// and effects add (the original's CopyObject of a bandit with +4 Constitution from an effect reads 12/12, its
    /// own maximum, and a henchman copied with its Constitution boots on keeps their 8).
    /// </summary>
    public static int StoredMaxHitPoints(World w, Creature c, int max) =>
        Math.Max(1, max - (HitPointBonus(w, c) - HitPointBonus(w, c, effects: false, items: false)));

    public const int ActionModeDetect = 0, ActionModeStealth = 1;

    // ------------------------------------------------------------------ classes and levels

    /// <summary>Level in a class (0 if none).</summary>
    public static int ClassLevel(Creature c, int cls) => c.Classes.Where(x => x.Class == cls).Sum(x => x.Level);

    /// <summary>A class's progression table named by a classes.2da column (AttackBonusTable, SavingThrowTable, ...).</summary>
    public static TwoDA ClassTable(World w, int cls, string column)
    {
        var name = w.Resources.TwoDA("classes").Get(cls, column);
        return name.Length == 0 ? TwoDA.Empty : w.Resources.TwoDA(name.ToLowerInvariant());
    }

    /// <summary>
    /// Class levels taken before character level 21 (which use the class tables), and the
    /// number of epic levels after them. The level history (LvlStatList) decides which
    /// classes were taken first; without one, classes fill the first 20 levels in order.
    /// A history with an entry for every level counts levels by its classes, and an entry for a class the creature
    /// no longer has counts for nothing (nwserver nwnx_creature_levels: a fighter 1 / bard 2 whose fighter position
    /// became cleric, the history kept, has a bard's saves only).
    /// </summary>
    public static (List<(int Class, int Level)> PreEpic, int Epic) LevelSplit(Creature c)
    {
        int total = c.TotalLevel;
        var pre = new List<(int Class, int Level)>();
        var history = c.Template.GetList("LvlStatList");
        if (total <= 20 && history.Count != total)
        {
            foreach (var cc in c.Classes) pre.Add((cc.Class, cc.Level));
            return (pre, 0);
        }
        var counts = new Dictionary<int, int>();
        if (history.Count >= Math.Min(20, total))
        {
            for (int i = 0; i < Math.Min(20, total); i++)
            {
                int cls = (int)history[i].GetInt("LvlStatClass");
                counts[cls] = counts.GetValueOrDefault(cls) + 1;
            }
        }
        else
        {
            int left = 20;
            foreach (var cc in c.Classes)
            {
                int take = Math.Min(left, cc.Level);
                counts[cc.Class] = counts.GetValueOrDefault(cc.Class) + take;
                left -= take;
            }
        }
        foreach (var cc in c.Classes)
            if (counts.TryGetValue(cc.Class, out int n) && n > 0) { pre.Add((cc.Class, Math.Min(n, cc.Level))); counts[cc.Class] = 0; }
        return (pre, Math.Max(0, total - 20));
    }

    // ------------------------------------------------------------------ abilities

    /// <summary>
    /// Bonuses (or penalties) from effects: those of one spell don't stack (the largest counts), the rest add up.
    /// </summary>
    sealed class SpellBonuses
    {
        int _loose;
        Dictionary<int, int>? _bySpell;

        public void Add(Effect e, int value)
        {
            if (e.SpellId < 0) { _loose += value; return; }
            _bySpell ??= new Dictionary<int, int>();
            _bySpell[e.SpellId] = _bySpell.TryGetValue(e.SpellId, out int best) ? Math.Max(best, value) : value;
        }

        public int Total => _loose + (_bySpell?.Values.Sum() ?? 0);
    }

    /// <summary>Total item and effect adjustments to an ability: (bonus, penalty), both positive.</summary>
    public static (int Bonus, int Penalty) AbilityAdjustments(World w, Creature c, int ability, bool effects = true, bool items = true)
    {
        int dec = 0;
        var bonuses = new SpellBonuses();
        var penalties = new SpellBonuses();
        foreach (var e in effects ? Effects(c) : Enumerable.Empty<Effect>())
        {
            switch (e.Type)
            {
                case NwRules.EffectAbilityIncrease when e.Ints[0] == ability: bonuses.Add(e, e.Ints[1]); break;
                case NwRules.EffectAbilityDecrease when e.Ints[0] == ability: penalties.Add(e, e.Ints[1]); break;
                // A curse's Constitution loss sets in a moment after it is applied.
                case NwRules.EffectCurse when (e.CurseBlockedAbilities & (1 << ability)) == 0
                    && (ability != NwRules.AbilityCon || e.AppliedAt < w.Time):
                    dec += e.Ints[ability]; break;
            }
        }
        int inc = bonuses.Total;
        dec += penalties.Total;
        foreach (var (_, _, p) in items ? EquippedProperties(c) : Enumerable.Empty<(Item, int, ItemProperty)>())
        {
            if (p.SubType != ability) continue;
            if (p.Type == NwRules.IpAbilityBonus) inc += PropertyValue(w, p);
            else if (p.Type == NwRules.IpDecreasedAbility) dec += Math.Abs(PropertyValue(w, p));
        }
        return (Math.Max(0, inc), Math.Max(0, dec));
    }

    /// <summary>GetAbilityScore: base score plus item and effect adjustments within the bonus/penalty limits (never below 3).</summary>
    static readonly string[] RacialAdjustColumns = { "StrAdjust", "DexAdjust", "ConAdjust", "IntAdjust", "WisAdjust", "ChaAdjust" };

    /// <summary>
    /// Base ability score: the stored score plus the racial adjustment (racialtypes.2da). Character
    /// and creature files store scores before racial adjustments; the engine applies them at runtime.
    /// </summary>
    public static int BaseAbility(World w, Creature c, int ability)
    {
        // A polymorphed creature has its form's Strength, Constitution and Dexterity; the racial adjustments are
        // the form's race's either way (a drow form has the elf's +2 Dexterity and -2 Constitution).
        int score = c.Abilities[ability];
        if (ability is NwRules.AbilityStr or NwRules.AbilityCon or NwRules.AbilityDex && Polymorph(w, c) is { } p)
        {
            int v = ability == NwRules.AbilityStr ? p.Str : ability == NwRules.AbilityCon ? p.Con : p.Dex;
            if (v > 0) score = v;
        }
        return score + w.Resources.TwoDA("racialtypes").GetInt(c.Race, RacialAdjustColumns[ability]);
    }

    /// <summary>The polymorph a creature is under (EffectPolymorph), or null.</summary>
    public static PolymorphState? Polymorph(World w, Creature c) => c.PolymorphForm;

    /// <param name="helpless">Whether a helpless creature's Dexterity counts as 3 (combat leaves that to entanglement).</param>
    public static int AbilityScore(World w, Creature c, int ability, bool baseOnly = false, bool effects = true, bool helpless = true, bool items = true)
    {
        if ((uint)ability > 5) return 0;
        EnsureInit(w, c);
        int b = BaseAbility(w, c, ability);
        if (baseOnly) return b;
        // A helpless creature (held, stunned, asleep, entangled, petrified) has a Dexterity of 3.
        if (effects && helpless && ability == NwRules.AbilityDex && IsHelpless(c)) return 3;
        var (inc, dec) = AbilityAdjustments(w, c, ability, effects, items);
        int total = b + Clamp(inc, dec, w.Limits.AbilityBonus, w.Limits.AbilityPenalty);
        return Math.Max(3, total);
    }

    /// <summary>Effects that leave a creature helpless (Dexterity 3).</summary>
    public static bool IsHelpless(Creature c)
    {
        foreach (var e in c.Effects)
            if (e.Type is NwRules.EffectParalyze or NwRules.EffectStunned or NwRules.EffectSleep or NwRules.EffectEntangle
                or NwRules.EffectPetrify or NwRules.EffectCutsceneParalyze or NwRules.EffectCutsceneImmobilize)
                return true;
        return false;
    }

    /// <summary>
    /// How fast effects let a creature move, as a factor of its normal speed (measured on the original,
    /// parity suites fx_misc and movement_speed): increases multiply (haste counts as +50%) up to at most
    /// MOVEMENT_SPEED_BONUS_DEFAULT_CAP, decreases multiply (slow counts as -50%, and also stops running, see
    /// Movement), and the result is at least MOVEMENT_SPEED_PENALTY_CAP: +20% twice is 1.44, -30% and -40% is
    /// 0.42, +50% with -90% is 0.15, -90% alone 0.125. Entangled, held, stunned, asleep or petrified creatures
    /// don't move at all.
    /// </summary>
    public static float MovementSpeedFactor(World w, Creature c) => IsHelpless(c) ? 0f : MovementRateFactor(w, c);

    /// <summary>
    /// The movement rate factor speed effects give (or one a script set, which stands until the speed effects
    /// change: nwserver nwnx_creature_move), not counting conditions that stop the creature.
    /// </summary>
    public static float MovementRateFactor(World w, Creature c)
    {
        var o = c.Overrides;
        if (o?.MovementFactor is float set)
        {
            if (o.MovementFactorEffects == CreatureOverrides.EffectStamp(c, CreatureOverrides.IsSpeedEffect)) return set;
            // A speed effect taken away changes the factor at the next update, not in the same moment (nwserver
            // nwnx_creature_move: 0.8 read right after removing haste, 1.0 a tenth of a second later); one applied
            // changes it at once.
            if (c.Effects.Count(e => CreatureOverrides.IsSpeedEffect(e.Type)) < o.MovementFactorEffectCount)
            {
                o.MovementFactorRemovedAt ??= w.Time;
                if (w.Time <= o.MovementFactorRemovedAt) return set;
            }
            o.MovementFactor = null;
            o.MovementFactorRemovedAt = null;
        }
        float up = 1f, down = 1f;
        foreach (var e in c.Effects)
        {
            if (e.Type == NwRules.EffectMovementSpeedIncrease) up *= MovementIncreasePercent(e.Ints[0]) / 100f;
            else if (e.Type == NwRules.EffectMovementSpeedDecrease) down *= 1f - Math.Clamp(e.Ints[0], 0, 100) / 100f;
        }
        var (hasted, slowed) = Speed(c);
        if (hasted) up *= 1.5f;
        if (slowed) down *= 0.5f;
        up = Math.Min(up, MovementRateFactorCap(w, c));
        return Math.Max(up * down, Ruleset.Float(w, "MOVEMENT_SPEED_PENALTY_CAP", 0.125f));
    }

    /// <summary>The highest movement rate factor: the creature's own cap, else MOVEMENT_SPEED_BONUS_DEFAULT_CAP.</summary>
    public static float MovementRateFactorCap(World w, Creature c) =>
        c.Overrides is { MovementFactorCap: > 0 } o ? o.MovementFactorCap : Ruleset.Float(w, "MOVEMENT_SPEED_BONUS_DEFAULT_CAP", 1.5f);

    // Native speed_payload/speed_absolute: below 100 is an additive percentage; 100+ is absolute.
    public static int MovementIncreasePercent(int amount) => amount < 100 ? 100 + amount : amount;

    /// <summary>Slowed (and not hasted, which cancels it).</summary>
    public static bool IsSlowed(Creature c) => Speed(c).Slowed;

    /// <summary>Hasted, by an effect or an equipped Haste item property (and not slowed, which cancels it).</summary>
    public static bool IsHasted(Creature c) => Speed(c).Hasted;

    /// <summary>Haste (effect or item property) and slow cancel each other out.</summary>
    static (bool Hasted, bool Slowed) Speed(Creature c)
    {
        bool haste = false, slow = false;
        foreach (var e in c.Effects)
        {
            if (e.Type == NwRules.EffectHaste) haste = true;
            else if (e.Type == NwRules.EffectSlow) slow = true;
        }
        if (!haste)
            foreach (var (_, _, p) in EquippedProperties(c))
                if (p.Type == NwRules.IpHaste) { haste = true; break; }
        return (haste && !slow, slow && !haste);
    }

    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    public static int AbilityModifier(World w, Creature c, int ability) => Modifier(AbilityScore(w, c, ability));

    // ------------------------------------------------------------------ attack

    /// <summary>Base attack bonus from the class attack tables (pre-epic levels) plus +1 per two epic levels.</summary>
    public static int BaseAttackBonus(World w, Creature c)
    {
        if (c.Overrides is { BaseAttackBonus: > 0 } o) return o.BaseAttackBonus;
        var (pre, epic) = LevelSplit(c);
        int bab = 0;
        foreach (var (cls, lvl) in pre)
            if (lvl > 0) bab += ClassTable(w, cls, "AttackBonusTable").GetInt(lvl - 1, "BAB");
        return bab + (epic + 1) / 2;
    }

    /// <summary>GetAttacksPerRound: attacks from the pre-epic BAB (monks unarmed use their faster progression).</summary>
    public static int AttacksPerRound(World w, Creature c, bool checkOverride = true)
    {
        EnsureInit(w, c);
        if (checkOverride && c.BaseAttackOverride > 0) return Math.Clamp(c.BaseAttackOverride, 1, 6);
        var (pre, _) = LevelSplit(c);
        int bab = 0;
        foreach (var (cls, lvl) in pre)
            if (lvl > 0) bab += ClassTable(w, cls, "AttackBonusTable").GetInt(lvl - 1, "BAB");
        // A base attack bonus set by script counts for the attacks too (nwserver nwnx_creature_attack: 11 gives three).
        if (c.Overrides is { BaseAttackBonus: > 0 } o) bab = o.BaseAttackBonus;
        int monk = ClassLevel(c, NwRules.ClassMonk);
        bool unarmed = !c.Equipment.ContainsKey(EquipSlot.RightHand) || c.Equipment[EquipSlot.RightHand].BaseItem == 40; // kama
        if (monk > 0 && unarmed && ArmourBaseAC(w, c) == 0 && ShieldBaseAC(w, c) == 0)
        {
            // The ruleset value is the penalty per extra attack; 0 (no iterative penalty) keeps the standard steps.
            int monkStep = Ruleset.Int(w, "MULTIPLE_ATTACKS_BAB_PENALTY_MULTIPLIER_MONK", 3);
            if (monkStep <= 0) monkStep = 3;
            return Math.Clamp((bab - 1) / monkStep + 1, 1, 6);
        }
        int step = Ruleset.Int(w, "MULTIPLE_ATTACKS_BAB_PENALTY_MULTIPLIER", 5);
        if (step <= 0) step = 5;
        return Math.Clamp((bab - 1) / step + 1, 1, 4);
    }

    // ------------------------------------------------------------------ saving throws

    static readonly string[] SaveColumns = { "", "FortSave", "RefSave", "WillSave" };
    static readonly int[] SaveAbility = { 0, NwRules.AbilityCon, NwRules.AbilityDex, NwRules.AbilityWis };

    /// <summary>IP_CONST_SAVEVS_* (iprp_saveelement row) to SAVING_THROW_TYPE_*; -1 for rows no save type matches.</summary>
    static readonly int[] SaveVsToType = { 0, 6, -1, 12, 11, 3, 13, 8, 4, 7, -1, 1, 10, 2, 9, 5, 14, 15, 18, 19, 16, 17 };

    /// <summary>Saving throw from the class tables only (pre-epic levels) plus +1 per two epic levels.</summary>
    public static int BaseSave(World w, Creature c, int save)
    {
        if (save < 1 || save > 3) return 0;
        var (pre, epic) = LevelSplit(c);
        int total = 0;
        foreach (var (cls, lvl) in pre)
            if (lvl > 0) total += ClassTable(w, cls, "SavingThrowTable").GetInt(lvl - 1, SaveColumns[save]);
        return total + epic / 2;
    }

    /// <summary>
    /// The save a cls_pres_* SAVE row compares: the class save plus the feats' save bonuses (Great Fortitude counts;
    /// the Constitution modifier and an EffectSavingThrowIncrease don't: parity suite prog_prereq).
    /// </summary>
    public static int PrerequisiteSave(World w, Creature c, int save) =>
        save is < 1 or > 3 ? 0 : BaseSave(w, c, save) + FeatSaveBonus(w, c, save, NwRules.SaveTypeAll);

    /// <summary>Save bonus from feats; with a save type, only the bonuses specific to that type.</summary>
    static int FeatSaveBonus(World w, Creature c, int save, int saveType)
    {
        bool Has(int feat) => HasFeat(w, c, feat, ignoreUses: true);
        int R(string rule, int fallback) => Ruleset.Int(w, rule, fallback);
        int b = 0;
        if (saveType == NwRules.SaveTypeAll)
        {
            if (save == NwRules.SaveFort)
            {
                if (Has(NwRules.FeatGreatFortitude)) b += R("GREAT_FORTITUDE_SAVE_BONUS", 2);
                if (Has(NwRules.FeatEpicFortitude)) b += R("EPIC_FORTITUDE_SAVE_BONUS", 4);
            }
            if (save == NwRules.SaveReflex)
            {
                if (Has(NwRules.FeatLightningReflexes)) b += R("LIGHTNING_REFLEXES_SAVE_BONUS", 2);
                if (Has(NwRules.FeatEpicReflexes)) b += R("EPIC_REFLEXES_REFLEX_BONUS", 4);
                if (Has(NwRules.FeatSnakeblood)) b += R("SNAKE_BLOOD_REFLEX_BONUS", 1);
            }
            if (save == NwRules.SaveWill)
            {
                if (Has(NwRules.FeatIronWill)) b += R("IRON_WILL_SAVE_BONUS", 2);
                if (Has(NwRules.FeatEpicWill)) b += R("EPIC_WILL_SAVE_BONUS", 4);
                if (Has(NwRules.FeatBullheaded)) b += R("BULLHEADED_WILL_SAVE_BONUS", 1);
            }
            if ((save == NwRules.SaveFort || save == NwRules.SaveWill) && Has(NwRules.FeatStrongSoul)) b += R("STRONG_SOUL_SAVE_BONUS", 1);
            if (Has(NwRules.FeatLuckOfHeroes)) b += R("LUCKOFHEROES_SAVE_BONUS", 1);
            if (Has(NwRules.FeatLucky)) b += R("LUCKY_SAVE_BONUS", 1);
            if (Has(NwRules.FeatDivineGrace) || Has(NwRules.FeatDarkBlessing))
                b += Math.Max(0, AbilityModifier(w, c, NwRules.AbilityCha));
            return b;
        }
        switch (saveType)
        {
            case NwRules.SaveTypePoison:
                if (Has(NwRules.FeatHardinessPoisons)) b += R("HARDINESS_SAVE_BONUS", 2);
                if (save == NwRules.SaveFort && Has(NwRules.FeatResistPoison)) b += R("RESIST_POISON_BONUS", 4);
                if (save == NwRules.SaveFort && Has(NwRules.FeatSnakeblood)) b += R("SNAKE_BLOOOD_POISON_BONUS", 2);
                break;
            case NwRules.SaveTypeDisease:
                if (save == NwRules.SaveFort && Has(NwRules.FeatResistDisease)) b += R("RESIST_DISEASE_BONUS", 4);
                break;
            case NwRules.SaveTypeSpell:
                if (Has(NwRules.FeatHardinessSpells)) b += R("HARDINESS_SAVE_BONUS", 2);
                break;
            case NwRules.SaveTypeMind:
                if (Has(NwRules.FeatHardinessEnchantments)) b += R("HARDINESS_SAVE_BONUS", 2);
                if (Has(NwRules.FeatStillMind)) b += R("STILL_MIND_COMPETANCE_BONUS", 2);
                break;
            case NwRules.SaveTypeFear:
                if (Has(NwRules.FeatFearless)) b += R("FEARLESS_MORALE_BONUS", 2);
                break;
            case NwRules.SaveTypeDeath:
                if (Has(NwRules.FeatStrongSoul)) b += R("STRONG_SOUL_SAVE_VS_DEATH_BONUS", 1);
                break;
            case NwRules.SaveTypeNegative:
                if (Has(NwRules.FeatStrongSoul)) b += R("STRONG_SOULD_SAVE_VS_NEG_BONUS", 1);
                break;
            case NwRules.SaveTypeTrap:
                if (Has(NwRules.FeatDeneirsEye)) b += R("DENEIRS_EYE_SAVE_BONUS", 2);
                break;
        }
        return b;
    }

    /// <summary>Item and effect save adjustments (bonus, penalty) for a save; a save type adds the bonuses specific to it.</summary>
    internal static (int Bonus, int Penalty) SaveAdjustments(World w, Creature c, int save, int saveType, GameObject? versus)
    {
        int dec = 0;
        var bonuses = new SpellBonuses();
        var penalties = new SpellBonuses();
        bool frightened = false;
        bool Applies(int effSave, int effType) =>
            (effSave == NwRules.SaveAll || effSave == save) && (effType == NwRules.SaveTypeAll || (saveType != NwRules.SaveTypeAll && effType == saveType));
        foreach (var e in Effects(c))
        {
            // Saves versus a race or alignment count only against a matching object (WillSave's oSaveVersus).
            if (e.Type is NwRules.EffectSavingThrowIncrease or NwRules.EffectSavingThrowDecrease && !EffectQueries.AppliesVersus(e, versus)) continue;
            if (e.Type == NwRules.EffectSavingThrowIncrease && Applies(e.Ints[0], e.Ints[2])) bonuses.Add(e, e.Ints[1]);
            else if (e.Type == NwRules.EffectSavingThrowDecrease && Applies(e.Ints[0], e.Ints[2])) penalties.Add(e, e.Ints[1]);
            else if (e.Type == NwRules.EffectNegativeLevel) dec += e.Ints[0];
            else if (e.Type is NwRules.EffectFrightened or NwRules.EffectTurned) frightened = true;
        }
        int inc = bonuses.Total;
        dec += penalties.Total;
        // Fear and turning: -2 on all saves. Slow: -2 on reflex.
        if (frightened) dec += 2;
        if (save == NwRules.SaveReflex && Speed(c).Slowed) dec += 2;
        foreach (var (_, _, p) in EquippedProperties(c))
        {
            switch (p.Type)
            {
                case NwRules.IpSaveBonus or NwRules.IpDecreasedSaves:
                {
                    int type = p.SubType >= 0 && p.SubType < SaveVsToType.Length ? SaveVsToType[p.SubType] : -1;
                    if (type < 0 || !Applies(NwRules.SaveAll, type)) continue;
                    if (p.Type == NwRules.IpSaveBonus) inc += PropertyValue(w, p); else dec += Math.Abs(PropertyValue(w, p));
                    break;
                }
                case NwRules.IpSaveBonusSpecific or NwRules.IpDecreasedSavesSpecific:
                {
                    // IP_CONST_SAVEBASETYPE: 1 fortitude, 2 will, 3 reflex.
                    int s = p.SubType switch { 1 => NwRules.SaveFort, 2 => NwRules.SaveWill, 3 => NwRules.SaveReflex, 0 => NwRules.SaveAll, _ => -1 };
                    if (s < 0 || !Applies(s, NwRules.SaveTypeAll)) continue;
                    if (p.Type == NwRules.IpSaveBonusSpecific) inc += PropertyValue(w, p); else dec += Math.Abs(PropertyValue(w, p));
                    break;
                }
            }
        }
        return (Math.Max(0, inc), Math.Max(0, dec));
    }

    /// <summary>
    /// Total saving throw (Get*SavingThrow): class base + ability + template bonus + feats + capped
    /// item/effect adjustments. With a save type, type-specific bonuses (vs. poison, fear, ...) are included.
    /// Doors and placeables use their template values.
    /// </summary>
    public static int SavingThrow(World w, GameObject o, int save, int saveType = NwRules.SaveTypeAll, GameObject? versus = null)
    {
        if (save < 1 || save > 3) return 0;
        if (o is not Creature c) return StaticSave(o, save);
        EnsureInit(w, c);
        int total = BaseSave(w, c, save) + AbilityModifier(w, c, SaveAbility[save]);
        total += save switch { NwRules.SaveFort => c.FortBonus, NwRules.SaveReflex => c.RefBonus, _ => c.WillBonus };
        total += FeatSaveBonus(w, c, save, NwRules.SaveTypeAll);
        if (saveType != NwRules.SaveTypeAll) total += FeatSaveBonus(w, c, save, saveType);
        var (inc, dec) = SaveAdjustments(w, c, save, saveType, versus);
        return total + Clamp(inc, dec, w.Limits.SavingThrowBonus, w.Limits.SavingThrowBonus);
    }

    static readonly string[] StaticSaveFields = { "", "Fort", "Ref", "Will" };

    /// <summary>A door's or placeable's saving throw (template Fort/Ref/Will).</summary>
    public static int StaticSave(GameObject o, int save) => o switch
    {
        Door d => (int)d.Template.GetInt(StaticSaveFields[save]),
        Placeable p => (int)p.Template.GetInt(StaticSaveFields[save]),
        _ => 0,
    };

    public static void SetStaticSave(GameObject o, int save, int value)
    {
        value = Math.Clamp(value, 0, 250);
        if (o is Door d) d.Template.SetByte(StaticSaveFields[save], value);
        else if (o is Placeable p) p.Template.SetByte(StaticSaveFields[save], value);
    }

    // ------------------------------------------------------------------ armour class

    /// <summary>
    /// The parts of a creature's armour class. AC decreases come off their own type: dodge and deflection are net of
    /// theirs (combat drops the whole dodge part when the creature is flat-footed, as the original does); the others'
    /// are in <see cref="Penalty"/>.
    /// </summary>
    public record struct ArmorClassParts(int Armour, int ArmourEnchant, int Shield, int ShieldEnchant, int Dex, int Size,
        int Natural, int Deflection, int Dodge, int Penalty)
    {
        public int Total => 10 + Armour + ArmourEnchant + Shield + ShieldEnchant + Dex + Size + Natural + Deflection + Dodge - Penalty;
    }

    /// <summary>Base AC of a piece of body armour (parts_chest.2da ACBONUS of its torso part).</summary>
    public static int ArmourItemAC(World w, Item item)
    {
        if (item.Template.Has("ArmorPart_Torso") || item.Template.Has("xArmorPart_Torso"))
            return w.Resources.TwoDA("parts_chest").GetInt((int)CreatureFactory.Wide(item.Template, "ArmorPart_Torso"), "ACBONUS");
        return 0;
    }

    public static int ArmourBaseAC(World w, Creature c) =>
        c.Equipment.TryGetValue(EquipSlot.Chest, out var armour) ? ArmourItemAC(w, armour) : 0;

    public static int ShieldBaseAC(World w, Creature c) =>
        c.Equipment.TryGetValue(EquipSlot.LeftHand, out var shield) ? w.Resources.TwoDA("baseitems").GetInt(shield.BaseItem, "BaseAC") : 0;

    /// <summary>Which AC type an item's AC bonus property gives, by where it is worn.</summary>
    static int ItemAcType(World w, Item item, int slot) => slot switch
    {
        EquipSlot.Chest => NwRules.AcArmour,
        EquipSlot.LeftHand when w.Resources.TwoDA("baseitems").GetInt(item.BaseItem, "BaseAC") > 0 => NwRules.AcShield,
        EquipSlot.Neck => NwRules.AcNatural,
        EquipSlot.Boots => NwRules.AcDodge,
        EquipSlot.Arms when item.BaseItem == 78 => NwRules.AcArmour, // bracers
        _ => NwRules.AcDeflection,
    };

    /// <summary>
    /// Armour class parts. Without <paramref name="versus"/> only unconditional bonuses count (GetAC);
    /// combat passes the attacker and its weapon's DAMAGE_TYPE_* bits so versus-alignment/race effects and
    /// item properties and AC-versus-damage-type bonuses apply.
    /// </summary>
    /// <param name="helplessDex">Whether a helpless creature's Dexterity of 3 counts (GetAC); combat passes false.</param>
    public static ArmorClassParts ArmorClass(World w, Creature c, GameObject? versus = null, int damageType = 0, bool helplessDex = true)
    {
        EnsureInit(w, c);
        var p = new ArmorClassParts();
        // Polymorphed: the armour and shield are put away and size doesn't count; the form's natural armour does.
        var poly = Polymorph(w, c);
        var armour = c.Equipment.GetValueOrDefault(EquipSlot.Chest);
        p.Armour = armour != null ? ArmourItemAC(w, armour) : 0;
        p.Shield = ShieldBaseAC(w, c);

        int dex = Modifier(AbilityScore(w, c, NwRules.AbilityDex, helpless: helplessDex));
        if (armour != null)
        {
            var cap = w.Resources.TwoDA("armor").Get(p.Armour, "DEXBONUS");
            if (cap.Length > 0 && int.TryParse(cap, out int max)) dex = Math.Min(dex, max);
        }
        p.Dex = dex;
        p.Size = poly == null ? SizeACModifier(w, c.Size) : 0;

        // Bonuses of one type don't stack (best applies) except dodge; so do penalties. Each comes off its own type.
        int[] best = new int[5], worst = new int[5];
        int dodge = 0, dodgePenalty = 0;
        foreach (var e in Effects(c))
        {
            if (e.Type != NwRules.EffectAcIncrease && e.Type != NwRules.EffectAcDecrease) continue;
            if (e.Ints[2] != 0 && e.Ints[2] != NwRules.AcVsDamageTypeAll && (e.Ints[2] & damageType) == 0) continue; // only versus some damage types
            if (!EffectQueries.AppliesVersus(e, versus)) continue;
            int type = Math.Clamp(e.Ints[1], 0, 4);
            if (e.Type == NwRules.EffectAcDecrease)
            {
                if (type == NwRules.AcDodge) dodgePenalty += e.Ints[0];
                else worst[type] = Math.Max(worst[type], e.Ints[0]);
            }
            else if (type == NwRules.AcDodge) dodge += e.Ints[0];
            else best[type] = Math.Max(best[type], e.Ints[0]);
        }
        // Haste: +4; slow: -2 (they cancel each other out). Touch attacks ignore it, like natural armour.
        var (hasted, slowed) = Speed(c);
        int speedAc = hasted ? 4 : slowed ? -2 : 0;
        foreach (var (item, slot, ip) in EquippedProperties(c))
        {
            if (ip.Type == NwRules.IpAcBonus)
            {
                int type = ItemAcType(w, item, slot), v = PropertyValue(w, ip);
                if (type == NwRules.AcDodge) dodge += v; else best[type] = Math.Max(best[type], v);
            }
            else if (ip.Type == NwRules.IpDecreasedAc)
            {
                int type = Math.Clamp(ip.SubType, 0, 4), v = Math.Abs(PropertyValue(w, ip));
                if (type == NwRules.AcDodge) dodgePenalty += v; else worst[type] = Math.Max(worst[type], v);
            }
            else if (versus != null && ip.Type is >= NwRules.IpAcBonusVsAlignmentGroup and <= NwRules.IpAcBonusVsSpecificAlignment
                && ItemPropertyVersus.Applies(w, ip, versus, damageType))
            {
                int type = ItemAcType(w, item, slot), v = PropertyValue(w, ip);
                if (type == NwRules.AcDodge) dodge += v; else best[type] = Math.Max(best[type], v);
            }
        }

        int natural = poly?.NaturalAC ?? c.NaturalAC;
        bool Has(int feat) => HasFeat(w, c, feat, ignoreUses: true);
        if (Has(NwRules.FeatEpicArmorSkin)) natural += Ruleset.Int(w, "EPIC_ARMOR_SKIN_NATURAL_AC_BONUS", 2);
        if (Has(NwRules.FeatBoneSkin6)) natural += 6; else if (Has(NwRules.FeatBoneSkin4)) natural += 4; else if (Has(NwRules.FeatBoneSkin2)) natural += 2;
        int rdd = ClassLevel(c, NwRules.ClassDragonDisciple);
        if (rdd > 0 && Has(NwRules.FeatDragonArmor)) natural += rdd >= 10 ? 4 + (rdd - 10) / 5 : rdd >= 8 ? 3 : rdd >= 5 ? 2 : 1;

        if (Has(NwRules.FeatShouDodge3)) dodge += Ruleset.Int(w, "SHOU_DISCIPLE_DODGE_AC_BONUS_3", 3);
        else if (Has(NwRules.FeatShouDodge2)) dodge += Ruleset.Int(w, "SHOU_DISCIPLE_DODGE_AC_BONUS_2", 2);
        // The Dodge feat only counts against the creature's current opponent; combat rounds apply it.
        int tumbleStep = Ruleset.Int(w, "TUMBLE_NUM_RANKS_PER_AC_BONUS", 5);
        if (tumbleStep > 0) dodge += SkillRankBase(c, NwRules.SkillTumble) / tumbleStep;
        int monk = ClassLevel(c, NwRules.ClassMonk);
        if ((monk > 0 || Has(NwRules.FeatMonkAcBonus)) && p.Armour == 0 && p.Shield == 0)
            dodge += Math.Max(0, AbilityModifier(w, c, NwRules.AbilityWis)) + monk / 5;

        p.Natural = natural + best[NwRules.AcNatural] + speedAc;
        p.ArmourEnchant = best[NwRules.AcArmour];
        p.ShieldEnchant = best[NwRules.AcShield];
        p.Deflection = best[NwRules.AcDeflection] - worst[NwRules.AcDeflection];
        p.Dodge = Math.Min(dodge, Ruleset.Int(w, "MAX_AC_DODGE_MOD", 20)) - dodgePenalty;
        p.Penalty = worst[NwRules.AcNatural] + worst[NwRules.AcArmour] + worst[NwRules.AcShield];
        return p;
    }

    /// <summary>AC modifier for a CREATURE_SIZE_* (ruleset.2da, else creaturesize.2da).</summary>
    public static int SizeACModifier(World w, int size) => size switch
    {
        1 => Ruleset.Int(w, "CREATURE_SIZE_TINY_AC_BONUS", 2),
        2 => Ruleset.Int(w, "CREATURE_SIZE_SMALL_AC_BONUS", 1),
        4 => Ruleset.Int(w, "CREATURE_SIZE_LARGE_AC_PENALTY", -1),
        5 => Ruleset.Int(w, "CREATURE_SIZE_HUGE_AC_PENALTY", -2),
        _ => w.Resources.TwoDA("creaturesize").GetInt(size, "ACATTACKMOD"),
    };

    // ------------------------------------------------------------------ hit points

    /// <summary>
    /// Hit points from Constitution and feats (Toughness, Epic Toughness) on top of the rolled hit dice. Without
    /// effects and items it is the part a blueprint's or character file's MaxHitPoints holds (parity suite hp_items:
    /// nw_hen_bod_05 stores 54 and reads 62 with its boots of +2 Constitution, 54 without them).
    /// </summary>
    public static int HitPointBonus(World w, Creature c, bool effects = true, bool items = true)
    {
        int hd = c.TotalLevel;
        int bonus = hd * Modifier(AbilityScore(w, c, NwRules.AbilityCon, effects: effects, items: items));
        if (HasFeat(w, c, NwRules.FeatToughness, ignoreUses: true)) bonus += hd * Ruleset.Int(w, "FEAT_TOUGHNESS_HP_BONUS", 1);
        for (int f = NwRules.FeatEpicToughness10; f >= NwRules.FeatEpicToughness1; f--)
        {
            if (!HasFeat(w, c, f, ignoreUses: true)) continue;
            int rank = f - NwRules.FeatEpicToughness1 + 1;
            bonus += Ruleset.Int(w, $"FEAT_EPIC_TOUGHNESS_HP_BONUS_{rank}", 20 * rank);
            break;
        }
        return bonus;
    }

    /// <summary>GetMaxHitPoints for a creature: rolled hit dice + Constitution and feat bonuses (never below 1).</summary>
    public static int MaxHitPoints(World w, Creature c)
    {
        EnsureInit(w, c);
        return Math.Max(1, c.BaseHitPoints + HitPointBonus(w, c) + c.HitPointOffset);
    }

    /// <summary>Current hit points (without temporary ones), once the template's bonus is applied.</summary>
    public static int CurrentHitPoints(World w, Creature c)
    {
        EnsureInit(w, c);
        return c.CurrentHitPoints;
    }

    /// <summary>Temporary hit points from effects.</summary>
    public static int TemporaryHitPoints(GameObject o) =>
        Effects(o).Where(e => e.Type == NwRules.EffectTemporaryHitpoints).Sum(e => Math.Max(0, e.Ints[0]));

    /// <summary>
    /// Brings MaxHitPoints in line with the rules; when the maximum moves (Constitution change, level
    /// loss) current hit points move with it, like the original, down to death if they reach 0. The
    /// dead keep the maximum they died with.
    /// </summary>
    public static void SyncHitPoints(World w, Creature c)
    {
        if (c.IsDead) return;
        int max = MaxHitPoints(w, c);
        if (max == c.MaxHitPoints) return;
        int delta = max - c.MaxHitPoints;
        int before = c.CurrentHitPoints;
        if (before > 0 && before + delta <= 0)
        {
            // Killed by the change: like the original, the creature keeps the maximum it had.
            c.CurrentHitPoints = before + delta;
            HitPoints.CheckDeath(w, c, GameObject.InvalidId, before);
        }
        else
        {
            c.MaxHitPoints = max;
            if (before > 0) c.CurrentHitPoints = Math.Min(before + delta, max);
        }
        w.MarkChanged(c);
    }

    // ------------------------------------------------------------------ skills

    public static int SkillRankBase(Creature c, int skill) => skill >= 0 && skill < c.Skills.Length ? c.Skills[skill] : 0;

    /// <summary>GetHasSkill: the skill exists and is usable untrained or the creature has ranks in it.</summary>
    public static bool HasSkill(World w, Creature c, int skill)
    {
        var skills = w.Resources.TwoDA("skills");
        if (skill < 0 || skill >= skills.RowCount || skills.Get(skill, "Label").Length == 0) return false;
        return skills.GetInt(skill, "Untrained") != 0 || SkillRankBase(c, skill) > 0;
    }

    /// <summary>Racial and background feats that add to skills: (skill, ruleset.2da constant, default) per feat.</summary>
    static readonly Dictionary<int, (int Skill, string Rule, int Bonus)[]> SkillFeats = new()
    {
        [NwRules.FeatAlertness] = new[] { (6, "ALERTNESS_SKILL_BONUS", 2), (17, "ALERTNESS_SKILL_BONUS", 2) },
        [NwRules.FeatSkillAffinityLore] = new[] { (7, "SKILL_AFFINITY_SKILL_BONUS", 2) },
        [NwRules.FeatSkillAffinityListen] = new[] { (6, "SKILL_AFFINITY_SKILL_BONUS", 2) },
        [NwRules.FeatSkillAffinitySearch] = new[] { (14, "SKILL_AFFINITY_SKILL_BONUS", 2) },
        [NwRules.FeatSkillAffinitySpot] = new[] { (17, "SKILL_AFFINITY_SKILL_BONUS", 2) },
        [NwRules.FeatSkillAffinityConcentration] = new[] { (1, "SKILL_AFFINITY_SKILL_BONUS", 2) },
        [NwRules.FeatSkillAffinityMoveSilently] = new[] { (8, "SKILL_AFFINITY_SKILL_BONUS", 2) },
        [NwRules.FeatPartialAffinityListen] = new[] { (6, "PARTIAL_SKILL_FOCUS_SKILL_BONUS", 1) },
        [NwRules.FeatPartialAffinitySearch] = new[] { (14, "PARTIAL_SKILL_FOCUS_SKILL_BONUS", 1) },
        [NwRules.FeatPartialAffinitySpot] = new[] { (17, "PARTIAL_SKILL_FOCUS_SKILL_BONUS", 1) },
        [NwRules.FeatArtist] = new[] { (11, "ARTIST_PERFORM_BONUS", 2), (17, "ARTIST_SPOT_BONUS", 2) },
        [NwRules.FeatBlooded] = new[] { (17, "BLOODED_SPOT_BONUS", 2) },
        [NwRules.FeatCourtlyMagocracy] = new[] { (7, "COURTEOUS_MAGOCRACY_LORE_BONUS", 2), (16, "COURTEOUS_MAGOCRACY_LORE_BONUS", 2) },
        [NwRules.FeatSilverPalm] = new[] { (20, "SILVER_PALM_APPRAISE_BONUS", 2), (12, "SILVER_PALM_PERSUADE_BONUS", 2) },
        [NwRules.FeatStealthy] = new[] { (5, "STEALTHY_HIDE_BONUS", 2), (8, "STEALTHY_MOVE_SILENTLY_BONUS", 2) },
        [NwRules.FeatThug] = new[] { (12, "THUG_PERSUADE_BONUS", 2) },
        [NwRules.FeatStonecunning] = new[] { (14, "STONECUNNING_SEARCH_SKILL_BONUS", 2) },
    };

    /// <summary>Skill bonus from feats: Skill Focus and Epic Skill Focus (feat.2da MASTERFEAT + REQSKILL) plus racial and background feats.</summary>
    public static int FeatSkillBonus(World w, Creature c, int skill)
    {
        var feats = w.Resources.TwoDA("feat");
        var masters = w.Resources.TwoDA("masterfeats");
        int focus = masters.Find("LABEL", "SkillFocus"), epicFocus = masters.Find("LABEL", "EpicSkillFocus");
        int b = 0;
        foreach (int f in AllFeats(w, c))
        {
            if (SkillFeats.TryGetValue(f, out var list))
                foreach (var (s, rule, v) in list) if (s == skill) b += Ruleset.Int(w, rule, v);
            var master = feats.Get(f, "MASTERFEAT");
            if (master.Length == 0 || feats.GetInt(f, "REQSKILL", -1) != skill) continue;
            int m = int.TryParse(master, out int mm) ? mm : -1;
            if (m >= 0 && m == focus) b += Ruleset.Int(w, "SKILL_FOCUS_SKILL_BONUS", 3);
            else if (m >= 0 && m == epicFocus) b += Ruleset.Int(w, "EPIC_SKILL_FOCUS_SKILL_BONUS", 10);
        }
        return b;
    }

    /// <summary>Armour check penalty (positive number) from worn armour and shield.</summary>
    public static int ArmourCheckPenalty(World w, Creature c)
    {
        if (Polymorph(w, c) != null) return 0;
        int penalty = 0;
        if (c.Equipment.TryGetValue(EquipSlot.Chest, out var armour))
            penalty += Math.Abs(w.Resources.TwoDA("armor").GetInt(ArmourItemAC(w, armour), "ACCHECK"));
        if (c.Equipment.TryGetValue(EquipSlot.LeftHand, out var shield))
            penalty += Math.Abs(w.Resources.TwoDA("baseitems").GetInt(shield.BaseItem, "ArmorCheckPen"));
        return penalty;
    }

    /// <summary>
    /// GetSkillRank: ranks + key ability + feats + capped item/effect adjustments - armour check
    /// penalty (for skills that take it) - negative levels. -1 when the creature can't use the skill.
    /// </summary>
    public static int SkillRank(World w, Creature c, int skill, bool baseOnly = false)
    {
        EnsureInit(w, c);
        if (baseOnly) return HasSkill(w, c, skill) || SkillRankBase(c, skill) > 0 ? SkillRankBase(c, skill) : 0;
        if (!HasSkill(w, c, skill)) return -1;
        var skills = w.Resources.TwoDA("skills");
        int total = SkillRankBase(c, skill);
        int ability = NwRules.AbilityFromLabel(skills.Get(skill, "KeyAbility"));
        if (ability >= 0) total += AbilityModifier(w, c, ability);
        total += FeatSkillBonus(w, c, skill);
        if (skills.GetInt(skill, "ArmorCheckPenalty") != 0) total -= ArmourCheckPenalty(w, c);

        int dec = 0;
        var bonuses = new SpellBonuses();
        var penalties = new SpellBonuses();
        bool blind = false;
        foreach (var e in Effects(c))
        {
            // Skill effects versus a race or alignment don't count in the plain rank.
            if (e.Type is NwRules.EffectSkillIncrease or NwRules.EffectSkillDecrease && !EffectQueries.AppliesVersus(e, null)) continue;
            if (e.Type == NwRules.EffectSkillIncrease && (e.Ints[0] == skill || e.Ints[0] == NwRules.SkillAll)) bonuses.Add(e, e.Ints[1]);
            else if (e.Type == NwRules.EffectSkillDecrease && (e.Ints[0] == skill || e.Ints[0] == NwRules.SkillAll)) penalties.Add(e, e.Ints[1]);
            // Each negative level costs two points on skills.
            else if (e.Type == NwRules.EffectNegativeLevel) dec += 2 * e.Ints[0];
            else if (e.Type is NwRules.EffectBlindness or NwRules.EffectDarkness) blind = true;
        }
        int inc = bonuses.Total;
        dec += penalties.Total;
        // Blind: -4 on Strength and Dexterity skills.
        if (blind && ability is NwRules.AbilityStr or NwRules.AbilityDex) dec += 4;
        foreach (var (_, _, p) in EquippedProperties(c))
        {
            if (p.SubType != skill && p.SubType != NwRules.SkillAll) continue;
            if (p.Type == NwRules.IpSkillBonus) inc += PropertyValue(w, p);
            else if (p.Type == NwRules.IpDecreasedSkill) dec += Math.Abs(PropertyValue(w, p));
        }
        return total + Clamp(Math.Max(0, inc), Math.Max(0, dec), w.Limits.SkillBonus, w.Limits.SkillBonus);
    }

    // ------------------------------------------------------------------ spell resistance, spell failure

    /// <summary>GetSpellResistance: best of effects, item properties and feats, less decreases.</summary>
    public static int SpellResistance(World w, Creature c)
    {
        int best = 0, dec = 0;
        foreach (var e in Effects(c))
        {
            if (e.Type == NwRules.EffectSpellResistanceIncrease) best = Math.Max(best, e.Ints[0]);
            else if (e.Type == NwRules.EffectSpellResistanceDecrease) dec += e.Ints[0];
        }
        foreach (var (_, _, p) in EquippedProperties(c))
            if (p.Type == NwRules.IpSpellResistance) best = Math.Max(best, PropertyValue(w, p));
        int monk = ClassLevel(c, NwRules.ClassMonk);
        int feat = 0;
        if (HasFeat(w, c, NwRules.FeatDiamondSoul, ignoreUses: true)) feat = Ruleset.Int(w, "DIAMOND_SOUL_SPELL_RESISTANCE_BASE", 10) + monk;
        for (int f = NwRules.FeatEpicImprovedSr10; f >= NwRules.FeatEpicImprovedSr1; f--)
        {
            if (!HasFeat(w, c, f, ignoreUses: true)) continue;
            int rank = f - NwRules.FeatEpicImprovedSr1 + 1;
            feat += Ruleset.Int(w, $"EPIC_SPELL_RESISTANCE_{rank}", 2 * rank);
            break;
        }
        best = Math.Max(best, feat);
        // A spell resistance a script set stands until the creature's spell resistance effects change, and then is
        // gone (nwserver nwnx_creature_spells: 15 set, 20 from an effect, 0 once it ended).
        if (c.Overrides?.SpellResistance is int set)
        {
            if (c.Overrides.SpellResistanceEffects == CreatureOverrides.EffectStamp(c, CreatureOverrides.IsSpellResistanceEffect)) best = Math.Max(best, set);
            else c.Overrides.SpellResistance = null;
        }
        return Math.Max(0, best - dec);
    }

    /// <summary>GetArcaneSpellFailure: armour and shield failure plus item adjustments, 0-100.</summary>
    public static int ArcaneSpellFailure(World w, Creature c)
    {
        int asf = 0;
        if (c.Equipment.TryGetValue(EquipSlot.Chest, out var armour))
            asf += w.Resources.TwoDA("armor").GetInt(ArmourItemAC(w, armour), "ARCANEFAILURE%");
        if (c.Equipment.TryGetValue(EquipSlot.LeftHand, out var shield))
            asf += w.Resources.TwoDA("baseitems").GetInt(shield.BaseItem, "ArcaneSpellFailure");
        foreach (var (_, _, p) in EquippedProperties(c))
            if (p.Type == NwRules.IpArcaneSpellFailure) asf += PropertyValue(w, p);
        foreach (var e in Effects(c))
            if (e.Type == NwRules.EffectArcaneSpellFailure) asf += e.Ints[0];
        return Math.Clamp(asf, 0, 100);
    }

    // ------------------------------------------------------------------ weight

    /// <summary>GetWeight of an item in tenths of pounds (see ItemRules.WeightTenths).</summary>
    public static int ItemWeight(World w, Item item) => w.Inventory.Rules.WeightTenths(item);

    /// <summary>Total weight a creature carries (equipped and inventory), in tenths of pounds.</summary>
    public static int CarriedWeight(World w, Creature c) => w.Inventory.Rules.CarriedTenths(c);
}

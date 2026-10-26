using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Combat rules: attack bonus, armour class, concealment, criticals, damage, sneak attack, ammunition,
/// special attacks, whirlwind, attacks of opportunity and round timing. Base game data, scripted dice.
/// </summary>
public class CombatRulesTests
{
    const int Fighter = 4, Rogue = 8, Monk = 5;
    const int Longsword = 1, Greatsword = 13, Dagger = 22, Rapier = 51, TwoBladedSword = 12, Longbow = 8, Arrow = 20, ThrowingAxe = 63, Shortsword = 0;
    static readonly int[] Proficiencies = { 44, 45, 46 };

    sealed class RecordingOutput : IWorldOutput
    {
        public readonly List<(string Kind, GameObject? Subject, object Data)> Notifies = new();
        public readonly List<string> Messages = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) => Messages.Add(text);
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) => Notifies.Add((kind, subject, data));
        /// <summary>Every attack's notify at its swing.</summary>
        public IEnumerable<object> Attacks => Notifies.Where(n => n.Kind == "attack").Select(n => n.Data);
        /// <summary>Every attack's notify once its blow has landed (attack-impact, or attack when it lands at once).</summary>
        public IEnumerable<object> Landed => Notifies.Where(n => n.Kind == "attack-impact" || (n.Kind == "attack" && !Get<bool>(n.Data, "pending"))).Select(n => n.Data);
    }

    sealed class Fx
    {
        public required World W;
        public required Area Area;
        public required CombatSystem Combat;
        public required RecordingOutput Output;
        public CreatureCombatRules Rules => (CreatureCombatRules)Combat.Rules;
        public ScriptedDice Dice(params int[] rolls) { var d = new ScriptedDice(7, rolls); Combat.Dice = d; return d; }
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    static Fx Make(bool scripts = false)
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        rm.AddBaseGame();
        var w = new World(rm);
        var output = new RecordingOutput();
        w.Output = output;
        if (scripts) w.EnableScripts();
        w.Register(w.Module);
        var area = new Area { ResRef = "combat_area", Tag = "combat_area" };
        w.Register(area);
        w.Areas.Add(area);
        // Defenders (heroes) and hostiles hate each other.
        w.Factions.SetFactionReputation(FactionTable.Hostile, FactionTable.Defender, 0);
        w.Factions.SetFactionReputation(FactionTable.Defender, FactionTable.Hostile, 0);
        return new Fx { W = w, Area = area, Combat = w.System<CombatSystem>(), Output = output };
    }

    /// <summary>A human of the given class and level, Str/Dex as given, 100 hit points, in the test area.</summary>
    static Creature Make(Fx f, int cls, int level, Vector3 pos, int str = 10, int dex = 10, int faction = FactionTable.Defender, params int[] feats)
    {
        var c = f.W.Creatures.FromTemplate(new GffStruct());
        c.Classes.Add(new CreatureClass { Class = cls, Level = level });
        c.Race = 6;
        c.Abilities[0] = str;
        c.Abilities[1] = dex;
        c.MaxHitPoints = c.CurrentHitPoints = 100;
        c.Faction = faction;
        c.Feats.AddRange(Proficiencies);
        c.Feats.AddRange(feats);
        f.W.Register(c);
        CreatureRules.EnsureInit(f.W, c);
        c.Size = 3;
        f.W.AddToArea(c, f.Area, pos, 0);
        return c;
    }

    static Creature Target(Fx f, Vector3 pos, int faction = FactionTable.Hostile) => Make(f, Fighter, 1, pos, faction: faction);

    static Item Equip(Fx f, Creature c, int baseItem, int slot, int stack = 1, params ItemProperty[] props)
    {
        var item = new Item { BaseItem = baseItem, StackSize = stack, ResRef = "test_" + baseItem, Tag = "test_" + baseItem };
        item.Properties.AddRange(props);
        f.W.Register(item);
        c.Equipment[slot] = item;
        item.Possessor = c.Id;
        return item;
    }

    static Item Carry(Fx f, Creature c, int baseItem, int stack = 1)
    {
        var item = new Item { BaseItem = baseItem, StackSize = stack, ResRef = "test_" + baseItem, Tag = "test_" + baseItem };
        f.W.Register(item);
        c.Inventory.Add(item);
        item.Possessor = c.Id;
        return item;
    }

    static Effect Effect(int type, params int[] ints)
    {
        var e = new Effect { Type = type, DurationType = 2 };
        for (int i = 0; i < ints.Length; i++) e.Ints[i] = ints[i];
        return e;
    }

    static AttackContext Ctx(Fx f, Creature a, GameObject t, bool offHand = false)
    {
        var (main, off, _) = f.Rules.Weapons(a);
        return f.Rules.Context(a, t, offHand ? off : main, offHand, f.Combat.Dice);
    }

    static int Ab(Fx f, Creature a, GameObject t, bool offHand = false) => f.Rules.AttackBonus(Ctx(f, a, t, offHand));

    static Creature LoadBic(Fx f, string name, Vector3 pos)
    {
        var path = Path.Combine(TestData.Config!.UserRoot, "localvault", name + ".bic");
        var c = f.W.Creatures.FromTemplate(Gff.Read(File.ReadAllBytes(path)));
        f.W.Register(c);
        f.W.AddToArea(c, f.Area, pos, 0);
        return c;
    }

    static T Get<T>(object data, string name) => (T)data.GetType().GetProperty(name)!.GetValue(data)!;

    // ------------------------------------------------------------------ attack bonus

    [DataFact]
    public void VaultRogueAttackBonusMatchesClassFeatsAndEquipment()
    {
        var f = Make();
        var dummy = Target(f, new Vector3(11, 10, 0));

        // damienoneknife6: halfling rogue 15 (small), +3 short sword, Weapon Finesse, Str 10 / Dex 21.
        // A small creature finesses a short sword (baseitems WeaponFinesseMinimumCreatureSize 2; parity suite
        // nwnx_weapon smallss), so Dexterity applies; small size adds +1.
        var small = LoadBic(f, "damienoneknife6", new Vector3(10, 10, 0));
        CreatureRules.EnsureInit(f.W, small);
        var t = small.Template;
        int bab = CreatureRules.BaseAttackBonus(f.W, small);
        Assert.Equal((int)t.GetInt("BaseAttackBonus"), bab);
        Assert.Equal(11, bab);
        var (main, off, _) = f.Rules.Weapons(small);
        Assert.Equal(Shortsword, main!.BaseItem);
        Assert.Null(off);
        Assert.Equal(2, small.Size);
        int enhancement = CreatureRules.PropertyValue(f.W, main.Properties.Single(p => p.Type == 6));
        Assert.Equal(3, enhancement);
        int dexModSmall = CreatureRules.AbilityModifier(f.W, small, NwRules.AbilityDex);
        Assert.Equal(bab + dexModSmall + 1 + enhancement, Ab(f, small, dummy));
        // Iterative attacks at -5 each.
        var plan = f.Rules.PlanRound(small);
        Assert.Equal(new[] { 0, 5, 10 }, plan.Select(p => p.Penalty));

        // damienoneknife: the same rogue at medium size: the short sword is light, Weapon Finesse uses Dex (+5).
        // The character was saved with SetBaseAttackBonus(2) in effect: two attacks a round.
        var medium = LoadBic(f, "damienoneknife", new Vector3(12, 10, 0));
        CreatureRules.EnsureInit(f.W, medium);
        Assert.Equal(3, medium.Size);
        int dexMod = CreatureRules.AbilityModifier(f.W, medium, NwRules.AbilityDex);
        Assert.Equal(5, dexMod);
        Assert.Equal(bab + dexMod + enhancement, Ab(f, medium, dummy));
        Assert.Equal(2, f.Rules.PlanRound(medium).Count);
    }

    [DataFact]
    public void WeaponFinesseOnlyWithLightWeaponsRapierAndUnarmed()
    {
        var f = Make();
        var dummy = Target(f, new Vector3(11, 10, 0));
        var plain = Make(f, Fighter, 4, new Vector3(10, 10, 0), str: 10, dex: 18);
        var finesse = Make(f, Fighter, 4, new Vector3(10, 11, 0), str: 10, dex: 18, feats: CombatK.FeatWeaponFinesse);

        Equip(f, plain, Dagger, EquipSlot.RightHand);
        Equip(f, finesse, Dagger, EquipSlot.RightHand);
        Assert.Equal(4, Ab(f, plain, dummy));
        Assert.Equal(8, Ab(f, finesse, dummy));

        Equip(f, finesse, Longsword, EquipSlot.RightHand);
        Assert.Equal(4, Ab(f, finesse, dummy));
        Equip(f, finesse, Rapier, EquipSlot.RightHand);
        Assert.Equal(8, Ab(f, finesse, dummy));
        finesse.Equipment.Remove(EquipSlot.RightHand);
        Assert.Equal(8, Ab(f, finesse, dummy));

        // Without proficiency: -4.
        plain.Feats.Clear();
        Assert.Equal(0, Ab(f, plain, dummy));
    }

    [DataFact]
    public void TwoWeaponPenaltiesFollowTheRuleset()
    {
        var f = Make();
        var dummy = Target(f, new Vector3(11, 10, 0));
        var c = Make(f, Fighter, 6, new Vector3(10, 10, 0));
        Equip(f, c, Longsword, EquipSlot.RightHand);
        Equip(f, c, Longsword, EquipSlot.LeftHand);
        Assert.Equal(6 - 6, Ab(f, c, dummy));
        Assert.Equal(6 - 10, Ab(f, c, dummy, offHand: true));

        // A light off-hand weapon: +2 to both hands.
        Equip(f, c, Dagger, EquipSlot.LeftHand);
        Assert.Equal(2, Ab(f, c, dummy));
        Assert.Equal(-2, Ab(f, c, dummy, offHand: true));
        Assert.Equal(new[] { false, false, true }, f.Rules.PlanRound(c).Select(p => p.OffHand));

        // Two-Weapon Fighting +2 both, Ambidexterity +4 off hand; Improved Two-Weapon Fighting: a second off-hand attack.
        c.Feats.AddRange(new[] { CombatK.FeatTwoWeaponFighting, CombatK.FeatAmbidexterity, CombatK.FeatImprovedTwoWeaponFighting });
        Assert.Equal(4, Ab(f, c, dummy));
        Assert.Equal(4, Ab(f, c, dummy, offHand: true));
        var plan = f.Rules.PlanRound(c);
        Assert.Equal(new[] { 0, 5, 0, 5 }, plan.Select(p => p.Penalty));
        Assert.Equal(new[] { false, false, true, true }, plan.Select(p => p.OffHand));

        // A double weapon attacks with its other end as a light off-hand weapon.
        c.Feats.RemoveAll(x => x is CombatK.FeatTwoWeaponFighting or CombatK.FeatAmbidexterity or CombatK.FeatImprovedTwoWeaponFighting);
        c.Equipment.Remove(EquipSlot.LeftHand);
        var tbs = Equip(f, c, TwoBladedSword, EquipSlot.RightHand);
        Assert.Same(tbs, f.Rules.Weapons(c).Off);
        Assert.Equal(2, Ab(f, c, dummy));
        Assert.Equal(-2, Ab(f, c, dummy, offHand: true));
    }

    [DataFact]
    public void IterativeAttacksHasteAndEffects()
    {
        var f = Make();
        var dummy = Target(f, new Vector3(11, 10, 0));
        var c = Make(f, Fighter, 16, new Vector3(10, 10, 0));
        Equip(f, c, Longsword, EquipSlot.RightHand);
        Assert.Equal(new[] { 0, 5, 10, 15 }, f.Rules.PlanRound(c).Select(p => p.Penalty));

        c.Effects.Add(Effect(CombatK.EffectHaste));
        var plan = f.Rules.PlanRound(c);
        Assert.Equal(5, plan.Count);
        // The extra attack comes after the normal ones (nwnx_damage).
        Assert.True(plan[4].Extra);
        Assert.Equal(0, plan[4].Penalty);
        // Extra attacks (haste, then EffectModifyAttacks) have their own iterative penalties.
        var more = Effect(CombatK.EffectModifyAttacks, 2);
        c.Effects.Add(more);
        Assert.Equal(new[] { 0, 5, 10, 15 }, f.Rules.PlanRound(c).Where(p => !p.Extra).Select(p => p.Penalty));
        Assert.Equal(new[] { 0, 5, 10 }, f.Rules.PlanRound(c).Where(p => p.Extra).Select(p => p.Penalty));
        c.Effects.Remove(more);

        // Attack increase (on-hand only) and decrease; versus-race effects only against that race.
        int baseAb = Ab(f, c, dummy);
        c.Effects.Add(Effect(CombatK.EffectAttackIncrease, 3, CombatK.AttackBonusOnHand));
        c.Effects.Add(Effect(CombatK.EffectAttackDecrease, 1, CombatK.AttackBonusMisc));
        var vsElves = Effect(CombatK.EffectAttackIncrease, 5, CombatK.AttackBonusMisc);
        vsElves.VersusRace = 1;
        c.Effects.Add(vsElves);
        Assert.Equal(baseAb + 2, Ab(f, c, dummy));
        dummy.Race = 1;
        Assert.Equal(baseAb + 7, Ab(f, c, dummy));

        // Weapon Focus and an attack bonus property versus evil.
        dummy.Race = 6;
        c.Feats.Add(f.Rules.Stats(Longsword).Focus);
        c.Equipment[EquipSlot.RightHand].Properties.Add(ItemPropertyRules.Make(f.W, CombatK.IpAttackVsAlignGroup, 5, 4));
        Assert.Equal(baseAb + 3, Ab(f, c, dummy));
        dummy.GoodEvil = 0;
        Assert.Equal(baseAb + 3 + 4, Ab(f, c, dummy));
    }

    [DataFact]
    public void SlowLeavesOneAttackAndAHasteItemAddsOne()
    {
        var f = Make();
        var c = Make(f, Fighter, 15, new Vector3(10, 10, 0));
        Equip(f, c, Longsword, EquipSlot.RightHand);
        Assert.Equal(3, f.Rules.PlanRound(c).Count);
        c.Effects.Add(Effect(CombatK.EffectSlow));
        Assert.Single(f.Rules.PlanRound(c));
        // A Haste item property hastes like the effect (and cancels slow).
        Equip(f, c, 26, EquipSlot.Boots, props: ItemPropertyRules.Make(f.W, CombatK.IpHaste));
        Assert.Equal(3, f.Rules.PlanRound(c).Count);
        c.Effects.RemoveAll(e => e.Type == CombatK.EffectSlow);
        Assert.Equal(4, f.Rules.PlanRound(c).Count);
    }

    [DataFact]
    public void ConditionsInCombatFollowTheOriginal()
    {
        var f = Make();
        var a = Make(f, Fighter, 5, new Vector3(10, 10, 0));
        var t = Make(f, Fighter, 5, new Vector3(11, 10, 0), dex: 16, faction: FactionTable.Hostile);
        f.Combat.Attack(t, a);
        int ab = Ab(f, a, t), ac = f.Rules.ArmorClass(Ctx(f, a, t));
        // Paralysed (Dexterity 3 for GetAC): no bonus to hit and its Dexterity still counts in combat.
        var held = Effect(CombatK.EffectParalyze);
        t.Effects.Add(held);
        Assert.Equal(ab, Ab(f, a, t));
        Assert.Equal(ac, f.Rules.ArmorClass(Ctx(f, a, t)));
        t.Effects.Remove(held);
        // Entangled: Dexterity 3 does count.
        var tangle = Effect(CombatK.EffectEntangle);
        t.Effects.Add(tangle);
        Assert.Equal(ac - 3 - 4, f.Rules.ArmorClass(Ctx(f, a, t)));
        t.Effects.Remove(tangle);
        // A blind attacker: -4 and a 50% miss chance; darkness on the attacker changes nothing.
        var blind = Effect(CombatK.EffectBlindness);
        a.Effects.Add(blind);
        Assert.Equal(ab - 4, Ab(f, a, t));
        Assert.Equal(50, f.Rules.Concealment(Ctx(f, a, t)));
        a.Effects.Remove(blind);
        a.Effects.Add(Effect(CombatK.EffectDarkness));
        Assert.Equal(ab, Ab(f, a, t));
        Assert.Equal(0, f.Rules.Concealment(Ctx(f, a, t)));
    }

    [DataFact]
    public void CombatLogShowsWhatGotThrough()
    {
        var f = Make();
        var a = Make(f, Fighter, 5, new Vector3(10, 10, 0), str: 10);
        a.Tag = "Hero";
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        t.Tag = "Bim";
        // A plot target: 0 physical, and the attacker is told it can't hurt it.
        t.Plot = true;
        f.Dice(15, 6);
        var lines = f.Combat.LogLines(f.Combat.Resolve(Ctx(f, a, t)));
        Assert.Contains("Hero damages Bim: 0 (0 Physical)", lines);
        Assert.Contains("You cannot damage your target with your currently equipped weapon!", lines);
        // Damage reduction soaking the whole hit: no damage line, what it absorbed.
        t.Plot = false;
        var k = EffectConstants.Of(f.W);
        var dr = new Effect { Type = k.DamageReduction, DurationType = 2 };
        dr.Ints[0] = 10;
        dr.Ints[1] = 5;
        t.Effects.Add(dr);
        f.Dice(15, 6);
        lines = f.Combat.LogLines(f.Combat.Resolve(Ctx(f, a, t)));
        Assert.DoesNotContain(lines, l => l.Contains("damages"));
        Assert.Contains("Bim : Damage Reduction absorbs 6 damage", lines);
        Assert.Contains("You cannot damage your target with your currently equipped weapon!", lines);
        // Partly soaked: the damage that got through, then what was absorbed.
        dr.Ints[0] = 2;
        f.Dice(15, 6);
        lines = f.Combat.LogLines(f.Combat.Resolve(Ctx(f, a, t)));
        Assert.Contains("Hero damages Bim: 4 (4 Physical)", lines);
        Assert.Contains("Bim : Damage Reduction absorbs 2 damage", lines);
    }

    // ------------------------------------------------------------------ armour class

    [DataFact]
    public void ArmorClassAgainstAnAttacker()
    {
        var f = Make();
        var a = Make(f, Fighter, 5, new Vector3(10, 10, 0));
        var t = Make(f, Fighter, 5, new Vector3(11, 10, 0), dex: 16, faction: FactionTable.Hostile);
        f.Combat.Attack(t, a);
        Assert.Equal(13, f.Rules.ArmorClass(Ctx(f, a, t)));
        // Flat-footed (not attacking anyone, even in combat): no Dex bonus. Uncanny Dodge keeps it.
        f.Combat.Stop(t);
        t.InCombat = true;
        Assert.Equal(10, f.Rules.ArmorClass(Ctx(f, a, t)));
        t.Feats.Add(CombatK.FeatUncannyDodge1);
        Assert.Equal(13, f.Rules.ArmorClass(Ctx(f, a, t)));
        // AC versus evil applies only against evil attackers; Dodge only against the current opponent.
        t.Effects.Add(new Effect { Type = NwRules.EffectAcIncrease, Ints = { [0] = 2, [1] = NwRules.AcDeflection }, VersusGoodEvil = 3 });
        Assert.Equal(13, f.Rules.ArmorClass(Ctx(f, a, t)));
        a.GoodEvil = 0;
        Assert.Equal(15, f.Rules.ArmorClass(Ctx(f, a, t)));
        t.Feats.Add(CombatK.FeatDodge);
        Assert.Equal(16, f.Rules.ArmorClass(Ctx(f, a, t)));
        // Touch attacks only face 10 + size + deflection, as in the original.
        var touch = Ctx(f, a, t);
        touch.Touch = true;
        Assert.Equal(10 + 2, f.Rules.ArmorClass(touch));
        // A small attacker's size bonus counts for weapon attacks but not touch attacks.
        a.Size = 2;
        Assert.Equal(f.Rules.AttackBonus(touch) + 1, f.Rules.AttackBonus(Ctx(f, a, t)));
    }

    [DataFact]
    public void TouchAttacksRetainNegativeDexterityIncludingHelplessStates()
    {
        var f = Make();
        var a = Make(f, Fighter, 1, new Vector3(10, 10, 0));
        var t = Make(f, Fighter, 1, new Vector3(11, 10, 0), dex: 16);
        t.Effects.Add(Effect(NwRules.EffectAcIncrease, 8, NwRules.AcDeflection));
        foreach (bool ranged in new[] { false, true })
        {
            // Fixed roll 14 misses AC 18 (+1 BAB), but hits helpless/low-Dex AC 14.
            f.Dice(14);
            Assert.Equal(0, f.Combat.TouchAttack(a, t, ranged, false));
            foreach (int state in new[] { NwRules.EffectParalyze, NwRules.EffectStunned, NwRules.EffectSleep,
                NwRules.EffectEntangle, NwRules.EffectPetrify, NwRules.EffectCutsceneParalyze, NwRules.EffectCutsceneImmobilize })
            {
                var held = Effect(state);
                t.Effects.Add(held);
                f.Dice(14);
                Assert.Equal(1, f.Combat.TouchAttack(a, t, ranged, false));
                t.Effects.Remove(held);
            }
            t.Abilities[NwRules.AbilityDex] = 3;
            f.Dice(14);
            Assert.Equal(1, f.Combat.TouchAttack(a, t, ranged, false));
            t.Abilities[NwRules.AbilityDex] = 16;
        }
    }

    [DataFact]
    public void ScriptedTouchAttacksIgnoreWeaponSituationsButKeepBlindMissChance()
    {
        var f = Make();
        var a = Make(f, Fighter, 1, new Vector3(10, 10, 0));
        var t = Make(f, Fighter, 1, new Vector3(11, 10, 0));
        t.Effects.Add(Effect(NwRules.EffectAcIncrease, 4, NwRules.AcDeflection));
        foreach (bool ranged in new[] { false, true })
        {
            // Against AC 14, roll 12 + BAB 1 still misses even if a weapon's situation would add +2.
            a.Effects.Add(Effect(CombatK.EffectInvisibility, CombatK.InvisibilityNormal));
            f.Dice(12);
            Assert.Equal(0, f.Combat.TouchAttack(a, t, ranged, false));
            a.Effects.Clear();
            var blind = Effect(CombatK.EffectBlindness);
            t.Effects.Add(blind);
            f.Dice(12);
            Assert.Equal(0, f.Combat.TouchAttack(a, t, ranged, false));
            t.Effects.Remove(blind);
            t.Effects.Add(Effect(CombatK.EffectKnockdown));
            f.Dice(12);
            Assert.Equal(0, f.Combat.TouchAttack(a, t, ranged, false));
            f.Dice(14);
            Assert.Equal(1, f.Combat.TouchAttack(a, t, ranged, false));
            t.Effects.RemoveAll(e => e.Type == CombatK.EffectKnockdown);
            a.Effects.Add(blind);
            f.Dice(14, 51);
            Assert.Equal(1, f.Combat.TouchAttack(a, t, ranged, false));
            f.Dice(14, 50);
            Assert.Equal(0, f.Combat.TouchAttack(a, t, ranged, false));
            a.Effects.Clear();
        }
        // No flanking bonus or ranged moving-target penalty, also measured in touch_states.
        var other = Make(f, Fighter, 1, new Vector3(12, 10, 0), faction: FactionTable.Hostile);
        f.Combat.Attack(t, other);
        f.Dice(12);
        Assert.Equal(0, f.Combat.TouchAttack(a, t, false, false));
        f.Combat.Stop(t);
        t.MoveDirection = Vector3.UnitX;
        f.Dice(14);
        Assert.Equal(1, f.Combat.TouchAttack(a, t, true, false));
    }

    // ------------------------------------------------------------------ criticals

    [DataFact]
    public void CriticalHitsMultiplyDamageAndUndeadAreImmune()
    {
        var f = Make();
        var a = Make(f, Fighter, 10, new Vector3(10, 10, 0), str: 14);
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        t.InCombat = true;

        Assert.Equal((19, 2), f.Rules.Critical(Ctx(f, a, t)));
        // Threat on 19, confirmed with 15; 1d8 rolled twice (5, 6) plus Str +2 each time.
        f.Dice(19, 15, 5, 6);
        var o = f.Combat.Resolve(Ctx(f, a, t));
        Assert.True(o.Hit && o.Threat && o.Critical);
        Assert.Equal(15, o.Damage!.ByType[CombatK.DamageSlashing]);
        Assert.Equal(85, t.CurrentHitPoints);
        Assert.Contains("Critical Hit", o.Feedback);

        // Keen doubles the threat range, and Improved Critical adds as much again: they stack in the original.
        a.Equipment[EquipSlot.RightHand].Properties.Add(ItemPropertyRules.Make(f.W, CombatK.IpKeen));
        Assert.Equal(17, f.Rules.Critical(Ctx(f, a, t)).Threat);
        a.Feats.Add(f.Rules.Stats(Longsword).ImprovedCritical);
        Assert.Equal(15, f.Rules.Critical(Ctx(f, a, t)).Threat);

        // Undead: the threat is still rolled, but no critical; the log says so (visual scene difficultypc).
        var undead = Target(f, new Vector3(10, 11, 0));
        undead.Race = CombatK.RaceUndead;
        f.Dice(19, 15, 5);
        o = f.Combat.Resolve(Ctx(f, a, undead));
        Assert.True(o.Hit && o.Threat && o.CriticalImmune);
        Assert.False(o.Critical);
        Assert.Equal(7, o.Damage!.Total);
        Assert.Contains(f.Combat.LogLines(o), l => l.EndsWith(" : Immune to Critical Hits."));

        // Critical hit immunity from an item.
        var warded = Target(f, new Vector3(9, 10, 0));
        Equip(f, warded, 52, EquipSlot.LeftRing, 1, ItemPropertyRules.Make(f.W, NwRules.IpImmunityMisc, 8));
        f.Dice(20, 4);
        o = f.Combat.Resolve(Ctx(f, a, warded));
        Assert.True(o.Hit);
        Assert.False(o.Critical);
    }

    // ------------------------------------------------------------------ concealment

    [DataFact]
    public void ConcealmentAndBlindFight()
    {
        var f = Make();
        var a = Make(f, Fighter, 5, new Vector3(10, 10, 0));
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        t.InCombat = true;
        t.Effects.Add(Effect(CombatK.EffectConcealment, 50, CombatK.MissNormal));
        Assert.Equal(50, f.Rules.Concealment(Ctx(f, a, t)));

        f.Dice(15, 30);
        var o = f.Combat.Resolve(Ctx(f, a, t));
        Assert.True(o.Concealed);
        Assert.False(o.Hit);
        Assert.Equal(50, o.Concealment);

        f.Dice(15, 80, 4);
        o = f.Combat.Resolve(Ctx(f, a, t));
        Assert.True(o.Hit);
        Assert.Equal(4, o.Damage!.Total);

        // Blind-Fight rerolls a failed concealment roll.
        a.Feats.Add(CombatK.FeatBlindFight);
        f.Dice(15, 30, 70, 4);
        o = f.Combat.Resolve(Ctx(f, a, t));
        Assert.True(o.Hit);

        // Concealment against ranged attacks only doesn't help in melee; invisibility gives 50% unless seen through.
        t.Effects.Clear();
        t.Effects.Add(Effect(CombatK.EffectConcealment, 40, CombatK.MissVsRanged));
        Assert.Equal(0, f.Rules.Concealment(Ctx(f, a, t)));
        t.Effects.Add(Effect(CombatK.EffectInvisibility, CombatK.InvisibilityNormal));
        Assert.Equal(50, f.Rules.Concealment(Ctx(f, a, t)));
        a.Effects.Add(Effect(CombatK.EffectSeeInvisible));
        Assert.Equal(0, f.Rules.Concealment(Ctx(f, a, t)));
    }

    // ------------------------------------------------------------------ damage

    [DataFact]
    public void DamageStrengthEnhancementPropertiesAndPower()
    {
        var f = Make();
        var t = Target(f, new Vector3(11, 10, 0));
        t.InCombat = true;
        t.MaxHitPoints = t.CurrentHitPoints = 500;

        // Two-handed: 1.5x Strength (+3 -> +4).
        var big = Make(f, Fighter, 5, new Vector3(10, 10, 0), str: 16);
        Equip(f, big, Greatsword, EquipSlot.RightHand);
        f.Dice(15, 3, 4);
        var o = f.Combat.Resolve(Ctx(f, big, t));
        Assert.Equal(3 + 4 + 4, o.Damage!.Total);

        // Off hand: half Strength.
        var dual = Make(f, Fighter, 5, new Vector3(10, 11, 0), str: 16);
        Equip(f, dual, Longsword, EquipSlot.RightHand);
        Equip(f, dual, Dagger, EquipSlot.LeftHand);
        f.Dice(18, 2);
        o = f.Combat.Resolve(Ctx(f, dual, t, offHand: true));
        Assert.Equal(2 + 1, o.Damage!.Total);

        // +2 enhancement (damage and DAMAGE_POWER_PLUS_TWO), 1d6 fire, Weapon Specialization +2.
        var magic = Make(f, Fighter, 5, new Vector3(9, 10, 0), str: 10);
        Equip(f, magic, Longsword, EquipSlot.RightHand, 1,
            ItemPropertyRules.Make(f.W, CombatK.IpEnhancement, 0, 2),
            ItemPropertyRules.Make(f.W, CombatK.IpDamageBonus, 10, 7));
        magic.Feats.Add(f.Rules.Stats(Longsword).Spec);
        f.Dice(15, 5, 3);
        o = f.Combat.Resolve(Ctx(f, magic, t));
        Assert.Equal(5 + 2 + 2, o.Damage!.ByType[CombatK.DamageSlashing]);
        Assert.Equal(3, o.Damage.ByType[CombatK.DamageFire]);
        Assert.Equal(2, o.Damage.Power);
        Assert.Equal(3, t.LastDamageByType[CombatK.DamageFire]);

        // Damage increase effects by type, and Power Attack mode (a player's: it does nothing for other creatures).
        magic.Effects.Add(Effect(CombatK.EffectDamageIncrease, 2, CombatK.DamageCold));
        int abBefore = Ab(f, magic, t);
        magic.Feats.Add(CombatK.FeatPowerAttack);
        magic.ActionModes.Add(CombatK.ModePowerAttack);
        Assert.Equal(abBefore, Ab(f, magic, t));
        magic.Player = new PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };
        f.W.GameDifficulty = Difficulty.Normal; // at Easy a player's weapon dice come up highest
        Assert.Equal(abBefore - 5, Ab(f, magic, t));
        f.Dice(15, 5, 3);
        o = f.Combat.Resolve(Ctx(f, magic, t));
        Assert.Equal(5 + 2 + 2 + 5, o.Damage!.ByType[CombatK.DamageSlashing]);
        Assert.Equal(2, o.Damage.ByType[CombatK.DamageCold]);

        // As in the original, a damage effect of another type gets past resistance (item property damage doesn't,
        // and a type resisted to nothing is still reported); a physical one joins the weapon's damage.
        var k = EffectConstants.Of(f.W);
        var resist = new Effect { Type = k.DamageResistance, DurationType = 2 };
        resist.Ints[0] = CombatK.DamageCold | CombatK.DamageFire;
        resist.Ints[1] = 10;
        t.Effects.Add(resist);
        magic.Effects.Add(Effect(CombatK.EffectDamageIncrease, 2, CombatK.DamagePiercing));
        f.Dice(15, 5, 3);
        o = f.Combat.Resolve(Ctx(f, magic, t));
        Assert.Equal(5 + 2 + 2 + 5 + 2, o.Damage!.ByType[CombatK.DamageSlashing]);
        Assert.Equal(16 + 2, o.Dealt);
        Assert.Equal(2, t.LastDamageByType[CombatK.DamageCold]);
        Assert.Equal(0, t.LastDamageByType[CombatK.DamageFire]);
        Assert.Equal(16, t.LastDamageByType[CombatK.DamageBaseWeapon]);
    }

    [DataFact]
    public void MonkUnarmedDamageAndAttacks()
    {
        var f = Make();
        var t = Target(f, new Vector3(11, 10, 0));
        t.InCombat = true;
        var monk = Make(f, Monk, 8, new Vector3(10, 10, 0), feats: new[] { CombatK.FeatFlurryOfBlows });
        // Monk BAB 6 with the monk progression: 6 / 3 -> two attacks at -3; flurry adds one and -2 to all.
        Assert.Equal(new[] { 0, 3 }, f.Rules.PlanRound(monk).Select(p => p.Penalty));
        monk.ActionModes.Add(CombatK.ModeFlurry);
        Assert.Equal(3, f.Rules.PlanRound(monk).Count);
        Assert.Equal(6 - 2, Ab(f, monk, t));
        // Level 8 monk: 1d10.
        f.Dice(15, 10);
        var o = f.Combat.Resolve(Ctx(f, monk, t));
        Assert.Equal(10, o.Damage!.ByType[CombatK.DamageBludgeoning]);
        Assert.Equal(CombatK.SpecialFlurry, f.Combat.State(monk).LastAttackType);
        Assert.Equal(CombatK.CombatModeFlurry, f.Combat.State(monk).LastAttackMode);
    }

    // ------------------------------------------------------------------ sneak attack

    [DataFact]
    public void SneakAttackOnFlatFootedOrFlankedTargetsButNotUndead()
    {
        var f = Make();
        var rogue = Make(f, Rogue, 5, new Vector3(10, 10, 0), feats: new[] { 221, 345, 346 });
        Equip(f, rogue, Dagger, EquipSlot.RightHand);
        Assert.Equal((3, 0), f.Rules.SneakDice(rogue));

        // Not attacking anyone: flat-footed.
        var t = Target(f, new Vector3(11, 10, 0));
        f.Dice(15, 2, 3, 3, 3);
        var o = f.Combat.Resolve(Ctx(f, rogue, t));
        Assert.Equal(9, o.Damage!.Sneak);
        Assert.Equal(11, o.Damage.Total);
        Assert.Contains("Sneak Attack", o.Feedback);
        Assert.StartsWith("Sneak Attack : ", f.Combat.LogLines(o)[0]);

        // A plot creature is immune to sneak attacks, and the log says so (stealthself).
        var dummy = Target(f, new Vector3(10, 11, 0));
        dummy.Plot = true;
        f.Dice(15, 2);
        o = f.Combat.Resolve(Ctx(f, rogue, dummy));
        Assert.True(o.SneakImmune);
        Assert.Equal(0, o.Damage!.Sneak);
        Assert.Contains(f.Combat.LogLines(o), l => l == $"{dummy.Tag} : Immune to Sneak Attacks." || l.EndsWith(" : Immune to Sneak Attacks."));

        // Passive but in combat: still flat-footed, and Uncanny Dodge keeps the Dex bonus but not sneak attacks off.
        Assert.True(t.InCombat);
        t.Feats.Add(CombatK.FeatUncannyDodge1);
        var ctx = Ctx(f, rogue, t);
        Assert.False(ctx.TargetDeniedDex);
        f.Dice(15, 2, 1, 1, 1);
        Assert.Equal(3, f.Combat.Resolve(ctx).Damage!.Sneak);

        // Fighting the rogue: no sneak attack.
        f.Combat.Attack(t, rogue);
        f.Dice(15, 2);
        o = f.Combat.Resolve(Ctx(f, rogue, t));
        Assert.Equal(0, o.Damage!.Sneak);

        // Fighting someone else, wherever they stand: flanked.
        var ally = Make(f, Fighter, 5, new Vector3(11, 11, 0));
        f.Combat.Attack(t, ally);
        ctx = Ctx(f, rogue, t);
        Assert.True(ctx.Flanked);
        Assert.False(ctx.TargetDeniedDex);
        f.Dice(15, 2, 1, 1, 1);
        o = f.Combat.Resolve(ctx);
        Assert.Equal(3, o.Damage!.Sneak);

        // So is anything immune to critical hits.
        var hardy = Target(f, new Vector3(9, 10, 0));
        var k = EffectConstants.Of(f.W);
        hardy.Effects.Add(Effect(k.Immunity, k["IMMUNITY_TYPE_CRITICAL_HIT"]));
        f.Dice(15, 2);
        Assert.Equal(0, f.Combat.Resolve(Ctx(f, rogue, hardy)).Damage!.Sneak);

        // Undead are immune.
        var undead = Target(f, new Vector3(10, 9, 0));
        undead.Race = CombatK.RaceUndead;
        f.Dice(15, 2);
        o = f.Combat.Resolve(Ctx(f, rogue, undead));
        Assert.Equal(0, o.Damage!.Sneak);
        Assert.Equal(2, o.Damage.Total);
    }

    // ------------------------------------------------------------------ ammunition

    [DataFact]
    public void RangedAttacksUseAmmunitionAndStopWhenOut()
    {
        var f = Make();
        var archer = Make(f, Fighter, 5, new Vector3(10, 10, 0));
        Equip(f, archer, Longbow, EquipSlot.RightHand);
        var arrows = Equip(f, archer, Arrow, EquipSlot.Arrows, 2);
        var spare = Carry(f, archer, Arrow, 1);
        var t = Target(f, new Vector3(18, 10, 0));
        t.MaxHitPoints = t.CurrentHitPoints = 500;
        f.Dice(1, 1, 1, 1);

        var o = f.Combat.Resolve(Ctx(f, archer, t));
        Assert.False(o.OutOfAmmo);
        Assert.True(o.Context.Ranged);
        Assert.Equal(1, arrows.StackSize);
        f.Combat.Resolve(Ctx(f, archer, t));
        Assert.True(arrows.IsDestroyed);
        Assert.Same(spare, archer.Equipment[EquipSlot.Arrows]);
        f.Combat.Resolve(Ctx(f, archer, t));
        Assert.False(archer.Equipment.ContainsKey(EquipSlot.Arrows));
        o = f.Combat.Resolve(Ctx(f, archer, t));
        Assert.True(o.OutOfAmmo);

        // Bows take a Strength penalty to damage (a hit still deals at least 1); crossbows don't.
        var weak = Make(f, Fighter, 5, new Vector3(10, 13, 0), str: 6);
        Equip(f, weak, Longbow, EquipSlot.RightHand);
        Equip(f, weak, Arrow, EquipSlot.Arrows, 5);
        f.Dice(15, 5);
        Assert.Equal(5 - 2, f.Combat.Resolve(Ctx(f, weak, t)).Damage!.Total);
        Assert.Equal(0, f.Rules.StrengthDamage(Ctx(f, weak, t), f.Rules.Stats(6)));

        // Thrown weapons use themselves up.
        var thrower = Make(f, Fighter, 5, new Vector3(10, 12, 0));
        var axes = Equip(f, thrower, ThrowingAxe, EquipSlot.RightHand, 2);
        f.Combat.Resolve(Ctx(f, thrower, t));
        Assert.Equal(1, axes.StackSize);
        f.Combat.Resolve(Ctx(f, thrower, t));
        Assert.False(thrower.Equipment.ContainsKey(EquipSlot.RightHand));

        // Unlimited ammunition needs none.
        var endless = Make(f, Fighter, 5, new Vector3(10, 14, 0));
        Equip(f, endless, Longbow, EquipSlot.RightHand, 1, ItemPropertyRules.Make(f.W, CombatK.IpUnlimitedAmmo, 0, 1));
        o = f.Combat.Resolve(Ctx(f, endless, t));
        Assert.False(o.OutOfAmmo);
        Assert.NotNull(o.Context.Ammo);
    }

    // ------------------------------------------------------------------ special attacks

    [DataFact]
    public void KnockdownAndDisarmFromActionUseFeat()
    {
        var f = Make();
        var a = Make(f, Fighter, 10, new Vector3(10, 10, 0), feats: new[] { CombatK.FeatKnockdown, CombatK.FeatDisarm });
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        t.Scripts["ScriptAttacked"] = "none"; // don't fight back
        var sword = Equip(f, t, Longsword, EquipSlot.RightHand);

        // Knockdown: hit (15), damage (3), the target's Discipline check fails (1).
        Assert.True(CombatSystem.TryUseCombatFeat(f.W, a, CombatK.FeatKnockdown, 0, t.Id));
        f.Dice(15, 3, 1);
        // The feat starts an attack whose round begins as it arrives; the first attack comes 0.3 seconds into the round,
        // and its blow lands 0.55 seconds later.
        for (int i = 0; i < 4; i++) f.W.Step(0.1f);
        Assert.DoesNotContain(t.Effects, e => e.Type == CombatK.EffectKnockdown);
        for (int i = 0; i < 7; i++) f.W.Step(0.1f);
        Assert.Contains(t.Effects, e => e.Type == CombatK.EffectKnockdown);
        Assert.Equal(CombatK.SpecialKnockdown, f.Combat.State(a).LastAttackType);
        var data = f.Output.Landed.Last();
        Assert.Contains("Knockdown", Get<List<string>>(data, "feedback"));

        // Disarm: hit, no damage, Discipline fails: the sword falls to the ground.
        var ctx = Ctx(f, a, t);
        ctx.SpecialAttack = CombatK.SpecialDisarm;
        ctx.SpecialFeat = CombatK.FeatDisarm;
        f.Dice(18, 1);
        var o = f.Combat.Resolve(ctx);
        Assert.True(o.Hit);
        Assert.Null(o.Damage);
        Assert.False(t.Equipment.ContainsKey(EquipSlot.RightHand));
        Assert.Same(f.Area, sword.Area);
    }

    [DataFact]
    public void WhirlwindAttacksEveryAdjacentEnemy()
    {
        var f = Make();
        var a = Make(f, Fighter, 10, new Vector3(10, 10, 0), feats: new[] { CombatK.FeatWhirlwindAttack });
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var enemies = new[] { Target(f, new Vector3(11, 10, 0)), Target(f, new Vector3(9, 10, 0)), Target(f, new Vector3(10, 11, 0)) };
        var friend = Make(f, Fighter, 1, new Vector3(10, 9, 0));
        var far = Target(f, new Vector3(20, 10, 0));
        f.Dice(15, 1, 15, 1, 15, 1);
        var results = f.Combat.Whirlwind(a, improved: false, feedback: true);
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Hit));
        Assert.All(enemies, e => Assert.Equal(a.Id, f.Combat.State(e).LastAttacker));
        Assert.Null(f.Combat.TryState(friend)?.LastAttack);
        Assert.Equal(GameObject.InvalidId, f.Combat.TryState(far)?.LastAttacker ?? GameObject.InvalidId);
        Assert.Equal("whirlwind", results[0].Animation);
    }

    // ------------------------------------------------------------------ attacks of opportunity

    [DataFact]
    public void MovingAwayProvokesAnAttackOfOpportunity()
    {
        var f = Make();
        var hero = Make(f, Fighter, 5, new Vector3(10, 10, 0));
        Equip(f, hero, Longsword, EquipSlot.RightHand);
        var foe = Target(f, new Vector3(20, 10, 0));
        var runner = Target(f, new Vector3(11, 10, 0));
        runner.Scripts["ScriptAttacked"] = "none";
        f.Combat.Attack(hero, foe);
        f.Dice(15, 15, 15, 15);
        f.W.Step(0.1f); // the hero notes the runner in reach
        // A creature that isn't attacking the hero walks away freely (nwserver, combat_aoo walk).
        runner.Position = new Vector3(14, 10, 0);
        runner.MoveDirection = new Vector3(1, 0, 0);
        f.Combat.Step(0.1f);
        Assert.DoesNotContain(f.Output.Attacks, d => Get<uint>(d, "target") == runner.Id);
        // One attacking it draws an attack as it leaves.
        runner.Position = new Vector3(11, 10, 0);
        runner.MoveDirection = Vector3.Zero;
        var rs = f.Combat.State(runner);
        rs.Target = hero.Id;
        rs.RoundStart = f.W.Time; // attacking, between swings
        rs.Plan = new();
        runner.Actions.Add(new WaitAction { Remaining = -1 }); // busy: it doesn't close in again
        f.Combat.Step(0.1f);
        runner.Position = new Vector3(14, 10, 0);
        runner.MoveDirection = new Vector3(1, 0, 0);
        f.Combat.Step(0.1f);
        var aoo = f.Output.Attacks.Where(d => Get<uint>(d, "target") == runner.Id).ToList();
        Assert.Single(aoo);
        Assert.Equal("opportunity", Get<string>(aoo[0], "kind"));
        Assert.Contains("Attack Of Opportunity", Get<List<string>>(aoo[0], "feedback"));
        Assert.Equal(1, f.Combat.State(hero).AttacksOfOpportunity);

        // Mobility: +4 AC against attacks of opportunity (dodge bonuses need the runner not flat-footed).
        f.Combat.Attack(runner, hero);
        var ctx = Ctx(f, hero, runner);
        ctx.Kind = AttackKind.Opportunity;
        int ac = f.Rules.ArmorClass(ctx);
        runner.Feats.Add(CombatK.FeatMobility);
        Assert.Equal(ac + 4, f.Rules.ArmorClass(ctx));
    }

    // ------------------------------------------------------------------ rounds

    [DataFact]
    public void PlayerMeleeRoundsAgainstPassiveTargetsRetainNativeHalfSecondDelay()
    {
        foreach (int count in new[] { 1, 2, 6 })
            foreach (bool player in new[] { false, true })
            {
                var f = Make();
                var actor = Make(f, Fighter, 1, new Vector3(10, 10, 0));
                actor.BaseAttackOverride = count;
                if (player) actor.Player = new PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };
                Equip(f, actor, Dagger, EquipSlot.RightHand);
                var target = Target(f, new Vector3(11, 10, 0));
                target.MaxHitPoints = target.CurrentHitPoints = 10000;
                var attacks = new List<double>();
                f.Combat.AttackResolved += outcome =>
                {
                    if (outcome.Context.Attacker == actor) attacks.Add(f.W.Time);
                };
                f.Combat.Attack(actor, target);
                for (int step = 0; step < 400; step++) f.W.Step(.05f);
                Assert.True(attacks.Count >= count * 3);
                double expected = (count == 1 ? 5.0 : 5.5) + (player ? .5 : 0);
                for (int i = 0; i < count * 2; i++)
                    Assert.InRange(attacks[i + count] - attacks[i], expected - .06, expected + .11);
            }
    }

    [DataFact]
    public void ClearedPlayerMeleeRoundKeepsHalfSecondOffsetForFollowingCast()
    {
        foreach (int count in new[] { 1, 2, 6 })
            foreach (bool player in new[] { false, true })
            {
                var f = Make();
                var actor = Make(f, Fighter, 1, new Vector3(10, 10, 0));
                actor.BaseAttackOverride = count;
                if (player) actor.Player = new PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };
                Equip(f, actor, Dagger, EquipSlot.RightHand);
                var target = Target(f, new Vector3(11, 10, 0));
                target.MaxHitPoints = target.CurrentHitPoints = 10000;
                actor.Actions.Add(new AttackAction { Target = target.Id });
                for (int i = 0; i < 20; i++) f.W.Step(.05f);
                var state = f.Combat.State(actor);
                Assert.Equal(count == 6 ? 2 : 1, state.NextAttack);
                double start = state.RoundStart;
                ActionQueues.Clear(f.W, actor);
                f.W.Step(.05f);
                double shortened = count == 1 ? 5 : count == 2 ? 4.5 : 3.65;
                Assert.InRange(f.Combat.NextCastTime(actor) - start,
                    shortened + (player ? .5 : 0) - .001, shortened + (player ? .5 : 0) + .001);
            }
    }

    [DataFact]
    public void AttacksFollowTheOriginalsRoundTiming()
    {
        var f = Make();
        var a = Make(f, Fighter, 11, new Vector3(10, 10, 0));
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        t.MaxHitPoints = t.CurrentHitPoints = 1000;
        t.Scripts["ScriptAttacked"] = "none";
        f.Combat.Attack(a, t);
        var times = new List<double>();
        int seen = 0;
        double start = f.W.Time;
        for (int i = 0; i < 62; i++)
        {
            f.W.Step(0.1f);
            int n = f.Output.Attacks.Count(d => Get<uint>(d, "attacker") == a.Id);
            if (n > seen) { times.Add(f.W.Time - start); seen = n; }
        }
        // Three attacks at about 0.15, 1.95 and 3.8 seconds; the round lasts 5.5, so the next starts before 6.
        Assert.Equal(4, seen);
        Assert.InRange(times[0], 0.1, 0.4);
        Assert.InRange(times[1] - times[0], 1.7, 1.95);
        Assert.InRange(times[2] - times[1], 1.7, 1.95);
        Assert.InRange(times[3], 5.5, 5.9);
        Assert.True(a.InCombat);
        Assert.True(t.InCombat);
        var first = f.Output.Attacks.First();
        Assert.Equal(5.5f / 3, Get<float>(first, "swing"), 3);

        // A round whose target dies early is cut short by the swing that killed it.
        Assert.Equal(5f, CombatSystem.CutShortRound(1, 1));
        Assert.Equal(4.5f, CombatSystem.CutShortRound(2, 1));
        Assert.Equal(5.5f, CombatSystem.CutShortRound(2, 2));
        Assert.Equal(3.65f, CombatSystem.CutShortRound(4, 2));
        Assert.Equal(4.65f, CombatSystem.CutShortRound(5, 4));
        Assert.Equal(5.5f, CombatSystem.CutShortRound(6, 5));
        Assert.NotNull(Get<Dictionary<string, int>>(first, "damageByType"));
        Assert.Equal(a.Id, f.Combat.State(t).LastAttacker);
    }

    [DataFact]
    public void RoundEventsFollowTheOriginal()
    {
        var f = Make(scripts: true);
        var a = Make(f, Fighter, 11, new Vector3(10, 10, 0), str: 14);
        a.Tag = "attacker";
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        t.Tag = "target";
        t.MaxHitPoints = t.CurrentHitPoints = 1000;
        t.Scripts["ScriptAttacked"] = "combat_onatk";
        t.Scripts["ScriptEndRound"] = "combat_oneor";
        t.Scripts["ScriptDamaged"] = "combat_ondmg";
        a.Scripts["ScriptEndRound"] = "combat_oneor";
        f.Dice(19, 5);
        f.Combat.Attack(a, t);
        for (int i = 0; i < 120; i++) f.W.Step(0.1f);
        var m = f.W.Module.Locals.Ints;
        // OnPhysicalAttacked for every attack, and nothing fights back without a script telling it to.
        int attacks = f.Output.Attacks.Count(d => Get<uint>(d, "attacker") == a.Id);
        Assert.Equal(7, attacks);
        Assert.Equal(attacks, m.GetValueOrDefault("attacked"));
        Assert.DoesNotContain(f.Output.Attacks, d => Get<uint>(d, "attacker") == t.Id);
        // Rounds of three attacks last 5.5 seconds; the defender has six-second rounds while in combat.
        Assert.Equal(2, m.GetValueOrDefault("rounds_attacker"));
        Assert.Equal(1, m.GetValueOrDefault("rounds_target"));
        // A weapon hit reports its physical damage as base weapon damage.
        Assert.True(m.GetValueOrDefault("dmg_base") > 0);
        Assert.Equal(m.GetValueOrDefault("dmg_total"), m.GetValueOrDefault("dmg_base"));
        Assert.Equal(-1, m.GetValueOrDefault("dmg_slashing"));
    }

    // ------------------------------------------------------------------ script functions

    [DataFact]
    public void ScriptFunctionsTouchWhirlwindAndLastAttack()
    {
        var f = Make(scripts: true);
        var a = Make(f, Fighter, 8, new Vector3(10, 10, 0), str: 14, feats: new[] { CombatK.FeatPowerAttack });
        var sword = Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        t.Scripts["ScriptAttacked"] = "none";
        a.Locals.Objects["target"] = t.Id;
        // Touch attack 15 (hit); whirlwind: 15 to hit, 6 damage.
        f.Dice(15, 15, 6);
        f.W.Scripts!.Run("combat_funcs", a.Id);
        var m = f.W.Module.Locals.Ints;
        Assert.Equal(1, m["touch"]);
        Assert.Equal(0, m["type"]);
        Assert.Equal(CombatK.CombatModePowerAttack, m["mode"]);
        Assert.Equal(1, m["weapon_ok"]);
        Assert.Equal(1, m["attacker_ok"]);
        // 1d8 (6) + Str 2: Power Attack does nothing for a creature that isn't a player (nwserver, combat_modes).
        Assert.Equal(100 - 8, m["hp"]);
        Assert.Equal(sword.Id, f.Combat.State(a).LastWeapon);
    }

    [DataFact]
    public void BashApproachesPlaceablesFromEverySideAndClearingTheOrderStopsIt()
    {
        foreach (string blueprint in new[] { "plc_chest1", "plc_desk", "plc_armoire", "plc_table" })
            for (int side = 0; side < 4; side++)
            {
                var f = Make();
                f.Area.Width = f.Area.Height = 4;
                f.Area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
                f.Area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
                var centre = new Vector3(20, 20, 0);
                var t = new AreaLoader(f.W).CreatePlaceable(f.Area, f.W.Resources.ReadGff(blueprint, ResType.Utp)!, centre, 0);
                t.Plot = true;
                float angle = side * MathF.PI / 2;
                var start = centre + new Vector3(MathF.Cos(angle) * 6, MathF.Sin(angle) * 6, 0);
                var a = Make(f, Fighter, 5, start);
                Equip(f, a, Longsword, EquipSlot.RightHand);
                a.Actions.Add(new AttackAction { Target = t.Id });
                for (int i = 0; i < 240; i++) f.W.Step(1f / 30);
                Assert.True(f.Output.Attacks.Any(), $"{blueprint}, side {side}: no attack at {a.Position}");
                Assert.InRange(Vector3.Distance(a.Position, centre), 1.45f, 2.01f);
                Assert.IsType<AttackAction>(Assert.Single(a.Actions));
                ActionQueues.Clear(f.W, a);
                Assert.False(f.Combat.IsInCombat(a));
                Assert.Equal(Vector3.Zero, a.MoveDirection);
            }
    }

    [DataFact]
    public void EndOfRoundFiresOnceWhenTheTargetDiesAndTheAiPicksItAgain()
    {
        // Found by the playtest autopilot (HotU, the drow attack on the Yawning Portal): defenders' end-of-round
        // scripts went back to a foe that had just died, the attack ended the round at once, and OnEndCombatRound
        // (with its battle cries) ran every tick - about ten shouts a second each.
        var f = Make(scripts: true);
        var a = Make(f, Fighter, 5, new Vector3(10, 10, 0));
        a.Scripts["ScriptEndRound"] = "combat_retarget";
        var t = Target(f, new Vector3(11, 10, 0));
        t.Scripts["ScriptAttacked"] = "none";
        a.Locals.Objects["foe"] = t.Id;
        f.Combat.Attack(a, t);
        f.W.Step(0.1f);
        HitPoints.Kill(f.W, t, a.Id);
        // The round still ends on time (5 s for one attack, as on nwserver), and only once (the idle six-second
        // OnEndCombatRound, counted from when the attack went, would come at about 6.1 s: round_idle).
        for (int i = 0; i < 165; i++) f.W.Step(1f / 30);
        Assert.InRange(a.Locals.Ints.GetValueOrDefault("end_rounds"), 1, 1);
        Assert.Equal(GameObject.InvalidId, f.Combat.TargetOf(a));
    }
    [DataFact]
    public void AuthoredPreferredDistanceBeyondRangedReachStillAttacks()
    {
        var f = Make();
        var source = new OpenWinter.Game.Persistence.MemorySource("authored far bow range");
        source.Set(ResKey.Of("baseitems", ResType.TwoDA), System.Text.Encoding.UTF8.GetBytes(
            "2DA V2.0\n\nlabel WeaponWield RangedWeapon NumDice DieToRoll WeaponType AmmunitionType PrefAttackDist MaxRange\n8 bow 5 20 1 8 1 1 45 12\n20 arrow 0 **** 1 6 1 1 1 0\n"));
        f.W.Resources.AddFirst(source);
        f.Area.Width = f.Area.Height = 10;
        f.Area.Walkmesh.Add(new(0, 0, 0), new(100, 0, 0), new(100, 100, 0), 4, true);
        f.Area.Walkmesh.Add(new(0, 0, 0), new(100, 100, 0), new(0, 100, 0), 4, true);
        var a = Make(f, Fighter, 1, new Vector3(10, 10, 0));
        var t = Target(f, new Vector3(80, 10, 0));
        t.Plot = true;
        Equip(f, a, Longbow, EquipSlot.RightHand);
        Equip(f, a, Arrow, EquipSlot.Arrows, 99);
        a.Actions.Add(new AttackAction { Target = t.Id });
        for (int tick = 0; tick < 900 && !f.Output.Attacks.Any(); tick++) f.W.Step(1f / 30);
        Assert.NotEmpty(f.Output.Attacks);
        Assert.True(Vector3.Distance(a.Position, t.Position) <= 40, "attacker should stop inside the 40 m ranged reach");
    }

    [DataFact]
    public void RangedAttacksKeepFortyMetreReachAndCloseToAuthoredPreferredDistance()
    {
        foreach (float distance in new[] { 20f, 40f, 40.3f, 70f })
        {
            var f = Make();
            var source = new OpenWinter.Game.Persistence.MemorySource("authored bow range");
            source.Set(ResKey.Of("baseitems", ResType.TwoDA), System.Text.Encoding.UTF8.GetBytes(
                "2DA V2.0\n\nlabel WeaponWield RangedWeapon NumDice DieToRoll WeaponType AmmunitionType PrefAttackDist MaxRange\n8 bow 5 20 1 8 1 1 5 12\n20 arrow 0 **** 1 6 1 1 1 0\n"));
            f.W.Resources.AddFirst(source);
            f.Area.Width = f.Area.Height = 10;
            f.Area.Walkmesh.Add(new(0, 0, 0), new(100, 0, 0), new(100, 100, 0), 4, true);
            f.Area.Walkmesh.Add(new(0, 0, 0), new(100, 100, 0), new(0, 100, 0), 4, true);
            var a = Make(f, Fighter, 1, new Vector3(10, 10, 0));
            var t = Target(f, new Vector3(10 + distance, 10, 0));
            t.Plot = true;
            Equip(f, a, Longbow, EquipSlot.RightHand);
            Equip(f, a, Arrow, EquipSlot.Arrows, 99);
            a.Actions.Add(new AttackAction { Target = t.Id });
            for (int tick = 0; tick < 900 && !f.Output.Attacks.Any(); tick++) f.W.Step(1f / 30);
            Assert.NotEmpty(f.Output.Attacks);
            float actual = Vector3.Distance(a.Position, t.Position);
            if (distance <= 40) Assert.Equal(distance, actual, 3);
            else Assert.InRange(actual, 5.25f, 5.36f);
        }
    }


    // ------------------------------------------------------------------ coup de grace

    [DataFact]
    public void AsleepOrHeldTargetsAreKilledByACoupDeGrace()
    {
        var f = Make();
        var a = Make(f, Fighter, 1, new Vector3(10, 10, 0));
        a.Tag = "Hero";
        Equip(f, a, Dagger, EquipSlot.RightHand);
        foreach (var type in new[] { NwRules.EffectSleep, NwRules.EffectParalyze })
        {
            var t = Target(f, new Vector3(11, 10, 0));
            t.Tag = "Bim";
            // Immunity to critical hits and to death doesn't help.
            t.Effects.Add(Effect(type));
            t.Effects.Add(Effect(NwRules.EffectImmunity, NwRules.ImmunityCriticalHit));
            f.Dice(1, 1, 1);
            var o = f.Combat.Resolve(Ctx(f, a, t));
            Assert.True(o.CoupDeGrace);
            Assert.True(t.IsDead);
            Assert.Equal(-11, t.CurrentHitPoints);
            Assert.Equal($"Coup De Grace : Hero attacks Bim : *automatic hit* : (20 + {o.AttackBonus} = {20 + o.AttackBonus})", Assert.Single(f.Combat.LogLines(o)));
            f.W.Destroy(t);
        }
    }

    [DataFact]
    public void StunnedOrPetrifiedTargetsAreAttackedNormally()
    {
        var f = Make();
        var a = Make(f, Fighter, 1, new Vector3(10, 10, 0));
        Equip(f, a, Dagger, EquipSlot.RightHand);
        foreach (var type in new[] { NwRules.EffectStunned, NwRules.EffectPetrify, NwRules.EffectCutsceneParalyze, NwRules.EffectEntangle })
        {
            var t = Target(f, new Vector3(11, 10, 0));
            t.Effects.Add(Effect(type));
            f.Dice(1);
            var o = f.Combat.Resolve(Ctx(f, a, t));
            Assert.False(o.CoupDeGrace);
            Assert.False(o.Hit);
            Assert.False(t.IsDead);
            f.W.Destroy(t);
        }
    }

    // ------------------------------------------------------------------ game difficulty and follow-ups

    static void MakePlayer(Creature c) => c.Player = new PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };

    [DataFact]
    public void DifficultyScalesNpcWeaponDiceOnPlayersAndSparesThemCriticals()
    {
        var f = Make();
        Assert.Equal(Difficulty.Easy, f.W.GameDifficulty); // the original's default (difficulty_default)
        var a = Make(f, Fighter, 10, new Vector3(10, 10, 0), str: 10);
        Equip(f, a, Dagger, EquipSlot.RightHand);
        var pc = Target(f, new Vector3(11, 10, 0));
        pc.MaxHitPoints = pc.CurrentHitPoints = 1000;
        MakePlayer(pc);
        var npc = Target(f, new Vector3(10, 11, 0));
        npc.MaxHitPoints = npc.CurrentHitPoints = 1000;

        // Easy (MaxNPCDamagePercent 50): a dagger's 1d4 loses 2, leaving at least 1; not against a creature that isn't a player.
        f.Dice(15, 4);
        Assert.Equal(2, f.Combat.Resolve(Ctx(f, a, pc)).Damage!.Total);
        f.Dice(15, 2);
        Assert.Equal(1, f.Combat.Resolve(Ctx(f, a, pc)).Damage!.Total);
        f.Dice(15, 4);
        Assert.Equal(4, f.Combat.Resolve(Ctx(f, a, npc)).Damage!.Total);
        // Normal: full damage.
        f.W.GameDifficulty = Difficulty.Normal;
        f.Dice(15, 4);
        Assert.Equal(4, f.Combat.Resolve(Ctx(f, a, pc)).Damage!.Total);
        // Very Difficult (200): 0-99% of the dice's highest roll is added (random 75: 4 x 75 / 100 = 3).
        f.W.GameDifficulty = Difficulty.Difficult;
        f.Dice(15, 4, 76);
        Assert.Equal(7, f.Combat.Resolve(Ctx(f, a, pc)).Damage!.Total);
        // A player's own weapon dice at Easy (MinPCDamagePercent 50) are topped up by 50-99% of their highest roll,
        // capped there; above Easy they roll as usual.
        f.W.GameDifficulty = Difficulty.Easy;
        var hero = Make(f, Fighter, 10, new Vector3(12, 10, 0));
        Equip(f, hero, Dagger, EquipSlot.RightHand);
        MakePlayer(hero);
        f.Dice(15, 1, 1);
        Assert.Equal(3, f.Combat.Resolve(Ctx(f, hero, npc)).Damage!.Total);
        f.Dice(15, 1, 50);
        Assert.Equal(4, f.Combat.Resolve(Ctx(f, hero, npc)).Damage!.Total);
        f.W.GameDifficulty = Difficulty.Normal;
        f.Dice(15, 1);
        Assert.Equal(1, f.Combat.Resolve(Ctx(f, hero, npc)).Damage!.Total);

        // Below D&D Hardcore a threat on a player is rolled and confirmed but stays an ordinary hit.
        foreach (var (difficulty, critical) in new[] { (Difficulty.Easy, false), (Difficulty.Normal, false), (Difficulty.CoreRules, true), (Difficulty.Difficult, true) })
        {
            f.W.GameDifficulty = difficulty;
            f.Dice(20, 15, 2, 2, 2, 2);
            var o = f.Combat.Resolve(Ctx(f, a, pc));
            Assert.True(o.Hit && o.Threat);
            Assert.Equal(critical, o.Critical);
            Assert.Contains("Threat Roll", f.Combat.LogLines(o)[0]);
        }
        f.W.GameDifficulty = Difficulty.Easy;
        f.Dice(20, 15, 2, 2);
        Assert.True(f.Combat.Resolve(Ctx(f, a, npc)).Critical);
    }

    [DataFact]
    public void RoundBlowsLandAfterTheSwing()
    {
        var f = Make();
        var a = Make(f, Fighter, 1, new Vector3(10, 10, 0), str: 10);
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        t.Scripts["ScriptAttacked"] = "none";
        f.Dice(15, 5);
        f.Combat.Attack(a, t);
        for (int i = 0; i < 3; i++) f.W.Step(0.1f); // the swing, 0.2 s into the round
        var swing = Assert.Single(f.Output.Attacks);
        Assert.True(Get<bool>(swing, "pending"));
        Assert.Empty(Get<List<string>>(swing, "log"));
        Assert.Equal(100, t.CurrentHitPoints);
        Assert.DoesNotContain(f.Output.Notifies, n => n.Kind == "attack-impact");
        for (int i = 0; i < 6; i++) f.W.Step(0.1f);
        Assert.Equal(95, t.CurrentHitPoints);
        var landed = Assert.Single(f.Output.Notifies, n => n.Kind == "attack-impact").Data;
        Assert.Equal(5, Get<int>(landed, "dealt"));
        Assert.Contains(Get<List<string>>(landed, "log"), l => l.Contains("damages"));
        // Attacks resolved outside a round (attacks of opportunity, scripts) land at once.
        f.Dice(15, 5);
        var o = f.Combat.Resolve(Ctx(f, a, t));
        Assert.False(o.Pending);
        Assert.Equal(90, t.CurrentHitPoints);
    }

    [DataFact]
    public void CombatLogPrintsANegativeAttackBonusAsASubtraction()
    {
        var f = Make();
        var a = Make(f, Fighter, 1, new Vector3(10, 10, 0), str: 1);
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        f.Dice(9);
        var o = f.Combat.Resolve(Ctx(f, a, t));
        Assert.True(o.AttackBonus < 0);
        Assert.EndsWith($"({o.Roll} - {-o.AttackBonus} = {o.Roll + o.AttackBonus})", f.Combat.LogLines(o)[0]);
    }

    [DataFact]
    public void LargeCreaturesCantWieldTinyWeapons()
    {
        var f = Make();
        var ogre = Make(f, Fighter, 5, new Vector3(10, 10, 0));
        ogre.Size = 4;
        var giant = Make(f, Fighter, 5, new Vector3(12, 10, 0));
        giant.Size = 5;
        var man = Make(f, Fighter, 5, new Vector3(14, 10, 0));
        var inv = f.W.Inventory;
        // combat_equip: a large ogre can't take a dagger (size 1) but can a light mace (2); a huge giant neither.
        Assert.False(inv.Equip(ogre, Carry(f, ogre, Dagger), EquipSlot.RightHand));
        Assert.True(inv.Equip(ogre, Carry(f, ogre, 9), EquipSlot.RightHand));
        Assert.False(inv.Equip(giant, Carry(f, giant, 9), EquipSlot.RightHand));
        Assert.True(inv.Equip(giant, Carry(f, giant, Longsword), EquipSlot.RightHand));
        Assert.True(inv.Equip(man, Carry(f, man, Dagger), EquipSlot.RightHand));
    }

    [DataFact]
    public void ScriptedCombatModesDoNothingForNpcAttackersAndEndWithTheirRound()
    {
        var f = Make();
        var a = Make(f, Fighter, 1, new Vector3(10, 10, 0), str: 10, feats: new[] { CombatK.FeatPowerAttack });
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Target(f, new Vector3(11, 10, 0));
        t.MaxHitPoints = t.CurrentHitPoints = 1000;
        t.Scripts["ScriptAttacked"] = "none";
        t.Feats.Add(CombatK.FeatExpertise);
        t.ActionModes.Add(CombatK.ModeExpertise);
        a.ActionModes.Add(CombatK.ModePowerAttack);
        f.Dice(15, 5);
        var o = f.Combat.Resolve(Ctx(f, a, t));
        Assert.Equal(5, o.Damage!.Total); // no +5
        f.Combat.Attack(a, t);
        for (int i = 0; i < 20; i++) f.W.Step(0.1f);
        Assert.Contains(CombatK.ModePowerAttack, a.ActionModes);
        for (int i = 0; i < 40; i++) f.W.Step(0.1f); // past the end of the first round (5 s)
        Assert.DoesNotContain(CombatK.ModePowerAttack, a.ActionModes);
        Assert.Contains(CombatK.ModeExpertise, t.ActionModes); // a defender keeps its mode
    }

    [DataFact]
    public void ArchersStepBackFromCloseTargetsAndDrawAnAttack()
    {
        // archer_backoff (nwserver, archers with no scripts): 0.8 m steps straight back up to 3.8 m. archer_aoo: a shot
        // fired inside the target's reach (preferred distance + 1.5 m) once the target is in combat draws its attack
        // of opportunity, and the passive target then takes up the fight; the steps themselves draw nothing.
        foreach (var (dist, steps) in new[] { (1.5f, true), (3f, true), (6f, false) })
        {
            var f = Make();
            f.Area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 3, true);
            f.Area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 3, true);
            f.Area.Width = f.Area.Height = 4;
            var archer = Make(f, Fighter, 1, new Vector3(10, 10, 0));
            Equip(f, archer, Longbow, EquipSlot.RightHand);
            Equip(f, archer, Arrow, EquipSlot.Arrows, 99);
            var t = Target(f, new Vector3(10 + dist, 10, 0));
            t.MaxHitPoints = t.CurrentHitPoints = 1000;
            t.Scripts["ScriptAttacked"] = "none";
            t.Facing = MathF.PI; // facing the archer
            Equip(f, t, Longsword, EquipSlot.RightHand);
            f.W.Step(4.1f); // both have perceived each other
            f.Combat.Attack(archer, t);
            for (int i = 0; i < 120 && f.Output.Attacks.All(d => Get<uint>(d, "attacker") != t.Id); i++) f.W.Step(0.1f);
            float moved = Vector3.Distance(archer.Position, new Vector3(10, 10, 0));
            if (!steps) { Assert.True(moved < 0.01f); continue; }
            Assert.Equal(10, archer.Position.Y, 2); // straight back
            Assert.True(archer.Position.X < 10 - 0.5f);
            var aoo = f.Output.Attacks.Where(d => Get<uint>(d, "attacker") == t.Id).ToList();
            if (dist > 2.5f)
            {
                // Out of the target's reach from the start: one step to 3.8 m, no attack of opportunity.
                Assert.Equal(CombatSystem.RangedBackStepRange, Vector3.Distance(archer.Position, t.Position), 2);
                Assert.Empty(aoo);
                continue;
            }
            Assert.Equal("opportunity", Get<string>(Assert.Single(aoo), "kind"));
            Assert.True(Vector3.Distance(archer.Position, t.Position) <= f.Combat.MeleeDistance(t, archer) + CombatSystem.ShotThreatReach);
            Assert.Equal(archer.Id, Assert.IsType<AttackAction>(t.Actions[0]).Target);
        }
    }

    [DataFact]
    public void APlayersShotsProvokeOnlyAtHardcoreAndNeverFromAPlotCreature()
    {
        // nwmain plotshot: a player shooting a dummy between two bandits in combat facing
        // her, 1.5 m away. At D&D Hardcore the immortal one's attack of opportunity came at each shot, the plot one's
        // never; at Normal neither made one.
        foreach (int difficulty in new[] { Difficulty.CoreRules, Difficulty.Normal })
        {
            var f = Make();
            f.W.GameDifficulty = difficulty;
            var pc = Make(f, Fighter, 1, new Vector3(10, 10, 0));
            pc.Player = new PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };
            Equip(f, pc, Longbow, EquipSlot.RightHand);
            Equip(f, pc, Arrow, EquipSlot.Arrows, 99);
            var dummy = Target(f, new Vector3(18, 10, 0));
            dummy.MaxHitPoints = dummy.CurrentHitPoints = 1000;
            f.W.Factions.SetPersonal(pc, dummy, 0);
            var guards = new List<Creature>();
            foreach (var (y, facing, plot) in new[] { (11.5f, -MathF.PI / 2, true), (8.5f, MathF.PI / 2, false) })
            {
                var g = Target(f, new Vector3(10, y, 0));
                g.MaxHitPoints = g.CurrentHitPoints = 1000;
                g.Facing = facing;
                g.Plot = plot;
                f.W.Factions.SetPersonal(g, pc, 0);
                f.W.Factions.SetPersonal(pc, g, 0);
                Equip(f, g, Longsword, EquipSlot.RightHand);
                guards.Add(g);
            }
            for (int i = 0; i < 45; i++) f.W.Step(0.1f); // everyone has perceived everyone
            foreach (var g in guards) g.InCombat = true;
            f.Combat.Attack(pc, dummy);
            for (int i = 0; i < 130; i++) f.W.Step(0.1f);
            var plotAoo = f.Output.Attacks.Where(d => Get<uint>(d, "attacker") == guards[0].Id).ToList();
            var aoo = f.Output.Attacks.Where(d => Get<uint>(d, "attacker") == guards[1].Id && Get<string>(d, "kind") == "opportunity").ToList();
            Assert.Empty(plotAoo);
            Assert.True(f.Output.Attacks.Count(d => Get<uint>(d, "attacker") == pc.Id) >= 2);
            if (difficulty == Difficulty.Normal) { Assert.Empty(aoo); continue; }
            // The first shot's, and later ones' while it was already fighting her (one a round at most).
            Assert.True(aoo.Count >= 2);
        }
    }

    [DataFact]
    public void DefensiveRollHalvesALethalBlowUnlessFlatFooted()
    {
        var f = Make();
        var a = Make(f, Fighter, 1, new Vector3(10, 10, 0), str: 10);
        Equip(f, a, Longsword, EquipSlot.RightHand);
        var t = Make(f, Rogue, 10, new Vector3(11, 10, 0), faction: FactionTable.Hostile, feats: new[] { CombatK.FeatDefensiveRoll });
        t.MaxHitPoints = t.CurrentHitPoints = 6;
        t.InCombat = true;
        f.Combat.State(t).Target = a.Id; // fighting: not flat-footed
        // Hit (15) for 8 on 6 hit points: Reflex save against DC 8 made with a natural 20: 4 damage.
        f.Dice(15, 8, 20);
        var o = f.Combat.Resolve(Ctx(f, a, t));
        Assert.Contains("Defensive Roll", o.Feedback);
        Assert.Equal(2, t.CurrentHitPoints);
        Assert.False(t.IsDead);
    }

    [DataFact]
    public void ACreatureModelSwingsWithItsOwnClipsByTheDistance()
    {
        // A summoned dire badger (appearance 9, PREFATCKDIST 2.6) played nothing: the server sent the
        // humanoid nwslashl. Now ca1slashl/ca1slashr/ca1stab where it stops to fight (2.8 m from a skeleton) and
        // ccloseh within its PREFATCKDIST (parity/visual scenes creatureswing*).
        const int DireBadger = 9, CreaturePierce = 70;
        var f = Make();
        var badger = Make(f, Fighter, 3, new Vector3(10, 10, 0));
        badger.AppearanceType = DireBadger;
        Equip(f, badger, CreaturePierce, EquipSlot.CreatureBite);
        var far = Target(f, new Vector3(12.8f, 10, 0));
        var near = Target(f, new Vector3(10, 12, 0));
        for (int i = 0; i < 12; i++)
        {
            f.Dice(1);
            Assert.Contains(f.Combat.Resolve(Ctx(f, badger, far)).Animation, new[] { "ca1slashl", "ca1slashr", "ca1stab" });
            f.Dice(1);
            Assert.Equal("ccloseh", f.Combat.Resolve(Ctx(f, badger, near)).Animation);
        }
        // A humanoid model keeps its stance's swings.
        var human = Make(f, Fighter, 3, new Vector3(20, 10, 0));
        human.AppearanceType = 6;
        var foe = Target(f, new Vector3(21, 10, 0));
        f.Dice(1);
        Assert.StartsWith("nw", f.Combat.Resolve(Ctx(f, human, foe)).Animation);
    }
}

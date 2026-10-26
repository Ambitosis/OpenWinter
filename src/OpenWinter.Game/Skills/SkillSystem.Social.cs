using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Skills;

/// <summary>
/// Skills used on creatures: Pick Pocket, Taunt, Heal and Animal Empathy, with the rules from the game's
/// skill descriptions and ruleset.2da.
/// </summary>
public sealed partial class SkillSystem
{
    /// <summary>Creatures dominated through Animal Empathy: target → (master, effect id).</summary>
    readonly Dictionary<uint, (uint Master, long Effect)> _dominated = new();

    // ------------------------------------------------------------------ pick pocket

    /// <summary>
    /// Pick Pocket: d20 + Pick Pocket against DC 20 (30 if the victim is hostile, 0 if dead) to take a random
    /// pickpocketable item or some gold; the victim rolls Spot (+10 if hostile) against the same total and
    /// notices if it beats it. A noticing NPC turns hostile and its OnDisturbed runs with
    /// INVENTORY_DISTURB_TYPE_STOLEN; a noticing player is told. Returns whether something was stolen.
    /// </summary>
    public bool PickPocket(Creature thief, Creature victim)
    {
        if (victim == thief || victim.IsDestroyed || !CanUse(thief, SkillIds.PickPocket)) return false;
        var items = victim.Inventory.Where(i => !i.IsDestroyed && i.Pickpocketable && !i.Plot).ToList();
        if (items.Count == 0 && victim.Gold <= 0)
        {
            Tell(thief, Tlk(SkillIds.StrNoItemsOrGold, "Target has no items or gold."));
            return false;
        }
        bool dead = victim.IsDead;
        bool hostile = !dead && _world.Factions.IsEnemyOf(victim, thief);
        int dc = dead ? Rule("PICKPOCKET_CREATURE_DEAD", 0)
            : hostile ? Rule("PICKPOCKET_HOSTILE_CREATURE_DC", 30) : Rule("PICKPOCKET_NEUTRAL_CREATURE_DC", 20);
        var r = Check(thief, SkillIds.PickPocket, dc);

        bool noticed = false;
        if (!dead)
        {
            int spot = Dice(20) + Bonus(victim, SkillIds.Spot) + (hostile ? Rule("SKILL_PICKPOCKET_HOSTILE_SPOT_BONUS", 10) : 0);
            noticed = spot > r.Total;
        }

        Item? stolen = null;
        int gold = 0;
        if (r.Success)
        {
            int pick = Dice(items.Count + (victim.Gold > 0 ? 1 : 0)) - 1;
            if (pick < items.Count)
            {
                var item = items[pick];
                stolen = _world.Inventory.MoveTo(item, thief) ?? (item.IsDestroyed || _world.Inventory.Refused(item) ? null : item);
                if (stolen != null)
                {
                    stolen.Stolen = true;
                    Tell(thief, Format(SkillIds.StrStoleItem, "You stole a <CUSTOM1> from <CUSTOM0>.", Name(victim), Name(item)));
                }
            }
            else
            {
                gold = Math.Min(victim.Gold, Dice(10) * Math.Max(1, thief.TotalLevel));
                _world.Inventory.AddGold(victim, -gold);
                _world.Inventory.AddGold(thief, gold);
                Tell(thief, Format(SkillIds.StrStoleGold, "You stole <CUSTOM0> GP from <CUSTOM1>.", gold.ToString(), Name(victim)));
            }
        }

        if (noticed)
        {
            if (victim.IsPC)
            {
                string text = stolen != null ? Format(SkillIds.StrStoleItemFromYou, "<CUSTOM0> stole a <CUSTOM1> from you!", Name(thief), Name(stolen))
                    : gold > 0 ? Format(SkillIds.StrStoleMoneyFromYou, "<CUSTOM0> stole some money from you!", Name(thief))
                    : Format(SkillIds.StrAttemptingSteal, "<CUSTOM0> is attempting to steal from you!", Name(thief));
                Tell(victim, text);
            }
            else
            {
                BecomeHostile(victim, thief);
                var ctx = new EventContext().Set("Disturber", thief.Id).Set("DisturbItem", stolen?.Id ?? GameObject.InvalidId)
                    .Set("DisturbType", SkillIds.DisturbStolen);
                _world.After(0, () => _world.RunEvent(victim, "ScriptDisturbed", ctx));
            }
        }
        return r.Success;
    }

    /// <summary>An NPC turns on a creature that wronged it (personal reputation, as SetIsTemporaryEnemy without decay).</summary>
    void BecomeHostile(Creature npc, Creature offender)
    {
        if (npc.IsPC || npc.IsDead) return;
        npc.Society.LastHostileActor = offender.Id;
        if (!_world.Factions.IsEnemyOf(npc, offender)) _world.Factions.SetTemporary(npc, offender, 0, false, 0);
    }

    // ------------------------------------------------------------------ taunt

    readonly Dictionary<uint, string> _tauntLinks = new();

    /// <summary>
    /// Taunt: the taunter's Taunt roll against the target's Concentration roll (+BULLHEADED_BONUS_VS_TAUNT with
    /// Bullheaded). On a win the target loses AC equal to the difference (at most TAUNT_MAX_MODIFIER) and gets
    /// TAUNT_ARCANE_SPELL_FAILURE% spell failure for TAUNT_EFFECT_DURATION seconds; taunts don't stack (a new one
    /// replaces the old). Taunting is an attack: NPC targets turn hostile. Only intelligent creatures (Int 3+) can be taunted.
    /// </summary>
    public bool Taunt(Creature taunter, Creature target)
    {
        if (target == taunter || target.IsDead || target.IsDestroyed) return false;
        if (CreatureRules.AbilityScore(_world, target, NwRules.AbilityInt) < 3)
        {
            Tell(taunter, Tlk(SkillIds.StrInvalidTauntTarget, "Invalid Target. Only intelligent creatures are valid targets for Taunt."));
            return false;
        }
        int resist = HasFeat(target, SkillIds.FeatBullheaded) ? Rule("BULLHEADED_BONUS_VS_TAUNT", 2) : 0;
        var conc = Roll(target, SkillIds.Concentration, resist);
        var r = Check(taunter, SkillIds.Taunt, conc.Total, feedback: false);
        r = r with { Success = r.Total > conc.Total }; // the taunt has to beat the Concentration roll
        Report(taunter, r, target);
        BecomeHostile(target, taunter);
        if (!r.Success) return false;

        var k = EffectConstants.Of(_world);
        int penalty = Math.Min(r.Total - conc.Total, Rule("TAUNT_MAX_MODIFIER", 6));
        var ac = NewEffect(k, "AC_DECREASE", taunter, penalty, k["AC_DODGE_BONUS"], k["AC_VS_DAMAGE_TYPE_ALL"]);
        var failure = NewEffect(k, "SPELL_FAILURE", taunter, Rule("TAUNT_ARCANE_SPELL_FAILURE", 30), k["SPELL_SCHOOL_GENERAL"]);
        var link = Link(ac, failure);

        var effects = _world.System<EffectSystem>();
        if (_tauntLinks.TryGetValue(target.Id, out var old)) effects.RemoveWhere(target, e => e.LinkId == old);
        effects.Apply(link, k.Temporary, target, RuleF("TAUNT_EFFECT_DURATION", 30f));
        var applied = target.Effects.LastOrDefault(e => e.Type == ac.Type && e.Creator == taunter.Id);
        if (applied != null) _tauntLinks[target.Id] = applied.LinkId;
        return true;
    }

    Effect NewEffect(EffectConstants k, string type, Creature creator, params int[] ints)
    {
        var e = new Effect { Type = k["EFFECT_TYPE_" + type], SubType = k.Extraordinary, Creator = creator.Id };
        for (int i = 0; i < ints.Length && i < e.Ints.Length; i++) e.Ints[i] = ints[i];
        return e;
    }

    Effect Link(params Effect[] effects)
    {
        var link = effects[0].Copy(false);
        link.Linked.Clear();
        foreach (var e in effects) link.Linked.Add(e.Copy(false));
        link.LinkId = _world.System<EffectSystem>().NewLinkId();
        foreach (var leaf in link.Linked) leaf.LinkId = link.LinkId;
        return link;
    }

    // ------------------------------------------------------------------ heal

    /// <summary>
    /// Heal with a healer's kit: d20 + Heal + the kit's bonus. The target regains that many hit points, and each
    /// poison or disease whose DC (poison.2da Save_DC, disease.2da First_Save) the total reaches is cured. Valid
    /// targets are diseased, poisoned or injured (a dying player is injured: healed above 0 it gets up); the kit is used up.
    /// </summary>
    public bool Heal(Creature healer, Creature target, Item? kit)
    {
        if (target.IsDestroyed) return false;
        // On a corpse the kit is spent and nothing heals (parity suite death_raise: kit.left 0, still dead).
        if (target.IsDead)
        {
            if (kit != null) UseUp(kit);
            return true;
        }
        var afflictions = target.Effects.Where(e => e.Type is SkillIds.EffectPoison or SkillIds.EffectDisease).ToList();
        bool injured = HitPoints.Current(target) < HitPoints.Maximum(target);
        if (!injured && afflictions.Count == 0)
        {
            Tell(healer, Tlk(SkillIds.StrInvalidHealTarget, "Invalid Target. Valid targets for Heal are any creature that is diseased, poisoned, or injured."));
            return false;
        }
        int kitBonus = kit != null && !kit.IsDestroyed ? ToolBonus(kit, SkillIds.IpHealersKit) : 0;
        int hardest = afflictions.Count == 0 ? 0 : afflictions.Max(AfflictionDc);
        var r = Check(healer, SkillIds.Heal, hardest, kitBonus, versus: target);
        HitPoints.Heal(_world, target, Math.Max(0, r.Total));
        var effects = _world.System<EffectSystem>();
        foreach (var e in afflictions)
            if (r.Total >= AfflictionDc(e)) effects.Remove(target, e);
        if (kit != null) UseUp(kit);
        return true;
    }

    int AfflictionDc(Effect e) => e.Type == SkillIds.EffectPoison
        ? _world.Resources.TwoDA("poison").GetInt(e.Ints[0], "Save_DC", 10)
        : _world.Resources.TwoDA("disease").GetInt(e.Ints[0], "First_Save", 10);

    // ------------------------------------------------------------------ animal empathy

    /// <summary>
    /// Animal Empathy: against ANIMAL_EMPATHY_ANIMAL_DC + hit dice for animals, ANIMAL_EMPATHY_BEAST_DC + hit dice
    /// for beasts and magical beasts. Success dominates the creature for one turn (60 s) per level of the user and
    /// makes it an associate (ASSOCIATE_TYPE_DOMINATED); failing by 5 or more turns it hostile.
    /// </summary>
    public bool AnimalEmpathy(Creature user, Creature target)
    {
        if (target == user || target.IsDead || target.IsDestroyed || !CanUse(user, SkillIds.AnimalEmpathy)) return false;
        int baseDc = target.Race switch
        {
            SkillIds.RaceAnimal => Rule("ANIMAL_EMPATHY_ANIMAL_DC", 15),
            SkillIds.RaceBeast or SkillIds.RaceMagicalBeast => Rule("ANIMAL_EMPATHY_BEAST_DC", 19),
            _ => -1,
        };
        if (baseDc < 0 || target.IsPC || target.Master != GameObject.InvalidId)
        {
            Tell(user, "Invalid Target. Only animals, beasts and magical beasts can be calmed with Animal Empathy.");
            return false;
        }
        var r = Check(user, SkillIds.AnimalEmpathy, baseDc + target.TotalLevel, versus: target);
        if (!r.Success)
        {
            if (r.Margin <= -5) BecomeHostile(target, user);
            return false;
        }
        var k = EffectConstants.Of(_world);
        var dominate = NewEffect(k, "DOMINATED", user);
        _world.System<EffectSystem>().Apply(dominate, k.Temporary, target, 60f * Math.Max(1, user.TotalLevel));
        var applied = target.Effects.LastOrDefault(e => e.Type == SkillIds.EffectDominated && e.Creator == user.Id);
        if (applied == null) return false; // immune
        ActionQueues.Clear(_world, target);
        _world.System<Combat.CombatSystem>().Stop(target);
        AssociateService.Add(_world, user, target, AssociateService.Dominated);
        _dominated[target.Id] = (user.Id, applied.UniqueId);
        return true;
    }

    /// <summary>Releases Animal Empathy dominations whose effect ended.</summary>
    void StepDomination()
    {
        if (_dominated.Count == 0) return;
        foreach (var (id, (masterId, effect)) in _dominated.ToList())
        {
            var target = _world.Get<Creature>(id);
            if (target != null && target.Effects.Any(e => e.UniqueId == effect)) continue;
            _dominated.Remove(id);
            if (target != null && _world.Get<Creature>(masterId) is { } master) AssociateService.Remove(_world, master, target);
        }
    }
}

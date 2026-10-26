using OpenWinter.Game.Objects;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Rules;

/// <summary>What a hit did against a target's defences, for combat feedback.</summary>
public sealed class DamageBreakdown
{
    /// <summary>Damage that got through, by DAMAGE_TYPE_* (0 for a type that was all taken away).</summary>
    public readonly Dictionary<int, int> Dealt = new();
    /// <summary>Points taken by immunity, by type.</summary>
    public readonly Dictionary<int, int> Immunity = new();
    public int Resistance, Reduction;
    /// <summary>The target was plot: nothing at all got through.</summary>
    public bool Plot;
}

/// <summary>
/// Hit point changes shared by effects and combat: healing, damage (after damage
/// immunity, resistance, reduction and temporary hit points), dying and death.
///
/// Damage on creatures fires ScriptDamaged and death fires ScriptDeath with event key
/// "Killer"; for player characters the module's Mod_OnPlrDying ("PlayerDying") runs when
/// they drop to 0..-9 and Mod_OnPlrDeath ("PlayerDied") at -10 or below. Placeables and
/// doors fire OnDamaged / OnDeath and are destroyed. Damage event keys: "Damager" (object),
/// "TotalDamage" (int), "Damage:&lt;DAMAGE_TYPE_* bit&gt;" (int per type), "DamagePower".
/// Events run on the next world step, like the original's event queue.
/// </summary>
public static class HitPoints
{
    /// <summary>Raised when damage lands: target, damager id, damage per DAMAGE_TYPE_* bit, total.</summary>
    public static event Action<GameObject, uint, IReadOnlyDictionary<int, int>, int>? Damaged;
    /// <summary>Raised when an object dies: victim, killer id.</summary>
    public static event Action<GameObject, uint>? Died;

    public const int PlayerDeathThreshold = -10;
    const int PhysicalTypes = 1 | 2 | 4, BaseWeaponDamage = 4096;

    public static int Current(GameObject o) => o switch
    {
        Creature c => c.CurrentHitPoints,
        Placeable p => p.CurrentHitPoints,
        Door d => d.CurrentHitPoints,
        _ => 0,
    };

    public static int Maximum(GameObject o) => o switch
    {
        Creature c => c.MaxHitPoints,
        Placeable p => p.HitPoints,
        Door d => d.HitPoints,
        _ => 0,
    };

    static void Set(GameObject o, int hp)
    {
        switch (o)
        {
            case Creature c: c.CurrentHitPoints = hp; break;
            case Placeable p: p.CurrentHitPoints = hp; break;
            case Door d: d.CurrentHitPoints = hp; break;
        }
    }

    public static bool IsDead(GameObject o) => o is Creature { IsDead: true } or Door { Broken: true } || o.IsDestroyed;

    /// <summary>Heals up to the maximum. Returns the hit points restored. The dead can't be healed.</summary>
    public static int Heal(World world, GameObject target, int amount)
    {
        if (target is Creature init) CreatureRules.EnsureInit(world, init);
        if (amount <= 0 || IsDead(target)) return 0;
        int cur = Current(target), max = Maximum(target);
        if (cur >= max) return 0;
        int now = Math.Min(max, cur + amount);
        Set(target, now);
        world.MarkChanged(target);
        world.Output?.Notify("hp-change", target, new { target = target.Id, amount = now - cur, type = 0, combat = false });
        if (cur <= 0 && now > 0 && target is Creature { IsPC: true } risen) StopDying(world, risen);
        return now - cur;
    }

    /// <summary>
    /// A dying player healed above 0 (dying, measured on nwmain): commandable again, and its hit point change when
    /// dying effects end (each adding its change once more as it goes, which can pass the maximum).
    /// </summary>
    static void StopDying(World world, Creature c)
    {
        c.Commandable = true;
        var k = EffectConstants.Of(world);
        world.System<EffectSystem>().RemoveWhere(c, e => e.Type == k.HitPointChangeWhenDying);
    }

    /// <summary>
    /// A player dropping from above 0 to dying: the original makes it uncommandable at once and it drops what it
    /// was doing; it lies down (the client's death pose) until healed, raised or dead.
    /// </summary>
    static void StartDying(World world, Creature pc)
    {
        ActionQueues.Clear(world, pc);
        Movement.Stop(pc);
        pc.Commandable = false;
        world.MarkChanged(pc);
    }

    /// <summary>Deals one type of damage. Returns the hit points actually lost.</summary>
    public static int Damage(World world, GameObject target, uint damager, int amount, int damageType, int power = 0) =>
        Damage(world, target, damager, new Dictionary<int, int> { [damageType] = amount }, power);

    /// <summary>
    /// Deals damage by DAMAGE_TYPE_* bit (combat passes the whole hit at once). Applies damage
    /// immunity (effects and items), resistance (best one, using up limited resistances),
    /// damage reduction against physical damage of lower <paramref name="power"/>, and temporary
    /// hit points. Ranged-only resistances and reductions apply when <paramref name="ranged"/>.
    /// Returns the hit points actually lost.
    /// </summary>
    /// <param name="combat">A weapon hit: the client already shows it from the attack notify, so the hp-change notify says so.</param>
    /// <param name="unresisted">The part of each type's damage that no immunity or resistance touches (a weapon hit's damage effects).</param>
    /// <param name="breakdown">Filled with what got through and what immunity, resistance and reduction took (combat feedback).</param>
    /// <param name="spellId">The spell the damage comes from, or -1.</param>
    /// <param name="weaponStyle">Reported like a weapon hit (physical damage as base weapon damage) without being one.</param>
    /// <param name="defended">The amounts already got past the target's defences (keys as given, zeros kept).</param>
    public static int Damage(World world, GameObject target, uint damager, IReadOnlyDictionary<int, int> byType, int power = 0, bool ignoreReductions = false, bool ranged = false,
        bool combat = false, IReadOnlyDictionary<int, int>? unresisted = null, DamageBreakdown? breakdown = null, int spellId = -1, bool weaponStyle = false,
        bool defended = false)
    {
        if (IsDead(target) || target is Creature { Player.IsAreaLoading: true } || target is not (Creature or Placeable or Door)) return 0;
        if (target is Creature init) CreatureRules.EnsureInit(world, init);
        var k = EffectConstants.Of(world);
        var effects = world.System<EffectSystem>();
        weaponStyle |= combat;
        if (combat && target.Plot)
        {
            // A plot target takes a weapon hit as 0 physical damage, and OnDamaged still runs, as in the original.
            if (breakdown != null) breakdown.Plot = true;
            var none = new Dictionary<int, int> { [BaseWeaponDamage] = 0 };
            CreatureRules.RecordDamage(target, world.Get(damager), none);
            var pctx = new EventContext().Set("Damager", damager).Set("TotalDamage", 0).Set("DamagePower", power).Set("Damage:" + BaseWeaponDamage, 0);
            world.After(0, () => world.RunEvent(target, target is Creature ? "ScriptDamaged" : "OnDamaged", pctx));
            return 0;
        }

        // NWNX_Damage: scripted damage meets the damage event before the target's defences, a weapon-style hit after them.
        if (!weaponStyle && !defended && Nwnx.Plugins.DamagePlugin.RunDamageEvent(world, target, damager, byType, spellId) is { } scripted)
        {
            byType = scripted;
            unresisted = null;
        }
        Dictionary<int, int> dealt = defended
            ? byType.Where(kv => kv.Value >= 0).ToDictionary(kv => kv.Key, kv => kv.Value)
            : Defend(world, target, damager, byType, power, ignoreReductions, ranged, unresisted, breakdown, consume: true);

        // Weapon hits report their physical damage as base weapon damage, and every type they carried even when
        // nothing of it got through, as the original does.
        Dictionary<int, int>? reported = null;
        if (weaponStyle)
        {
            reported = new Dictionary<int, int>();
            if (defended) foreach (var (type, amount) in dealt) reported[type] = amount;
            else
                foreach (var (type, raw) in byType)
                {
                    if (raw <= 0) continue;
                    int key = (type & PhysicalTypes) != 0 ? BaseWeaponDamage : type;
                    reported[key] = reported.GetValueOrDefault(key) + dealt.GetValueOrDefault(type);
                }
            if (Nwnx.Plugins.DamagePlugin.RunDamageEvent(world, target, damager, reported, spellId) is { } changed)
                reported = dealt = changed;
        }
        if (breakdown != null && (defended || reported == dealt))
        {
            breakdown.Dealt.Clear();
            foreach (var (type, amount) in dealt) breakdown.Dealt[type] = amount;
        }

        int total = dealt.Values.Where(v => v > 0).Sum();
        if (total <= 0) return 0;
        if (target is Creature { IsPC: true } trader) world.System<Requests.BarterService>().Interrupt(trader);
        // A master hurt while its player controls the familiar takes control back first, so the player
        // character rules (dying, OnPlayerDying/Death, no corpse decay) apply to it.
        if (target is Creature { FamiliarController: not null } possessedMaster)
            world.System<Players.FamiliarPossession>().ReleaseControl(possessedMaster);

        // Temporary hit points go first, oldest effect first.
        int hpLoss = total;
        foreach (var e in EffectQueries.Active(target, k.TemporaryHitpoints, ignoreVersus: true).ToList())
        {
            if (hpLoss <= 0) break;
            int take = Math.Min(hpLoss, e.Ints[0]);
            e.Ints[0] -= take;
            hpLoss -= take;
            // A polymorph's bonus hit points running out doesn't end the polymorph.
            if (e.Ints[0] <= 0)
            {
                if (e.Hidden && target.Effects.Any(x => x.LinkId == e.LinkId && x.Type == k["EFFECT_TYPE_POLYMORPH"])) effects.RemoveOnly(target, e);
                else effects.Remove(target, e);
            }
        }

        int before = Current(target);
        if (!target.Plot && hpLoss > 0)
        {
            int after = before - hpLoss;
            if (target is Creature { IsImmortal: true } && after < 1) after = Math.Min(before, 1);
            Set(target, after);
        }
        // Each hit asks a caster for its own Concentration check (CastSpellAction).
        if (target is Creature { Actions.Count: > 0 } caster && caster.Actions[0] is Spells.CastSpellAction cast) cast.TookDamage(total);
        world.MarkChanged(target);
        // Damage wakes a sleeper (nwserver, parity suite condition_actions: sleep.wake.after; no other condition breaks).
        if (total > 0 && target is Creature && target.Effects.Any(e => e.Type == NwRules.EffectSleep))
            effects.RemoveWhere(target, e => e.Type == NwRules.EffectSleep);

        reported ??= dealt;
        CreatureRules.RecordDamage(target, world.Get(damager), reported);
        // Direct positive HP loss enters combat before OnDamaged; ordinary weapon hits
        // retain their existing round/LastHostile handling in CombatSystem.Resolve. Scripted damage on a plot
        // creature, which loses nothing, puts it in combat too (nwserver trap_crit_ctl cplot: 6 of 6).
        if (!combat && (Current(target) < before || target.Plot && byType.Values.Any(v => v > 0)))
            world.System<CombatSystem>().RecordScriptHostility(world.Get(damager), target, world.Time, damage: true);
        // Floating numbers: the largest damage type picks the colour.
        world.Output?.Notify("hp-change", target, new { target = target.Id, source = damager, amount = -total, type = dealt.MaxBy(kv => kv.Value).Key, combat });
        var ctx = new EventContext().Set("Damager", damager).Set("TotalDamage", total).Set("DamagePower", power);
        foreach (var (type, amount) in reported) ctx.Set("Damage:" + type, amount);
        // A creature the damage killed doesn't hear OnDamaged, by weapon or by script (nwserver, parity suite
        // combat_hitdelay kill.damaged.events / skill.damaged.events); OnDeath runs instead.
        world.After(0, () => { if (target is not Creature { IsDead: true }) world.RunEvent(target, target is Creature ? "ScriptDamaged" : "OnDamaged", ctx); });
        Damaged?.Invoke(target, damager, dealt, total);

        CheckDeath(world, target, damager, before);
        return before - Current(target);
    }

    /// <summary>
    /// What of each damage type gets past the target's defences: immunity (vulnerability adds), the best resistance,
    /// then damage reduction against physical damage the power doesn't overcome. With <paramref name="consume"/>
    /// limited resistances and reductions are used up and <paramref name="breakdown"/> is filled; without, nothing
    /// changes (a preview of a hit).
    /// </summary>
    public static Dictionary<int, int> Defend(World world, GameObject target, uint damager, IReadOnlyDictionary<int, int> byType, int power, bool ignoreReductions,
        bool ranged, IReadOnlyDictionary<int, int>? unresisted, DamageBreakdown? breakdown, bool consume)
    {
        var k = EffectConstants.Of(world);
        var effects = world.System<EffectSystem>();
        var source = world.Get(damager);
        if (!consume) breakdown = null;
        var dealt = new Dictionary<int, int>();
        foreach (var (type, raw) in byType)
        {
            int free = Math.Clamp(unresisted?.GetValueOrDefault(type) ?? 0, 0, Math.Max(0, raw));
            int amount = raw - free;
            if (free > 0) dealt[type] = free;
            if (amount <= 0) continue;
            if (!ignoreReductions)
            {
                // Immunity percentage (vulnerability lowers it below zero).
                int pct = 0;
                foreach (var e in EffectQueries.Active(target, k.DamageImmunityIncrease, source))
                    if ((e.Ints[0] & type) != 0) pct += e.Ints[1];
                foreach (var e in EffectQueries.Active(target, k.DamageImmunityDecrease, source))
                    if ((e.Ints[0] & type) != 0) pct -= e.Ints[1];
                pct += ItemPropertyRules.ItemDamageImmunity(world, target, type);
                pct = Math.Clamp(pct, -100, 100);
                // Immunity takes at least 1 point (rounding down otherwise); vulnerability adds the rounded-down extra.
                int immune = pct > 0 ? Math.Min(amount, Math.Max(1, amount * pct / 100)) : amount * pct / 100;
                amount -= immune;
                if (immune > 0 && breakdown != null) breakdown.Immunity[type] = breakdown.Immunity.GetValueOrDefault(type) + immune;

                // Resistance: the best single source applies.
                Effect? bestEffect = null;
                int best = ItemPropertyRules.ItemDamageResistance(world, target, type);
                foreach (var e in EffectQueries.Active(target, k.DamageResistance, source))
                    if ((e.Ints[0] & type) != 0 && e.Ints[1] > best && (ranged || e.Ints[3] == 0)) { best = e.Ints[1]; bestEffect = e; }
                int absorbed = Math.Min(amount, best);
                if (bestEffect != null && bestEffect.Ints[2] > 0)
                {
                    absorbed = Math.Min(absorbed, bestEffect.Ints[2]);
                    if (consume)
                    {
                        bestEffect.Ints[2] -= absorbed;
                        if (bestEffect.Ints[2] <= 0) effects.Remove(target, bestEffect);
                    }
                }
                amount -= absorbed;
                if (breakdown != null) breakdown.Resistance += absorbed;
            }
            if (amount > 0) dealt[type] = dealt.GetValueOrDefault(type) + amount;
        }

        // Damage reduction soaks physical damage the hit's power doesn't overcome.
        if (!ignoreReductions)
        {
            int physical = dealt.Where(kv => (kv.Key & PhysicalTypes) != 0).Sum(kv => kv.Value);
            if (physical > 0)
            {
                Effect? bestEffect = null;
                int soak = ItemPropertyRules.ItemDamageReduction(world, target, power);
                foreach (var e in EffectQueries.Active(target, k.DamageReduction, source))
                    if (power < e.Ints[1] && e.Ints[0] > soak && (ranged || e.Ints[3] == 0)) { soak = e.Ints[0]; bestEffect = e; }
                int absorbed = Math.Min(physical, soak);
                if (bestEffect != null && bestEffect.Ints[2] > 0)
                {
                    absorbed = Math.Min(absorbed, bestEffect.Ints[2]);
                    if (consume)
                    {
                        bestEffect.Ints[2] -= absorbed;
                        if (bestEffect.Ints[2] <= 0) effects.Remove(target, bestEffect);
                    }
                }
                if (breakdown != null) breakdown.Reduction += absorbed;
                foreach (var type in dealt.Keys.Where(t => (t & PhysicalTypes) != 0).ToList())
                {
                    if (absorbed <= 0) break;
                    int take = Math.Min(absorbed, dealt[type]);
                    dealt[type] -= take;
                    absorbed -= take;
                }
            }
        }
        if (breakdown != null)
            foreach (var (type, raw) in byType)
                if (raw > 0) breakdown.Dealt[type] = breakdown.Dealt.GetValueOrDefault(type) + dealt.GetValueOrDefault(type);
        return dealt;
    }

    /// <summary>After hit points dropped from <paramref name="before"/>: dying for PCs, death for everything at its threshold.</summary>
    public static void CheckDeath(World world, GameObject target, uint killer, int before)
    {
        int hp = Current(target);
        if (hp > 0 || IsDead(target)) return;
        if (target is Creature possessed) world.System<Players.FamiliarPossession>().ReleaseControl(possessed);
        if (target is Creature { IsPC: true } pc && hp > PlayerDeathThreshold)
        {
            if (before > 0)
            {
                StartDying(world, pc);
                world.After(0, () => world.RunEvent(world.Module, "Mod_OnPlrDying", new EventContext().Set("PlayerDying", pc.Id).Set("Killer", killer)));
            }
            return;
        }
        // A blow that leaves -11 or fewer hit points ends the body as a spectacular death does: it lasts three
        // seconds, not its decay time (nwserver corpse_drops dmgover5..12: -10 lasts 5 s, -11 3 s).
        if (target is Creature { IsDead: false } overkilled && hp <= PlayerDeathThreshold - 1) overkilled.SpectacularDeath = true;
        Die(world, target, killer);
    }

    /// <summary>EffectDeath: kills outright unless plot or immortal.</summary>
    public static void Kill(World world, GameObject target, uint killer, bool spectacular = false)
    {
        if (IsDead(target) || target.Plot || target is Creature { IsImmortal: true } or Creature { Player.IsAreaLoading: true }) return;
        if (target is not (Creature or Placeable or Door)) return;
        if (target is Creature init)
        {
            CreatureRules.EnsureInit(world, init);
            world.System<Players.FamiliarPossession>().ReleaseControl(init);
        }
        if (target is Creature form && world.System<EffectSystem>().SurvivesDeath(form, PlayerDeathThreshold - 1)) return;
        if (target is Creature dying) dying.SpectacularDeath = spectacular;
        // A door keeps its hit points (nwserver, parity suite door_bashed: EK_0.now.hp).
        if (target is not Door) Set(target, Math.Min(Current(target), PlayerDeathThreshold - 1));
        Die(world, target, killer);
    }

    /// <summary>Marks the object dead and runs its death events (creature OnDeath, module OnPlayerDeath for PCs).</summary>
    public static void Die(World world, GameObject target, uint killer)
    {
        if (IsDead(target)) return;
        if (target is Creature c)
        {
            c.IsDead = true;
            world.System<Players.FamiliarPossession>().ReleaseControl(c);
            // A creature that dies has at most -11 hit points (a player bled to exactly -10 reads -11 too: dyingfx).
            c.CurrentHitPoints = Math.Min(c.CurrentHitPoints, PlayerDeathThreshold - 1);
            c.Actions.Clear();
            // Death strips effects except unyielding ones (state-changing effects go regardless).
            var k = EffectConstants.Of(world);
            // Native retains standalone beams on corpses. A linked removable buff
            // still removes its whole link, including the beam.
            world.System<EffectSystem>().RemoveOnDeath(c, e => e.Type != k.Beam
                && (e.SubType != k.Unyielding || EffectSystem.IsStateEffect(k, e)));
            // A polymorphed creature changes back as it dies.
            world.System<EffectSystem>().EndPolymorphNow(c);
            world.MarkChanged(c);
            // Native soundsets use slot 18 for the automatic death cry, without chat text.
            WorldObjects.VoiceService.Play(world, c, 18, caption: false);
            var ctx = new EventContext().Set("Killer", killer);
            world.After(0, () =>
            {
                world.RunEvent(c, "ScriptDeath", ctx);
                // Stock familiar death scripts still need their master for the
                // capped damage penalty. Native detaches the corpse afterward.
                if (c.IsDead && c.AssociateType is AssociateService.Familiar or AssociateService.AnimalCompanion or AssociateService.Summoned
                    && world.Get<Creature>(c.Master) is { } master)
                    AssociateService.Remove(world, master, c);
                if (c.IsPC)
                    world.RunEvent(world.Module, "Mod_OnPlrDeath", new EventContext().Set("PlayerDied", c.Id).Set("Killer", killer));
            });
            // Kill experience and "X killed Y" for the killer's party: worked out now (who is near the body), given after
            // the killing blow's own log lines and after OnDeath (killxp3: GetXP in the victim's OnDeath still reads the
            // old value).
            world.System<KillRewards>().Died(c, killer);
            Died?.Invoke(c, killer);
            return;
        }
        // A door breaks open and stays a doorway (docs/INTER_AREA_MOVEMENT.md, "Killed doors").
        if (target is Door door)
        {
            WorldObjects.DoorService.Break(world, door, killer);
            Died?.Invoke(target, killer);
            return;
        }
        // Placeables are destroyed after their OnDeath script.
        Died?.Invoke(target, killer);
        world.After(0, () =>
        {
            world.RunEvent(target, "OnDeath", new EventContext().Set("Killer", killer));
            world.Destroy(target);
        });
    }

    /// <summary>EffectResurrection: brings a dead creature back with 1 hit point.</summary>
    public static void Resurrect(World world, GameObject target)
    {
        if (target is not Creature { IsDead: true } c) return;
        c.IsDead = false;
        c.SpectacularDeath = false;
        c.CurrentHitPoints = Math.Max(1, Math.Min(c.CurrentHitPoints, 1));
        // A raised player can act again (dying: GetCommandable reads 1 after EffectResurrection), and its death panel
        // closes (dying: no panel after the raise).
        if (c.IsPC) c.Commandable = true;
        if (c.Player != null) c.Player.DeathPanelRespawn = false;
        world.MarkChanged(c);
    }
}

using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Combat;

/// <summary>Resolving one attack: rolls, defences, damage, side effects, bookkeeping, events and client feedback.</summary>
public sealed partial class CombatSystem
{
    int RI(string label, int fallback) => Ruleset.Int(_world, label, fallback);
    float RF(string label, float fallback) => Ruleset.Float(_world, label, fallback);
    bool HasFeat(Creature c, int feat) => feat >= 0 && CreatureRules.HasFeat(_world, c, feat, ignoreUses: true);

    /// <summary>
    /// Resolves one attack. Order: ammunition, attack roll (natural 1 misses, 20 hits), Epic Dodge,
    /// concealment (Blind-Fight rerolls), Deflect Arrows, parry (with riposte), critical threat and
    /// confirmation, bookkeeping, OnPhysicalAttacked and the damage roll at the swing; then, when the blow lands
    /// (<see cref="Impact"/>, <paramref name="impactDelay"/> seconds later), damage, special attack and on-hit
    /// effects, devastating critical, feedback and cleave / circle kick follow-ups. A round's attacks land
    /// <see cref="ImpactDelay"/> after their swing, as in the original; other callers resolve at once.
    /// </summary>
    public AttackOutcome Resolve(AttackContext a, float swing = 1f, float impactDelay = 0f)
    {
        a.Dice = Dice; // every roll of the attack comes from the system's dice
        var o = new AttackOutcome { Context = a, Swing = swing };
        var att = a.Attacker;
        var t = a.Target;
        var tc = t as Creature;
        var aState = State(att);
        var tState = State(t);
        Counters(aState);
        Counters(tState);

        if (a.Ranged && !ConsumeAmmo(a))
        {
            o.OutOfAmmo = true;
            OutOfAmmo(att);
            return o;
        }
        o.Animation = a.Animation ?? Animation(a);
        // Native grunts accompany attempts, including misses, without a fixed
        // cooldown. Measurements support approximate rates of 50% melee / 40%
        // ranged, with equal choice of three slots; see docs/VOICES.md.
        if (!att.IsDead)
        {
            int voice = VoiceDice.Roll(a.Ranged ? 15 : 6);
            if (voice <= (a.Ranged ? 6 : 3))
                WorldObjects.VoiceService.Play(_world, att, 11 + (voice - 1) % 3, caption: false);
        }

        o.AttackBonus = Rules.AttackBonus(a);
        o.ArmorClass = Rules.ArmorClass(a);
        if (IsCoupDeGraceTarget(t))
            return CoupDeGrace(o, aState, tState);
        o.Roll = Dice.Roll(20);
        bool hit = o.Roll == 20 || (o.Roll != 1 && o.Roll + o.AttackBonus >= o.ArmorClass);

        if (hit && tc != null && !a.TargetDeniedDex && !tState.EpicDodgeUsed && HasFeat(tc, CombatK.FeatEpicDodge))
        {
            tState.EpicDodgeUsed = true;
            o.EpicDodged = true;
            o.Feedback.Add("Epic Dodge");
            hit = false;
        }
        if (hit)
        {
            int pct = Rules.Concealment(a);
            if (pct > 0)
            {
                o.Concealment = pct;
                int r = Dice.Roll(100);
                if (r <= pct && HasFeat(att, CombatK.FeatBlindFight)) r = Dice.Roll(100);
                if (r <= pct) { o.Concealed = true; hit = false; o.Feedback.Add($"Target Concealed: {pct}%"); }
            }
        }
        if (hit && a.Ranged && tc != null && TryDeflect(a, tState)) { o.Deflected = true; hit = false; o.Feedback.Add("Deflect Arrows"); }
        bool riposte = false;
        if (hit && !a.Ranged && tc != null && TryParry(o, tState, out riposte)) { o.Parried = true; hit = false; o.Feedback.Add("Parried"); }

        if (hit)
        {
            var (threat, _) = Rules.Critical(a);
            if (o.Roll >= threat)
            {
                o.Threat = true;
                // The confirmation roll only has to reach the AC: a natural 1 doesn't fail it in the original.
                o.ConfirmRoll = Dice.Roll(20);
                bool confirmed = o.ConfirmRoll == 20 || o.ConfirmRoll + o.AttackBonus >= o.ArmorClass;
                // A target immune to critical hits still sees the threat rolled, then "<target> : Immune to Critical
                // Hits." (visual scene difficultypc). Below D&D Hardcore a player isn't critically hit: the threat is
                // rolled and shown, and the blow is an ordinary hit (visual scenes difficulty and difficultyhits).
                if (Rules.IsCriticalImmune(a)) o.CriticalImmune = confirmed && t is Creature;
                else o.Critical = confirmed && !Difficulty.SparesCritical(_world, att, t);
            }
        }
        o.Hit = hit;
        if (a.Kind == AttackKind.Opportunity) o.Feedback.Insert(0, "Attack Of Opportunity");
        else if (a.Kind == AttackKind.Cleave) o.Feedback.Insert(0, "Cleave");
        else if (a.Kind == AttackKind.GreatCleave) o.Feedback.Insert(0, "Great Cleave");
        else if (a.Kind == AttackKind.CircleKick) o.Feedback.Insert(0, "Circle Kick");
        else if (a.Kind == AttackKind.Riposte) o.Feedback.Insert(0, "Riposte");
        else if (a.Kind == AttackKind.Whirlwind) o.Feedback.Insert(0, "Whirlwind Attack");
        // A sneak (or death) attack is announced whenever the situation allows one, hit or miss, as in the original.
        else if (Creature is { } cr && cr.SneakAttackApplies(a, out _, out int deathDice))
        {
            o.Feedback.Insert(0, deathDice > 0 ? "Death Attack" : "Sneak Attack");
            o.SneakSituation = true;
        }
        // A sneak attack on a target immune to it is announced as such when the blow hits (stealthself: a plot dummy).
        if (hit && Creature is { } sr && sr.SneakAttackBlocked(a)) o.SneakImmune = true;
        if (SpecialName(a) is { } special) o.Feedback.Insert(0, special);

        // Bookkeeping before damage so OnDamaged/OnDeath scripts see the attacker.
        double now = _world.Time;
        aState.LastAttack = o;
        aState.LastWeapon = a.Weapon?.Id ?? GameObject.InvalidId;
        aState.LastAttackType = a.SpecialAttack != 0 ? a.SpecialAttack : ModeAttackType(att);
        aState.LastAttackMode = CombatMode(att);
        aState.LastHostile = now;
        tState.LastAttacker = att.Id;
        tState.LastHostile = now;
        Society.SocietyService.SetLastHostileActor(_world, t, att);
        if (!att.InCombat) { att.InCombat = true; _world.MarkChanged(att); }
        if (tc != null && !tc.InCombat) { tc.InCombat = true; _world.MarkChanged(tc); }
        BreakConcealingEffects(att);
        // OnPhysicalAttacked comes with the swing, before any damage lands (so it fires for a killing blow too).
        TellAttacked(att, t);

        if (hit)
        {
            if (o.Critical) o.Feedback.Add("Critical Hit");
            if (!DealsNoDamage(a))
            {
                o.Damage = Rules.Damage(a, o.Critical);
                if (o.Damage.Sneak > 0 && !o.SneakSituation) o.Feedback.Add(o.Damage.DeathAttack ? "Death Attack" : "Sneak Attack");
            }
        }
        if (impactDelay > 0)
        {
            // The swing is seen now (animation, sounds); the blow lands later.
            o.Pending = true;
            Report(o);
            _world.After(impactDelay, () => Impact(o, hit, riposte));
            return o;
        }
        Impact(o, hit, riposte);
        return o;
    }

    /// <summary>
    /// Seconds from a round attack's swing to its damage, measured on nwserver (parity suite combat_hitdelay: the
    /// target's hit points drop and a killing blow's OnDeath runs about this long after OnPhysicalAttacked).
    /// </summary>
    public const float ImpactDelay = 0.55f;

    /// <summary>A ranged hit lands a little later, more so from afar (combat_hitdelay: about 0.68 s at 3 and 8 m, 0.85 s at 16 m).</summary>
    public static float RangedImpactDelay(float distance) => 0.6f + 0.015f * Math.Clamp(distance, 0f, 40f);

    /// <summary>The blow lands: damage and what follows from it (<see cref="Resolve"/>).</summary>
    void Impact(AttackOutcome o, bool hit, bool riposte)
    {
        var a = o.Context;
        var att = a.Attacker;
        var t = a.Target;
        var tc = t as Creature;
        if (hit)
        {
            if (o.Damage != null)
            {
                if (tc != null) DefensiveRoll(o, tc);
                ApplyHardness(t, o.Damage);
                o.Breakdown = new DamageBreakdown();
                o.Dealt = HitPoints.Damage(_world, t, att.Id, o.Damage.ByType, o.Damage.Power, ignoreReductions: false, ranged: a.Ranged, combat: true,
                    unresisted: o.Damage.Unresisted, breakdown: o.Breakdown);
                o.Killed = HitPoints.IsDead(t);
                // A blow that lands puts the attacker back in combat, even after ClearAllActions(TRUE) took it out
                // between the swing and the blow (nwserver round_idle cut: its idle OnEndCombatRound came 5.85-5.97 s
                // after the blow; none when the blow missed).
                if (o.Dealt > 0 && !att.IsDead)
                {
                    State(att).LastHostile = _world.Time;
                    if (!att.InCombat) { att.InCombat = true; _world.MarkChanged(att); }
                }
            }
            if (!o.Killed && tc != null)
            {
                SpecialAttackHit(o);
                if (o.Critical) DevastatingCritical(o);
            }
            if (!HitPoints.IsDead(t)) OnHitProperties(o);
        }
        else if (a.SpecialAttack != 0 || a.SpecialFeat >= 0) SpecialAttackMissed(o);

        // Bounded presentation approximation: native nonfatal melee controls voiced
        // 100/152 damaging hits (Wilson 95% 57.94-72.86%). Use 65%, not an asserted
        // exact RNG constant; the three measured pain slots are equally selected.
        // Scripted damage and absorbed hits were silent. Ranged/fatal cases remain
        // separate measurements; see docs/VOICES.md. Never consume combat dice.
        if (!a.Ranged && tc is { IsDead: false, IsDestroyed: false } && o.Dealt > 0
            && VoiceDice.Roll(100) <= 65)
            WorldObjects.VoiceService.Play(_world, tc, 13 + VoiceDice.Roll(3), caption: false);

        o.Landed = true;
        Report(o);
        AttackResolved?.Invoke(o);

        if (riposte && tc != null && !tc.IsDead && !att.IsDead) Riposte(tc, att);
        if (o.Killed) { if (!ScriptedCleave) Cleave(o); }
        else if (hit && a.Unarmed && a.Kind is AttackKind.Normal) CircleKick(o);
    }

    /// <summary>
    /// A coup de grace, as measured on nwserver (parity suite condition_actions, helpless.*): any attack, melee or
    /// ranged, on a creature that is asleep (EffectSleep) or held (EffectParalyze) kills it outright with no roll,
    /// leaving it at -11 hit points. Critical-hit and death immunity don't help, no damage is dealt (OnDamaged doesn't
    /// run) but OnPhysicalAttacked does. Stunned, petrified, cutscene-paralysed, knocked down, entangled and dazed
    /// creatures are attacked normally.
    /// </summary>
    public static bool IsCoupDeGraceTarget(GameObject t) =>
        t is Creature { IsDead: false } && t.Effects.Any(e => e.Type is NwRules.EffectSleep or NwRules.EffectParalyze);

    AttackOutcome CoupDeGrace(AttackOutcome o, CombatState aState, CombatState tState)
    {
        var a = o.Context;
        var att = a.Attacker;
        var t = a.Target;
        o.Hit = true;
        o.CoupDeGrace = true;
        // The log shows the roll as a natural 20.
        o.Roll = 20;
        o.Feedback.Add(CoupDeGraceText);
        double now = _world.Time;
        aState.LastAttack = o;
        aState.LastWeapon = a.Weapon?.Id ?? GameObject.InvalidId;
        aState.LastAttackType = a.SpecialAttack != 0 ? a.SpecialAttack : ModeAttackType(att);
        aState.LastAttackMode = CombatMode(att);
        aState.LastHostile = now;
        tState.LastAttacker = att.Id;
        tState.LastHostile = now;
        Society.SocietyService.SetLastHostileActor(_world, t, att);
        if (!att.InCombat) { att.InCombat = true; _world.MarkChanged(att); }
        if (t is Creature { InCombat: false } tc) { tc.InCombat = true; _world.MarkChanged(tc); }
        BreakConcealingEffects(att);
        TellAttacked(att, t);
        // The original says who was killed (after the experience it gives) before it prints the attack.
        HitPoints.Kill(_world, t, att.Id);
        _world.System<KillRewards>().Flush(t);
        o.Killed = HitPoints.IsDead(t);
        // Floated over the attacker for its player, not written to the chat window (parity/visual scene coupdegrace).
        if (att is { IsPC: true } && a.ShowFeedback) _world.Output?.Notify("float-text", att, new { text = "*" + CoupDeGraceText + "*" }, att);
        Report(o);
        AttackResolved?.Invoke(o);
        return o;
    }

    /// <summary>dialog.tlk 61630 (floats over the attacker as "*Coup De Grace!*"), 61629 (the log line's label) and 61628.</summary>
    const string CoupDeGraceText = "Coup De Grace!", CoupDeGraceLabel = "Coup De Grace", AutomaticHit = "automatic hit";

    /// <summary>OnPhysicalAttacked (creatures) / OnMeleeAttacked (doors, placeables) for every attack. Fighting back is left to scripts.</summary>
    void TellAttacked(Creature att, GameObject t)
    {
        if (t.IsDestroyed || HitPoints.IsDead(t)) return;
        _world.RunEvent(t, t is Creature ? "ScriptAttacked" : "OnMeleeAttacked", new EventContext().Set("Attacker", att.Id));
        // A master's associates hear ASSOCIATE_COMMAND_MASTERUNDERATTACK as its attacker swings (nwserver ai_associate:
        // the stock henchman scripts get it in OnConversation for an NPC master too).
        if (t is Creature master && !master.IsDead)
            foreach (var associate in Society.AssociateService.AllAssociates(_world, master).ToList())
                if (associate != att && !associate.IsDead) Society.AssociateService.IssueCommand(_world, master, associate, AssociateCommandMasterUnderAttack);
    }

    const int AssociateCommandMasterUnderAttack = -11;

    /// <summary>
    /// Defensive Roll (feat 223, once a day): a weapon blow that would bring the creature to 0 hit points or below
    /// allows a Reflex save against the damage; made, the blow deals half. Not when it is flat-footed, and not against
    /// scripted damage (nwserver, parity suite combat_extras: rollers fighting someone else lived through the blow,
    /// passive ones and ones hit by EffectDamage died).
    /// </summary>
    void DefensiveRoll(AttackOutcome o, Creature t)
    {
        var d = o.Damage!;
        if (o.Context.TargetDeniedDex || d.Total < HitPoints.Current(t) + CreatureRules.TemporaryHitPoints(t)) return;
        if (!CreatureRules.HasFeat(_world, t, CombatK.FeatDefensiveRoll)) return;
        CreatureRules.AdjustFeatUses(_world, t, CombatK.FeatDefensiveRoll, -1);
        if (SavingThrow(t, NwRules.SaveReflex, d.Total, NwRules.SaveTypeAll, o.Context.Attacker) == 0) return;
        foreach (var k in d.ByType.Keys.ToList()) d.ByType[k] /= 2;
        foreach (var k in d.Unresisted.Keys.ToList()) d.Unresisted[k] /= 2;
        o.Feedback.Add("Defensive Roll");
    }

    /// <summary>Disarm attacks deal no damage.</summary>
    static bool DealsNoDamage(AttackContext a) => a.SpecialAttack is CombatK.SpecialDisarm or CombatK.SpecialImprovedDisarm;

    /// <summary>Attacking ends stealth mode (with the swing, measured: stealth_modes), normal invisibility, sanctuary and etherealness.</summary>
    void BreakConcealingEffects(Creature att)
    {
        _world.System<Skills.SkillSystem>().BreakStealth(att);
        if (!att.Effects.Any(IsBroken)) return;
        _world.System<EffectSystem>().RemoveWhere(att, IsBroken);
        static bool IsBroken(Effect e) =>
            (e.Type == CombatK.EffectInvisibility && (e.Ints[0] == CombatK.InvisibilityNormal || e.Ints[0] == 0))
            || e.Type == CombatK.EffectSanctuary || e.Type == CombatK.EffectEthereal;
    }

    /// <summary>Doors and placeables subtract their hardness from each hit.</summary>
    static void ApplyHardness(GameObject t, DamageRoll d)
    {
        int hardness = t switch
        {
            Placeable p => (int)p.Template.GetInt("Hardness"),
            Door door => (int)door.Template.GetInt("Hardness"),
            _ => 0,
        };
        if (hardness <= 0) return;
        foreach (var k in d.ByType.Keys.OrderBy(k => (k & CombatK.PhysicalDamage) != 0 ? 0 : 1).ToList())
        {
            if (hardness <= 0) break;
            int take = Math.Min(hardness, d.ByType[k]);
            d.ByType[k] -= take;
            hardness -= take;
        }
    }

    /// <summary>Deflect Arrows: once per round a creature that sees the shot coming may deflect a ranged hit (Reflex save).</summary>
    bool TryDeflect(AttackContext a, CombatState tState)
    {
        if (a.Target is not Creature tc || a.TargetDeniedDex || !HasFeat(tc, CombatK.FeatDeflectArrows) || !CanAttack(tc)) return false;
        if (tState.Deflections > 0) return false;
        tState.Deflections++;
        return SavingThrow(tc, NwRules.SaveReflex, RI("DEFLECT_ARROWS_DC", 20), NwRules.SaveTypeAll, a.Attacker) != 0;
    }

    /// <summary>
    /// Parry mode: up to one parry per attack the defender would have this round; Parry check against the attack
    /// total. Beating it by PARRY_RIPOSTE_DIFFERENCE earns an immediate riposte.
    /// </summary>
    bool TryParry(AttackOutcome o, CombatState tState, out bool riposte)
    {
        riposte = false;
        var a = o.Context;
        if (a.Target is not Creature tc || !tc.ActionModes.Contains(CombatK.ModeParry) || a.TargetDeniedDex || !CanAttack(tc)) return false;
        if (tState.Parries >= Math.Max(1, CreatureRules.AttacksPerRound(_world, tc)) && !Nwnx.Plugins.NwnxTweaks.Of(_world).ParryAllAttacks) return false;
        tState.Parries++;
        int parry = Dice.Roll(20) + CreatureRules.SkillRank(_world, tc, CombatK.SkillParry);
        if (HasFeat(tc, CombatK.FeatImprovedParry)) parry += RI("IMPROVED_PARRY_MODIFIER", 4);
        int total = o.Roll + o.AttackBonus;
        if (parry < total) return false;
        riposte = parry - total >= RI("PARRY_RIPOSTE_DIFFERENCE", 10);
        return true;
    }

    void Riposte(Creature defender, Creature attacker)
    {
        if (Distance2(defender, attacker) > MeleeReach(defender, attacker) || Creature is not { } r) return;
        var main = r.Weapons(defender).Main;
        if (main != null && r.Stats(main).Ranged) return;
        var a = Rules.Context(defender, attacker, main, false, Dice);
        a.Kind = AttackKind.Riposte;
        Resolve(a);
    }

    /// <summary>Cleave: a melee kill earns one free attack on another adjacent enemy per round (Great Cleave: no limit).</summary>
    void Cleave(AttackOutcome o)
    {
        var a = o.Context;
        if (a.Ranged) return;
        var att = a.Attacker;
        bool great = HasFeat(att, CombatK.FeatGreatCleave);
        if (!great && !HasFeat(att, CombatK.FeatCleave)) return;
        var s = State(att);
        if (!great && s.Cleaves >= RI("NUM_CLEAVE_ATTACKS", 1)) return;
        var next = NearestEnemy(att, a.Target, (c, t) => MeleeReach(c, t));
        if (next == null) return;
        s.Cleaves++;
        var ca = Rules.Context(att, next, a.Weapon, a.OffHand, Dice);
        ca.Penalty = a.Penalty;
        ca.Kind = great ? AttackKind.GreatCleave : AttackKind.Cleave;
        Resolve(ca, o.Swing);
    }

    /// <summary>Circle Kick: an unarmed hit earns one extra unarmed attack per round on another adjacent enemy.</summary>
    void CircleKick(AttackOutcome o)
    {
        var a = o.Context;
        var att = a.Attacker;
        if (!HasFeat(att, CombatK.FeatCircleKick)) return;
        var s = State(att);
        if (s.CircleKicks >= RI("NUM_CIRCLE_KICK_ATTACKS", 1)) return;
        var next = NearestEnemy(att, a.Target, (c, t) => MeleeReach(c, t));
        if (next == null) return;
        s.CircleKicks++;
        var ka = Rules.Context(att, next, null, false, Dice);
        ka.Kind = AttackKind.CircleKick;
        Resolve(ka, o.Swing);
    }

    /// <summary>The closest living enemy of <paramref name="c"/> within reach, other than <paramref name="except"/>.</summary>
    Creature? NearestEnemy(Creature c, GameObject? except, Func<Creature, GameObject, float> reach)
    {
        if (c.Area == null) return null;
        Creature? best = null;
        float bestDist = float.MaxValue;
        foreach (var obj in c.Area.Objects)
        {
            if (obj is not Creature o || o == c || o == except || o.IsDead || o.IsDestroyed) continue;
            if (!_world.Factions.IsEnemyOf(c, o)) continue;
            float d = Distance2(c, o);
            if (d <= reach(c, o) && d < bestDist) { best = o; bestDist = d; }
        }
        return best;
    }

    /// <summary>Devastating Critical: a critical hit with the weapon forces a Fortitude save (10 + half HD + Str) or death.</summary>
    void DevastatingCritical(AttackOutcome o)
    {
        var a = o.Context;
        if (Creature is not { } r || a.Target is not Creature tc) return;
        var s = r.Stats(a.Weapon);
        if (!HasFeat(a.Attacker, s.Devastating)) return;
        int dc = RI("DEVASTATING_CRITICAL_BASE_DC", 10) + a.Attacker.TotalLevel / 2 + CreatureRules.AbilityModifier(_world, a.Attacker, NwRules.AbilityStr);
        if (SavingThrow(tc, NwRules.SaveFort, dc, NwRules.SaveTypeAll, a.Attacker) == 0)
        {
            o.Feedback.Add("Devastating Critical");
            ApplyEffect(MakeEffect("DEATH", a.Attacker, 1, 1), tc, 0);
            o.Killed = HitPoints.IsDead(tc);
        }
    }

    /// <summary>A deterministic saving throw on the combat dice: 0 failed, 1 succeeded, 2 immune.</summary>
    public int SavingThrow(GameObject target, int save, int dc, int saveType, GameObject? versus)
    {
        if (target is Creature c)
        {
            int imm = CreatureRules.ImmunityForSaveType(saveType);
            if (imm > 0 && CreatureRules.IsImmune(_world, c, imm, versus)) return 2;
        }
        int bonus = CreatureRules.SavingThrow(_world, target, save, saveType);
        int roll = Dice.Roll(20);
        return roll == 20 || (roll != 1 && roll + bonus >= dc) ? 1 : 0;
    }

    /// <summary>A skill check on the combat dice (d20 + rank against a DC).</summary>
    public bool SkillCheck(Creature c, int skill, int dc) => Dice.Roll(20) + Math.Max(0, CreatureRules.SkillRank(_world, c, skill)) >= dc;

    Effect MakeEffect(string type, GameObject creator, params int[] ints)
    {
        var k = EffectConstants.Of(_world);
        var e = new Effect { Type = k["EFFECT_TYPE_" + type], SubType = k.Extraordinary, Creator = creator.Id };
        for (int i = 0; i < ints.Length && i < e.Ints.Length; i++) e.Ints[i] = ints[i];
        return e;
    }

    /// <summary>Applies an effect: instant when <paramref name="seconds"/> is 0, permanent when negative, else temporary.</summary>
    void ApplyEffect(Effect e, GameObject target, float seconds)
    {
        var k = EffectConstants.Of(_world);
        int duration = seconds == 0 ? k.Instant : seconds < 0 ? k.Permanent : k.Temporary;
        _world.System<EffectSystem>().Apply(e, duration, target, Math.Max(0, seconds));
    }

    // ------------------------------------------------------------------ modes

    /// <summary>COMBAT_MODE_* of the creature's current action mode (GetLastAttackMode).</summary>
    public static int CombatMode(Creature c)
    {
        var m = c.ActionModes;
        if (m.Contains(CombatK.ModeParry)) return CombatK.CombatModeParry;
        if (m.Contains(CombatK.ModeImprovedPowerAttack)) return CombatK.CombatModeImprovedPowerAttack;
        if (m.Contains(CombatK.ModePowerAttack)) return CombatK.CombatModePowerAttack;
        if (m.Contains(CombatK.ModeFlurry)) return CombatK.CombatModeFlurry;
        if (m.Contains(CombatK.ModeRapidShot)) return CombatK.CombatModeRapidShot;
        if (m.Contains(CombatK.ModeImprovedExpertise)) return CombatK.CombatModeImprovedExpertise;
        if (m.Contains(CombatK.ModeExpertise)) return CombatK.CombatModeExpertise;
        if (m.Contains(CombatK.ModeDefensiveCast)) return CombatK.CombatModeDefensiveCasting;
        if (m.Contains(CombatK.ModeDirtyFighting)) return CombatK.CombatModeDirtyFighting;
        return CombatK.CombatModeNone;
    }

    int ModeAttackType(Creature c)
    {
        if (Creature is not { } r) return 0;
        var main = r.Weapons(c).Main;
        if (r.FlurryActive(c, main)) return CombatK.SpecialFlurry;
        if (r.RapidShotActive(c, main)) return CombatK.SpecialRapidShot;
        return 0;
    }

    // ------------------------------------------------------------------ feedback

    string Name(GameObject o)
    {
        var n = o.DisplayName(_world.Resources.Talk);
        return n.Length > 0 ? n : o.Tag;
    }

    const string CannotDamage = "You cannot damage your target with your currently equipped weapon!";
    /// <summary>The original's lines for a confirmed threat or a sneak attack on a target immune to it.</summary>
    const string ImmuneCriticals = "Immune to Critical Hits.", ImmuneSneak = "Immune to Sneak Attacks.";

    internal static string DamageTypeName(int bit) => bit switch
    {
        1 or 2 or 4 => "Physical",
        8 => "Magical",
        16 => "Acid",
        32 => "Cold",
        64 => "Divine",
        128 => "Electrical",
        256 => "Fire",
        512 => "Negative",
        1024 => "Positive",
        2048 => "Sonic",
        _ => "Physical",
    };

    /// <summary>The combat log lines for an attack, as the original prints them.</summary>
    public List<string> LogLines(AttackOutcome o)
    {
        var a = o.Context;
        var lines = new List<string>();
        if (o.OutOfAmmo) return lines;
        if (o.EpicDodged)
        {
            lines.Add($"{Name(a.Target)} : Epic Dodge : Attack evaded");
            return lines;
        }
        if (o.CoupDeGrace)
        {
            // parity/visual scene coupdegrace: "Coup De Grace : Aluvian Darkstar attacks Bandit : *automatic hit* : (20 + 0 = 20)".
            lines.Add($"{CoupDeGraceLabel} : {Name(a.Attacker)} attacks {Name(a.Target)} : *{AutomaticHit}* : ({o.Roll} + {o.AttackBonus} = {o.Roll + o.AttackBonus})");
            return lines;
        }
        string prefix = o.Feedback.Count > 0 && (a.Kind != AttackKind.Normal || a.SpecialAttack != 0 || a.SpecialFeat >= 0 || o.SneakSituation) ? o.Feedback[0] + " : " : "";
        string result = o.Concealed ? $"*target concealed: {o.Concealment}%*"
            : o.Parried ? "*parried*"
            : o.Deflected ? "*deflected*"
            : o.EpicDodged ? "*epic dodge*"
            : o.Critical ? "*critical hit*"
            : o.Hit ? "*hit*" : "*miss*";
        // A negative attack bonus prints as a subtraction ("(9 - 5 = 4)": visual scene difficultypc).
        string bonus = o.AttackBonus < 0 ? $"- {-o.AttackBonus}" : $"+ {o.AttackBonus}";
        string roll = $"({o.Roll} {bonus} = {o.Roll + o.AttackBonus}";
        if (o.Threat) roll += $" : Threat Roll: {o.ConfirmRoll} {bonus} = {o.ConfirmRoll + o.AttackBonus}";
        roll += ")";
        lines.Add($"{prefix}{Name(a.Attacker)} attacks {Name(a.Target)} : {result} : {roll}");
        if (o.Damage == null || o.Breakdown is not { } b) return lines;
        string who = Name(a.Attacker), whom = Name(a.Target);
        // As the original prints it: the damage that got through (every type the hit carried, 0 where it was all
        // taken), then what the target's defences took; a plot target takes 0 physical.
        if (b.Plot)
        {
            lines.Add($"{who} damages {whom}: 0 (0 Physical)");
            if (o.CriticalImmune) lines.Add($"{whom} : {ImmuneCriticals}");
            if (o.SneakImmune) lines.Add($"{whom} : {ImmuneSneak}");
            lines.Add(CannotDamage);
            return lines;
        }
        int total = b.Dealt.Values.Sum();
        if (total > 0)
        {
            var parts = b.Dealt.OrderBy(kv => (kv.Key & CombatK.PhysicalDamage) != 0 ? 0 : kv.Key)
                .GroupBy(kv => DamageTypeName(kv.Key)).Select(g => $"{g.Sum(kv => kv.Value)} {g.Key}");
            lines.Add($"{who} damages {whom}: {total} ({string.Join(" ", parts)})");
        }
        if (o.CriticalImmune) lines.Add($"{whom} : {ImmuneCriticals}");
        if (o.SneakImmune) lines.Add($"{whom} : {ImmuneSneak}");
        if (b.Reduction > 0) lines.Add($"{whom} : Damage Reduction absorbs {b.Reduction} damage");
        if (b.Resistance > 0) lines.Add($"{whom} : Damage Resistance absorbs {b.Resistance} damage");
        foreach (var g in b.Immunity.GroupBy(kv => DamageTypeName(kv.Key)))
            lines.Add($"{whom} : Damage Immunity absorbs {g.Sum(kv => kv.Value)} point(s) of {g.Key}");
        if (total <= 0) lines.Add(CannotDamage);
        return lines;
    }

    /// <summary>
    /// Tells clients about an attack: the <c>attack</c> notify (swing animation, sounds, outcome) and the combat log
    /// lines. An attack whose blow lands later sends <c>attack</c> at the swing (marked pending, without damage or
    /// log lines) and <c>attack-impact</c>, with the same fields and the log, when it lands.
    /// </summary>
    void Report(AttackOutcome o)
    {
        var a = o.Context;
        bool swingOnly = o.Pending && !o.Landed;
        var log = swingOnly ? new List<string>() : LogLines(o);
        _world.Output?.Notify(o.Pending && o.Landed ? "attack-impact" : "attack", a.Attacker, new
        {
            pending = swingOnly,
            attacker = a.Attacker.Id, target = a.Target.Id, hit = o.Hit, critical = o.Critical, roll = o.Roll,
            ab = o.AttackBonus, ac = o.ArmorClass, damage = o.Damage?.Total ?? 0, ranged = a.Ranged, animation = o.Animation,
            dealt = o.Dealt,
            damageByType = o.Damage?.ByType.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key.ToString(), kv => kv.Value) ?? new Dictionary<string, int>(),
            power = o.Damage?.Power ?? 0,
            threat = o.Threat, confirmRoll = o.ConfirmRoll,
            concealment = o.Concealment, concealed = o.Concealed,
            parried = o.Parried, deflected = o.Deflected, epicDodge = o.EpicDodged,
            sneak = o.Damage?.Sneak ?? 0, deathAttack = o.Damage?.DeathAttack ?? false,
            killed = o.Killed, coupDeGrace = o.CoupDeGrace, offHand = a.OffHand, weapon = a.Weapon?.Id ?? GameObject.InvalidId,
            kind = a.Kind.ToString().ToLowerInvariant(), attackType = a.SpecialAttack, swing = o.Swing,
            feedback = o.Feedback, log,
            sounds = AttackSounds(o),
        });
        if (!a.ShowFeedback || swingOnly) return;
        // The players involved see the lines, and so does every other player near the target (a party member, a
        // bystander, the master of an associate that attacks or is attacked).
        var attackerPlayer = Society.AssociateService.PlayerMaster(_world, a.Attacker);
        foreach (var who in Society.AssociateService.AttackFeedbackPlayers(_world, a.Attacker, a.Target))
            foreach (var line in log)
                if (line != CannotDamage || who == attackerPlayer) _world.Output?.SendMessage(who, line);
    }

    /// <summary>
    /// Swing animation (<see cref="WeaponStances"/>): ranged shots and throws; for a non-humanoid creature model
    /// its creature set's ccloseh or ca1 swing by the distance to the target; else the stance's slashes and stabs
    /// weighted by baseitems %AnimSlash*, or the off-hand slash.
    /// </summary>
    string Animation(AttackContext a)
    {
        if (a.Kind == AttackKind.Whirlwind) return "whirlwind";
        var stance = StanceOf(a.Attacker);
        if (a.Ranged) return stance.Shot.Length > 0 ? stance.Shot : a.Thrown ? "throwr" : "bowshot";
        var baseItems = _world.Resources.TwoDA("baseitems");
        int roll = Random.Shared.Next(1 << 20);
        var app = _world.Resources.TwoDA("appearance");
        int row = a.Attacker.AppearanceType;
        if (WeaponStances.CreatureSet(app.Get(row, "MODELTYPE")))
            return WeaponStances.CreatureSwing(baseItems, a.Weapon?.BaseItem ?? -1, roll, Distance2(a.Attacker, a.Target),
                app.GetFloat(row, "PREFATCKDIST", 1.0f));
        if (a.OffHand) return stance.Prefix + "slasho";
        return WeaponStances.Swing(stance, baseItems, a.Weapon?.BaseItem ?? -1, roll);
    }

    /// <summary>The stance a creature's held items give it (for creatures, from their equipped hands).</summary>
    public WeaponStances.Stance StanceOf(Creature c)
    {
        int Base(int slot) => c.Equipment.GetValueOrDefault(slot) is { } item ? item.BaseItem : -1;
        return WeaponStances.For(_world.Resources.TwoDA("baseitems"), c.Size, Base(EquipSlot.RightHand), Base(EquipSlot.LeftHand));
    }
}

using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Skills;

/// <summary>
/// Search (trap detection), Disable Trap (examine, flag, disarm, recover) and Set Trap, as measured on the original
/// (parity suites trap_disarm, trap_detect, trap_set; visual scenes traprules, traplook; docs/TRAPS.md).
/// <para>
/// Search, players and their associates: each round, d20 + Search (half when not in detect mode, full with Keen Sense)
/// against the trap's detect DC, within reach of the trap; only rogues find traps above DC 35
/// (MAX_NON_ROGUE_DETECT_LEVEL). What one of the party finds, the player and its associates all know (traprules: the
/// player and the henchman learned D5 and D9 at the same moment). Other NPCs search too (<see cref="NpcSearch"/>).
/// </para>
/// <para>
/// Disable Trap: the subskill moves the disarm DC (examine -7, flag -5, recover +10, disarm 0); 5 ranks of Set Trap give
/// +2; out of combat the check takes 20, and a taken 20 that can't reach the DC is "*success not possible*" and never
/// sets the trap off; a rolled disarm or recover that fails by 11 or more is a "*critical failure*" that sets it off.
/// Non-rogues can't disarm a trap above DC 35 (MAX_NON_ROGUE_DISARM_LEVEL; 35 itself works; recovering is allowed). Each attempt
/// takes 4.5 s at the trap (ruleset SKILL_*_TRAP_DURATION). Disarming, recovering and setting give no experience.
/// </para>
/// <para>
/// Set Trap uses a trap kit (ActionUseItemOnObject / AtLocation; ActionUseSkill(SKILL_SET_TRAP) does nothing): a check
/// against traps.2da SetDC (taking 20 out of combat, +2 with 5 ranks of Disable Trap). The trap's DCs are traps.2da
/// DetectDCMod and DisarmDCMod plus the setter's Set Trap bonus (no roll); a trap on the ground is a 4 m square at the
/// spot, of the setter's faction, seen by the setter's party though GetTrapDetectedBy stays FALSE. The kit is used up
/// only when the trap is set.
/// </para>
/// </summary>
public sealed partial class SkillSystem
{
    /// <summary>How far passive and active (detect mode) searching reaches, in metres from the trap (a trigger's edge).</summary>
    /// <remarks>Active: a player in detect mode found a trap whose edge was 5.5 m away but not one 7 m away (traplook).
    /// Passive was not measured: the original's players stay in detect mode (SetActionMode off left it on).</remarks>
    public float PassiveSearchRange = 5f, ActiveSearchRange = 6f;

    readonly Dictionary<uint, double> _nextSearch = new();

    public const int ExamineDcModifier = -7, FlagDcModifier = -5, RecoverDcModifier = 10;

    /// <summary>The examine window's text for a trap (the original's, not in dialog.tlk).</summary>
    public const string TrapExamineText = "This trap is a complicated mass of cogs and gears.";

    // ------------------------------------------------------------------ search

    void StepSearch()
    {
        double now = _world.Time;
        foreach (var area in _world.Areas)
        {
            List<GameObject>? traps = null;
            foreach (var o in area.Objects)
                if (!o.IsDestroyed && o is ITrappable { IsTrapped: true, Trap.Detectable: true, Trap.Flagged: false }) (traps ??= new()).Add(o);
            if (traps == null) continue;
            foreach (var o in area.Objects.ToArray())
            {
                if (o is not Creature c || c.IsDestroyed || c.IsDead) continue;
                if (!TrapSystem.IsPlayerSide(_world, c))
                {
                    NpcSearch(c, traps, now);
                    continue;
                }
                if (_nextSearch.TryGetValue(c.Id, out double next) && now < next) continue;
                _nextSearch[c.Id] = now + RoundSeconds;
                float range = ActiveDetect(_world, c) ? ActiveSearchRange : PassiveSearchRange;
                foreach (var trap in traps)
                {
                    if (trap.IsDestroyed || ((ITrappable)trap).Trap.DetectedBy.Contains(c.Id)) continue;
                    if (DistanceTo(trap, c.Position) > range) continue;
                    if (SearchCheck(c, trap)) Detected(c, trap);
                }
            }
        }
    }

    /// <summary>
    /// An NPC's search, measured on nwserver by parity suite trap_rules_npc (with Prelude's faction table): in detect
    /// mode (or with Keen Sense) every 3 s within 6.66 m with a d20, otherwise every 6 s within 3.33 m with a d10, the
    /// full Search skill either way; only traps of another faction that rates the searcher below 90; non-rogues never
    /// above DC 35. Standing still is not detect mode here: idle NPCs found traps 3 m away but not 5 m, in detect mode
    /// 5 m but not 7 m, and walkers 2.5 m but not 5 m off.
    /// </summary>
    void NpcSearch(Creature c, List<GameObject> traps, double now)
    {
        bool active = ActiveDetect(_world, c);
        if (_nextSearch.TryGetValue(c.Id, out double next) && now < next) return;
        _nextSearch[c.Id] = now + (active ? NpcActiveSearchInterval : NpcPassiveSearchInterval);
        float range = active ? NpcActiveSearchRange : NpcPassiveSearchRange;
        int faction = _world.Factions.FactionOf(c) ?? c.Faction;
        foreach (var trap in traps)
        {
            var data = ((ITrappable)trap).Trap;
            // The "same faction" test uses the trap's faction; the "friendly" test a trapped door's or placeable's own
            // faction (trap_rules_npc: a hostile searcher never found a defender trap on a chest of the hostile
            // faction, a commoner did; trap_crit_ctl: a commoner never found a commoner trap on a hostile chest, a
            // merchant did).
            int ownFaction = trap is Trigger ? data.Faction : _world.Factions.FactionOf(trap) ?? data.Faction;
            if (trap.IsDestroyed || data.DetectedBy.Contains(c.Id) || data.Faction == faction) continue;
            if (_world.Factions.FactionReputation(ownFaction, faction) >= FactionTable.FriendFrom) continue;
            if (DistanceTo(trap, c.Position) > range) continue;
            int dc = ((ITrappable)trap).TrapDetectDCValue;
            if (dc > Rule("MAX_NON_ROGUE_DETECT_LEVEL", 35) && CreatureRules.ClassLevel(c, SkillIds.ClassRogue) == 0) continue;
            if (Dice(active ? 20 : 10) + Bonus(c, SkillIds.Search) < dc) continue;
            TrapSystem.SetDetected(_world, trap, c, true);
            // An NPC's summon finding a trap tells its master, however far off (parity suite trap_npc with Prelude's
            // faction table: masters 5.8 m and 14.6 m from the trap's edge knew it, an NPC of the same faction 15 m
            // off didn't). Other associates of that master: not measured.
            if (_world.Get<Creature>(c.Master) is { IsDead: false } master && master.Area == trap.Area)
                TrapSystem.SetDetected(_world, trap, master, true);
        }
    }

    /// <summary>An NPC's search interval and reach, in seconds and metres.</summary>
    public const float NpcActiveSearchInterval = 3f, NpcPassiveSearchInterval = 6f, NpcActiveSearchRange = 6.66f, NpcPassiveSearchRange = 3.33f;

    /// <summary>One Search roll against a trap's detect DC (no combat-log line: passive searching would flood it).</summary>
    public bool SearchCheck(Creature c, GameObject trapObject)
    {
        if (trapObject is not ITrappable { IsTrapped: true } t || !t.Trap.Detectable) return false;
        int dc = t.TrapDetectDCValue;
        if (dc > Rule("MAX_NON_ROGUE_DETECT_LEVEL", 35) && CreatureRules.ClassLevel(c, SkillIds.ClassRogue) == 0) return false;
        int bonus = Bonus(c, SkillIds.Search);
        if (!c.DetectMode && !HasFeat(c, SkillIds.FeatKeenSense)) bonus /= 2;
        return Dice(20) + bonus >= dc;
    }

    /// <summary>
    /// A party member found a trap: the finder, its player and the player's associates in the area know it. An
    /// associate's find reaches the player within MAX_MASTER_DETECTION_DISTANCE_FROM_ASSOCIATE of the trap.
    /// </summary>
    void Detected(Creature c, GameObject trap)
    {
        TrapSystem.SetDetected(_world, trap, c, true);
        if (TrapSystem.PlayerOf(_world, c) is not { } pc || pc.Area != trap.Area) return;
        if (pc != c && DistanceTo(trap, pc.Position) > RuleF("MAX_MASTER_DETECTION_DISTANCE_FROM_ASSOCIATE", 10f)) return;
        TrapSystem.SetDetected(_world, trap, pc, true);
        foreach (var o in trap.Area!.Objects.ToArray())
            if (o is Creature a && a != c && a.Master != GameObject.InvalidId && TrapSystem.PlayerOf(_world, a) == pc)
                TrapSystem.SetDetected(_world, trap, a, true);
    }

    /// <summary>Distance from a point to a trap: to the trigger's polygon (0 inside), else to the object.</summary>
    static float DistanceTo(GameObject trap, Vector3 p)
    {
        if (trap is Trigger { Geometry.Count: >= 3 } t)
        {
            if (t.Contains(p)) return 0;
            float best = float.MaxValue;
            for (int i = 0, j = t.Geometry.Count - 1; i < t.Geometry.Count; j = i++)
                best = MathF.Min(best, SegmentDistance(new(p.X, p.Y), new(t.Geometry[j].X, t.Geometry[j].Y), new(t.Geometry[i].X, t.Geometry[i].Y)));
            return best;
        }
        return Vector2.Distance(new(p.X, p.Y), new(trap.Position.X, trap.Position.Y));
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float len = ab.LengthSquared();
        float t = len < 1e-6f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / len, 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    // ------------------------------------------------------------------ disable trap

    /// <summary>How long a trap skill takes once at the trap (ruleset SKILL_*_TRAP_DURATION, 4.5 s).</summary>
    public float TrapSkillDuration(int skill, int subSkill) => skill == SkillIds.SetTrap
        ? RuleF("SKILL_SET_TRAP_DURATION", 4.5f)
        : RuleF(subSkill switch
        {
            SkillIds.SubSkillFlagTrap => "SKILL_FLAG_TRAP_DURATION",
            SkillIds.SubSkillRecoverTrap => "SKILL_RECOVER_TRAP_DURATION",
            SkillIds.SubSkillExamineTrap => "SKILL_EXAMINE_TRAP_DURATION",
            _ => "SKILL_DISABLE_TRAP_DURATION",
        }, 4.5f);

    /// <summary>Whether a Disable Trap attempt can begin at all (checked on arrival, before the 4.5 s).</summary>
    public bool CanDisableTrap(Creature c, GameObject trapObject, int subSkill)
    {
        if (trapObject is not ITrappable { IsTrapped: true } t || trapObject.IsDestroyed) return false;
        if (!CanUse(c, SkillIds.DisableTrap)) return false;
        bool removes = subSkill is 0 or SkillIds.SubSkillRecoverTrap;
        return !removes || t.Trap.Disarmable;
    }

    /// <summary>
    /// Disable Trap on a trap: <paramref name="subSkill"/> 0 disarms, SUBSKILL_FLAGTRAP / RECOVERTRAP /
    /// EXAMINETRAP do the others. Returns whether the attempt succeeded.
    /// </summary>
    public bool DisableTrap(Creature c, GameObject trapObject, int subSkill)
    {
        if (!CanDisableTrap(c, trapObject, subSkill)) return false;
        var t = (ITrappable)trapObject;
        bool removes = subSkill is 0 or SkillIds.SubSkillRecoverTrap;
        // Never below 1: flagging a DC 1 trap showed "vs. DC: 1" (traplook), examining a DC 10 one "vs. DC: 3".
        int dc = Math.Max(1, t.TrapDisarmDCValue + subSkill switch
        {
            SkillIds.SubSkillExamineTrap => ExamineDcModifier,
            SkillIds.SubSkillFlagTrap => FlagDcModifier,
            SkillIds.SubSkillRecoverTrap => RecoverDcModifier,
            _ => 0,
        });
        // Only rogues disarm traps above DC 35: a fighter disarmed DC 35 but not 36, yet flagged and recovered a DC 36 trap
        // (trap_disarm ROGUE*).
        bool rogueOnly = t.TrapDisarmDCValue > Rule("MAX_NON_ROGUE_DISARM_LEVEL", 35) && CreatureRules.ClassLevel(c, SkillIds.ClassRogue) == 0;
        int synergy = Ranks(c, SkillIds.SetTrap) >= Rule("SKILL_DISABLETRAP_SYNERGY_LEVEL", 5) ? Rule("SKILL_DISABLE_TRAP_SYNERGY_BONUS", 2) : 0;
        bool take20 = TakesTwenty(c);
        var r = Check(c, SkillIds.DisableTrap, dc, synergy, take20, trapObject, feedback: false);
        if (subSkill == 0 && rogueOnly) r = r with { Success = false };
        // The trap's creator disarms it whatever the DC (trap_rules_npc: the setter disarmed its own kit trap
        // at DC 120, another rogue didn't).
        if (subSkill == 0 && t.Trap.Creator == c.Id) r = r with { Success = true };
        string label = subSkill switch
        {
            SkillIds.SubSkillExamineTrap => Tlk(1604, "Examine Trap"),
            SkillIds.SubSkillFlagTrap => Tlk(1589, "Flag Trap"),
            SkillIds.SubSkillRecoverTrap => Tlk(1473, "Recover Trap"),
            _ => SkillName(SkillIds.DisableTrap),
        };
        // A rolled disarm or recover that misses by 11 or more is a critical failure: the trap goes off (nwserver
        // trap_crit_ctl tthr: NPCs in combat with skill 12 against DC skill + 17 set it off 16 times in 72,
        // the rolls 1-4, and disarmed it 22 times; a player's miss by 20 did too, traplook).
        bool critical = removes && !r.Success && !r.Take20 && r.Margin <= -11;

        switch (subSkill)
        {
            case SkillIds.SubSkillExamineTrap:
                Report(c, r, trapObject, label);
                // A success opens the examine window: the trap's name and icon, "Difficulty Level: Effortless" and the
                // original's line about cogs and gears (traprules b_set_trap).
                if (r.Success && c.IsPC)
                    _world.Output?.Notify("examine", trapObject, new
                    {
                        @object = trapObject.Id,
                        kind = "trap",
                        name = TrapSystem.TrapName(_world, t.TrapBaseType),
                        description = TrapExamineText,
                        portrait = "",
                        icon = new[] { _world.Resources.TwoDA("traps").Get(t.TrapBaseType, "IconResRef") },
                        properties = Array.Empty<string>(),
                        subtitle = "Difficulty Level: " + Difficulty(c, t, synergy, take20, rogueOnly),
                        info = (object?)null,
                        identified = true,
                    }, c);
                return r.Success;
            case SkillIds.SubSkillFlagTrap:
                Report(c, r, trapObject, label);
                if (!r.Success) return false;
                t.Trap.Flagged = true;
                _world.MarkChanged(trapObject);
                TrapSystem.Publish(_world, trapObject);
                return true;
            default:
                if (!r.Success)
                {
                    Report(c, r, trapObject, label, critical);
                    // The trap goes off on the one trying, a player, or an NPC the trap can fire on (nwserver
                    // trap_crit_ctl: every miss by 14 or more in combat set a hostile trap off
                    // on a non-plot NPC; the 108 misses of trap_rules_npc and trap_disarm_crit that never did were all
                    // by plot disarmers).
                    if (critical && t.Trap.Active && !c.Plot
                        && (TrapSystem.SidesClash(_world, t.Trap.Faction, c) || TrapSystem.NpcCriticalClash(_world, trapObject, c)))
                    {
                        TrapSystem.Fire(_world, trapObject, c);
                        TrapSystem.Spent(_world, trapObject);
                    }
                    return false;
                }
                int trapType = t.TrapBaseType;
                if (subSkill == SkillIds.SubSkillRecoverTrap)
                {
                    // The kit arrives before the check line (traprules chat).
                    string kit = _world.Resources.TwoDA("traps").Get(trapType, "ResRef");
                    if (kit.Length > 0) _world.Inventory.CreateOn(kit.ToLowerInvariant(), c, 1, "");
                }
                Report(c, r, trapObject, label);
                TrapSystem.Disarm(_world, trapObject, c);
                return true;
        }
    }

    /// <summary>Examine Trap's verdict on disarming: the expected margin against the disarm DC in ASSESSTRAP_* bands.</summary>
    string Difficulty(Creature c, ITrappable t, int synergy, bool take20, bool rogueOnly)
    {
        int margin = (take20 ? 20 : 10) + Bonus(c, SkillIds.DisableTrap) + synergy - t.TrapDisarmDCValue;
        if (rogueOnly || margin < Rule("ASSESSTRAP_DIFFICULT_MODIFIER", 0) - 10) return Tlk(SkillIds.StrImpossible, "Impossible");
        if (margin >= Rule("ASSESSTRAP_EFORTLESS_MODIFIER", 15)) return Tlk(SkillIds.StrEffortless, "Effortless");
        if (margin >= Rule("ASSESSTRAP_EASY_MODIFIER", 10)) return Tlk(SkillIds.StrEasy, "Easy");
        if (margin >= Rule("ASSESSTRAP_CHALLENGING_MODIFIER", 5)) return Tlk(SkillIds.StrChallenging, "Challenging");
        return Tlk(SkillIds.StrVeryDifficult, "Very Difficult");
    }

    // ------------------------------------------------------------------ set trap

    /// <summary>The traps.2da row a trap kit sets (ITEM_PROPERTY_TRAP: subtype = strength, cost value = iprp_trapcost type), or -1.</summary>
    public int TrapKitType(Item kit)
    {
        var p = kit.Properties.FirstOrDefault(x => x.Valid && x.Type == SkillIds.IpTrap);
        if (p == null) return -1;
        string[] levels = { "Minor", "Average", "Strong", "Deadly", "Epic" };
        if (p.SubType < 0 || p.SubType >= levels.Length) return -1;
        return _world.Resources.TwoDA("iprp_trapcost").GetInt(p.CostTableValue, levels[p.SubType], -1);
    }

    /// <summary>Whether a Set Trap attempt can begin (checked on arrival, before the 4.5 s): a kit, training, no trap there already.</summary>
    public bool CanSetTrap(Creature c, Item kit, GameObject? onObject)
    {
        if (TrapKitType(kit) < 0 || !CanUse(c, SkillIds.SetTrap)) return false;
        return onObject is not ITrappable { IsTrapped: true };
    }

    /// <summary>
    /// Set Trap with a trap kit, on a door or placeable (<paramref name="onObject"/>) or at a location. On success the
    /// trap belongs to the setter's side (its party sees it), its DCs are the trap's DetectDCMod / DisarmDCMod plus the
    /// setter's Set Trap bonus, and the kit is used up. Failing by 10 or more in combat sets the trap off on the setter.
    /// </summary>
    public bool SetTrap(Creature c, Item kit, GameObject? onObject, Location at)
    {
        int type = TrapKitType(kit);
        if (onObject != null && onObject is not (Door or Placeable)) onObject = null;
        if (!CanSetTrap(c, kit, onObject)) return false;
        if (onObject == null && _world.Get(at.Area) is not Area) return false;

        var traps = _world.Resources.TwoDA("traps");
        int synergy = Ranks(c, SkillIds.DisableTrap) >= Rule("SKILL_SETTRAP_SYNERGY_LEVEL", 5) ? Rule("SKILL_SETTRAP_SYNERGY_BONUS", 2) : 0;
        var r = Check(c, SkillIds.SetTrap, traps.GetInt(type, "SetDC"), synergy, TakesTwenty(c), onObject, feedback: false);
        bool spectacular = !r.Success && !r.Take20 && r.Margin <= -10 && InCombat(c);
        if (!r.Success && !spectacular) { Report(c, r, onObject); return false; }

        int faction = _world.Factions.FactionOf(c) ?? c.Faction;
        // The DCs add the setter's Set Trap bonus, not the roll (trap_set HIGH, NOSYN; traprules p5).
        int bonus = r.Bonus;
        GameObject? trap;
        if (onObject != null)
        {
            TrapSystem.Arm(_world, onObject, type, faction, "", "", c.Id, bonus);
            trap = onObject;
        }
        else trap = TrapSystem.CreateAt(_world, type, at with { FacingDegrees = 0 }, 2f, "", faction, "", "", c.Id, null, bonus);
        UseUp(kit);
        Report(c, r, onObject, critical: spectacular);
        if (trap is not ITrappable t) return false;

        if (spectacular)
        {
            // The trap goes off on its setter.
            TrapSystem.Fire(_world, trap, c);
            TrapSystem.Spent(_world, trap);
            if (trap is Trigger trigger && !trigger.IsDestroyed) _world.Destroy(trigger);
            return false;
        }
        TrapSystem.Publish(_world, trap);
        return true;
    }

    /// <summary>Uses up one of a stack (kits, tools).</summary>
    void UseUp(Item item)
    {
        if (item.IsDestroyed) return;
        if (item.StackSize > 1) { item.StackSize--; _world.MarkChanged(item); }
        else _world.Destroy(item);
    }
}

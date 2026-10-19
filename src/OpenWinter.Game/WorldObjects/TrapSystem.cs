using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.WorldObjects;

/// <summary>
/// Traps on doors, placeables and trap triggers (trigger type 2): the engine side of them. What a trap does when it
/// goes off is the stock or module script (traps.2da TrapScript, nw_t1_* / x2_t1_*); the engine decides who sets it
/// off, runs that script, and keeps the detection and disarm bookkeeping. Measured on the original (docs/TRAPS.md):
/// <list type="bullet">
/// <item>A trap trigger notices only creatures it goes off for: a player or associate on a trap that isn't the
/// players', or a creature hostile to the players on the players' trap; between NPCs, a creature of another faction
/// that the trap's setter (or, with no setter left, the trap's faction) likes less than 90 (<see cref="NpcClash"/>).
/// For those that set it off, OnTrapTriggered runs first, then OnEnter, both with the trap still armed; a one-shot trap
/// trigger is then destroyed.</item>
/// <item>A trapped door or placeable goes off for whoever opens it, whatever the factions.</item>
/// <item>Disarming (Disable Trap or SetTrapDisabled) runs OnDisarm with GetLastDisarmed() = whoever disarmed it; a trap
/// trigger is still armed during OnDisarm and is destroyed after it, a door or placeable is disarmed before it.</item>
/// </list>
/// </summary>
public sealed class TrapSystem : IWorldSystem
{
    readonly World _world;
    public int Order => 15;

    /// <summary>
    /// Optional extra detection check (creature, trap) run each second for creatures near undetected traps.
    /// Search itself runs in <see cref="Skills.SkillSystem"/>; leave this null unless a server wants a second, custom rule.
    /// </summary>
    public static Func<Creature, GameObject, bool>? DetectCheck;

    double _nextDetect;

    public TrapSystem(World world)
    {
        _world = world;
    }

    public void Step(float dt)
    {
        if (DetectCheck == null || _world.Time < _nextDetect) return;
        _nextDetect = _world.Time + 1.0;
        foreach (var area in _world.Areas)
        {
            List<GameObject>? traps = null;
            foreach (var o in area.Objects)
                if (o is ITrappable { IsTrapped: true, Trap.Detectable: true }) (traps ??= new()).Add(o);
            if (traps == null) continue;
            foreach (var c in area.Objects.OfType<Creature>().ToList())
                foreach (var trap in traps)
                {
                    var t = ((ITrappable)trap).Trap;
                    if (t.DetectedBy.Contains(c.Id) || Vector3.DistanceSquared(c.Position, trap.Position) > 20 * 20) continue;
                    if (DetectCheck(c, trap)) SetDetected(_world, trap, c, true);
                }
        }
    }

    // ------------------------------------------------------------------ sides

    /// <summary>The player whose side a creature is on: itself, or the player at the top of its chain of masters.</summary>
    public static Creature? PlayerOf(World world, Creature? c)
    {
        for (int i = 0; i < 8 && c != null; i++)
        {
            if (c.IsPC) return c;
            c = world.Get<Creature>(c.Master);
        }
        return null;
    }

    /// <summary>A player, an associate of one, or a member of a player party.</summary>
    public static bool IsPlayerSide(World world, Creature c) =>
        PlayerOf(world, c) != null || world.Factions.FactionOf(c) is int f && IsPlayerFaction(f);

    static bool IsPlayerFaction(int faction) => FactionTable.IsPartyFaction(faction) || faction == FactionTable.PlayerFaction;

    /// <summary>
    /// Whether a creature sets off a trap: anyone who opens a trapped door or placeable; for a trap trigger, players
    /// and their associates set off every trap that isn't the players', anyone else only the players' traps and only
    /// when hostile to the players. NPC trap triggers never go off for NPCs.
    /// </summary>
    public static bool SpringsFor(World world, GameObject trapObject, Creature victim)
    {
        if (victim.IsDMAvatar || victim.IsDead) return false; // DMs don't set off traps
        // A trap never goes off on a plot creature (nwserver trap_crit_ctl: plot NPCs walking through a hostile trap
        // trigger 0 of 4, against 4 of 4 not plot; plot disarmers missing by 14 or more 0 of 12).
        if (victim.Plot) return false;
        if (trapObject is not Trigger) return true;
        var trap = ((ITrappable)trapObject).Trap;
        return SidesClash(world, trap.Faction, victim) || NpcClash(world, trap, victim);
    }

    /// <summary>
    /// A trap trigger between NPCs (neither the trap nor the creature on the players' side): it goes off for a creature
    /// that isn't immune to traps, isn't of the trap's faction, and that rates the trap's setter, while it exists, else
    /// the trap's faction, below 90: the creature's view, not the setter's, on nwserver. Parity suite trap_trigger with
    /// Prelude's faction table, whose Defender rates Commoner 100 and Commoner rates Defender 50:
    /// a Defender trap went off for a Commoner walker and a Commoner trap didn't for a Defender walker. trap_rules_npc
    /// (kit traps and scripted trap triggers going off for walkers rated 0 or 50 both ways, not for their own faction or
    /// 100 both ways, and not when the setter had joined the walker's faction) fits either direction; for a living setter
    /// the direction is not measured. (Without a faction table every faction likes every other on nwserver.)
    /// </summary>
    public static bool NpcClash(World world, TrapData trap, Creature victim)
    {
        if (victim.IsDMAvatar || victim.IsDead || victim.Plot || IsPlayerFaction(trap.Faction) || IsPlayerSide(world, victim)) return false;
        if (Rules.CreatureRules.IsImmune(world, victim, Rules.NwRules.ImmunityTrap)) return false;
        int victimFaction = world.Factions.FactionOf(victim) ?? victim.Faction;
        if (victimFaction == trap.Faction) return false;
        int reputation = world.Get<Creature>(trap.Creator) is { IsDestroyed: false } setter
            ? world.Factions.Reputation(victim, setter)
            : world.Factions.FactionReputation(victimFaction, trap.Faction);
        return reputation < FactionTable.FriendFrom;
    }

    /// <summary>
    /// Whether a disarm's critical failure sets an NPC trap off on an NPC (nwserver trap_crit_ctl, Prelude factions,
    /// disarmers in combat missing by 14 or more): as walking into it for a trap trigger (a Hostile trap 10 of 10 on
    /// Commoner disarmers, 0 of 6 on Hostile ones); for a trapped placeable the reputation is the placeable's own
    /// faction's view of the disarmer (a stock Hostile chest with a Hostile trap 10 of 10 on Commoners; with a Defender
    /// trap 0 of 6 on Hostile disarmers), and for a door the setter's (not measured). Never a plot disarmer (0 of 12).
    /// </summary>
    public static bool NpcCriticalClash(World world, GameObject trapObject, Creature victim)
    {
        var trap = ((ITrappable)trapObject).Trap;
        if (trapObject is Trigger) return NpcClash(world, trap, victim);
        if (victim.IsDMAvatar || victim.IsDead || victim.Plot || IsPlayerFaction(trap.Faction) || IsPlayerSide(world, victim)) return false;
        if (Rules.CreatureRules.IsImmune(world, victim, Rules.NwRules.ImmunityTrap)) return false;
        int victimFaction = world.Factions.FactionOf(victim) ?? victim.Faction;
        if (victimFaction == trap.Faction) return false;
        int reputation;
        if (trapObject is Placeable)
            reputation = world.Factions.FactionReputation(world.Factions.FactionOf(trapObject) ?? trap.Faction, victimFaction);
        else reputation = world.Get<Creature>(trap.Creator) is { IsDestroyed: false } setter
            ? world.Factions.Reputation(setter, victim)
            : world.Factions.FactionReputation(trap.Faction, victimFaction);
        return reputation < FactionTable.FriendFrom;
    }

    /// <summary>
    /// The trap-trigger rule (also a botched disarm's): exactly one of trap and creature is on the players' side, and
    /// the other side is hostile to players. A player didn't set off a commoner trap, a commoner didn't set off the
    /// player's trap, a hostile creature did (trap_trigger, traprules, traplook). Between NPCs: <see cref="NpcClash"/>.
    /// </summary>
    public static bool SidesClash(World world, int trapFaction, Creature victim)
    {
        if (victim.IsDMAvatar || victim.IsDead) return false;
        bool playerTrap = IsPlayerFaction(trapFaction), playerVictim = IsPlayerSide(world, victim);
        // Own side, or NPC against NPC (another party's trap was not measured: treated as one's own).
        if (playerTrap == playerVictim) return false;
        int npcFaction = playerTrap ? world.Factions.FactionOf(victim) ?? victim.Faction : trapFaction;
        if (playerVictim && PlayerOf(world, victim) is { } pc && world.Factions.FactionOf(pc) is int pf)
            return world.Factions.BaseReputation(npcFaction, pf, pc) <= FactionTable.HostileTo;
        return world.Factions.FactionReputation(npcFaction, FactionTable.PlayerFaction) <= FactionTable.HostileTo;
    }

    /// <summary>Whether an armed, active trap trigger notices a creature walking in (it notices only those it goes off for).</summary>
    public static bool Notices(World world, Trigger t, Creature c) =>
        t.TriggerType == Trigger.TypeTrap && t.TrapFlag != 0 && ((ITrappable)t).Trap.Active && !t.IsDestroyed && SpringsFor(world, t, c);

    // ------------------------------------------------------------------ going off

    /// <summary>
    /// An NPC using a trapped container (DoorService.Use): the trap goes off when <see cref="NpcCriticalClash"/> says so.
    /// Returns whether it fired.
    /// </summary>
    public static bool TrySpringOnNpcUse(World world, Placeable container, Creature user)
    {
        if (container is not ITrappable { IsTrapped: true } trappable || !trappable.Trap.Active || container.IsDestroyed) return false;
        if (!NpcCriticalClash(world, container, user)) return false;
        Fire(world, container, user);
        Spent(world, container);
        return true;
    }

    /// <summary>Springs a trap on <paramref name="trapObject"/> if it is armed and goes off for <paramref name="victim"/>. Returns whether it fired.</summary>
    public static bool TrySpring(World world, GameObject trapObject, Creature victim)
    {
        if (trapObject is not ITrappable { IsTrapped: true } trappable || !trappable.Trap.Active || trapObject.IsDestroyed) return false;
        if (!SpringsFor(world, trapObject, victim)) return false;
        Fire(world, trapObject, victim);
        Spent(world, trapObject);
        return true;
    }

    /// <summary>
    /// Runs the trap's script against a victim: the object's OnTrapTriggered, else traps.2da TrapScript, with
    /// GetEnteringObject() = the victim. The trap stays armed meanwhile (see <see cref="Spent"/>). A player who sets
    /// a trap off is told "You triggered a Trap!".
    /// </summary>
    public static void Fire(World world, GameObject trapObject, Creature victim)
    {
        var trappable = (ITrappable)trapObject;
        trappable.Trap.LastTriggeredBy = victim.Id;
        string script = trapObject.Script("OnTrapTriggered");
        if (script.Length == 0) script = world.Resources.TwoDA("traps").Get(trappable.TrapBaseType, "TrapScript");
        if (victim.IsPC) world.Output?.SendMessage(victim, TriggeredMessage);
        world.Output?.Notify("trap-triggered", trapObject, new { victim = victim.Id, type = trappable.TrapBaseType });
        var ctx = new EventContext().Set("Entering", victim.Id).Set("Clicking", victim.Id);
        if (script.Length > 0 && world.Scripts != null) world.Scripts.Run(script, trapObject.Id, ctx);
    }

    /// <summary>The original's chat line when a player sets off a trap (not in dialog.tlk).</summary>
    public const string TriggeredMessage = "You triggered a Trap!";

    /// <summary>After a trap went off: a one-shot trap is disarmed, a one-shot trap trigger goes away.</summary>
    public static void Spent(World world, GameObject trapObject)
    {
        if (trapObject is not ITrappable { IsTrapped: true } t || !t.Trap.OneShot || trapObject.IsDestroyed) return;
        t.IsTrapped = false;
        t.Trap.DetectedBy.Clear();
        world.MarkChanged(trapObject);
        world.Output?.Notify("trap-disarmed", trapObject, new { by = GameObject.InvalidId });
        if (trapObject is Trigger { TriggerType: Trigger.TypeTrap } trigger) world.Destroy(trigger);
    }

    // ------------------------------------------------------------------ detection

    /// <summary>
    /// Marks a trap detected (or undetected) by a creature (SetTrapDetectedBy, Search). Returns false when nothing
    /// changed. <paramref name="found"/> also sets GetLastTrapDetected; nothing measured does (neither
    /// SetTrapDetectedBy nor a player's own Search find changed it: trap_api, traprules, traplook), so it stays for
    /// servers' own rules.
    /// </summary>
    public static bool SetDetected(World world, GameObject trapObject, Creature by, bool detected, bool found = false)
    {
        if (trapObject is not ITrappable t) return false;
        if (detected)
        {
            if (!t.Trap.DetectedBy.Add(by.Id)) return false;
            if (found) by.LastTrapDetected = trapObject.Id;
        }
        else if (!t.Trap.DetectedBy.Remove(by.Id)) return false;
        if (by.IsPC && t.IsTrapped && world.Output != null)
            world.Output.Notify("trap-detected", trapObject, DetectedData(world, trapObject, by, detected || Shows(world, trapObject, by)), by);
        return true;
    }

    /// <summary>
    /// Whether a player sees a trap: one it has detected, a flagged one (everyone sees those), or one of its own
    /// party's (a trap a party member set).
    /// </summary>
    public static bool Shows(World world, GameObject trapObject, Creature pc)
    {
        if (trapObject is not ITrappable { IsTrapped: true } t || trapObject.IsDestroyed) return false;
        return t.Trap.DetectedBy.Contains(pc.Id) || t.Trap.Flagged || IsOwnTrap(world, trapObject, pc);
    }

    /// <summary>A trap of the player's own side (set by the player or a party member).</summary>
    public static bool IsOwnTrap(World world, GameObject trapObject, Creature pc) =>
        trapObject is ITrappable t && IsPlayerFaction(t.Trap.Faction)
        && (t.Trap.Faction == FactionTable.PlayerFaction || world.Factions.FactionOf(pc) == t.Trap.Faction);

    /// <summary>
    /// The trap-detected payload for one player. Clients never get triggers, so a trap trigger's polygon (area
    /// coordinates) comes along for drawing it; flagged / own / active pick the colour.
    /// </summary>
    public static object DetectedData(World world, GameObject trapObject, Creature pc, bool detected)
    {
        var t = (ITrappable)trapObject;
        var geometry = trapObject is Trigger trigger ? trigger.Geometry.Select(p => new[] { p.X, p.Y, p.Z }).ToList() : null;
        return new
        {
            detected, type = t.TrapBaseType, flagged = t.Trap.Flagged, own = IsOwnTrap(world, trapObject, pc), active = t.Trap.Active,
            disarmable = t.Trap.Disarmable, recoverable = t.Trap.Recoverable, name = TrapName(world, t.TrapBaseType), geometry,
        };
    }

    /// <summary>Tells every player in the trap's area that can see it (after flagging, setting or a state change).</summary>
    public static void Publish(World world, GameObject trapObject)
    {
        if (trapObject.Area == null || world.Output == null) return;
        foreach (var pc in trapObject.Area.Objects.OfType<Creature>().Where(x => x.IsPC).ToList())
            if (Shows(world, trapObject, pc)) world.Output.Notify("trap-detected", trapObject, DetectedData(world, trapObject, pc, true), pc);
    }

    /// <summary>traps.2da TrapName (a dialog.tlk string), else the row label.</summary>
    public static string TrapName(World world, int type)
    {
        var traps = world.Resources.TwoDA("traps");
        return uint.TryParse(traps.Get(type, "TrapName"), out var s) && world.Resources.TlkString(s) is { Length: > 0 } name ? name : traps.Get(type, "Label");
    }

    // ------------------------------------------------------------------ disarming

    /// <summary>
    /// Disarms a trap (Disable Trap or Recover Trap succeeded, or SetTrapDisabled): fires OnDisarm with
    /// GetLastDisarmed() = <paramref name="by"/>. A trap trigger stays armed during OnDisarm, then goes away;
    /// a door or placeable is disarmed first. Nobody has detected the trap afterwards.
    /// </summary>
    public static void Disarm(World world, GameObject trapObject, GameObject? by)
    {
        if (trapObject is not ITrappable t || !t.IsTrapped || trapObject.IsDestroyed) return;
        t.Trap.LastDisarmedBy = by?.Id ?? GameObject.InvalidId;
        var ctx = new EventContext().Set("Disarmer", t.Trap.LastDisarmedBy);
        world.Output?.Notify("trap-disarmed", trapObject, new { by = t.Trap.LastDisarmedBy });
        if (trapObject is Trigger { TriggerType: Trigger.TypeTrap } trigger)
        {
            // Still armed during OnDisarm and through same-moment DelayCommand(0.0) work; gone a moment later (trap_api d0).
            world.RunEvent(trapObject, "OnDisarm", ctx);
            t.Trap.DetectedBy.Clear();
            world.After(0, () =>
            {
                t.IsTrapped = false;
                if (!trigger.IsDestroyed) world.Destroy(trigger);
            });
            return;
        }
        t.IsTrapped = false;
        t.Trap.DetectedBy.Clear();
        world.MarkChanged(trapObject);
        world.RunEvent(trapObject, "OnDisarm", ctx);
    }

    // ------------------------------------------------------------------ making traps

    /// <summary>Whether traps.2da has the row (CreateTrapAtLocation and CreateTrapOnObject do nothing for others).</summary>
    public static bool IsTrapType(World world, int type)
    {
        var traps = world.Resources.TwoDA("traps");
        return type >= 0 && type < traps.RowCount && traps.Get(type, "TrapScript").Length > 0;
    }

    /// <summary>
    /// Arms a door or placeable with a trap of a traps.2da type: DCs from DetectDCMod and DisarmDCMod (plus
    /// <paramref name="dcBonus"/>, a setter's Set Trap bonus), every flag on.
    /// </summary>
    public static void Arm(World world, GameObject o, int trapType, int faction, string onDisarm, string onTriggered, uint creator = GameObject.InvalidId, int dcBonus = 0)
    {
        if (o is not ITrappable t) return;
        var traps = world.Resources.TwoDA("traps");
        t.IsTrapped = true;
        t.TrapBaseType = trapType;
        t.TrapDetectDCValue = Math.Clamp(traps.GetInt(trapType, "DetectDCMod", 0) + dcBonus, 0, 250);
        t.TrapDisarmDCValue = Math.Clamp(traps.GetInt(trapType, "DisarmDCMod", 0) + dcBonus, 0, 250);
        t.Trap.Active = true;
        t.Trap.Detectable = true;
        t.Trap.Disarmable = true;
        t.Trap.OneShot = true;
        t.Trap.Recoverable = true;
        t.Trap.Flagged = false;
        t.Trap.DetectedBy.Clear();
        t.Trap.Faction = faction;
        t.Trap.Creator = creator;
        o.Scripts["OnDisarm"] = onDisarm;
        o.Scripts["OnTrapTriggered"] = onTriggered;
        world.MarkChanged(o);
    }

    /// <summary>
    /// Creates a square trap trigger (CreateTrapAtLocation, Set Trap at a spot): <paramref name="size"/> is half its
    /// side (a 2.0 trap is 4 m across, measured), square to the area axes and facing 0 whatever the location's facing.
    /// </summary>
    public static Trigger? CreateAt(World world, int trapType, Location at, float size, string tag, int faction, string onDisarm, string onTriggered,
        uint creator = GameObject.InvalidId, LocString? name = null, int dcBonus = 0)
    {
        if (world.Get(at.Area) is not Area area) return null;
        var t = new Trigger { TriggerType = Trigger.TypeTrap, Tag = tag, Position = at.Position, Faction = faction };
        if (name != null) t.Name = name;
        foreach (var corner in new[] { new Vector3(-size, -size, 0), new Vector3(size, -size, 0), new Vector3(size, size, 0), new Vector3(-size, size, 0) })
            t.Geometry.Add(at.Position + corner);
        Arm(world, t, trapType, faction, onDisarm, onTriggered, creator, dcBonus);
        world.AddToArea(t, area, at.Position, 0);
        world.Factions.SetObjectFaction(t, faction); // after AddToArea, which gives the trigger its id
        return t;
    }
}

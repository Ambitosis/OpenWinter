using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Society;

/// <summary>
/// Associates: henchmen, animal companions, familiars, summons and dominated creatures. An associate
/// has <see cref="Creature.Master"/> and <see cref="Creature.AssociateType"/> set, joins its master's
/// faction (the PC's party) and returns to its original faction when released.
/// Other packages (summoning, domination, familiars) should call <see cref="Add"/> and <see cref="Remove"/>.
/// </summary>
public static class AssociateService
{
    public const int None = 0, Henchman = 1, AnimalCompanion = 2, Familiar = 3, Summoned = 4, Dominated = 5;

    /// <summary>Puts <paramref name="associate"/> into <paramref name="master"/>'s service.</summary>
    public static void Add(World world, Creature master, Creature associate, int type)
    {
        if (associate == master) return;
        if (world.Get<Creature>(associate.Master) is { } old && old != master) Remove(world, old, associate);
        // NWNX_ON_ADD_ASSOCIATE around it, OBJECT_SELF the master (nwnx_events_creature).
        var ev = Nwnx.NwnxEvent.Begin(world, "NWNX_ON_ADD_ASSOCIATE", master)?.Add("ASSOCIATE_OBJECT_ID", associate);
        ev?.Before();
        AddNow(world, master, associate, type);
        ev?.After();
    }

    static void AddNow(World world, Creature master, Creature associate, int type)
    {
        // Henchmen and dominated creatures go back to the faction they came from; familiars, companions and summons
        // were made for their master and go to the standard Commoner faction (parity suites faction_release and
        // associate_death: the same thing with a repute.fac, but without one Commoner isn't FactionID 2).
        if (type is Henchman or Dominated) associate.Society.OriginalFaction ??= associate.Faction;
        associate.Master = master.Id;
        associate.AssociateType = type;
        master.Society.Associates.Remove(associate.Id);
        master.Society.Associates.Add(associate.Id);
        int faction = world.Factions.FactionOf(master) ?? master.Faction;
        MoveFaction(world, associate, faction);
        PartyService.Of(world).Refresh(master);
    }

    /// <summary>Releases <paramref name="associate"/> from <paramref name="master"/>'s service, restoring its original faction.</summary>
    public static bool Remove(World world, Creature master, Creature associate)
    {
        if (associate.Master != master.Id) return false;
        var ev = Nwnx.NwnxEvent.Begin(world, "NWNX_ON_REMOVE_ASSOCIATE", master)?.Add("ASSOCIATE_OBJECT_ID", associate);
        ev?.Before();
        RemoveNow(world, master, associate);
        ev?.After();
        return true;
    }

    static void RemoveNow(World world, Creature master, Creature associate)
    {
        world.System<Players.FamiliarPossession>().Unpossess(associate);
        master.Society.Associates.Remove(associate.Id);
        associate.Master = GameObject.InvalidId;
        associate.AssociateType = None;
        int faction = associate.Society.OriginalFaction ?? world.Factions.StandardFaction(1);
        associate.Society.OriginalFaction = null;
        MoveFaction(world, associate, faction);
        PartyService.Of(world).Refresh(master);
    }

    static void MoveFaction(World world, Creature c, int faction)
    {
        c.Faction = faction;
        foreach (var sub in AllAssociates(world, c)) MoveFaction(world, sub, faction);
    }

    /// <summary>
    /// Everything serving <paramref name="master"/>, in the order added. Creatures whose Master was set
    /// directly (without <see cref="Add"/>) are included after those, by object id.
    /// </summary>
    public static List<Creature> AllAssociates(World world, Creature master)
    {
        var result = new List<Creature>();
        var list = master.Society.Associates;
        for (int i = list.Count - 1; i >= 0; i--)
            if (world.Get<Creature>(list[i]) is not { } a || a.Master != master.Id) list.RemoveAt(i);
        foreach (var id in list) result.Add(world.Get<Creature>(id)!);
        foreach (var o in world.AllObjects)
            if (o is Creature c && c.Master == master.Id && !c.IsDestroyed && !list.Contains(c.Id) && c != master) result.Add(c);
        return result;
    }

    /// <summary>
    /// The player a creature acts for: itself when it is one, the player possessing it, or the player at the end of
    /// its master chain (a henchman's summon). Null for creatures nobody plays.
    /// </summary>
    public static Creature? PlayerMaster(World world, Creature? creature)
    {
        var c = creature;
        for (int depth = 0; depth < 8 && c != null; depth++)
        {
            if (c.Player?.FamiliarMaster is { } possessor) return possessor;
            if (c.IsPC) return c;
            c = world.Get<Creature>(c.Master);
        }
        return null;
    }

    /// <summary>
    /// Who sees an attack or damage line: each player involved, and the player an involved associate serves when she
    /// is within <see cref="Rules.KillRewards.ShareRange"/> of the target in its area. Measured: a henchman's, summon's
    /// and familiar's lines, and a goblin's on a henchman, reach the master beside the target (killmsg, killxp, even
    /// from a henchman 70 m away); a henchman's damage to a goblin 60 m from her did not (killmsg henchman_far). The
    /// exact distance is not measured; the share range is used.
    /// </summary>
    public static List<Creature> FeedbackPlayers(World world, GameObject target, params Creature?[] involved)
    {
        var result = new List<Creature>();
        foreach (var c in involved)
        {
            if (PlayerMaster(world, c) is not { } player || result.Contains(player)) continue;
            bool direct = c == player || c!.Player != null;
            if (direct || (player.Area != null && player.Area == target.Area
                && System.Numerics.Vector3.Distance(player.Position, target.Position) <= Rules.KillRewards.ShareRange))
                result.Add(player);
        }
        return result;
    }

    /// <summary>
    /// Who sees an attack's lines: the players involved and the masters of involved associates
    /// (<see cref="FeedbackPlayers"/>), nobody else. Measured with the visual scene combatlines (one original client):
    /// a commoner or defender fighting a hostile goblin 4 or 12 m from the player printed nothing, seen or not
    /// (blinded); her henchman attacking the goblin, or attacked by it, 12 m away printed its lines. The lines
    /// mp_delivery saw 4-30 m away came from her three idle henchmen (stock scripts, also named "Bandit") joining
    /// those fights: with such henchmen near a commoner's fight, only their attacks were printed.
    /// </summary>
    public static List<Creature> AttackFeedbackPlayers(World world, Creature attacker, GameObject target) =>
        FeedbackPlayers(world, target, attacker, target as Creature);

    /// <summary>The <paramref name="nth"/> (1-based) associate of a type, or null.</summary>
    public static Creature? Get(World world, Creature master, int type, int nth)
    {
        if (nth < 1) return null;
        foreach (var a in AllAssociates(world, master))
            if (a.AssociateType == type && --nth == 0) return a;
        return null;
    }

    public static int Count(World world, Creature master, int type) => AllAssociates(world, master).Count(a => a.AssociateType == type);

    // ASSOCIATE_COMMAND_* (nwscript.nss).
    public const int CommandStandGround = -2, CommandAttackNearest = -3, CommandHealMaster = -4, CommandFollowMaster = -5,
        CommandGuardMaster = -7, CommandUnsummonFamiliar = -8, CommandUnsummonCompanion = -9, CommandUnsummonSummoned = -10,
        CommandReleaseDomination = -12, CommandLeaveParty = -17, CommandPickLock = -18, CommandInventory = -19,
        CommandDisarmTrap = -20, CommandToggleCasting = -21, CommandToggleStealth = -22, CommandToggleSearch = -23;

    /// <summary>
    /// Whether the radial on an associate of <paramref name="type"/> offers <paramref name="command"/>. Measured on the
    /// original (associateradial, associatesubmenus, associatedominated): every associate's root has Heal Me, Attack
    /// Nearest, Follow, Stand Your Ground and Guard Me; its NW ring has Unsummon for summons and familiars, Release from
    /// Domination for dominated creatures, and the henchman's Pick Lock, Inventory, Remove From Party, Disarm Trap and the
    /// casting / stealth / search toggles. The animal companion's ring was not measured (its Unsummon is assumed).
    /// </summary>
    public static bool Offers(int type, int command) => command switch
    {
        CommandStandGround or CommandAttackNearest or CommandHealMaster or CommandFollowMaster or CommandGuardMaster => type != None,
        CommandUnsummonSummoned => type == Summoned,
        CommandUnsummonFamiliar => type == Familiar,
        CommandUnsummonCompanion => type == AnimalCompanion,
        CommandReleaseDomination => type == Dominated,
        >= CommandToggleSearch and <= CommandLeaveParty => type == Henchman,
        _ => false,
    };

    /// <summary>
    /// A player's order to one of her own associates from its radial menu. Unsummoning and releasing a dominated
    /// creature are the engine's own work (the stock associate scripts don't handle those numbers); every other
    /// command goes to the associate's OnConversation script like a voice command (<see cref="IssueCommand"/>).
    /// Returns false when the order is refused (not her associate, or not one its radial offers).
    /// </summary>
    /// <summary>
    /// Whether a player may order an associate: her own, or, with player party control on, one SetCommandingPlayer gave
    /// her (World.CommandingPlayer); an override to anyone else takes it from its master.
    /// </summary>
    public static bool CommandableBy(World world, Creature player, Creature associate) =>
        world.PlayerPartyControl && associate.CommandingPlayer != GameObject.InvalidId
            ? world.CommandingPlayer(associate) == player
            : associate.Master == player.Id;

    public static bool Command(World world, Creature master, Creature associate, int command)
    {
        if (associate == master || associate.IsDestroyed || !CommandableBy(world, master, associate) || master.IsDead) return false;
        if (world.Get<Creature>(associate.Master) == null || !Offers(associate.AssociateType, command)) return false;
        switch (command)
        {
            case CommandUnsummonSummoned or CommandUnsummonFamiliar or CommandUnsummonCompanion:
                associate.Society.LastAssociateCommand = command;
                Unsummon(world, master, associate);
                break;
            case CommandReleaseDomination:
                associate.Society.LastAssociateCommand = command;
                ReleaseDomination(world, master, associate);
                break;
            default:
                IssueCommand(world, master, associate, command);
                break;
        }
        return true;
    }

    /// <summary>
    /// Sends a summon, familiar or animal companion away: a summon through its master's summon effect (which plays the
    /// effect's unsummon visual), the others with VFX_IMP_UNSUMMON (the familiar's visual is not measured).
    /// </summary>
    public static void Unsummon(World world, Creature master, Creature associate)
    {
        var effects = world.System<Rules.EffectSystem>();
        var k = EffectConstants.Of(world);
        if (master.Effects.FirstOrDefault(e => e.Type == k.SummonCreature && e.SummonedObject == associate.Id) is { } summon)
        {
            effects.Remove(master, summon);
            if (associate.IsDestroyed) return;
        }
        world.Output?.Notify("vfx", associate, new { vfx = k["VFX_IMP_UNSUMMON"], miss = false, scale = 1f, translate = new[] { 0f, 0f, 0f }, rotate = new[] { 0f, 0f, 0f }, duration = 0f, id = 0L });
        Remove(world, master, associate);
        world.Destroy(associate);
    }

    /// <summary>
    /// Releases a dominated creature: its master's domination effects end, it returns to its own faction, and the master
    /// reads TLK 61909 ("You are no longer dominating ..."), the line the original printed when the goblin left her
    /// service (associateradial chat.txt).
    /// </summary>
    public static void ReleaseDomination(World world, Creature master, Creature associate)
    {
        var k = EffectConstants.Of(world);
        int dominated = k["EFFECT_TYPE_DOMINATED"], cutscene = k["EFFECT_TYPE_CUTSCENE_DOMINATED"];
        // Ending the domination effects ends the service (EffectSystem calls EndDomination).
        world.System<Rules.EffectSystem>().RemoveWhere(associate, e => e.Type == dominated || e.Type == cutscene);
        if (associate.Master == master.Id) EndDomination(world, master, associate);
    }

    /// <summary>A dominated associate leaves its master's service; the master reads TLK 61909.</summary>
    public static void EndDomination(World world, Creature master, Creature associate)
    {
        if (!Remove(world, master, associate)) return;
        if (master.IsPC)
            world.Output?.SendMessage(master, world.Resources.TlkString(61909).Replace("<CUSTOM0>", associate.Name.Resolve(world.Resources.Talk)));
    }

    /// <summary>
    /// A master told an associate to do something (radial menu or voice command). Records it for
    /// GetLastAssociateCommand and, if the associate listens for associate commands, runs its
    /// OnConversation script with GetListenPatternNumber() = the command (ASSOCIATE_COMMAND_*).
    /// </summary>
    public static void IssueCommand(World world, Creature master, Creature associate, int command)
    {
        associate.Society.LastAssociateCommand = command;
        if (!associate.Society.AssociatePatterns) return;
        var ctx = new EventContext()
            .Set("Speaker", master.Id)
            .Set("ListenPatternNumber", command)
            .Set("EventScriptType", SocietyService.EventScriptCreatureOnDialogue);
        world.After(0, () => world.RunEvent(associate, "ScriptDialogue", ctx));
    }
}

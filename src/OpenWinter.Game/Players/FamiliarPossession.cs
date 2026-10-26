using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Society;
using OpenWinter.Net;

namespace OpenWinter.Game.Players;

/// <summary>Player control of an owned familiar. The connection's character identity stays the master.</summary>
public sealed class FamiliarPossession : IWorldSystem
{
    readonly World _world;
    public FamiliarPossession(World world)
    {
        _world = world;
        world.ObjectRemoved += o =>
        {
            if (o is Creature c && c.IsDestroyed)
            {
                if (c.Player is { FamiliarMaster: not null } session) Release(session);
                else if (c.FamiliarController is { } owner) Release(owner);
            }
        };
    }

    public void Step(float dt) { }

    public static bool CanPossess(World world, Creature master, Creature familiar) =>
        master.Player is { IsDM: false, FamiliarMaster: null } s && s.Creature == master
        && !master.IsDead && !master.IsDestroyed && master.Commandable
        && !familiar.IsDead && !familiar.IsDestroyed && familiar.Player == null
        && familiar.Area != null && familiar.Area == master.Area
        && familiar.Master == master.Id && familiar.AssociateType == AssociateService.Familiar
        && AssociateService.Get(world, master, AssociateService.Familiar, 1) == familiar;

    public bool Possess(PlayerSession session, Creature familiar)
    {
        if (session.Creature is not { } master || !CanPossess(_world, master, familiar)) return false;
        session.FamiliarMaster = master;
        master.FamiliarController = session;
        master.PossessedFamiliar = familiar.Id;
        master.Player = null;
        familiar.Player = session;
        session.Creature = familiar;
        Stop(master); Stop(familiar);
        Publish(session, master, familiar);
        return true;
    }

    public void Release(PlayerSession session)
    {
        if (session.FamiliarMaster is not { } master) return;
        // A familiar still loading into another area arrives there before control returns.
        (_world.Output as GameServer)?.SettleAreaEntry(session);
        var familiar = session.Creature;
        session.FamiliarMaster = null;
        master.FamiliarController = null;
        master.PossessedFamiliar = GameObject.InvalidId;
        if (familiar != null && familiar != master) { familiar.Player = null; Stop(familiar); }
        master.Player = session;
        session.Creature = master;
        Stop(master);
        if (master.Area is { } area && session.Area != area && _world.Output is GameServer server)
            server.EnterArea(session, area, master.Position, master.Facing, alreadyPlaced: true);
        Publish(session, master, familiar);
    }

    // Native accepts the possessed familiar; the original master argument is a measured no-op.
    public void Unpossess(Creature creature)
    {
        if (creature.Player is { FamiliarMaster: not null } session) Release(session);
    }

    /// <summary>Ends possession whichever side <paramref name="creature"/> is on (the possessed familiar or its
    /// master), so damage and death rules see the master as a player character again.</summary>
    public void ReleaseControl(Creature creature)
    {
        if (creature.Player is { FamiliarMaster: not null } session) Release(session);
        else if (creature.FamiliarController is { } owner) Release(owner);
    }

    /// <summary>The connection a character belongs to, also while that player is controlling its familiar.</summary>
    public static PlayerSession? SessionOf(Creature creature) => creature.Player ?? creature.FamiliarController;

    void Stop(Creature c)
    {
        ActionQueues.Clear(_world, c);
        c.MoveDirection = Vector3.Zero;
        c.DriveInput = null;
        c.MoveGoal = null;
    }

    void Publish(PlayerSession session, Creature master, Creature? familiar)
    {
        _world.MarkChanged(master);
        if (familiar != null) _world.MarkChanged(familiar);
        session.Send(new ControlObjectMessage { ObjectId = session.Creature!.Id }.Write());
        _world.System<HudFeed>().Resend(session.Creature!);
        PartyService.Of(_world).Refresh(master);
    }

    /// <summary>Original characters, rather than the familiars they currently control.</summary>
    public static IEnumerable<Creature> Players(World world) => world.AllObjects.OfType<Creature>()
        .Where(c => c.IsPC && !c.IsDestroyed).Select(c => c.Player?.PlayerCreature ?? c)
        .Distinct().OrderBy(c => c.Id);

    [ClientRequest("possess-familiar")]
    static void RequestPossess(Request r)
    {
        if (r.Player.Player is not { } session || r.Obj<Creature>("target") is not { } familiar) return;
        // NWNX_ON_POSSESS_FAMILIAR; a skip keeps control where it is.
        var ev = Nwnx.NwnxEvent.Begin(r.World, "NWNX_ON_POSSESS_FAMILIAR", r.Player)?.Add("FAMILIAR", familiar);
        if (ev?.Before() == true) { ev.After(); return; }
        r.World.System<FamiliarPossession>().Possess(session, familiar);
        ev?.After();
    }

    [ClientRequest("unpossess-familiar")]
    static void RequestRelease(Request r)
    {
        // NWNX_ON_UNPOSSESS_FAMILIAR, OBJECT_SELF the player character; a skip keeps the familiar possessed.
        var master = r.Player.Player?.FamiliarMaster;
        var ev = master != null ? Nwnx.NwnxEvent.Begin(r.World, "NWNX_ON_UNPOSSESS_FAMILIAR", master)?.Add("FAMILIAR", r.Player) : null;
        if (ev?.Before() == true) { ev.After(); return; }
        r.World.System<FamiliarPossession>().Unpossess(r.Player);
        ev?.After();
    }
}

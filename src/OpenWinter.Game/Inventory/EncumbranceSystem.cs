using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Inventory;

/// <summary>
/// What a player character's load does to her (docs/ENCUMBRANCE.md). Measured on the original (visual scenes
/// encumberspeed and encumberdrive, parity suite encumbrance_speed):
/// <list type="bullet">
/// <item>Above encumbrance.2da Normal for her Strength (effects included) she is encumbered: "You are encumbered and
/// cannot run." (dialog.tlk 6632) and one movement stage (ruleset MOVEMENT_STAGE_PENALTY_ENCUMBRANCE_HEAVY); above Heavy,
/// "You are encumbered and cannot walk at normal speed." (6633) and two stages (..._OVERLOADED). Stages are counted
/// with stealth and detect mode's and each halves the speed below running (<see cref="Movement.MaxSpeed"/>).</item>
/// <item>The line comes on entering either state, also from overloaded back to encumbered; returning to normal says
/// nothing (as the encumbrance voice cue, InventorySystem.Voices). A filled box picked up straight past both limits
/// gave the original's two load lines.</item>
/// <item>Only players: the original keeps no carried weight for other creatures (GetWeight reads 0; a goblin carrying
/// five times its Heavy limit walks and runs at full speed).</item>
/// <item>A plot player is told but not slowed when she crosses a limit; one made plot while already slowed stays
/// slowed, so the stage is fixed when the state changes.</item>
/// </list>
/// Inventory changes update it at once (each one, so a pick-up that passes both limits gives both lines); changes
/// to Strength are picked up by <see cref="Step"/>.
/// </summary>
public sealed class EncumbranceSystem : IWorldSystem
{
    public const uint EncumberedLine = 6632, OverloadedLine = 6633;
    readonly World _world;
    public EncumbranceSystem(World world) { _world = world; }
    public int Order => 40;

    public void Step(float dt)
    {
        foreach (var pc in _world.ConnectedPCs) Update(pc);
    }

    /// <summary>Re-reads a player's load: on a change of state, sets her movement stage and tells her.</summary>
    public void Update(Creature c)
    {
        if (!c.IsPC || c.IsDestroyed) return;
        int state = _world.Inventory.Rules.Encumbrance(c);
        if (state == c.LoadState) return;
        int old = c.LoadState;
        c.LoadState = state;
        c.LoadStage = state == 0 || c.Plot ? 0 : Stage(_world, state);
        if (old < 0 || state == 0) return;
        // Straight from normal to overloaded (a filled box picked up) the original gives both lines (docs/VOICES.md).
        if (state == 1 || old == 0) _world.Output?.SendFeedback(c, _world.Resources.TlkString(EncumberedLine), FeedbackKind.Message, 8);
        if (state == 2) _world.Output?.SendFeedback(c, _world.Resources.TlkString(OverloadedLine), FeedbackKind.Message, 9);
    }

    /// <summary>The movement stages a load state costs (ruleset.2da; 1 and 2 in the installed game).</summary>
    public static int Stage(World world, int state) => state switch
    {
        1 => Rules.Ruleset.Int(world, "MOVEMENT_STAGE_PENALTY_ENCUMBRANCE_HEAVY", 1),
        2 => Rules.Ruleset.Int(world, "MOVEMENT_STAGE_PENALTY_ENCUMBRANCE_OVERLOADED", 2),
        _ => 0,
    };
}

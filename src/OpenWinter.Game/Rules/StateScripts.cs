using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>
/// What a charmed, confused, frightened, turned, stunned, held or sleeping creature does is decided by scripts, not by
/// the engine: on each heartbeat the engine runs the statescripts.2da SCRIPTNAME of the creature's state (stock:
/// nw_g0_charm, NW_G0_confuse, NW_G0_fear, x2_sig_state, NW_G0_sleep), player or not, whatever its own event
/// scripts are. Measured on nwserver with a statescripts.2da whose rows only log (parity suite state_scripts):
/// <list type="bullet">
/// <item>The row is the highest of the creature's states (confused + frightened runs row 3, charmed + confused row 2,
/// asleep + confused row 9). Dazed (5) and dominated (7) never run theirs, and while either is the highest row nothing
/// runs (dazed + confused, dominated + confused: no script at all).</item>
/// <item>It runs on every heartbeat (6.0-7.2 s apart), after the creature's OnHeartbeat; an associate with a master
/// skips its own OnHeartbeat while it has a state.</item>
/// <item>Charm, confusion, fear, turning, daze, stun, domination, paralysis, sleep, petrification and cutscene paralysis
/// bring the next heartbeat forward to the moment they land (knockdown and entangle don't), so the state script
/// runs at once.</item>
/// </list>
/// The stock scripts make the choices the original is known for (NW_G0_confuse: d10, 1 a random walk, 2-6 nothing,
/// 7-10 attack the nearest creature; NW_G0_fear: run from an enemy within 5 m; nw_g0_charm: the generic combat AI).
/// </summary>
public static class StateScripts
{
    /// <summary>statescripts.2da rows that never run on a heartbeat (dazed, dominated).</summary>
    public const int Dazed = 5, Dominated = 7;

    /// <summary>The creature's statescripts.2da row: the highest of its states, or -1 for none.</summary>
    public static int Row(Creature c)
    {
        int row = -1;
        foreach (var e in c.Effects)
        {
            int r = RowOf(e.Type);
            if (r > row) row = r;
        }
        return row;
    }

    /// <summary>The statescripts.2da row of an effect type (-1 for one that isn't a state).</summary>
    public static int RowOf(int effectType) => effectType switch
    {
        CombatK.EffectCharmed => 1,
        CombatK.EffectConfused => 2,
        CombatK.EffectFrightened => 3,
        CombatK.EffectTurned => 4,
        CombatK.EffectDazed => 5,
        CombatK.EffectStunned => 6,
        CombatK.EffectDominated => 7,
        CombatK.EffectParalyze => 8,
        CombatK.EffectSleep => 9,
        _ => -1,
    };

    /// <summary>Effect types whose arrival brings the creature's next heartbeat forward to now.</summary>
    public static bool ResetsHeartbeat(int effectType) => effectType is CombatK.EffectCharmed or CombatK.EffectConfused
        or CombatK.EffectFrightened or CombatK.EffectTurned or CombatK.EffectDazed or CombatK.EffectStunned
        or CombatK.EffectDominated or CombatK.EffectParalyze or CombatK.EffectSleep or CombatK.EffectPetrify
        or CombatK.EffectCutsceneParalyze;

    /// <summary>Runs the state script for the creature's state, if it has one that runs (after its OnHeartbeat).</summary>
    public static void Run(World world, Creature c)
    {
        if (world.Scripts == null || c.IsDead || c.IsDying || c.IsDestroyed) return;
        int row = Row(c);
        if (row < 0 || row == Dazed || row == Dominated) return;
        string script = world.Resources.TwoDA("statescripts").Get(row, "SCRIPTNAME");
        if (string.IsNullOrEmpty(script) || script == "****") return;
        world.Scripts.Run(script.ToLowerInvariant(), c.Id, new Scripting.EventContext());
    }
}

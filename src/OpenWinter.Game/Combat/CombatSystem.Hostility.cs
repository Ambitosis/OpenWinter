using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

public sealed partial class CombatSystem
{
    // Public SignalEvent/EffectDamage controls: combat_hostile_signal. Staggered triggers
    // clear independently at about eight seconds (native tick/poll resolution ~33 ms).
    const double ScriptHostilitySeconds = 8;

    /// <summary>
    /// Records a harmful script interaction without initiating attacks or changing their rounds.
    /// The caller supplies the request/application time, before any deferred event callback.
    /// </summary>
    public void RecordScriptHostility(GameObject? source, GameObject? target, double requestedAt, bool damage = false)
    {
        // Native evidence covers ordinary living pairs, and plot ones for scripted damage (trap_crit_ctl: a plot hitter's
        // EffectDamage kept a disarmer, plot or not, in combat). Keep self, dead, absent and cross-area actors, and plot
        // ones for harmful spell events, outside this path until their behavior is measured separately.
        if (source is not Creature a || target is not Creature b || a == b ||
            !damage && (a.Plot || b.Plot) || a.IsDead || b.IsDead || a.CurrentHitPoints <= 0 || b.CurrentHitPoints <= 0 ||
            a.IsDestroyed || b.IsDestroyed || _world.Get(a.Id) != a || _world.Get(b.Id) != b ||
            a.Area == null || a.Area != b.Area) return;
        double until = requestedAt + ScriptHostilitySeconds;
        if (until <= _world.Time) return;
        Enter(a, until);
        Enter(b, until);
    }

    void Enter(Creature c, double until)
    {
        var state = State(c);
        state.ScriptHostilityUntil = Math.Max(state.ScriptHostilityUntil, until);
        if (c.InCombat) return;
        c.InCombat = true;
        _world.MarkChanged(c);
    }
}

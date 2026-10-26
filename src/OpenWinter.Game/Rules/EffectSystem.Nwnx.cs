using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Functions;

namespace OpenWinter.Game.Rules;

/// <summary>
/// NWNX_ON_EFFECT_APPLIED / _REMOVED (parity suite nwnx_events_core): temporary and permanent effects other than
/// visual effects, each part of a link on its own. Application raises both halves around the effect taking hold, in
/// the applying script; removal raises them once the removing script is over. Neither can be skipped.
/// </summary>
public sealed partial class EffectSystem
{
    NwnxEvent? EffectEvent(string name, Effect e, GameObject target)
    {
        if (e.Hidden || e.Type == K.VisualEffect) return null;
        var ev = NwnxEvent.Begin(_world, name, target);
        if (ev == null) return null;
        var k = K;
        var (native, state) = NativeEffects.NativeType(k, e);
        var ints = state >= 0 ? new[] { state, 0, 0, 0, 0, 0, 0, 0 } : EffectFunctions.ReportedInts(k, e);
        if (e.Type == k.RunScript || e.Type == k.Regenerate)
        {
            if (e.Type == k.RunScript) { ints[0] = (int)MathF.Round(e.Floats[0] * 1000); ints[1] = 0; }
            (ints[2], ints[3]) = e.NextTick > 0 ? Calendar(e.NextTick) : (0, 0);
        }
        ev.Add("UNIQUE_ID", (int)e.UniqueId).AddObject("CREATOR", e.Creator).Add("TYPE", native).Add("SUB_TYPE", e.SubType & ~7)
            .Add("DURATION_TYPE", e.DurationType).Add("DURATION", e.DurationType == k.Temporary ? e.Duration : 0f)
            .Add("SPELL_ID", unchecked((uint)e.SpellId).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Add("CASTER_LEVEL", e.CasterLevel > 0 ? e.CasterLevel : -1).Add("CUSTOM_TAG", e.Tag);
        for (int i = 0; i < 8; i++) ev.Add("INT_PARAM_" + (i + 1), ints[i]);
        for (int i = 0; i < 4; i++) ev.Add("FLOAT_PARAM_" + (i + 1), i < e.Floats.Length && e.Type != k.RunScript ? e.Floats[i] : 0f);
        for (int i = 0; i < 6; i++) ev.Add("STRING_PARAM_" + (i + 1), i < e.Strings.Length ? e.Strings[i] ?? "" : "");
        for (int i = 0; i < 4; i++) ev.AddObject("OBJECT_PARAM_" + (i + 1), i < e.Objects.Length ? e.Objects[i] : GameObject.InvalidId);
        return ev;
    }

    (int Day, int Time) Calendar(double worldTime)
    {
        var clock = _world.Clock;
        long dayMs = 24 * clock.HourMs;
        long at = clock.TotalMs + (long)Math.Round(Math.Max(0, worldTime - _world.Time) * 1000);
        return ((int)(at / dayMs), (int)(at % dayMs));
    }

    /// <summary>The effect's application, between NWNX_ON_EFFECT_APPLIED_BEFORE and _AFTER.</summary>
    void Applying(Effect e, GameObject target, Action apply)
    {
        var ev = EffectEvent("NWNX_ON_EFFECT_APPLIED", e, target);
        ev?.Before();
        apply();
        ev?.After();
    }

    /// <summary>NWNX_ON_EFFECT_REMOVED_BEFORE and _AFTER, after the removing script.</summary>
    void Removed(Effect e, GameObject target)
    {
        var ev = EffectEvent("NWNX_ON_EFFECT_REMOVED", e, target);
        if (ev == null) return;
        _world.After(0, () =>
        {
            ev.Before();
            ev.After();
        }, persist: false);
    }

    /// <summary>NWNX_ON_HEAL for an EffectHeal landing (nwnx_events_items: a potion's heal, its creator healing).</summary>
    NwnxEvent? NwnxHeal(Effect e, GameObject target) =>
        NwnxEvent.Begin(_world, "NWNX_ON_HEAL", e.Creator)?.Add("TARGET_OBJECT_ID", target).Add("HEAL_AMOUNT", e.Ints[0]);
}

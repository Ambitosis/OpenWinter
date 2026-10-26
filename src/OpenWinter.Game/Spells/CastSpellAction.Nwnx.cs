using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Spells;

/// <summary>
/// NWNX_Events spell events, at the points the original raises them (parity suite nwnx_events_spells):
/// NWNX_ON_INPUT_CAST_SPELL when the cast is queued; NWNX_ON_BROADCAST_CAST_SPELL as conjuring starts (not for instant
/// or item casts); NWNX_ON_DECREMENT_SPELL_COUNT as the slot is spent at the end of conjuring (a skip keeps it);
/// NWNX_ON_CAST_SPELL at the release, before the impact runs (a skip stops the impact; ID whitelist by spell);
/// NWNX_ON_SPELL_INTERRUPTED for a failed concentration check (a skip lets the cast go on) and
/// NWNX_ON_SPELL_FAILED for a cast that is cancelled, queued or under way, or fails (not skippable).
/// Class data is the index of the casting class, 255 for none (cheat casts and items) and 254 for abilities.
/// </summary>
public sealed partial class CastSpellAction
{
    const int NoClass = 255, AbilityClass = 254;
    bool _failedEvent;

    static int ClassIndex(Creature? c, int classType)
    {
        if (c == null || classType < 0 || classType == NwRules.ClassInvalid) return NoClass;
        int i = c.Classes.FindIndex(x => x.Class == classType);
        return i < 0 ? NoClass : i;
    }

    int SlotClass(GameObject owner) => _slot == null ? NoClass : _slot.Kind == SlotKind.Ability ? AbilityClass
        : _slot.ClassIndex >= 0 ? _slot.ClassIndex : ClassIndex(owner as Creature, _slot.Class);

    System.Numerics.Vector3 TargetPoint(World world) => world.Get(Target) is { } t ? t.Position : TargetLocation.Position;

    /// <summary>
    /// NWNX_ON_INPUT_CAST_SPELL as the cast is queued (not for a spell the caster has no slot for). False when a
    /// subscriber skipped it (nothing is queued).
    /// </summary>
    public bool Queued(World world, GameObject owner)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_INPUT_CAST_SPELL", owner);
        if (ev == null) return true;
        int cls = 255;
        if (!Cheat && !Fake && !IsItem && owner is Creature c)
        {
            var slot = SpellSlots.Find(world, c, Spell, MetaMagic, Class, DomainLevel, Spontaneous, anyMetaInClass: FromPlayer);
            if (slot == null && FeatId < 0 && !FormAbility) return true;
            cls = slot == null ? ClassIndex(c, Class) : slot.Kind == SlotKind.Ability ? AbilityClass : slot.ClassIndex >= 0 ? slot.ClassIndex : ClassIndex(c, slot.Class);
        }
        bool area = world.Get(Target) == null;
        ev.AddObject("TARGET", area ? GameObject.InvalidId : Target).Add("SPELL_ID", Spell).Add("MULTICLASS", cls)
            .Add("DOMAIN_LEVEL", DomainLevel).Add("META_TYPE", MetaMagic == SpellSlots.MetaAny ? 0 : MetaMagic).Add("INSTANT", Instant)
            .Add("PROJECTILE_PATH", ProjectilePathType).Add("SPONTANEOUS", Spontaneous).Add("FAKE", Fake).Add("FEAT", FeatId)
            .Add("CASTER_LEVEL", CasterLevel >= 0 ? CasterLevel : 255).Add("IS_AREA_TARGET", area).Position("POS_", area ? TargetLocation.Position : world.Get(Target)!.Position);
        bool skipped = ev.Before();
        ev.After();
        return !skipped;
    }

    void BroadcastEvent(World world, GameObject owner)
    {
        if (Instant || IsItem) return;
        var ev = NwnxEvent.Begin(world, "NWNX_ON_BROADCAST_CAST_SPELL", owner);
        if (ev == null) return;
        bool area = world.Get(Target) == null;
        ev.Add("SPELL_ID", Spell).Add("MULTI_CLASS", SlotClass(owner)).Add("FEAT", FeatId >= 0 ? FeatId : 65535)
            .AddObject("TARGET_OBJECT_ID", area ? owner.Area?.Id ?? GameObject.InvalidId : Target).Position("TARGET_POSITION_", TargetPoint(world))
            .Add("SPELL_DOMAIN", _slot?.Domain == true ? _slot.SlotLevel : 0).Add("SPELL_SPONTANEOUS", _slot?.Spontaneous == true)
            .Add("SPELL_METAMAGIC", _slot?.Meta ?? 0).Add("PROJECTILE_PATH_TYPE", ProjectilePathType);
        ev.Before();
        ev.After();
    }

    /// <summary>NWNX_ON_DECREMENT_SPELL_COUNT around spending the slot. False when skipped (the slot is kept).</summary>
    bool DecrementEvent(World world, Creature c)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_DECREMENT_SPELL_COUNT", c);
        if (ev == null) { SpellSlots.Spend(world, c, _slot!); return true; }
        ev.Add("SPELL_ID", Spell).Add("CLASS", SlotClass(c)).Add("DOMAIN", _slot!.Domain ? _slot.SlotLevel : 0).Add("METAMAGIC", _slot.Meta)
            .Add("CASTERLEVEL", _slot.Kind == SlotKind.Ability && _slot.Ability is { } a ? a.CasterLevel : 255);
        bool skipped = ev.Before();
        if (!skipped) SpellSlots.Spend(world, c, _slot);
        ev.After();
        return !skipped;
    }

    /// <summary>NWNX_ON_CAST_SPELL at the release. False when skipped (the spell has no impact).</summary>
    bool CastEvent(World world, GameObject owner)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_CAST_SPELL", owner, "NWNX_ON_CAST_SPELL", Spell);
        if (ev == null) return true;
        ev.Add("SPELL_ID", Spell).Position("TARGET_POSITION_", TargetPoint(world)).AddObject("TARGET_OBJECT_ID", world.Get(Target) == null ? GameObject.InvalidId : Target)
            .AddObject("ITEM_OBJECT_ID", ItemId).Add("MULTI_CLASS", SlotClass(owner)).Add("SPELL_COUNTERED", false)
            .Add("COUNTERING_SPELL", false).Add("PROJECTILE_PATH_TYPE", ProjectilePathType).Add("IS_INSTANT_SPELL", Instant);
        bool skipped = ev.Before();
        ev.After();
        return !skipped;
    }

    /// <summary>NWNX_ON_SPELL_INTERRUPTED for a failed concentration check. False when skipped (casting goes on).</summary>
    bool InterruptedEvent(World world, GameObject owner)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_SPELL_INTERRUPTED", owner);
        if (ev == null) return true;
        ev.Add("SPELL_ID", Spell).Add("SPELL_CLASS", SlotClass(owner)).Add("SPELL_DOMAIN", _slot?.Domain == true ? _slot.SlotLevel : 0)
            .Add("SPELL_METAMAGIC", _slot?.Meta ?? 0).Add("SPELL_FEAT", FeatId).Add("SPELL_SPONTANEOUS", _slot?.Spontaneous == true);
        bool skipped = ev.Before();
        ev.After();
        return !skipped;
    }

    /// <summary>
    /// NWNX_ON_BROADCAST_SAFE_PROJECTILE for a spell's projectile (PROJECTILE_TYPE 6, or 7 along a script-chosen path;
    /// whitelists NWNX_ON_BROADCAST_SAFE_PROJECTILE_TYPE and _SPELL); a skip sends no projectile, the impact still lands.
    /// </summary>
    void SafeProjectile(World world, GameObject owner)
    {
        if (_flight <= 0.001f || !Plugins_Listening(world)) return;
        int type = ProjectilePathType != 0 ? 7 : 6;
        if (!Nwnx.Plugins.EventsPlugin.Allows(world, "NWNX_ON_BROADCAST_SAFE_PROJECTILE_TYPE", type)
            || !Nwnx.Plugins.EventsPlugin.Allows(world, "NWNX_ON_BROADCAST_SAFE_PROJECTILE_SPELL", Spell)) return;
        var ev = NwnxEvent.Begin(world, "NWNX_ON_BROADCAST_SAFE_PROJECTILE", owner);
        if (ev == null) return;
        ev.AddObject("TARGET_OBJECT_ID", world.Get(Target) == null ? GameObject.InvalidId : Target).Position("TARGET_POSITION_", TargetPoint(world))
            .Add("DELTA", (int)MathF.Round(_flight * 1000)).Add("PROJECTILE_TYPE", type).Add("SPELL_ID", Spell).Add("ATTACK_RESULT", 0)
            .Add("PROJECTILE_PATH_TYPE", ProjectilePathType);
        ev.Before();
        ev.After();
    }

    static bool Plugins_Listening(World world) => Nwnx.Plugins.EventsPlugin.Listening(world, "NWNX_ON_BROADCAST_SAFE_PROJECTILE");

    /// <summary>NWNX_EVENTS_SPELLFAIL_REASON_* for a lost cast.</summary>
    static int FailReason(string reason) => reason switch
    {
        "countered" => 1,
        "failure" or "deaf" => 3,
        "lost-target" => 4,
        "silenced" => 5,
        "defensive-casting" => 6,
        "entangled" => 7,
        "polymorphed" => 8,
        "interrupted" => 9,
        _ => 0,
    };

    /// <summary>NWNX_ON_SPELL_FAILED, once per cast.</summary>
    void FailedEvent(World world, GameObject owner, int reason)
    {
        if (_failedEvent || _released) return;
        _failedEvent = true;
        var ev = NwnxEvent.Begin(world, "NWNX_ON_SPELL_FAILED", owner, "NWNX_ON_SPELL_FAILED", Spell);
        if (ev == null) return;
        bool area = world.Get(Target) == null;
        ev.Add("SPELL_ID", Spell).Add("MULTI_CLASS", SlotClass(owner) == NoClass && _slot == null ? QueuedClass(world, owner) : SlotClass(owner))
            .Add("DOMAIN", _slot?.Domain == true ? _slot.SlotLevel : DomainLevel).Add("METAMAGIC", _slot?.Meta ?? 0).Add("FEAT", FeatId)
            .Add("SPELL_SPONTANEOUS", Spontaneous).Add("DEFENSIVELY_CAST", owner is Creature dc && dc.ActionModes.Contains(ActionModeDefensiveCast))
            .AddObject("TARGET_OBJECT_ID", area ? GameObject.InvalidId : Target).Position("TARGET_POSITION_", TargetPoint(world))
            .Add("IS_INSTANT_SPELL", Instant).Add("PROJECTILE_PATH_TYPE", ProjectilePathType).Add("CASTERLEVEL", CasterLevel >= 0 ? CasterLevel : 255)
            .Add("IS_FAKE", Fake).Add("REASON", reason);
        ev.Before();
        ev.After();
    }

    /// <summary>The class a cast that never began would have used.</summary>
    int QueuedClass(World world, GameObject owner)
    {
        if (Cheat || Fake || IsItem || owner is not Creature c) return NoClass;
        var slot = SpellSlots.Find(world, c, Spell, MetaMagic, Class, DomainLevel, Spontaneous, anyMetaInClass: FromPlayer);
        return slot == null ? NoClass : slot.Kind == SlotKind.Ability ? AbilityClass : slot.ClassIndex >= 0 ? slot.ClassIndex : ClassIndex(c, slot.Class);
    }
}

using System.Globalization;
using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;

namespace OpenWinter.Game.DungeonMaster;

/// <summary>
/// The DM tool actions (the DM client's right-click and toolbar commands). Each takes the DM's
/// session and acts with the DM's authority; each raises the matching NWNX_ON_DM_* BEFORE/AFTER
/// events (a skipped BEFORE cancels it). They return false when the action doesn't apply.
/// </summary>
public static class DMActions
{
    static DungeonMasterSystem DM(World w) => w.System<DungeonMasterSystem>();
    static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>The DM's avatar if this session is a DM (DM tools work while possessing too).</summary>
    static Creature? Dm(World w, PlayerSession s) => s.IsDM ? DM(w).Avatar(s) : null;

    /// <summary>Runs BEFORE, the action, then AFTER for a group action on one target.</summary>
    static bool Group(World w, PlayerSession s, string evt, GameObject target, Func<bool> act)
    {
        if (Dm(w, s) is not { } dm) return false;
        var data = DungeonMasterSystem.Targets(target);
        if (DM(w).Before(dm, evt, data)) return false;
        bool done = act();
        DM(w).After(dm, evt, data);
        return done;
    }

    static bool Single(World w, PlayerSession s, string evt, Dictionary<string, string> data, Func<bool> act)
    {
        if (Dm(w, s) is not { } dm) return false;
        if (DM(w).Before(dm, evt, data)) return false;
        bool done = act();
        DM(w).After(dm, evt, data);
        return done;
    }

    /// <summary>Heal: back to full hit points; the dead are raised first.</summary>
    public static bool Heal(World w, PlayerSession s, GameObject target) => Group(w, s, "HEAL", target, () =>
    {
        if (target is Creature { IsDead: true }) HitPoints.Resurrect(w, target);
        if (target is Creature c) CreatureRules.EnsureInit(w, c);
        HitPoints.Heal(w, target, Math.Max(0, HitPoints.Maximum(target) - HitPoints.Current(target)));
        return true;
    });

    /// <summary>Kill (plot and immortal creatures survive, as with EffectDeath).</summary>
    public static bool Kill(World w, PlayerSession s, GameObject target) => Group(w, s, "KILL", target, () =>
    {
        if (target is Creature { DMOwner: not null }) return false;
        HitPoints.Kill(w, target, Dm(w, s)?.Id ?? GameObject.InvalidId);
        return HitPoints.IsDead(target);
    });

    /// <summary>Toggle invulnerable: the plot flag.</summary>
    public static bool ToggleInvulnerable(World w, PlayerSession s, GameObject target) => Group(w, s, "TOGGLE_INVULNERABLE", target, () =>
    {
        target.Plot = !target.Plot;
        w.MarkChanged(target);
        return true;
    });

    /// <summary>Toggle immortal: takes damage but can't drop below 1 hit point (SetImmortal).</summary>
    public static bool ToggleImmortal(World w, PlayerSession s, GameObject target) => Group(w, s, "TOGGLE_IMMORTAL", target, () =>
    {
        if (target is not Creature c) return false;
        c.IsImmortal = !c.IsImmortal;
        return true;
    });

    /// <summary>Toggle AI: an AI-less creature stands still (GetAILevel reads AI_LEVEL_VERY_LOW).</summary>
    public static bool ToggleAI(World w, PlayerSession s, GameObject target) => Group(w, s, "TOGGLE_AI", target, () =>
    {
        if (target is not Creature { IsPC: false } c) return false;
        c.DMAIOff = !c.DMAIOff;
        c.AILevel = c.DMAIOff ? 0 : -1;
        if (c.DMAIOff) { ActionQueues.Clear(w, c); c.MoveDirection = Vector3.Zero; }
        return true;
    });

    public static bool ForceRest(World w, PlayerSession s, GameObject target) => Group(w, s, "FORCE_REST", target, () =>
    {
        if (target is not Creature { IsDead: false } c) return false;
        CreatureRules.Rest(w, c);
        return true;
    });

    public static bool Limbo(World w, PlayerSession s, GameObject target) =>
        Group(w, s, "LIMBO", target, () => DM(w).SendToLimbo(target));

    /// <summary>Lock or unlock a door or placeable.</summary>
    public static bool ToggleLock(World w, PlayerSession s, GameObject target) => Group(w, s, "TOGGLE_LOCK", target, () =>
    {
        switch (target)
        {
            case Door d: d.Locked = !d.Locked; break;
            case Placeable p: p.Locked = !p.Locked; break;
            default: return false;
        }
        w.MarkChanged(target);
        return true;
    });

    /// <summary>Destroys an object (never a player or DM).</summary>
    public static bool Destroy(World w, PlayerSession s, GameObject target)
    {
        if (target is Creature { IsPC: true } or Creature { DMOwner: not null } or Areas.Area or Module) return false;
        return Group(w, s, "DESTROY", target, () => { w.Destroy(target); return true; });
    }

    /// <summary>"Goto": the DM jumps to the target.</summary>
    public static bool JumpTo(World w, PlayerSession s, GameObject target) => Group(w, s, "GOTO", target, () =>
    {
        if (s.Creature is not { } me || target.Area == null) return false;
        ActionQueues.Clear(w, me);
        w.Jump(me, new Location(target.Area.Id, target.Position + Offset(target), target.FacingDegrees));
        return true;
    });

    /// <summary>Jump target to the DM ("summon").</summary>
    public static bool JumpHere(World w, PlayerSession s, GameObject target)
    {
        if (s.Creature is not { Area: { } area } me || target.Area == null || target == me) return false;
        var data = DungeonMasterSystem.Targets(target);
        data["TARGET_AREA"] = DungeonMasterSystem.Hex(area.Id);
        data["POS_X"] = F(me.Position.X); data["POS_Y"] = F(me.Position.Y); data["POS_Z"] = F(me.Position.Z);
        return Single(w, s, "JUMP_TARGET_TO_POINT", data, () =>
        {
            ActionQueues.Clear(w, target);
            w.Jump(target, new Location(area.Id, me.Position + new Vector3(1.2f, 0, 0), me.FacingDegrees));
            return true;
        });
    }

    /// <summary>The DM jumps to a point in an area.</summary>
    public static bool JumpToPoint(World w, PlayerSession s, Areas.Area area, Vector3 pos)
    {
        if (s.Creature is not { } me) return false;
        var data = new Dictionary<string, string>
        {
            ["TARGET_AREA"] = DungeonMasterSystem.Hex(area.Id), ["POS_X"] = F(pos.X), ["POS_Y"] = F(pos.Y), ["POS_Z"] = F(pos.Z),
        };
        return Single(w, s, "JUMP_TO_POINT", data, () =>
        {
            ActionQueues.Clear(w, me);
            w.Jump(me, new Location(area.Id, pos, me.FacingDegrees));
            return true;
        });
    }

    /// <summary>Every player (not DMs) jumps to a point.</summary>
    public static bool JumpAllPlayers(World w, PlayerSession s, Areas.Area area, Vector3 pos)
    {
        var data = new Dictionary<string, string>
        {
            ["TARGET_AREA"] = DungeonMasterSystem.Hex(area.Id), ["POS_X"] = F(pos.X), ["POS_Y"] = F(pos.Y), ["POS_Z"] = F(pos.Z),
        };
        return Single(w, s, "JUMP_ALL_PLAYERS_TO_POINT", data, () =>
        {
            foreach (var pc in w.ConnectedPCs.Where(p => !p.IsDMAvatar && p.Player?.IsDM != true).ToList())
            {
                ActionQueues.Clear(w, pc);
                w.Jump(pc, new Location(area.Id, pos, pc.FacingDegrees));
            }
            return true;
        });
    }

    static Vector3 Offset(GameObject target) => target is Creature ? new Vector3(1.2f, 0, 0) : new Vector3(1.6f, 0, 0);

    /// <summary>Gives (or with a negative amount takes) experience.</summary>
    public static bool GiveXP(World w, PlayerSession s, GameObject target, int amount)
    {
        if (target is not Creature c || amount == 0) return false;
        var data = new Dictionary<string, string> { ["AMOUNT"] = I(amount), ["TARGET"] = DungeonMasterSystem.Hex(c.Id) };
        return Single(w, s, "GIVE_XP", data, () =>
        {
            if (amount > 0) CreatureRules.GiveXP(w, c, amount);
            else CreatureRules.SetXP(w, c, c.Experience + amount);
            return true;
        });
    }

    /// <summary>Gives (or takes) levels: sets XP to what the new level needs.</summary>
    public static bool GiveLevel(World w, PlayerSession s, GameObject target, int amount)
    {
        if (target is not Creature c || amount == 0) return false;
        var data = new Dictionary<string, string> { ["AMOUNT"] = I(amount), ["TARGET"] = DungeonMasterSystem.Hex(c.Id) };
        return Single(w, s, "GIVE_LEVEL", data, () =>
        {
            int level = Math.Clamp(Math.Max(c.TotalLevel, CreatureRules.LevelForXP(w, c.Experience)) + amount, 1, CreatureRules.MaxLevel);
            CreatureRules.SetXP(w, c, CreatureRules.XPForLevel(w, level));
            return true;
        });
    }

    /// <summary>Gives (or with a negative amount takes) gold.</summary>
    public static bool GiveGold(World w, PlayerSession s, GameObject target, int amount)
    {
        if (target is not Creature c || amount == 0) return false;
        var data = new Dictionary<string, string> { ["AMOUNT"] = I(amount), ["TARGET"] = DungeonMasterSystem.Hex(c.Id) };
        return Single(w, s, "GIVE_GOLD", data, () => { w.Inventory.AddGold(c, amount); return true; });
    }

    /// <summary>Creates an item from a blueprint in the target's inventory.</summary>
    public static Item? GiveItem(World w, PlayerSession s, GameObject target, string resref)
    {
        if (Dm(w, s) is not { } dm || !w.Inventory.HasInventory(target) || resref.Length == 0) return null;
        var data = new Dictionary<string, string> { ["TARGET"] = DungeonMasterSystem.Hex(target.Id), ["RESREF"] = resref };
        if (DM(w).Before(dm, "GIVE_ITEM", data)) return null;
        var item = w.Inventory.CreateOn(resref, target, 1, "");
        if (item != null) data["ITEM"] = DungeonMasterSystem.Hex(item.Id);
        DM(w).After(dm, "GIVE_ITEM", data);
        return item;
    }

    /// <summary>Takes an item from whoever holds it into the DM avatar's inventory.</summary>
    public static bool TakeItem(World w, PlayerSession s, Item item)
    {
        if (Dm(w, s) is not { } dm || w.Inventory.Owner(item) is not { } owner || owner == dm) return false;
        var data = new Dictionary<string, string> { ["TARGET"] = DungeonMasterSystem.Hex(owner.Id), ["ITEM"] = DungeonMasterSystem.Hex(item.Id) };
        return Single(w, s, "TAKE_ITEM", data, () => w.Inventory.MoveTo(item, dm, dm) != null);
    }

    /// <summary>Plays a visual effect (visualeffects.2da row) on an object or at a point.</summary>
    public static bool Vfx(World w, PlayerSession s, GameObject? target, Areas.Area? area, Vector3 pos, int vfx)
    {
        if (Dm(w, s) == null || vfx < 0) return false;
        float[] zero = { 0, 0, 0 };
        if (target != null) w.Output?.Notify("vfx", target, new { vfx, miss = false, scale = 1f, translate = zero, rotate = zero, duration = 0f, id = 0L });
        else if (area != null)
            w.Output?.Notify("vfx", area, new { vfx, miss = false, scale = 1f, pos = new[] { pos.X, pos.Y, pos.Z }, facing = 0f, translate = zero, rotate = zero, duration = 0f, id = 0L });
        return true;
    }

    /// <summary>Mutes or unmutes a player's chat.</summary>
    public static bool SetMuted(World w, PlayerSession s, Creature pc, bool muted)
    {
        if (Dm(w, s) == null || pc.Player is not { } other || other.IsDM) return false;
        DM(w).SetMuted(other, muted);
        w.Output?.SendMessage(pc, muted ? "A Dungeon Master has muted you." : "A Dungeon Master has unmuted you.");
        return true;
    }

    /// <summary>Boots a player (BootPC).</summary>
    public static bool Boot(World w, PlayerSession s, Creature pc, string reason)
    {
        if (Dm(w, s) == null || pc.Player is not { } other || other == s) return false;
        w.Output?.Notify("boot", pc, new { reason }, pc);
        other.Connection.Disconnect();
        return true;
    }

    /// <summary>Sets the time of day (hour), moving to the next day for an earlier hour, like SetTime.</summary>
    public static bool SetTime(World w, PlayerSession s, int hour, int minute)
    {
        hour = Math.Clamp(hour, 0, 23); minute = Math.Clamp(minute, 0, 59);
        var data = new Dictionary<string, string> { ["HOUR"] = I(hour), ["MINUTE"] = I(minute), ["SECOND"] = "0", ["MILLISECOND"] = "0" };
        return Single(w, s, "SET_TIME", data, () =>
        {
            var k = w.Clock;
            long before = k.TotalMs;
            k.SetTime(hour, minute, 0, 0);
            w.SkipTime((k.TotalMs - before) / 1000.0);
            if (w.Output is GameServer server)
            {
                var msg = new TimeMessage
                {
                    Hour = k.Hour, Minute = k.Minute, Second = k.Second, MinutesPerHour = k.MinutesPerHour, DawnHour = k.DawnHour, DuskHour = k.DuskHour,
                }.Write();
                foreach (var session in server.Sessions) if (session.Area != null) session.Send(msg);
            }
            return true;
        });
    }

    /// <summary>Sets an area's weather (WEATHER_* 0 clear, 1 rain, 2 snow; -1 back to the area's own).</summary>
    public static bool SetWeather(World w, PlayerSession s, Areas.Area area, int weather)
    {
        if (Dm(w, s) == null || area.IsInterior) return false;
        var env = AreaSettings.Of(w, area);
        env.WeatherOverride = weather is >= 0 and <= 2 ? weather : -1;
        w.Output?.Notify("weather", area, new { weather = WeatherSystem.Current(area, env) });
        return true;
    }

    /// <summary>"Possess" through the NWNX-visible action path.</summary>
    public static bool Possess(World w, PlayerSession s, Creature target) => DM(w).Possess(s, target);
}

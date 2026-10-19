using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.DungeonMaster;

/// <summary>
/// Client requests from the DM client (all refused for non-DM sessions):
/// <c>dm-palette</c> {kind}, <c>dm-spawn</c> {kind, resref, pos, facing, into?},
/// <c>dm-action</c> {action, target?, amount?, value?, pos?, item?, reason?},
/// <c>dm-examine</c> {target}, <c>dm-limbo</c>, <c>dm-players</c>.
/// Answers come back as notifies of the same name (see docs/notify/dm.md).
/// </summary>
public static class DMRequests
{
    static PlayerSession? Dm(Request r) => r.Player.Player is { IsDM: true } s ? s : null;
    static DungeonMasterSystem DM(Request r) => r.World.System<DungeonMasterSystem>();

    [ClientRequest("dm-palette")]
    static void Palette(Request r)
    {
        if (Dm(r) == null || Palettes.Kind(r.Str("kind")) is not { } kind) return;
        object Node(PaletteNode n) => n.IsBlueprint
            ? new { n = n.Name, r = n.ResRef, cr = n.CR, f = n.Faction }
            : new { n = n.Name, c = n.Children.Select(Node).ToList() };
        r.World.Output?.Notify("dm-palette", r.Player, new { kind = kind.Key, title = kind.Title, tree = DM(r).Palettes.Tree(kind).Select(Node).ToList() }, r.Player);
    }

    [ClientRequest("dm-spawn")]
    static void Spawn(Request r)
    {
        if (Dm(r) is not { } s || r.Player.Area is not { } area) return;
        var pos = r.Vec("pos");
        if (area.Walkmesh.HighestGround(pos.X, pos.Y) is { } z && MathF.Abs(z - pos.Z) > 1.5f) pos.Z = z;
        var into = r.World.Get(r.Id("into"));
        var o = DMCreator.Spawn(r.World, s, r.Str("kind"), r.Str("resref"), area, pos, r.Float("facing") * MathF.PI / 180f, into);
        r.World.Output?.SendMessage(r.Player, o != null ? $"Created {o.Name.Resolve(r.World.Resources.Talk)} ({r.Str("resref")})." : $"Could not create {r.Str("resref")}.");
    }

    [ClientRequest("dm-action")]
    static void Action(Request r)
    {
        if (Dm(r) is not { } s) return;
        var w = r.World;
        var target = w.Get(r.Id("target"));
        var area = r.Player.Area;
        bool ok = r.Str("action") switch
        {
            "heal" when target != null => DMActions.Heal(w, s, target),
            "kill" when target != null => DMActions.Kill(w, s, target),
            "invulnerable" when target != null => DMActions.ToggleInvulnerable(w, s, target),
            "immortal" when target != null => DMActions.ToggleImmortal(w, s, target),
            "ai" when target != null => DMActions.ToggleAI(w, s, target),
            "rest" when target != null => DMActions.ForceRest(w, s, target),
            "limbo" when target != null => DMActions.Limbo(w, s, target),
            "lock" when target != null => DMActions.ToggleLock(w, s, target),
            "destroy" when target != null => DMActions.Destroy(w, s, target),
            "goto" when target != null => DMActions.JumpTo(w, s, target),
            "summon" when target != null => DMActions.JumpHere(w, s, target),
            "jump-point" when area != null => DMActions.JumpToPoint(w, s, area, r.Vec("pos")),
            "jump-all" when area != null => DMActions.JumpAllPlayers(w, s, area, r.Vec("pos")),
            "give-xp" when target != null => DMActions.GiveXP(w, s, target, r.Int("amount")),
            "give-level" when target != null => DMActions.GiveLevel(w, s, target, r.Int("amount", 1)),
            "give-gold" when target != null => DMActions.GiveGold(w, s, target, r.Int("amount")),
            "give-item" when target != null => DMActions.GiveItem(w, s, target, r.Str("resref")) != null,
            "take-item" => w.Get<Item>(r.Id("item")) is { } item && DMActions.TakeItem(w, s, item),
            "vfx" => DMActions.Vfx(w, s, target, area, r.Vec("pos"), r.Int("value", -1)),
            "possess" when target is Creature c => DM(r).Possess(s, c),
            "unpossess" => Unpossess(r, s),
            "appear" => Hidden(r, s, false),
            "disappear" => Hidden(r, s, true),
            "mute" when target is Creature pc => DMActions.SetMuted(w, s, pc, true),
            "unmute" when target is Creature pc => DMActions.SetMuted(w, s, pc, false),
            "boot" when target is Creature pc => DMActions.Boot(w, s, pc, r.Str("reason")),
            "set-time" => DMActions.SetTime(w, s, r.Int("value"), r.Int("minute")),
            "set-weather" when area != null => DMActions.SetWeather(w, s, area, r.Int("value", -1)),
            "from-limbo" when area != null => DM(r).FromLimbo(r.Id("target"), new Location(area.Id, r.Vec("pos") is { } p && p != Vector3.Zero ? p : r.Player.Position + new Vector3(1.5f, 0, 0), r.Player.FacingDegrees)),
            _ => false,
        };
        if (ok && target != null && r.Str("action") is not ("destroy" or "limbo")) Examine(w, r.Player, target, quiet: true);
        if (ok && r.Str("action") is "limbo" or "from-limbo") SendLimbo(r);
    }

    static bool Unpossess(Request r, PlayerSession s) { r.World.System<DungeonMasterSystem>().Unpossess(s); return true; }
    static bool Hidden(Request r, PlayerSession s, bool hidden) { r.World.System<DungeonMasterSystem>().SetHidden(s, hidden); return true; }

    [ClientRequest("dm-examine")]
    static void ExamineRequest(Request r)
    {
        if (Dm(r) == null || r.World.Get(r.Id("target")) is not { } o) return;
        Examine(r.World, r.Player, o, quiet: false);
    }

    /// <summary>Full DM view of an object: stats, flags, inventory and locals.</summary>
    static void Examine(World w, Creature dm, GameObject o, bool quiet)
    {
        var talk = w.Resources.Talk;
        var lines = new List<string>();
        var items = new List<object>();
        lines.Add($"Tag: {o.Tag}   ResRef: {o.ResRef}   Id: {o.Id:x}");
        lines.Add($"Area: {o.Area?.Name.Resolve(talk) ?? "(limbo)"}   Position: {o.Position.X:0.0}, {o.Position.Y:0.0}, {o.Position.Z:0.0}");
        if (o is Creature c)
        {
            CreatureRules.EnsureInit(w, c);
            var classes = w.Resources.TwoDA("classes");
            string cls = string.Join(" / ", c.Classes.Select(x => $"{w.Resources.TlkString(unchecked((uint)classes.GetInt(x.Class, "Name", 0)))} {x.Level}"));
            lines.Add($"Level {c.TotalLevel}: {cls}");
            lines.Add($"HP {c.CurrentHitPoints}/{c.MaxHitPoints}   AC {CreatureRules.ArmorClass(w, c).Total}   BAB {CreatureRules.BaseAttackBonus(w, c)}   CR {c.ChallengeRating:0.#}");
            string[] ab = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
            lines.Add(string.Join("  ", ab.Select((a, i) => $"{a} {CreatureRules.AbilityScore(w, c, i)}")));
            lines.Add($"Fort {CreatureRules.SavingThrow(w, c, 1)}  Ref {CreatureRules.SavingThrow(w, c, 2)}  Will {CreatureRules.SavingThrow(w, c, 3)}");
            lines.Add($"Alignment {c.LawfulChaotic}/{c.GoodEvil}   Faction {c.Faction}   XP {c.Experience:N0}   Gold {c.Gold:N0}");
            var flags = new List<string>();
            if (c.Plot) flags.Add("invulnerable");
            if (c.IsImmortal) flags.Add("immortal");
            if (c.IsDead) flags.Add("dead");
            if (c.DMAIOff) flags.Add("AI off");
            if (c.IsDMPossessed) flags.Add("possessed");
            if (c.DMOwner != null) flags.Add(c.DMHidden ? "DM (hidden)" : "DM (visible)");
            if (c.Player is { } p) flags.Add($"player '{p.PlayerName}'" + (w.System<DungeonMasterSystem>().IsMuted(p) ? " (muted)" : ""));
            if (c.StealthMode) flags.Add("stealth");
            if (flags.Count > 0) lines.Add("Flags: " + string.Join(", ", flags));
        }
        else
        {
            if (HitPoints.Maximum(o) > 0) lines.Add($"HP {HitPoints.Current(o)}/{HitPoints.Maximum(o)}");
            bool locked = o is Door { Locked: true } || o is Placeable { Locked: true };
            lines.Add((o.Plot ? "Plot  " : "") + (locked ? "Locked" : o is Door or Placeable ? "Unlocked" : ""));
        }
        if (w.Inventory.HasInventory(o))
            foreach (var item in w.Inventory.AllItems(o))
                items.Add(new { id = item.Id, name = item.Name.Resolve(talk), stack = item.StackSize, icon = w.Inventory.Icon(item) });
        foreach (var kv in o.Locals.Ints.Take(40)) lines.Add($"int {kv.Key} = {kv.Value}");
        foreach (var kv in o.Locals.Strings.Take(40)) lines.Add($"string {kv.Key} = \"{kv.Value}\"");
        foreach (var kv in o.Locals.Floats.Take(20)) lines.Add($"float {kv.Key} = {kv.Value}");
        w.Output?.Notify("dm-examine", o, new
        {
            target = o.Id,
            kind = o.Type.ToString().ToLowerInvariant(),
            name = o.Name.Resolve(talk),
            lines,
            items,
            quiet,
            plot = o.Plot,
            immortal = o is Creature { IsImmortal: true },
            aiOff = o is Creature { DMAIOff: true },
        }, dm);
    }

    [ClientRequest("dm-limbo")]
    static void LimboRequest(Request r) { if (Dm(r) != null) SendLimbo(r); }

    static void SendLimbo(Request r)
    {
        var talk = r.World.Resources.Talk;
        var list = r.World.System<DungeonMasterSystem>().Limbo.Select(id => r.World.Get(id)).Where(o => o != null)
            .Select(o => new { id = o!.Id, name = o.Name.Resolve(talk) }).ToList();
        r.World.Output?.Notify("dm-limbo", r.Player, new { creatures = list }, r.Player);
    }

    [ClientRequest("dm-players")]
    static void Players(Request r)
    {
        if (Dm(r) == null || r.World.Output is not GameServer server) return;
        var talk = r.World.Resources.Talk;
        var dms = r.World.System<DungeonMasterSystem>();
        var list = server.Sessions.Where(s => s.Creature != null).Select(s => new
        {
            id = s.Creature!.Id,
            name = s.Creature.Name.Resolve(talk),
            player = s.PlayerName,
            area = s.Area?.Name.Resolve(talk) ?? "",
            dm = s.IsDM,
            muted = dms.IsMuted(s),
        }).ToList();
        r.World.Output?.Notify("dm-players", r.Player, new { players = list }, r.Player);
    }
}

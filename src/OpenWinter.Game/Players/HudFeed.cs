using System.Text.Json;
using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Players;

/// <summary>
/// What the HUD shows that the client can't know on its own: the player's hit points and
/// experience, the party's health, and the health and attitude of the object the player has
/// targeted. Sent as a "hud" notify only when something changed.
/// </summary>
public sealed class HudFeed : IWorldSystem
{
    const float Interval = 0.25f;

    readonly World _world;
    readonly Dictionary<uint, uint> _targets = new();
    float _timer;
    sealed class QueueIdentity { public uint Id; }
    readonly ConditionalWeakTable<GameAction, QueueIdentity> _queueIds = new();
    uint _nextQueueId;
    uint QueueId(GameAction action) => _queueIds.GetValue(action, _ => new QueueIdentity { Id = ++_nextQueueId }).Id;

    public HudFeed(World world) { _world = world; }

    public int Order => 100;

    /// <summary>
    /// Sends the whole payload again at the next update: control returning to an unchanged creature, or a fresh area
    /// (a load included). What was last sent lives on the player's session, so a loaded save can't restore it.
    /// </summary>
    public void Resend(Creature controlled, bool now = true)
    {
        FamiliarPossession.SessionOf(controlled)?.LastHud.Remove(controlled.Id);
        if (now) _timer = 0;
    }

    public void Step(float dt)
    {
        _timer -= dt;
        if (_timer > 0 || _world.Output == null) return;
        _timer = Interval;
        foreach (var pc in _world.ConnectedPCs)
        {
            var json = JsonSerializer.Serialize(Build(pc));
            // A possessed familiar and its master share one session, each with its own payload.
            if (FamiliarPossession.SessionOf(pc) is not { } session || session.LastHud.TryGetValue(pc.Id, out var last) && last == json) continue;
            session.LastHud[pc.Id] = json;
            _world.Output.Notify("hud", pc, JsonDocument.Parse(json).RootElement, pc);
        }
    }

    object Build(Creature pc)
    {
        int level = pc.TotalLevel;
        var party = new List<object>();
        var members = FactionTable.IsPartyFaction(pc.Faction) ? PartyService.Of(_world).Members(pc.Faction) : [pc];
        foreach (var member in members)
        {
            if (member != pc) party.Add(Member(member));
            foreach (var a in AssociateService.AllAssociates(_world, member))
                if (a != pc) party.Add(Member(a));
        }
        return new
        {
            self = new
            {
                id = pc.Id, name = pc.DisplayName(_world.Resources.Talk), portrait = PortraitOf(pc),
                area = AreaName(pc), hp = pc.CurrentHitPoints, maxHp = pc.MaxHitPoints, tempHp = CreatureRules.TemporaryHitPoints(pc),
                dead = pc.IsDead, level, xp = pc.Experience,
                xpLevel = CreatureRules.XPForLevel(_world, level),
                xpNext = level < CreatureRules.MaxLevel ? CreatureRules.XPForLevel(_world, level + 1) : pc.Experience,
                canLevelUp = CreatureRules.CanLevelUp(_world, pc), gold = pc.Gold,
                queue = Queue(pc),
                effects = _world.System<EffectSystem>().PortraitIcons(pc),
            },
            party,
            target = Target(pc),
        };
    }

    object Member(Creature c) => new
    {
        id = c.Id, name = c.DisplayName(_world.Resources.Talk), portrait = PortraitOf(c), area = AreaName(c), hp = c.CurrentHitPoints,
        maxHp = c.MaxHitPoints, dead = c.IsDead, pc = c.IsPC, associate = c.AssociateType, master = c.Master,
    };

    object? Target(Creature pc)
    {
        if (!_targets.TryGetValue(pc.Id, out var id) || _world.Get(id) is not { } o || o.IsDestroyed || o.Area != pc.Area) return null;
        int hp, max;
        switch (o)
        {
            case Creature c: hp = c.CurrentHitPoints; max = c.MaxHitPoints; break;
            case Door d: hp = d.CurrentHitPoints; max = d.HitPoints; break;
            case Placeable p: hp = p.CurrentHitPoints; max = p.HitPoints; break;
            default: return null;
        }
        int rep = _world.Factions.ReputationOf(o, pc);
        string attitude = o is not Creature ? "object" : rep < 0 ? "neutral" : rep <= FactionTable.HostileTo ? "hostile" : rep >= FactionTable.FriendFrom ? "friendly" : "neutral";
        // Like the original, only the party sees exact numbers; everyone else gets a fraction.
        bool exact = o is Creature cr && (cr == pc || cr.Faction == pc.Faction);
        return new
        {
            id = o.Id, name = o.DisplayName(_world.Resources.Talk), portrait = o is Creature tc ? PortraitOf(tc) : "",
            health = max > 0 ? Math.Clamp(hp / (float)max, 0, 1) : 1, hp = exact ? hp : -1, maxHp = exact ? max : -1,
            level = o is Creature lc ? lc.TotalLevel : 0, attitude, dead = o is Creature { IsDead: true } || hp <= 0 && max > 0,
            plot = o.Plot,
        };
    }

    string PortraitOf(Creature c) => Portraits.Display(_world, c);

    /// <summary>The area a party member stands in, by name: the original's portrait hover text shows it under the name.</summary>
    string AreaName(Creature c) => c.Area?.DisplayName(_world.Resources.Talk) ?? "";

    /// <summary>The actions the player sees in the original's action queue: ACTION_* and, for casts, the spell.</summary>
    static List<GameAction> Visible(Creature pc) => pc.Actions.Where(a => a.ActionType is not (65535 or 36)).Take(QueueShown).ToList();
    const int QueueShown = 6;

    object[] Queue(Creature pc) => Visible(pc).Select(a =>
    {
        int spell = a switch
        {
            Spells.CastSpellAction c => c.Spell,
            Inventory.UseItemAction item when item.Property.Type == Inventory.ItemRules.PropCastSpell =>
                _world.Resources.TwoDA("iprp_spells").GetInt(item.Property.SubType, "SpellIndex", -1),
            Scripting.Functions.UseFeatAction feat => feat.SubFeat > 0 ? feat.SubFeat : _world.Resources.TwoDA("feat").GetInt(feat.Feat, "SPELLID", -1),
            _ => -1,
        };
        var table = Spells.SpellTable.Table(_world);
        int strref = spell >= 0 ? table.GetInt(spell, "Name", -1) : -1;
        return (object)new { id = QueueId(a), action = a.ActionType, spell,
            icon = spell >= 0 ? table.Get(spell, "IconResRef").ToLowerInvariant() : "",
            name = strref >= 0 ? _world.Resources.TlkString((uint)strref) : "" };
    }).ToArray();

    /// <summary>Cancels one shown action by stable identity (legacy index accepted), or explicitly clears the queue.</summary>
    [ClientRequest("clear-actions")]
    static void ClearActions(Request r)
    {
        var pc = r.Player;
        if (!ActionQueues.Accepts(pc)) return;
        int index = r.Int("index", -1);
        var shown = Visible(pc);
        GameAction? action;
        if (r.Element("id") != null)
            action = shown.FirstOrDefault(a => r.World.System<HudFeed>().QueueId(a) == r.Id("id"));
        else
        {
            if (index < 0) { ActionQueues.Clear(r.World, pc); return; }
            action = index < shown.Count ? shown[index] : null;
        }
        // Stale clicks never cancel the action that has since moved into this slot.
        if (action == null) return;
        // Native queuecommit: once the current cast has spent its slot, its icon
        // and recovery remain; clicking it must neither remove it nor its successors.
        if (ReferenceEquals(pc.Actions.FirstOrDefault(), action) && action.RetainsAfterQueueClear(r.World, pc)) return;
        action.Cancel(r.World, pc);
        pc.Actions.Remove(action);
    }

    /// <summary>The HUD's target frame: the object the player selected (0 clears it).</summary>
    [ClientRequest("hud-target")]
    static void SetTarget(Request r)
    {
        var feed = r.World.System<HudFeed>();
        uint id = r.Id("id");
        if (id == Objects.GameObject.InvalidId || id == 0) feed._targets.Remove(r.Player.Id);
        else feed._targets[r.Player.Id] = id;
        feed._timer = 0; // answer on the next tick
    }
}

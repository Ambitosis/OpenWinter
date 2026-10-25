using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Requests;

/// <summary>
/// Barter between two players (the original's ingame_barter window). One player asks ("barter-request"), the other
/// is asked ("barter-prompt") and answers ("barter-answer"). Then each puts backpack items and gold in their own
/// offer, presses Offer to lock it ("barter-lock"), and once both offers are locked presses Accept ("barter-accept").
/// When both have accepted, the items and gold change hands at once; Cancel, either player leaving, dying or going
/// away ends it with "Barter cancelled" for both.
/// </summary>
/// <remarks>
/// The original can't run two clients on one licence, so these rules are inferred, not measured: the window layout
/// is the game's ingame_barter.gui (the partner's items and Offer/Accept lamps on the left, the player's items with
/// Cancel/Offer/Accept on the right), and the feedback is the game's own strings (dialog.tlk 6794-6797, 6836,
/// 63821, 66221). See docs/BARTER.md.
/// </remarks>
public sealed class BarterService : IWorldSystem
{
    /// <summary>
    /// A barter ends when one of its players moves and is then farther than this from the other, in three dimensions
    /// (not measured: two original clients can't share a server).
    /// </summary>
    public const float MaxDistance = 10f;

    public sealed class Side
    {
        public required Creature Player;
        public readonly List<uint> Items = new();
        /// <summary>Each offered item's stack size when it was offered; a stack that changes leaves the offer.</summary>
        public readonly Dictionary<uint, int> Stacks = new();
        public int Gold;
        public bool Offered, Accepted;
        public System.Numerics.Vector3 LastPosition;
    }

    public sealed class Session
    {
        public required Side A, B;
        public Side Of(Creature c) => A.Player == c ? A : B;
        public Side Other(Creature c) => A.Player == c ? B : A;
    }

    readonly World _world;
    readonly Dictionary<uint, Session> _sessions = new(); // either player -> session
    readonly Dictionary<uint, uint> _asked = new();        // asked player -> asking player

    public BarterService(World world) { _world = world; }

    public Session? SessionOf(Creature pc) => _sessions.GetValueOrDefault(pc.Id);

    string Tlk(uint strref) => _world.Resources.TlkString(strref);
    string Name(Creature c) => c.DisplayName(_world.Resources.Talk);
    void Say(Creature pc, string text) => _world.Output?.SendMessage(pc, text);

    bool Busy(Creature pc) =>
        _sessions.ContainsKey(pc.Id) || _asked.ContainsKey(pc.Id) || _asked.ContainsValue(pc.Id)
        || _world.System<Conversations.ConversationService>().SessionOf(pc) != null
        || _world.Inventory.SessionOf(pc) != null;

    /// <summary>A player asks another to barter.</summary>
    public bool Ask(Creature from, Creature? to)
    {
        if (from.FamiliarController != null) { Say(from, Tlk(63821)); return false; }
        if (to == null || to == from || !to.IsPC || to.IsDead || to.IsDestroyed || to.Area != from.Area)
        {
            if (to != null && to != from) Say(from, Tlk(6794).Replace("<FirstName>", Name(to)));
            return false;
        }
        if (Busy(from)) { Say(from, Tlk(6796)); return false; }
        if (Busy(to)) { Say(from, Tlk(6795).Replace("<FirstName>", Name(to))); return false; }
        _asked[to.Id] = from.Id;
        _world.Output?.Notify("barter-prompt", from, new
        {
            from = from.Id, name = Name(from),
            text = Tlk(6836).Replace("<FullName>", Name(from)),
        }, to);
        return true;
    }

    /// <summary>The asked player answers.</summary>
    public void Answer(Creature to, uint fromId, bool accept)
    {
        if (!_asked.TryGetValue(to.Id, out var asking) || asking != fromId) return;
        _asked.Remove(to.Id);
        if (_world.Get<Creature>(fromId) is not { IsDestroyed: false } from || from.Area != to.Area || _sessions.ContainsKey(from.Id)) return;
        if (!accept) { Say(from, Tlk(6797).Replace("<FirstName>", Name(to))); return; }
        var s = new Session { A = new Side { Player = from }, B = new Side { Player = to } };
        _sessions[from.Id] = s;
        _sessions[to.Id] = s;
        Publish(s);
    }

    /// <summary>Puts an item of the player's backpack into her offer (not while it's locked).</summary>
    public bool Offer(Creature pc, Item item)
    {
        if (SessionOf(pc) is not { } s || s.Of(pc) is not { Offered: false } side) return false;
        if (!Tradeable(pc, item) || side.Items.Contains(item.Id)) return false;
        side.Items.Add(item.Id);
        side.Stacks[item.Id] = item.StackSize;
        Changed(s);
        return true;
    }

    public bool Withdraw(Creature pc, uint item)
    {
        if (SessionOf(pc) is not { } s || s.Of(pc) is not { Offered: false } side || !side.Items.Remove(item)) return false;
        side.Stacks.Remove(item);
        Changed(s);
        return true;
    }

    public bool SetGold(Creature pc, int gold)
    {
        if (SessionOf(pc) is not { } s || s.Of(pc) is not { Offered: false } side) return false;
        side.Gold = Math.Clamp(gold, 0, pc.Gold);
        Changed(s);
        return true;
    }

    /// <summary>The Offer button: locks the player's offer.</summary>
    public void Lock(Creature pc)
    {
        if (SessionOf(pc) is not { } s || s.Of(pc).Offered) return;
        s.Of(pc).Offered = true;
        Publish(s);
    }

    /// <summary>The Accept button: needs both offers locked; when both have accepted, the trade is made.</summary>
    public void Accept(Creature pc)
    {
        if (SessionOf(pc) is not { } s || !s.A.Offered || !s.B.Offered) return;
        s.Of(pc).Accepted = true;
        if (s.A.Accepted && s.B.Accepted) Complete(s);
        else Publish(s);
    }

    /// <summary>
    /// What ends a player's barter regardless of distance (not measured): a jump or teleport, taking
    /// damage (temporary hit points included), starting a conversation; dying is checked every step.
    /// </summary>
    public void Interrupt(Creature pc)
    {
        if (SessionOf(pc) is { } s) End(s, cancelled: true);
    }

    public void Cancel(Creature pc)
    {
        if (_asked.Remove(pc.Id)) return;
        foreach (var asked in _asked.Where(p => p.Value == pc.Id).Select(p => p.Key).ToList()) _asked.Remove(asked);
        if (SessionOf(pc) is { } s) End(s, cancelled: true);
    }

    bool Tradeable(Creature pc, Item item) =>
        !item.IsDestroyed && _world.Inventory.Owner(item) == pc && _world.Inventory.Holder(item) == pc
        && Inventory.InventorySystem.SlotOf(pc, item) < 0 && !item.Cursed && item.Droppable;

    /// <summary>An offered item still tradeable and the same stack the partner saw.</summary>
    bool Offerable(Side side, uint id) =>
        _world.Get<Item>(id) is { } item && Tradeable(side.Player, item) && side.Stacks.GetValueOrDefault(id) == item.StackSize;

    /// <summary>Any change to an offer takes back both players' acceptance.</summary>
    void Changed(Session s)
    {
        s.A.Accepted = s.B.Accepted = false;
        Publish(s);
    }

    void Complete(Session s)
    {
        // Everything must still be there; otherwise the offers are refreshed and nobody has accepted.
        foreach (var side in new[] { s.A, s.B })
        {
            bool stale = side.Gold > side.Player.Gold;
            foreach (var id in side.Items.ToList())
                if (!Offerable(side, id)) { side.Items.Remove(id); stale = true; }
            if (stale) { s.A.Offered = s.B.Offered = false; Changed(s); return; }
        }
        End(s, cancelled: false);
        var inv = _world.Inventory;
        void Give(Side from, Side to)
        {
            foreach (var id in from.Items)
                if (_world.Get<Item>(id) is { } item && inv.MoveTo(item, to.Player, actor: from.Player, source: from.Player) == null)
                    inv.Drop(item, new Location(to.Player.Area!.Id, to.Player.Position, to.Player.Facing));
            if (from.Gold > 0)
            {
                inv.AddGold(from.Player, -from.Gold);
                inv.AddGold(to.Player, from.Gold);
                // The game's gold lines, as for scripted gold ("Lost 25GP" / "Acquired 25GP", dialog.tlk 62490 / 62489).
                Say(from.Player, _world.TlkStringOverridden(62490).Replace("<CUSTOM0>", from.Gold.ToString()));
                Say(to.Player, _world.TlkStringOverridden(62489).Replace("<CUSTOM0>", from.Gold.ToString()));
            }
        }
        Give(s.A, s.B);
        Give(s.B, s.A);
    }

    void End(Session s, bool cancelled)
    {
        _sessions.Remove(s.A.Player.Id);
        _sessions.Remove(s.B.Player.Id);
        foreach (var pc in new[] { s.A.Player, s.B.Player })
        {
            if (pc.IsDestroyed) continue;
            _world.Output?.Notify("barter-close", pc, new { cancelled }, pc);
            if (cancelled) Say(pc, Tlk(66221));
        }
    }

    void Publish(Session s)
    {
        foreach (var pc in new[] { s.A.Player, s.B.Player })
            if (!pc.IsDestroyed) _world.Output?.Notify("barter-state", pc, View(s, pc), pc);
    }

    object View(Session s, Creature pc)
    {
        var inv = _world.Inventory;
        var talk = _world.Resources.Talk;
        object Offer(Side side) => new
        {
            id = side.Player.Id, name = Name(side.Player), portrait = Portraits.ResRef(_world, side.Player), gold = side.Gold,
            offered = side.Offered, accepted = side.Accepted,
            items = side.Items.Select(id => _world.Get<Item>(id)).Where(i => i != null).Select(i => new
            {
                id = i!.Id, name = i.Name.Resolve(talk), baseItem = i.BaseItem, stack = i.StackSize, icon = inv.Icon(i, pc),
                identified = i.Identified, info = inv.ItemInfo(i, pc),
            }).ToArray(),
        };
        return new { mine = Offer(s.Of(pc)), theirs = Offer(s.Other(pc)) };
    }

    public void Step(float dt)
    {
        foreach (var (asked, asking) in _asked.ToList())
            if (_world.Get<Creature>(asked) is not { IsDestroyed: false } a || _world.Get<Creature>(asking) is not { IsDestroyed: false } b || a.Area != b.Area)
                _asked.Remove(asked);
        foreach (var s in _sessions.Values.Distinct().ToList())
        {
            var (a, b) = (s.A.Player, s.B.Player);
            // The distance counts only when one of them has moved since the last check.
            bool moved = a.Position != s.A.LastPosition || b.Position != s.B.LastPosition;
            s.A.LastPosition = a.Position;
            s.B.LastPosition = b.Position;
            if (a.IsDestroyed || b.IsDestroyed || a.IsDead || b.IsDead || a.Area == null || a.Area != b.Area
                || moved && System.Numerics.Vector3.Distance(a.Position, b.Position) > MaxDistance)
            {
                End(s, cancelled: true);
                continue;
            }
            // An offered item that left the backpack leaves the offer.
            bool changed = false;
            foreach (var side in new[] { s.A, s.B })
                changed |= side.Items.RemoveAll(id => !Offerable(side, id)) > 0;
            if (changed) Changed(s);
        }
    }

    // ----- client requests -----

    [ClientRequest("barter-request")]
    static void Request(Request r) => r.World.System<BarterService>().Ask(r.Player, r.Obj<Creature>("target"));

    [ClientRequest("barter-answer")]
    static void AnswerRequest(Request r) => r.World.System<BarterService>().Answer(r.Player, r.Id("from"), r.Bool("accept"));

    [ClientRequest("barter-offer")]
    static void OfferRequest(Request r)
    {
        if (r.Obj<Item>("item") is { } item) r.World.System<BarterService>().Offer(r.Player, item);
    }

    [ClientRequest("barter-withdraw")]
    static void WithdrawRequest(Request r) => r.World.System<BarterService>().Withdraw(r.Player, r.Id("item"));

    [ClientRequest("barter-gold")]
    static void GoldRequest(Request r) => r.World.System<BarterService>().SetGold(r.Player, r.Int("gold"));

    [ClientRequest("barter-lock")]
    static void LockRequest(Request r) => r.World.System<BarterService>().Lock(r.Player);

    [ClientRequest("barter-accept")]
    static void AcceptRequest(Request r) => r.World.System<BarterService>().Accept(r.Player);

    [ClientRequest("barter-cancel")]
    static void CancelRequest(Request r) => r.World.System<BarterService>().Cancel(r.Player);
}

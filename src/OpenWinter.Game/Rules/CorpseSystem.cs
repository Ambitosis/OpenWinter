using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>
/// What happens to the dead: a destroyable creature's corpse lingers for its decay time
/// (DecayTime, ms, never less than five seconds), then fades; its droppable items are left behind in a body bag
/// (bodybag.2da: the creature's BodyBag row picks the bag's appearance and name). Lootable
/// bodies with something to drop remain and expose an invisible inventory after falling; with nothing to drop they
/// decay like any other. Empty bags disappear when closed. Player characters never decay.
/// </summary>
public sealed class CorpseSystem : IWorldSystem
{
    /// <summary>Body bag tag (scripts look for it, e.g. to clean up).</summary>
    public const string BodyBagTag = "BodyBag";
    const float ScanInterval = 0.5f;

    readonly World _world;
    readonly Dictionary<uint, double> _decayAt = new();
    readonly HashSet<uint> _lootPrepared = new();
    // Lootable bodies that had nothing to drop when their inventory would have opened: they decay as ordinary corpses.
    readonly HashSet<uint> _ordinary = new();
    public const string CorpseLink = "ELY_CORPSE_BODY";
    public const string BagLink = "ELY_CORPSE_LOOT";
    /// <summary>The shortest corpse lifetime: DecayTime 0 and 1000 last as long as the default 5000 (nwserver corpse_drops).</summary>
    public const double MinDecaySeconds = 5;

    public Placeable? LootFor(Creature corpse) => corpse.IsDead
        ? _world.Get<Placeable>(corpse.Locals.Objects.GetValueOrDefault(BagLink, GameObject.InvalidId)) : null;
    float _scan;

    public CorpseSystem(World world)
    {
        _world = world;
        world.ObjectRemoved += o => { if (o.IsDestroyed) { _decayAt.Remove(o.Id); _lootPrepared.Remove(o.Id); _ordinary.Remove(o.Id); } };
    }

    public void Step(float dt)
    {
        _scan -= dt;
        if (_scan > 0) return;
        _scan = ScanInterval;
        foreach (var c in _world.AllObjects.OfType<Creature>())
        {
            if (!c.IsDead) { _decayAt.Remove(c.Id); _lootPrepared.Remove(c.Id); _ordinary.Remove(c.Id); continue; }
            if (c.IsPC || c.FamiliarController != null || c.IsDestroyed || (!c.IsDestroyable && !c.Lootable) || _decayAt.ContainsKey(c.Id) || _lootPrepared.Contains(c.Id)) continue;
            _decayAt[c.Id] = _world.Time + (c.Lootable ? 1.5 : Lifetime(c));
        }
        foreach (var (id, at) in _decayAt.ToList())
        {
            if (_world.Time < at) continue;
            _decayAt.Remove(id);
            if (_world.Get<Creature>(id) is { IsDead: true, IsDestroyed: false, FamiliarController: null } corpse && !corpse.IsPC && (corpse.IsDestroyable || corpse.Lootable))
                Decay(corpse);
        }
    }

    /// <summary>
    /// How long an ordinary corpse lasts: its DecayTime, at least five seconds (every campaign creature has DecayTime 0
    /// and still lies there five seconds); three seconds after a spectacular death.
    /// </summary>
    public static double Lifetime(Creature c) => c.SpectacularDeath ? 3 : Math.Max(MinDecaySeconds, c.DecayTime / 1000.0);

    void Decay(Creature corpse)
    {
        var inv = _world.Inventory;
        // Creature weapons and hides (slots 14-17) are part of the creature, never loot.
        var equipped = corpse.Equipment.Where(kv => EquipSlot.Index(kv.Key) < 14).Select(kv => kv.Value);
        var drops = inv.Items(corpse).Concat(equipped).Where(i => i.Droppable && !i.Plot && !corpse.Equipment.Any(kv => kv.Value == i && EquipSlot.Index(kv.Key) >= 14)).Distinct().ToList();
        // A lootable body with nothing to drop is an ordinary corpse: it goes when its lifetime is up (nwserver
        // corpse_drops "flags": Lootable 1, nothing droppable, gone after five seconds with no bag).
        if (corpse.Lootable && drops.Count == 0 && !_ordinary.Contains(corpse.Id))
        {
            if (!corpse.IsDestroyable) { _lootPrepared.Add(corpse.Id); return; }
            _ordinary.Add(corpse.Id);
            _decayAt[corpse.Id] = _world.Time + Math.Max(0, Lifetime(corpse) - 1.5);
            return;
        }
        bool lootable = corpse.Lootable && !_ordinary.Contains(corpse.Id);
        _ordinary.Remove(corpse.Id);
        if (drops.Count > 0 && corpse.Area is { } area)
        {
            var bag = BodyBag(corpse);
            _world.Register(bag);
            _world.AddToArea(bag, area, corpse.Position, corpse.Facing);
            foreach (var item in drops) inv.MoveTo(item, bag, actor: corpse, merge: false);
            if (lootable)
            {
                bag.Locals.Objects[CorpseLink] = corpse.Id;
                corpse.Locals.Objects[BagLink] = bag.Id;
                _world.MarkChanged(corpse);
            }
        }
        if (lootable) _lootPrepared.Add(corpse.Id);
        else _world.Destroy(corpse);
    }

    Placeable BodyBag(Creature corpse)
    {
        var table = _world.Resources.TwoDA("bodybag");
        int row = corpse.BodyBag;
        int nameRow = row;
        // Default appearance is selected by appearance.2da (humanoids: pouch;
        // skeletons: bones). The default name remains "Remains" (TLK 40108).
        if (row <= 0 || row >= table.RowCount || table.GetInt(row, "Appearance", -1) < 0)
            row = _world.Resources.TwoDA("appearance").GetInt(corpse.AppearanceType, "BODY_BAG", 6);
        int strref = nameRow <= 0 ? 40108 : table.GetInt(nameRow, "Name", 40108);
        var bag = new Placeable
        {
            Tag = BodyBagTag,
            Name = new LocString { StrRef = strref },
            Appearance = corpse.Lootable ? 157 : table.GetInt(row, "Appearance", 200),
            Useable = true,
            HasInventory = true,
        };
        bag.Template.SetInt("Appearance", bag.Appearance);
        return bag;
    }

    /// <summary>Emptied body bags vanish when closed (called by the loot window close).</summary>
    public void Closed(Placeable container)
    {
        if (container.Tag != BodyBagTag || _world.Inventory.Items(container).Count != 0) return;
        if (_world.Get<Creature>(container.Locals.Objects.GetValueOrDefault(CorpseLink, GameObject.InvalidId)) is { } corpse)
        {
            corpse.Locals.Objects.Remove(BagLink);
            _world.MarkChanged(corpse);
        }
        _world.Destroy(container);
    }
}

using OpenWinter.Game.Objects;
using OpenWinter.Game.Spells;

namespace OpenWinter.Game.Requests;

/// <summary>Client requests for casting spells, counterspelling and resting.</summary>
static class SpellRequests
{
    /// <summary>
    /// The player casts a spell: {spell, target (object id) or pos [x,y,z], class (-1 any), meta (255 any),
    /// domain (0), spontaneous (bool)}. Uses the player's own slots (never a cheat cast).
    /// </summary>
    [ClientRequest("cast-spell")]
    static void CastSpell(Request r)
    {
        int spell = r.Int("spell", -1);
        if (!SpellTable.Exists(r.World, spell) || r.Player.Area == null) return;
        var action = new CastSpellAction
        {
            Spell = spell, MetaMagic = r.Int("meta", SpellSlots.MetaAny), Class = r.Int("class", -1),
            DomainLevel = r.Int("domain", 0), Spontaneous = r.Bool("spontaneous"), FromPlayer = true,
            FormAbility = r.Bool("form"),
        };
        // Current classic/modern HUD requests omit the domain argument. Resolve
        // that UI choice against the player's own prepared slots only; script
        // ActionCastSpell and an explicit domain=0 retain native exact semantics.
        if (r.Element("domain") == null && !action.Spontaneous)
            action.DomainLevel = SpellSlots.InferDomainLevel(r.World, r.Player, spell, action.MetaMagic, action.Class, anyMetaInClass: true);
        var target = r.World.Get(r.Id("target"));
        if (target != null)
        {
            // An item target is the player's own or one lying on the ground nearby, never another's inventory.
            if (target is Item item && (item.Area == null ? !r.Owns(item) : item.Area != r.Player.Area)) return;
            if (target is not Item && target.Area != r.Player.Area) return;
            action.Target = target.Id;
        }
        else if (r.Element("pos") != null) action.TargetLocation = new Location(r.Player.Area.Id, r.Vec("pos"), 0);
        else action.Target = r.Player.Id;
        ActionQueues.EnqueuePlayerTask(r.World, r.Player, action);
    }

    [ClientRequest("counterspell")]
    static void Counterspell(Request r)
    {
        if (!ActionQueues.Accepts(r.Player) || r.World.Get<Creature>(r.Id("target")) is not { } target || target.Area != r.Player.Area) return;
        ActionQueues.Clear(r.World, r.Player);
        r.Player.Actions.Add(new CounterSpellAction { Target = target.Id });
    }

    /// <summary>The rest button.</summary>
    [ClientRequest("rest")]
    static void Rest(Request r)
    {
        // Not while the player may not act (dying, dazed, a script's SetCommandable).
        if (!ActionQueues.Accepts(r.Player) || r.Player.IsDying) return;
        ActionQueues.Clear(r.World, r.Player);
        r.Player.Actions.Add(new RestAction());
    }
}

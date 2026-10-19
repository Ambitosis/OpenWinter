using System.Numerics;
using OpenWinter.Client.Gameplay;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Corpses light up under the pointer and the highlight key even when they can't be selected, as the original's do
/// (nwmain scene corpsehilite: the Prelude's dead goblins in Pavel's room, not selectable when dead, glow and read
/// "Dead Goblin / Dead" under Tab; docs/DEATH_AND_DYING.md "Highlighting corpses").
/// </summary>
public sealed class CorpseHighlightTests
{
    [Fact]
    public void EveryCorpseHighlightsButOnlySelectableOnesTakeClicks()
    {
        // The Prelude's m1q0ddeadgob: dead, not destroyable, not selectable when dead.
        Assert.True(PickRules.Highlightable(ObjectKind.Creature, ObjectFlags.Dead));
        Assert.False(PickRules.Selectable(ObjectKind.Creature, ObjectFlags.Dead));
        // SetIsDestroyable(FALSE, FALSE, TRUE), or a lootable body with something to take.
        Assert.True(PickRules.Highlightable(ObjectKind.Creature, ObjectFlags.Dead | ObjectFlags.Selectable));
        Assert.True(PickRules.Selectable(ObjectKind.Creature, ObjectFlags.Dead | ObjectFlags.Selectable));
        // The living, doors as always.
        Assert.True(PickRules.Highlightable(ObjectKind.Creature, ObjectFlags.Selectable));
        Assert.True(PickRules.Selectable(ObjectKind.Door, ObjectFlags.None));
        // Only creatures: a destroyed placeable that can't be used stays dark, a plain placeable too.
        Assert.False(PickRules.Highlightable(ObjectKind.Placeable, ObjectFlags.Dead | ObjectFlags.Static));
        Assert.False(PickRules.Highlightable(ObjectKind.Placeable, ObjectFlags.None));
    }

    [DataFact]
    public void TheServerMarksACorpseThatCantBeSelectedDeadSoTheClientLightsItUp()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Hilite");
        var w = f.Server.World;
        var pc = session.Creature!;
        Creature Corpse(float dx, bool selectable)
        {
            var c = w.Creatures.FromBlueprint("nw_goblina")!;
            c.Scripts.Clear();
            w.AddToArea(c, pc.Area!, pc.Position + new Vector3(dx, 2, 0), 0);
            CreatureRules.EnsureInit(w, c);
            // SetIsDestroyable(FALSE, FALSE, selectable), as the Prelude's goblins (FALSE) and the scene's SelGob (TRUE).
            c.IsDestroyable = false;
            c.SelectableWhenDead = selectable;
            return c;
        }
        var plain = Corpse(2, selectable: false);
        var chosen = Corpse(-2, selectable: true);
        ModuleTransitionTests.Fixture.Drain(client);
        HitPoints.Kill(w, plain, pc.Id);
        HitPoints.Kill(w, chosen, pc.Id);
        for (int i = 0; i < 40; i++) f.Server.Tick(.25f);
        Assert.False(plain.IsDestroyed || chosen.IsDestroyed);
        var flags = new Dictionary<uint, ObjectFlags>();
        foreach (var raw in ModuleTransitionTests.Fixture.Drain(client))
            if ((MessageType)raw[0] == MessageType.Update && UpdateMessage.Read(new MessageReader(raw)) is var u)
                flags[u.ObjectId] = u.Flags;
        Assert.True(PickRules.Highlightable(ObjectKind.Creature, flags[plain.Id]));
        Assert.False(PickRules.Selectable(ObjectKind.Creature, flags[plain.Id]));
        Assert.True(PickRules.Highlightable(ObjectKind.Creature, flags[chosen.Id]));
        Assert.True(PickRules.Selectable(ObjectKind.Creature, flags[chosen.Id]));
    }
}

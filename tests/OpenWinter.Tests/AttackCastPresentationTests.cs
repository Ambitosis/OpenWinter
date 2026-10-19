using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Spells;
using Xunit;

namespace OpenWinter.Tests;

public class AttackCastPresentationTests
{
    [DataFact]
    public void QueuedAttackCastUsesCurrentWeaponWithoutCreatingAnotherCombatAttack()
    {
        // A bow equipped after enqueue must work, while a removed/replaced bow must
        // not leave a stale shot. Shortbows share the measured table wield family;
        // crossbows, unarmed and instant variants retain their prior presentation.
        foreach (var sample in new[] { (6, 8, false, "bowshot"), (8, 11, false, "bowshot"),
                     (8, 6, false, ""), (8, -1, false, ""), (8, 8, true, "") })
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            var world = new World(resources);
            var area = new Area { ResRef = "attackcast", Width = 4, Height = 4 };
            world.Register(area); world.Areas.Add(area);
            var caster = new Creature { MaxHitPoints = 100, CurrentHitPoints = 100 };
            world.AddToArea(caster, area, new Vector3(10, 10, 0), 0);
            CreatureRules.EnsureInit(world, caster);
            var output = new Output(); world.Output = output;
            caster.Equipment[EquipSlot.RightHand] = new Item { BaseItem = sample.Item1 };
            var cast = new CastSpellAction { Spell = 601, Target = caster.Id, Cheat = true, Instant = sample.Item3 };
            caster.Actions.Add(cast);
            if (sample.Item2 < 0) caster.Equipment.Remove(EquipSlot.RightHand);
            else caster.Equipment[EquipSlot.RightHand] = new Item { BaseItem = sample.Item2 };
            world.Step(.01f);

            var note = Assert.Single(output.Notes, n => n.Kind == "spell-cast");
            Assert.Equal("attack", note.Data.GetProperty("animation").GetString());
            Assert.Equal(sample.Item4, note.Data.GetProperty("attackAnimation").GetString());
            Assert.DoesNotContain(output.Notes, n => n.Kind == "attack");
            if (!sample.Item3)
            {
                Assert.Equal(.5f, note.Data.GetProperty("duration").GetSingle());
                Assert.Equal(1f, note.Data.GetProperty("recovery").GetSingle());
                Assert.Null(caster.LastSpell); // Presentation must not execute impact early.
            }
        }
    }

    sealed class Output : IWorldOutput
    {
        public readonly List<(string Kind, JsonElement Data)> Notes = new();
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Notes.Add((kind, JsonSerializer.SerializeToElement(data)));
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
    }
}

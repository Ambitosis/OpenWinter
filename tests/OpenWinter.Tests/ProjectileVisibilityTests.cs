using System.Numerics;
using System.Text;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Spells;
using Xunit;

namespace OpenWinter.Tests;

public class ProjectileVisibilityTests
{
    [DataFact]
    public void HiddenProjectileRetainsItsFlightWhileModuleFlagControlsTheModel()
    {
        // Native projectileaxis: omitting HasProjectile hides the arrow without
        // removing its ~0.91s flight; adding 1 displays it with the same impact time.
        foreach (bool? visible in new bool?[] { null, false, true })
        {
            var resources = new ResMan(TestData.Paths);
            resources.Add(new Spells(visible)); resources.AddBaseGame(includeUserOverride: false);
            var world = new World(resources);
            var area = new Area { Width = 8, Height = 8 };
            world.Register(area); world.Areas.Add(area);
            var caster = new Creature { MaxHitPoints = 100, CurrentHitPoints = 100 };
            world.AddToArea(caster, area, new Vector3(10, 10, 0), 0);
            CreatureRules.EnsureInit(world, caster);
            var output = new Output(); world.Output = output;
            caster.Actions.Add(new CastSpellAction
            {
                Spell = 0, Cheat = true,
                TargetLocation = new Location(area.Id, new Vector3(30, 10, 0), 0)
            });
            world.Step(.01f);
            var projectile = Assert.Single(output.Projectiles);
            Assert.Equal(visible == true, projectile.GetProperty("showModel").GetBoolean());
            Assert.Equal("wamar_001", projectile.GetProperty("model").GetString());
            Assert.InRange(projectile.GetProperty("travel").GetSingle(), .91f, .92f);
            Assert.Equal(.5f, projectile.GetProperty("delay").GetSingle());
        }
    }

    sealed class Spells(bool? visible) : IResourceSource
    {
        static readonly ResKey Key = ResKey.Of("spells", ResType.TwoDA);
        public string Description => "Authored projectile visibility control";
        public IEnumerable<ResKey> Keys => new[] { Key };
        public bool Contains(ResKey key) => key == Key;
        public byte[]? Read(ResKey key) => key != Key ? null : Encoding.UTF8.GetBytes(
            "2DA V2.0\n\nLabel School Range TargetType Innate ConjTime CastAnim CastTime Proj ProjModel ProjType UseConcentration" +
            (visible.HasValue ? " HasProjectile" : "") + "\n0 Arrow V L 0x2f 1 0 area 1000 1 wamar_001 homing 0" +
            (visible.HasValue ? visible.Value ? " 1" : " 0" : "") + "\n");
    }

    sealed class Output : IWorldOutput
    {
        public readonly List<JsonElement> Projectiles = new();
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null)
        {
            if (kind == "spell-projectile") Projectiles.Add(JsonSerializer.SerializeToElement(data));
        }
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
    }
}

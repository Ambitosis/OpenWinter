using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Society;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Kill experience and the kill line, with the numbers the original gave in the killxp, killxp2 and killmsg scenes
/// (docs/KILL_XP.md).
/// </summary>
public class KillRewardTests
{
    sealed class Log : IWorldOutput
    {
        public readonly List<(string To, string Text)> Lines = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) => Lines.Add((pc.Tag, text));
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) { }
        public string[] To(string tag) => Lines.Where(l => l.To == tag).Select(l => l.Text).ToArray();
    }

    sealed class Scene
    {
        public readonly World World;
        public readonly Area Area;
        public readonly Log Log = new();
        public readonly Creature Pc;

        public Scene()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            Area = new Area { ResRef = "killxp", Width = 8, Height = 8 };
            World.Register(Area); World.Areas.Add(Area);
            World.Output = Log;
            // The harness's player: an elf wizard 1 (killxp start line).
            Pc = Make("Aluvian Darkstar", new(20, 20, 0), (10, 1), race: 1, player: true);
        }

        public Creature Make(string name, Vector3 at, (int Class, int Level) cls, float cr = 0, byte race = 6, bool player = false)
        {
            var c = World.Creatures.FromTemplate(new GffStruct());
            c.Classes.Add(new CreatureClass { Class = cls.Class, Level = cls.Level });
            c.Race = race; c.AppearanceType = 6; c.Tag = name;
            c.ChallengeRating = cr;
            c.MaxHitPoints = c.CurrentHitPoints = 1;
            if (player) c.Player = new PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };
            World.Register(c); CreatureRules.EnsureInit(World, c);
            c.MaxHitPoints = c.CurrentHitPoints = player ? 20 : 1;
            World.AddToArea(c, Area, at, 0, exact: true);
            return c;
        }

        /// <summary>A 1 hit point goblin of a challenge rating, at an offset from the player.</summary>
        public Creature Goblin(float cr, float dx = 2.5f, float dy = 0) => Make("Goblin", Pc.Position + new Vector3(dx, dy, 0), (12, 1), cr);

        public Creature Henchman(string name, float dx, float dy = 0)
        {
            var h = Make(name, Pc.Position + new Vector3(dx, dy, 0), (4, 1));
            h.MaxHitPoints = h.CurrentHitPoints = 200;
            AssociateService.Add(World, Pc, h, AssociateService.Henchman);
            return h;
        }

        /// <summary>The killer's EffectDamage, then a frame for the rewards that wait for the blow's own lines.</summary>
        public void Burn(Creature? killer, Creature victim)
        {
            var k = EffectConstants.Of(World);
            var e = new Effect { Type = k.Damage, DurationType = k.Instant, Creator = killer?.Id ?? GameObject.InvalidId };
            e.Ints[0] = 20; e.Ints[1] = 8;
            World.System<EffectSystem>().Apply(e, k.Instant, victim, 0);
            World.Step(0.05f);
        }

        public void Kill(Creature? killer, Creature victim)
        {
            HitPoints.Kill(World, victim, killer?.Id ?? GameObject.InvalidId);
            World.Step(0.05f);
        }
    }

    [DataFact]
    public void AmountsFollowTheTableByExperienceLevelRoundedDownAndScaled()
    {
        var s = new Scene();
        int At(int xp, float cr, int scale = 10, int players = 1, int associates = 0)
        {
            s.Pc.Experience = xp;
            return KillRewards.Amount(s.World, s.Pc, cr, scale, players, associates);
        }
        // Level 1, scale 10 (killxp p1, killxp2 cr): challenge ratings round half down and stay within 0..40.
        Assert.Equal(20, At(0, 0));
        Assert.Equal(20, At(0, 0.5f));
        Assert.Equal(50, At(0, 0.51f));
        Assert.Equal(50, At(0, 1.5f));
        Assert.Equal(96, At(0, 1.51f));
        Assert.Equal(96, At(0, 2.5f));
        Assert.Equal(144, At(0, 2.51f));
        Assert.Equal(252, At(0, 4.5f));
        Assert.Equal(20, At(0, -1));
        Assert.Equal(600, At(0, 60));
        // The row is the level the experience gives, not the hit dice; fractions are truncated (killxp2 rows).
        Assert.Equal(4, At(200000, 1));
        Assert.Equal(189, At(35999, 8));
        Assert.Equal(100, At(36000, 8));
        Assert.Equal(396, At(36000, 12));
        Assert.Equal(5, At(36000, 0));
        Assert.Equal(4, At(55000, 0));
        Assert.Equal(300, At(55000, 13));
        Assert.Equal(346, At(153000, 20));
        Assert.Equal(225, At(210000, 21));
        Assert.Equal(100, At(780000, 33));
        // Module XP scale is tenths (killxp p3, killxp2 scale).
        Assert.Equal(0, At(0, 1, scale: 0));
        Assert.Equal(5, At(0, 1, scale: 1));
        Assert.Equal(475, At(0, 3, scale: 33));
        Assert.Equal(150, At(36000, 8, scale: 15));
        Assert.Equal(1999, At(36000, 8, scale: 199));
        Assert.Equal(104, At(36000, 0, scale: 199));
        // Associates weigh a quarter of a player (killxp p4, killxp2 party).
        Assert.Equal(100, At(10000, 5, associates: 1));
        Assert.Equal(84, At(10000, 5, associates: 2));
        Assert.Equal(171, At(0, 5, associates: 3));
        Assert.Equal(150, At(0, 5, associates: 4));
        Assert.Equal(33, At(0, 1, associates: 2));
        Assert.Equal(28, At(0, 1, associates: 3));
    }

    [DataFact]
    public void ThePlayersKillPrintsTheDamageThenTheExperienceThenTheKill()
    {
        var s = new Scene();
        s.Burn(s.Pc, s.Goblin(1));
        Assert.Equal(new[] { "Aluvian Darkstar damages Goblin: 20 (20 Magical)", "Experience Points Gained:  50", "Aluvian Darkstar killed Goblin" },
            s.Log.To("Aluvian Darkstar"));
        // EffectDeath: no damage line (killxp p4 pc_effectdeath).
        s.Log.Lines.Clear();
        s.Pc.Experience = 10000;
        s.Kill(s.Pc, s.Goblin(5));
        Assert.Equal(new[] { "Experience Points Gained:  126", "Aluvian Darkstar killed Goblin" }, s.Log.To("Aluvian Darkstar"));
        Assert.Equal(10126, s.Pc.Experience);
    }

    [DataFact]
    public void AssociatesShareAndTheirKillsCountForTheirMaster()
    {
        var s = new Scene();
        s.Pc.Experience = 10000;
        var h1 = s.Henchman("Bandit", -3, 3);
        s.Burn(h1, s.Goblin(5));
        // The henchman's own lines reach its master; the master gets 80% and the henchman nothing (killxp p4).
        Assert.Equal(new[] { "Bandit damages Goblin: 20 (20 Magical)", "Experience Points Gained:  100", "Bandit killed Goblin" },
            s.Log.To("Aluvian Darkstar"));
        Assert.Equal(0, h1.Experience);
        var h10 = s.Henchman("Fighter", -3, -3);
        s.Log.Lines.Clear();
        s.Kill(s.Pc, s.Goblin(5));
        Assert.Equal("Experience Points Gained:  84", s.Log.To("Aluvian Darkstar")[0]);
        // 41 m from the victim is out of the share; the killer always counts, wherever it is (killxp2 dist).
        h10.Position = s.Pc.Position + new Vector3(43.5f, 0, 0);
        s.Log.Lines.Clear();
        s.Kill(s.Pc, s.Goblin(5));
        Assert.Equal("Experience Points Gained:  100", s.Log.To("Aluvian Darkstar")[0]);
        h10.Position = s.Pc.Position + new Vector3(42.5f, 0, 0);
        s.Log.Lines.Clear();
        s.Kill(s.Pc, s.Goblin(5));
        Assert.Equal("Experience Points Gained:  84", s.Log.To("Aluvian Darkstar")[0]);
        h10.Position = s.Pc.Position + new Vector3(55, 0, 0);
        s.Log.Lines.Clear();
        s.Kill(h10, s.Goblin(5));
        Assert.Equal(new[] { "Experience Points Gained:  84", "Fighter killed Goblin" }, s.Log.To("Aluvian Darkstar"));
        // A henchman's kill far from the player gives her nothing and tells her nothing, not even its damage
        // (killmsg henchman_far).
        s.Log.Lines.Clear();
        int before = s.Pc.Experience;
        s.Burn(h10, s.Goblin(5, 54, 0));
        Assert.Empty(s.Log.To("Aluvian Darkstar"));
        Assert.Equal(before, s.Pc.Experience);
    }

    [DataFact]
    public void BystandersTheModuleAndPartyVictimsGiveNothing()
    {
        var s = new Scene();
        var bystander = s.Make("Commoner", s.Pc.Position + new Vector3(0, 4, 0), (4, 1));
        s.Burn(bystander, s.Goblin(5));
        s.Kill(null, s.Goblin(5));
        Assert.Empty(s.Log.To("Aluvian Darkstar"));
        Assert.Equal(0, s.Pc.Experience);
        // Her own henchman: the line, no experience (killxp p4 kill_own_henchman).
        var h = s.Henchman("Bandit", -3, 3);
        s.Kill(s.Pc, h);
        Assert.Equal(new[] { "Aluvian Darkstar killed Bandit" }, s.Log.To("Aluvian Darkstar"));
        // A goblin killing the henchman tells her its damage but not the kill (killmsg enemy_kills_henchman).
        var h2 = s.Henchman("Guard", -3, -3);
        s.Log.Lines.Clear();
        s.Burn(s.Goblin(1), h2);
        Assert.Equal(new[] { "Goblin damages Guard: 20 (20 Magical)" }, s.Log.To("Aluvian Darkstar"));
        // Scale 0: the line without experience (killxp p3 scale0).
        s.World.XPScale = 0;
        s.Log.Lines.Clear();
        s.Kill(s.Pc, s.Goblin(5));
        Assert.Equal(new[] { "Aluvian Darkstar killed Goblin" }, s.Log.To("Aluvian Darkstar"));
        Assert.Equal(0, s.Pc.Experience);
    }

    [DataFact]
    public void AKilledPlayerIsToldWhoKilledHer()
    {
        var s = new Scene();
        var goblin = s.Goblin(1);
        s.Pc.MaxHitPoints = s.Pc.CurrentHitPoints = 6;
        var k = EffectConstants.Of(s.World);
        var e = new Effect { Type = k.Damage, DurationType = k.Instant, Creator = goblin.Id };
        e.Ints[0] = 100; e.Ints[1] = 256;
        s.World.System<EffectSystem>().Apply(e, k.Instant, s.Pc, 0);
        s.World.Step(0.05f);
        Assert.Equal(new[] { "Goblin damages Aluvian Darkstar: 100 (100 Fire)", "Goblin killed Aluvian Darkstar" }, s.Log.To("Aluvian Darkstar"));
    }

    [DataFact]
    public void TheMulticlassPenaltyTakesAFifthOffKillsAndGiveXp()
    {
        var s = new Scene();
        // Elf wizard 1 / rogue 1 / fighter 3: rogue is more than a level behind fighter; wizard is the elf's favoured class.
        s.Pc.Classes.Add(new CreatureClass { Class = 8, Level = 1 });
        s.Pc.Classes.Add(new CreatureClass { Class = 4, Level = 3 });
        Assert.Equal(0.8m, CreatureRules.MulticlassXPFactor(s.World, s.Pc));
        s.Pc.Experience = 10000;
        CreatureRules.GiveXP(s.World, s.Pc, 100, multiclassPenalty: true);
        Assert.Equal(10080, s.Pc.Experience);
        CreatureRules.GiveXP(s.World, s.Pc, 7, multiclassPenalty: true);
        Assert.Equal(10085, s.Pc.Experience);
        s.Pc.Experience = 10000;
        s.Kill(s.Pc, s.Goblin(5));
        Assert.Equal(10100, s.Pc.Experience);
        // Wizard 36 / rogue 1: one class besides the favoured one, no penalty (killxp p5 L37).
        var elf = s.Make("Elf", new(5, 5, 0), (10, 36), race: 1);
        elf.Classes.Add(new CreatureClass { Class = 8, Level = 1 });
        Assert.Equal(1m, CreatureRules.MulticlassXPFactor(s.World, elf));
        // A human's highest class is the favoured one.
        var human = s.Make("Human", new(6, 6, 0), (4, 5), race: 6);
        human.Classes.Add(new CreatureClass { Class = 8, Level = 1 });
        Assert.Equal(1m, CreatureRules.MulticlassXPFactor(s.World, human));
        human.Classes.Add(new CreatureClass { Class = 0, Level = 3 });
        Assert.Equal(0.8m, CreatureRules.MulticlassXPFactor(s.World, human));
    }

    /// <summary>killxp3 (measured at difficulties 1 and 4 alike).</summary>
    [DataFact]
    public void RoundingSinglePrecisionAssociatesAndTheDead()
    {
        var s = new Scene();
        int At(int xp, float cr, int scale = 10)
        {
            s.Pc.Experience = xp;
            return KillRewards.Amount(s.World, s.Pc, cr, scale, 1, 0);
        }
        // The column is the challenge rating + 0.49, truncated: 2.505 and 2.509 take C2, 0.505 C0, 1.505 C1.
        Assert.Equal(96, At(0, 2.505f));
        Assert.Equal(96, At(0, 2.509f));
        Assert.Equal(20, At(0, 0.505f));
        Assert.Equal(50, At(0, 1.505f));
        // Single precision: 50 x 2.1 is 104.99999 (104), 50 x 4.2 209, 50 x 4.7 234; 90 x 1.3 at level 2 116.
        Assert.Equal(104, At(0, 1, 21));
        Assert.Equal(209, At(0, 1, 42));
        Assert.Equal(234, At(0, 1, 47));
        Assert.Equal(116, At(1000, 2, 13));
        // A kill that leaves a level-up waiting.
        s.World.XPScale = 13;
        s.Pc.Experience = 1000;
        s.Kill(s.Pc, s.Goblin(2));
        Assert.Equal(1116, s.Pc.Experience);
        s.World.XPScale = 10;
        // The row is the counted players' average level (truncated).
        var other = s.Make("Other", s.Pc.Position, (10, 6), player: true);
        s.Pc.Experience = 10000;
        Assert.Equal(5, KillRewards.PartyLevel(s.World, new[] { s.Pc, other }));

        s.Pc.Experience = 0;
        s.World.Destroy(other);
        // A hostile creature's summoned creature gives nothing; its henchman gives full experience.
        var boss = s.Make("Boss", s.Pc.Position + new Vector3(6, 3, 0), (4, 1));
        var badger = s.Goblin(3);
        AssociateService.Add(s.World, boss, badger, AssociateService.Summoned);
        s.Kill(s.Pc, badger);
        Assert.Equal(0, s.Pc.Experience);
        var guard = s.Goblin(5);
        AssociateService.Add(s.World, boss, guard, AssociateService.Henchman);
        s.Kill(s.Pc, guard);
        Assert.Equal(300, s.Pc.Experience);
        // A dead henchman beside the victim takes no share.
        var h = s.Henchman("Bandit", -2, 2);
        s.Pc.Experience = 0;
        s.Kill(s.Pc, s.Goblin(5));
        Assert.Equal(240, s.Pc.Experience);
        h.IsDead = true;
        s.Pc.Experience = 0;
        s.Kill(s.Pc, s.Goblin(5));
        Assert.Equal(300, s.Pc.Experience);
    }
}

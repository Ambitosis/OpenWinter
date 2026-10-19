using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Game.Spells;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// A player dying, bleeding, dying and coming back, as the original did in the dying, dyingbleed and dyingfx scenes
/// (docs/DEATH_AND_DYING.md).
/// </summary>
public sealed class DeathAndDyingTests
{
    sealed class Log : IWorldOutput
    {
        public readonly List<string> Lines = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) => Lines.Add(text);
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) { }
    }

    sealed class Game
    {
        public readonly World World;
        public readonly Creature Pc;
        public readonly Area Area;
        public readonly Log Log = new();
        public readonly EffectConstants K;

        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            World.Factions.Load(resources);
            World.EnableScripts();
            World.Output = Log;
            K = EffectConstants.Of(World);
            Area = new Area { ResRef = "dying", Width = 8, Height = 8 };
            World.Register(Area); World.Areas.Add(Area);
            Pc = World.Creatures.FromTemplate(new GffStruct());
            Pc.Classes.Add(new CreatureClass { Class = 9, Level = 1 });
            Pc.Race = 1; Pc.Tag = "pc";
            for (int i = 0; i < Pc.Abilities.Length; i++) Pc.Abilities[i] = 10;
            Pc.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, Creature = Pc };
            World.AddToArea(Pc, Area, new Vector3(20, 20, 0), 0, exact: true);
            CreatureRules.EnsureInit(World, Pc);
            Pc.BaseHitPoints = 5; Pc.HitPointOffset = 0;
            Pc.MaxHitPoints = 5; Pc.CurrentHitPoints = 5;
            Script("dy_dying", "void main(){object pc=GetLastPlayerDying();SetLocalInt(pc,\"dyings\",GetLocalInt(pc,\"dyings\")+1);}");
            Script("dy_death", "void main(){object pc=GetLastPlayerDied();SetLocalInt(pc,\"deaths\",GetLocalInt(pc,\"deaths\")+1);SetLocalInt(pc,\"deathhp\",GetCurrentHitPoints(pc));}");
            Script("dy_respawn", "void main(){object pc=GetLastRespawnButtonPresser();SetLocalInt(pc,\"respawns\",GetLocalInt(pc,\"respawns\")+1);}");
            World.Module.Scripts["Mod_OnPlrDying"] = "dy_dying";
            World.Module.Scripts["Mod_OnPlrDeath"] = "dy_death";
            World.Module.Scripts["Mod_OnSpawnBtnDn"] = "dy_respawn";
        }

        public void Script(string name, string source)
        {
            var bytes = NssCompiler.Compile(source, name, World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(bytes != null, error);
            World.Scripts.Register(name, new NcsProgram(name, bytes!));
        }

        public void Run(string source)
        {
            Script("dy_run", source);
            World.Scripts!.Run("dy_run", Pc.Id);
            Assert.False(World.Scripts.LastRunFailed);
        }

        public void Steps(float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / 30) World.Step(1f / 30);
        }

        /// <summary>Magical damage (no creature behind it) down to <paramref name="to"/> hit points.</summary>
        public void HurtTo(int to) => HitPoints.Damage(World, Pc, GameObject.InvalidId, Pc.CurrentHitPoints - to, 8);

        public void Heal(int amount) =>
            World.System<EffectSystem>().Apply(new Effect { Type = K.Heal, Ints = { [0] = amount } }, K.Instant, Pc, 0);

        public Effect Bleed(float change, float duration = 0)
        {
            var e = new Effect { Type = K.HitPointChangeWhenDying, Floats = { [0] = change } };
            World.System<EffectSystem>().Apply(e, duration > 0 ? K.Temporary : K.Permanent, Pc, duration);
            return Pc.Effects.Single(x => x.Type == K.HitPointChangeWhenDying);
        }

        public void BleedInstant(float change) =>
            World.System<EffectSystem>().Apply(new Effect { Type = K.HitPointChangeWhenDying, Floats = { [0] = change } }, K.Instant, Pc, 0);

        public void Strip() => World.System<EffectSystem>().RemoveWhere(Pc, e => e.Type == K.HitPointChangeWhenDying);

        public int Local(string name) => Pc.Locals.Ints.GetValueOrDefault(name);
    }

    [DataFact]
    public void DyingPlayerLiesUncommandableUntilHealed()
    {
        var g = new Game();
        g.Pc.Actions.Add(new MoveAction { Target = new Vector3(30, 20, 0) });
        g.HurtTo(-3);
        // The original: uncommandable at once, actions gone, GetIsDead true, OnPlayerDying once.
        Assert.True(g.Pc.IsDying);
        Assert.False(g.Pc.Commandable);
        Assert.Empty(g.Pc.Actions);
        g.Steps(0.2f);
        Assert.Equal(1, g.Local("dyings"));
        // Orders are refused while dying, and nothing bleeds by itself.
        g.Run("void main(){ActionMoveToLocation(Location(GetArea(OBJECT_SELF),Vector(30.0,20.0,0.0),0.0));SetLocalInt(OBJECT_SELF,\"dead\",GetIsDead(OBJECT_SELF));}");
        g.Steps(13f);
        Assert.Equal(-3, g.Pc.CurrentHitPoints);
        Assert.Equal(new Vector3(20, 20, 0), g.Pc.Position);
        Assert.Equal(1, g.Local("dead"));
        // Damage while dying lowers hit points without another dying event.
        g.HurtTo(-5);
        g.Steps(0.2f);
        Assert.Equal(1, g.Local("dyings"));
        // Healing from -5 by 8 leaves 3 and gives control back; "Healed" is told.
        g.Heal(8);
        Assert.Equal(3, g.Pc.CurrentHitPoints);
        Assert.True(g.Pc.Commandable);
        Assert.False(g.Pc.IsDying);
        Assert.Contains(" : Healed 8 hit points.", g.Log.Lines.Last());
    }

    [DataFact]
    public void PlayerBledToMinusTenDiesAtMinusEleven()
    {
        var g = new Game();
        g.HurtTo(-8);
        g.Bleed(-1f, 2f);
        g.Steps(2.2f);
        Assert.Equal(-9, g.Pc.CurrentHitPoints);
        g.Steps(2f);
        // The tick to -10 kills; the player reads -11 and the effect goes without a last change (dyingfx G4).
        Assert.True(g.Pc.IsDead);
        g.Steps(0.2f);
        Assert.Equal(1, g.Local("deaths"));
        Assert.Equal(-11, g.Local("deathhp"));
        Assert.Equal(-11, g.Pc.CurrentHitPoints);
        Assert.Contains("Someone killed", g.Log.Lines.Last());
    }

    [DataFact]
    public void TemporaryDyingChangeTicksOncePerDurationAndOutlastsIt()
    {
        var g = new Game();
        g.HurtTo(-9);
        g.Bleed(1f, 3f);
        g.Steps(2.9f);
        Assert.Equal(-9, g.Pc.CurrentHitPoints);
        g.Steps(0.2f);
        Assert.Equal(-8, g.Pc.CurrentHitPoints);
        g.Steps(3f);
        Assert.Equal(-7, g.Pc.CurrentHitPoints);
        Assert.Single(g.Pc.Effects, e => e.Type == g.K.HitPointChangeWhenDying);
        // Healed above 0: the effect goes, adding its change once more, past the maximum (dyingfx G1: 6 of 5 hit points).
        g.Heal(20);
        Assert.Equal(g.Pc.MaxHitPoints + 1, g.Pc.CurrentHitPoints);
        Assert.DoesNotContain(g.Pc.Effects, e => e.Type == g.K.HitPointChangeWhenDying);
        Assert.True(g.Pc.Commandable);
    }

    [DataFact]
    public void PermanentDyingChangeWaitsUntilRemovedThenRunsOn()
    {
        var g = new Game();
        g.HurtTo(-9);
        g.Bleed(1f);
        g.Steps(12f);
        Assert.Equal(-9, g.Pc.CurrentHitPoints);
        // Removed: nothing at once, then it runs up to 1, leaving the player uncommandable (dyingbleed A-E).
        g.Strip();
        Assert.Equal(-9, g.Pc.CurrentHitPoints);
        g.Steps(0.3f);
        Assert.Equal(1, g.Pc.CurrentHitPoints);
        Assert.False(g.Pc.IsDying);
        Assert.False(g.Pc.Commandable);
        g.Steps(1f);
        Assert.Equal(1, g.Pc.CurrentHitPoints);
        // A negative one removed kills (dyingbleed F).
        g.Heal(20);
        g.HurtTo(-3);
        g.Bleed(-1f);
        g.Steps(3f);
        Assert.Equal(-3, g.Pc.CurrentHitPoints);
        g.Strip();
        g.Steps(0.3f);
        Assert.True(g.Pc.IsDead);
        Assert.Equal(-11, g.Pc.CurrentHitPoints);
    }

    [DataFact]
    public void InstantDyingChangeTicksOnePointEverySixSecondsOverItsRate()
    {
        // nwmain bleedinst: -1 per round every 6 s, -2 every 3 s, one point each time.
        var g = new Game();
        g.HurtTo(-1);
        g.BleedInstant(-1f);
        var e = Assert.Single(g.Pc.Effects, x => x.Type == g.K.HitPointChangeWhenDying);
        Assert.Equal(g.K.Temporary, e.DurationType);
        g.Steps(5.9f);
        Assert.Equal(-1, g.Pc.CurrentHitPoints);
        g.Steps(0.2f);
        Assert.Equal(-2, g.Pc.CurrentHitPoints);
        g.Steps(6f);
        Assert.Equal(-3, g.Pc.CurrentHitPoints);
        // Removed while dying: a tick at once, and it is back with the same period (I1: -4 to -5, the next 6 s later).
        g.Strip();
        Assert.Equal(-3, g.Pc.CurrentHitPoints); // after the removing script
        g.Steps(0.05f);
        Assert.Equal(-4, g.Pc.CurrentHitPoints);
        Assert.Single(g.Pc.Effects, x => x.Type == g.K.HitPointChangeWhenDying);
        g.Steps(5.9f);
        Assert.Equal(-4, g.Pc.CurrentHitPoints);
        g.Steps(0.2f);
        Assert.Equal(-5, g.Pc.CurrentHitPoints);
        // Healed: the effect goes with its change once more (raised from -6 to 4 of 5).
        g.Heal(60);
        Assert.Equal(g.Pc.MaxHitPoints - 1, g.Pc.CurrentHitPoints);
        Assert.DoesNotContain(g.Pc.Effects, x => x.Type == g.K.HitPointChangeWhenDying);
        // -2 ticks every 3 s, one point at a time.
        g.HurtTo(-1);
        g.BleedInstant(-2f);
        g.Steps(3.1f);
        Assert.Equal(-2, g.Pc.CurrentHitPoints);
    }

    [DataFact]
    public void InstantPositiveChangeStopsAtOneAndAKillLeavesMinusTwelve()
    {
        var g = new Game();
        // +2 from -3: up one every 3 s, ending at 1 with the effect gone (bleedinst I3).
        g.HurtTo(-3);
        g.BleedInstant(2f);
        g.Steps(12.2f);
        Assert.Equal(1, g.Pc.CurrentHitPoints);
        Assert.DoesNotContain(g.Pc.Effects, x => x.Type == g.K.HitPointChangeWhenDying);
        g.Steps(3.1f);
        Assert.Equal(1, g.Pc.CurrentHitPoints);
        // -1 from -9: its first tick kills, and death leaves -12 (bleedinst D2).
        g.Heal(20);
        g.HurtTo(-9);
        g.BleedInstant(-1f);
        g.Steps(6.2f);
        Assert.True(g.Pc.IsDead);
        Assert.Equal(-12, g.Pc.CurrentHitPoints);
    }

    [DataFact]
    public void TemporaryDyingChangeRemovedTicksAtOnceAndComesBack()
    {
        // bleedinst T1: a 4 s -1 removed after 2 s: -1 at once, then every 4 s.
        var g = new Game();
        g.HurtTo(-1);
        g.Bleed(-1f, 4f);
        g.Steps(2f);
        g.Strip();
        g.Steps(0.05f);
        Assert.Equal(-2, g.Pc.CurrentHitPoints);
        g.Steps(3.85f);
        Assert.Equal(-2, g.Pc.CurrentHitPoints);
        g.Steps(0.2f);
        Assert.Equal(-3, g.Pc.CurrentHitPoints);
    }

    [DataFact]
    public void DeathAndRemovalOnTheDeadAddTheChangeOnce()
    {
        var g = new Game();
        g.HurtTo(-8);
        g.Bleed(-1f);
        // EffectDeath strips it: -11, then its change once more (dying: -12).
        g.World.System<EffectSystem>().Apply(new Effect { Type = g.K.Death }, g.K.Instant, g.Pc, 0);
        Assert.Equal(-12, g.Pc.CurrentHitPoints);
        // On the dead, one put on and removed later adds its change once (dyingbleed T2: -11 to -10).
        g.Bleed(1f);
        g.Steps(3f);
        Assert.Equal(-12, g.Pc.CurrentHitPoints);
        g.Strip();
        Assert.Equal(-11, g.Pc.CurrentHitPoints);
        g.Steps(1f);
        Assert.Equal(-11, g.Pc.CurrentHitPoints);
    }

    [DataFact]
    public void RaiseGivesControlBackAndClosesThePanel()
    {
        var g = new Game();
        g.HurtTo(-3);
        g.Run("void main(){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),OBJECT_SELF);}");
        Assert.True(g.Pc.IsDead);
        g.Run("void main(){PopUpGUIPanel(OBJECT_SELF,GUI_PANEL_PLAYER_DEATH);}");
        Assert.True(g.Pc.Player!.DeathPanelRespawn);
        g.Run("void main(){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectResurrection(),OBJECT_SELF);}");
        Assert.False(g.Pc.IsDead);
        Assert.Equal(1, g.Pc.CurrentHitPoints);
        Assert.True(g.Pc.Commandable);
        Assert.False(g.Pc.Player.DeathPanelRespawn);
    }

    [DataFact]
    public void RespawnPressNeedsThePanelButNotDeath()
    {
        var g = new Game();
        // No panel: the press does nothing, dead or alive.
        g.Run("void main(){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),OBJECT_SELF);}");
        OpenWinter.Game.Requests.ClientRequests.Handle(g.World, g.Pc, "respawn", "{}");
        g.Steps(0.1f);
        Assert.Equal(0, g.Local("respawns"));
        // Respawn hidden: still nothing.
        g.Run("void main(){PopUpDeathGUIPanel(OBJECT_SELF,FALSE,TRUE,0,\"x\");}");
        OpenWinter.Game.Requests.ClientRequests.Handle(g.World, g.Pc, "respawn", "{}");
        g.Steps(0.1f);
        Assert.Equal(0, g.Local("respawns"));
        // A living player shown the panel can press it (dying: ev_respawn_2 on the living), once.
        g.Run("void main(){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectResurrection(),OBJECT_SELF);PopUpDeathGUIPanel(OBJECT_SELF);}");
        Assert.False(g.Pc.IsDead);
        OpenWinter.Game.Requests.ClientRequests.Handle(g.World, g.Pc, "respawn", "{}");
        OpenWinter.Game.Requests.ClientRequests.Handle(g.World, g.Pc, "respawn", "{}");
        g.Steps(0.1f);
        Assert.Equal(1, g.Local("respawns"));
    }

    [DataFact]
    public void RestRefusalAndProgressUseTheOriginalLines()
    {
        var g = new Game();
        g.Pc.Actions.Add(new RestAction());
        ActionQueues.Step(g.World, g.Pc, 1f / 30);
        Assert.Equal("Resting.", g.Log.Lines.Last());
        g.Steps(RestAction.Duration(g.World, g.Pc) + 0.5f);
        Assert.Equal("Done resting.", g.Log.Lines.Last());
        g.Pc.Actions.Add(new RestAction());
        ActionQueues.Step(g.World, g.Pc, 1f / 30);
        ActionQueues.Clear(g.World, g.Pc);
        Assert.Equal("Cancelled Rest.", g.Log.Lines.Last());
        // A hostile 25 m away refuses it; REST_ENEMY_CHECK_DISTANCE is 30 (dying scene: refused at 25, rested at 35).
        var foe = g.World.Creatures.FromTemplate(new GffStruct());
        foe.Classes.Add(new CreatureClass { Class = 12, Level = 1 });
        foe.Faction = 1;
        g.World.AddToArea(foe, g.Area, g.Pc.Position + new Vector3(25, 0, 0), 0, exact: true);
        CreatureRules.EnsureInit(g.World, foe);
        Assert.False(RestAction.CanRest(g.World, g.Pc, false, out var why));
        Assert.Equal("You cannot rest while there are enemies nearby.", why);
        foe.Position = g.Pc.Position + new Vector3(35, 0, 0);
        Assert.True(RestAction.CanRest(g.World, g.Pc, false, out _));
    }
}

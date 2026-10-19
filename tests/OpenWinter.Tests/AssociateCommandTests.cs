using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Society;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Orders from an associate's radial reach its OnConversation script as ASSOCIATE_COMMAND_* listen patterns and the
/// stock associate scripts obey them; hostile acts on one's own associate are refused (docs/ASSOCIATE_COMMANDS.md).
/// </summary>
public class AssociateCommandTests
{
    const int StandGround = 0x08000000, DefendMaster = 0x04000000; // x0_i0_assoc NW_ASC_MODE_*
    const int ActionFollow = 35;

    sealed class NullConnection : IConnection
    {
        public int Id => 0;
        public bool IsConnected => true;
        public void Send(byte[] message, bool reliable = true) { }
        public bool TryReceive(out byte[] message) { message = Array.Empty<byte>(); return false; }
        public void Disconnect() { }
    }

    sealed class Output : IWorldOutput
    {
        public readonly List<(uint To, string Text)> Messages = new();
        public readonly List<(string Kind, uint Subject)> Notes = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) => Messages.Add((pc.Id, text));
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) => Notes.Add((kind, subject?.Id ?? GameObject.InvalidId));
    }

    sealed record Setup(World World, Area Area, Output Out, List<string> Log, Creature Pc)
    {
        public void Step(float seconds) { for (float t = 0; t < seconds - 1e-4f; t += 0.1f) World.Step(0.1f); }
        public void Order(Creature associate, int command, Creature? from = null) =>
            ClientRequests.Handle(World, from ?? Pc, "associate-command", JsonSerializer.Serialize(new { target = associate.Id, command }));
        public int State(Creature associate) => associate.Locals.Ints.GetValueOrDefault("NW_ASSOCIATE_MASTER");
        public bool Following(Creature associate) => associate.Actions.Any(a => a.ActionType == ActionFollow);
        public string Pvp => World.Resources.TlkString(66245);
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    static Setup Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        rm.AddBaseGame();
        var world = new World(rm);
        world.EnableScripts();
        world.Register(world.Module);
        world.Factions.Load(rm);
        var log = new List<string>();
        world.Scripts!.Log = log.Add;
        var output = new Output();
        world.Output = output;
        var area = new Area { ResRef = "assoc_test", Tag = "assoc_test", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
        var pc = new Creature { Tag = "pc", Faction = FactionTable.PlayerFaction, CurrentHitPoints = 20, MaxHitPoints = 20, PerceptionRange = 9 };
        pc.Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "pc" };
        world.AddToArea(pc, area, new Vector3(10, 10, 0), 0);
        return new Setup(world, area, output, log, pc);
    }

    /// <summary>The stock Summon Creature I badger (nw_s_badgerdire, scripts nw_ch_ac* / nw_ch_summon_9) through the summon effect.</summary>
    static Creature Summon(Setup s)
    {
        var fx = s.World.System<EffectSystem>();
        var k = EffectConstants.Of(s.World);
        fx.ApplyAtLocation(new Effect
        {
            Type = k.SummonCreature, Creator = s.Pc.Id, ScriptId = fx.NextId(),
            Strings = { [0] = "nw_s_badgerdire" }, Ints = { [0] = -1, [2] = k["VFX_IMP_UNSUMMON"] },
        }, k.Temporary, new Location(s.Area.Id, new Vector3(12, 10, 0), 0), 600);
        s.Step(0.5f);
        var badger = Assert.Single(AssociateService.AllAssociates(s.World, s.Pc));
        Assert.Equal(AssociateService.Summoned, badger.AssociateType);
        return badger;
    }

    static Creature Henchman(Setup s)
    {
        var h = s.World.Creatures.FromBlueprint("nw_hen_gal_01")!;
        s.World.AddToArea(h, s.Area, new Vector3(12, 12, 0), 0);
        s.World.RunEvent(h, "ScriptSpawn", new EventContext());
        AssociateService.Add(s.World, s.Pc, h, AssociateService.Henchman);
        s.Step(0.2f);
        return h;
    }

    [DataFact]
    public void CommandReachesOnConversationWithItsListenPattern()
    {
        var s = Make();
        var badger = Summon(s);
        Assert.True(badger.Society.AssociatePatterns); // nw_ch_summon_9 ran SetAssociateListenPatterns
        // A probe in place of nw_ch_ac4 records what the original's OnConversation sees.
        badger.Scripts["ScriptDialogue"] = "society_heard";
        s.Order(badger, AssociateService.CommandGuardMaster);
        s.Step(0.2f);
        Assert.Equal(AssociateService.CommandGuardMaster, badger.Locals.Ints["pattern"]);
        Assert.Equal(AssociateService.CommandGuardMaster, badger.Locals.Ints["command"]);
        Assert.Equal(1, badger.Locals.Ints["heard"]);
    }

    [DataFact]
    public void StockSummonStandsGroundThenFollowsAgain()
    {
        var s = Make();
        var badger = Summon(s);
        Assert.Equal("nw_ch_ac4", badger.Scripts["ScriptDialogue"]);

        s.Order(badger, AssociateService.CommandStandGround);
        s.Step(0.3f);
        Assert.NotEqual(0, s.State(badger) & StandGround);
        Assert.False(s.Following(badger));
        // The stock heartbeat leaves a creature standing its ground where it is, however far its master walks.
        s.World.Jump(s.Pc, new Location(s.Area.Id, new Vector3(30, 30, 0), 0));
        s.World.RunEvent(badger, "ScriptHeartbeat", new EventContext());
        s.Step(0.3f);
        Assert.False(s.Following(badger));

        s.Order(badger, AssociateService.CommandFollowMaster);
        s.Step(0.3f);
        Assert.Equal(0, s.State(badger) & StandGround);
        Assert.True(s.Following(badger));

        s.Order(badger, AssociateService.CommandGuardMaster);
        s.Step(0.3f);
        Assert.NotEqual(0, s.State(badger) & DefendMaster);
        s.Order(badger, AssociateService.CommandAttackNearest);
        s.Step(0.3f);
        Assert.Equal(0, s.State(badger) & (DefendMaster | StandGround));
        Assert.DoesNotContain(s.Log, l => l.Contains("not implemented"));
    }

    [DataFact]
    public void StockHenchmanTogglesStealthAndLeavesTheParty()
    {
        var s = Make();
        var h = Henchman(s);
        Assert.Equal("nw_ch_ac4", h.Scripts["ScriptDialogue"]); // the OC henchmen use the generic associate scripts
        Assert.True(h.Society.AssociatePatterns);
        s.Order(h, AssociateService.CommandToggleStealth);
        s.Step(0.3f);
        Assert.True(h.StealthMode);
        s.Order(h, AssociateService.CommandToggleStealth);
        s.Step(0.3f);
        Assert.False(h.StealthMode);
        s.Order(h, AssociateService.CommandToggleCasting);
        s.Step(0.3f);
        Assert.Equal(10, h.Locals.Ints.GetValueOrDefault("X2_L_STOPCASTING"));
        s.Order(h, AssociateService.CommandLeaveParty);
        s.Step(0.5f);
        Assert.NotEqual(s.Pc.Id, h.Master);
    }

    [DataFact]
    public void OrdersAreOnlyForOwnAssociatesAndTheirType()
    {
        var s = Make();
        var badger = Summon(s);
        badger.Scripts["ScriptDialogue"] = "society_heard";
        var other = new Creature { Tag = "other", Faction = FactionTable.PlayerFaction, CurrentHitPoints = 20, MaxHitPoints = 20 };
        other.Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "other" };
        s.World.AddToArea(other, s.Area, new Vector3(14, 10, 0), 0);

        s.Order(badger, AssociateService.CommandFollowMaster, from: other); // someone else's summon
        s.Order(badger, AssociateService.CommandToggleStealth);              // a henchman's order
        s.Order(badger, AssociateService.CommandReleaseDomination);          // not dominated
        s.Step(0.3f);
        Assert.False(badger.Locals.Ints.ContainsKey("heard"));
        Assert.Equal(0, badger.Society.LastAssociateCommand);
        Assert.False(badger.IsDestroyed);

        // Unsummon goes through the master's summon effect.
        s.Order(badger, AssociateService.CommandUnsummonFamiliar);
        Assert.False(badger.IsDestroyed);
        s.Order(badger, AssociateService.CommandUnsummonSummoned);
        s.Step(0.1f);
        Assert.True(badger.IsDestroyed);
        Assert.DoesNotContain(s.Pc.Effects, e => e.Type == EffectConstants.Of(s.World).SummonCreature);
    }

    [DataFact]
    public void ReleasingDominationEndsTheEffectAndTheService()
    {
        var s = Make();
        var goblin = new Creature { Tag = "goblin", Faction = 1, CurrentHitPoints = 8, MaxHitPoints = 8 };
        s.World.AddToArea(goblin, s.Area, new Vector3(13, 10, 0), 0);
        var k = EffectConstants.Of(s.World);
        // The player's EffectDominated puts the goblin in her service (measured).
        s.World.System<EffectSystem>().Apply(new Effect { Type = k["EFFECT_TYPE_DOMINATED"], Creator = s.Pc.Id }, k.Permanent, goblin, 0);
        Assert.Equal(s.Pc.Id, goblin.Master);
        Assert.Equal(AssociateService.Dominated, goblin.AssociateType);
        Assert.NotEqual(1, goblin.Faction);
        s.Order(goblin, AssociateService.CommandReleaseDomination);
        Assert.Equal(GameObject.InvalidId, goblin.Master);
        Assert.Equal(1, goblin.Faction);
        Assert.DoesNotContain(goblin.Effects, e => e.Type == k["EFFECT_TYPE_DOMINATED"]);
        Assert.Single(s.Out.Messages, m => m.To == s.Pc.Id && m.Text.StartsWith(s.World.Resources.TlkString(61909).Split('<')[0]));
    }

    [DataFact]
    public void PlayerCannotAttackOrTauntHerOwnSummonInPartyPvp()
    {
        var s = Make();
        var badger = Summon(s);
        s.Step(0.5f); // hit points settle after the summon arrives
        int hp = badger.CurrentHitPoints;

        ClientRequests.Handle(s.World, s.Pc, "attack", JsonSerializer.Serialize(new { target = badger.Id }));
        Assert.True(badger.CurrentHitPoints >= hp); // never hurt (it may still be settling upwards)
        Assert.False(s.World.System<OpenWinter.Game.Combat.CombatSystem>().IsInCombat(s.Pc));
        Assert.Contains(s.Out.Messages, m => m.To == s.Pc.Id && m.Text == s.Pvp);

        s.Out.Messages.Clear();
        s.Pc.Actions.Add(new OpenWinter.Game.Combat.AttackAction { Target = badger.Id });
        s.Step(1f);
        Assert.True(badger.CurrentHitPoints >= hp); // never hurt (it may still be settling upwards)
        Assert.False(s.World.System<OpenWinter.Game.Combat.CombatSystem>().IsInCombat(s.Pc));
        Assert.Single(s.Out.Messages, m => m.Text == s.Pvp);

        s.Out.Messages.Clear();
        s.Pc.Actions.Add(new OpenWinter.Game.Skills.UseSkillAction { Skill = OpenWinter.Game.Skills.SkillIds.Taunt, Target = badger.Id, Range = 3 });
        s.Step(1f);
        Assert.Single(s.Out.Messages, m => m.Text == s.Pvp);
        Assert.True(badger.CurrentHitPoints >= hp); // never hurt (it may still be settling upwards)
        Assert.Empty(badger.Effects);

        // Her associate's own AI is not bound by it, and Full PvP lets her (not measured).
        Assert.True(s.World.PvpAllows(badger, s.Pc));
        s.World.Pvp = PvpSetting.Full;
        Assert.True(s.World.PvpAllows(s.Pc, badger));
    }
}

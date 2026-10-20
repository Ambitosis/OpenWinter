using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Spells;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>Spell casting with the module's real player character (freddiepharkasf1) through the game server.</summary>
public class SpellTests
{
    readonly ITestOutputHelper _out;
    public SpellTests(ITestOutputHelper output) { _out = output; }

    const int SpellBless = 6, SpellBullsStrength = 9, SpellCureLight = 32, SpellGrease = 66, SpellMagicMissile = 107;
    const int ClassCleric = 2, ClassWizard = 10;

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    sealed class Fixture
    {
        public required World World;
        public required GameServer Server;
        public required IConnection Client;
        public required Creature Pc;
        public readonly List<string> Log = new();
        public readonly List<(string Kind, uint Subject, JsonElement Data)> Notes = new();
        public readonly List<string> Messages = new();
        public LocalVariables M => World.Module.Locals;
        public void Run(string script, GameObject? self = null) => World.Scripts!.Run(script, (self ?? Pc).Id);
        public void Tick(float seconds)
        {
            for (float t = 0; t < seconds - 1e-4f; t += 0.1f)
            {
                Server.Tick(0.1f);
                Drain();
            }
        }
        public void Drain()
        {
            while (Client.TryReceive(out var m))
            {
                var r = new MessageReader(m);
                if (r.Type == MessageType.Chat) { Messages.Add(ChatMessage.Read(r).Text); continue; }
                if (r.Type != MessageType.Notify) continue;
                var n = NotifyMessage.Read(r);
                Notes.Add((n.Kind, n.Subject, JsonDocument.Parse(n.Json).RootElement));
            }
        }
        public Creature Spawn(string tag, float dx, int hp = 100, int faction = FactionTable.Commoner)
        {
            var c = new Creature { Tag = tag, MaxHitPoints = hp, CurrentHitPoints = hp, Faction = faction };
            World.AddToArea(c, Pc.Area!, Pc.Position + new Vector3(dx, 0, 0), 0);
            return c;
        }
        /// <summary>Isolates ordinary casting: no armour failure or saved-character Haste item.</summary>
        public void Unarmour()
        {
            foreach (var slot in new[] { EquipSlot.Chest, EquipSlot.LeftHand })
                if (Pc.Equipment.TryGetValue(slot, out var item)) World.Inventory.Unequip(Pc, item);
            foreach (var item in Pc.Equipment.Values.Distinct().Where(i => i.Properties.Any(p => p.Type == NwRules.IpHaste)).ToArray())
                World.Inventory.Unequip(Pc, item);
            Assert.False(CreatureRules.IsHasted(Pc));
            Assert.Equal(0, CreatureRules.ArcaneSpellFailure(World, Pc));
        }
        public CreatureClass AddClass(int cls, int level)
        {
            CreatureRules.EnsureInit(World, Pc);
            var cc = new CreatureClass { Class = cls, Level = level };
            Pc.Classes.Add(cc);
            return cc;
        }
    }

    Fixture Login()
    {
        var rm = ResMan.ForModule(TestData.Paths, TestData.Config!.Module);
        rm.AddFirst(new DirectorySource(ScriptsDir()));
        var world = new World(rm);
        world.EnableScripts();
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", DefaultCharacter = "freddiepharkasf1" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        uint me = 0;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type == MessageType.ControlObject) me = ControlObjectMessage.Read(r).ObjectId;
        }
        var f = new Fixture { World = world, Server = server, Client = client, Pc = world.Get<Creature>(me)! };
        world.Scripts!.Log = l => { f.Log.Add(l); _out.WriteLine(l); };
        Assert.NotNull(f.Pc.Area);
        return f;
    }

    void NoScriptErrors(Fixture f) =>
        Assert.DoesNotContain(f.Log, l => l.Contains("not implemented") || l.Contains("error", StringComparison.OrdinalIgnoreCase));

    [DataFact]
    public void PlayerCastDoesNotUseLegacySpellCountAsAuthorization()
    {
        var f = Login();
        var cleric = f.AddClass(ClassCleric, 20);
        cleric.Domain1 = 13;
        cleric.Domain2 = 0;
        f.Pc.Abilities[NwRules.AbilityWis] = 20;
        f.Pc.Feats.Add(12);
        cleric.Memorized[2].Add((102, NwRules.MetaMaximize, true));
        Assert.Equal(3, CreatureRules.SpellCount(f.World, f.Pc, 102));
        void Request()
        {
            f.Client.Send(new ClientRequestMessage
            {
                Kind = "cast-spell", Json = JsonSerializer.Serialize(
                new { spell = 102, @class = ClassCleric, meta = NwRules.MetaExtend, domain = 1 })
            }.Write());
            f.Tick(8);
        }
        Request();
        Assert.True(cleric.Memorized[2][0].Ready);
        Assert.DoesNotContain(f.Pc.Effects, e => e.SpellId == 102);
        cleric.Memorized[2][0] = (102, NwRules.MetaExtend, true);
        Assert.Equal(3, CreatureRules.SpellCount(f.World, f.Pc, 102));
        Request();
        Assert.False(cleric.Memorized[2][0].Ready);
        Assert.Contains(f.Pc.Effects, e => e.SpellId == 102);
        Assert.Equal(0, CreatureRules.SpellCount(f.World, f.Pc, 102));
        NoScriptErrors(f);
    }

    [DataFact]
    public void PlayerDomainRequestDoesNotTreatUseReportsAsCastingPermission()
    {
        var f = Login();
        var cleric = f.AddClass(ClassCleric, 20);
        cleric.Domain1 = 13;
        cleric.Domain2 = 0;
        f.Pc.Abilities[NwRules.AbilityWis] = 20;
        f.Pc.Feats.Add(12);
        cleric.Memorized[2].Add((102, NwRules.MetaNone, true));
        Assert.Equal(1, CreatureRules.SpellUsesLeft(f.World, f.Pc, ClassCleric, 102, NwRules.MetaExtend, 1));
        void Request()
        {
            f.Client.Send(new ClientRequestMessage
            {
                Kind = "cast-spell", Json = JsonSerializer.Serialize(
                new { spell = 102, @class = ClassCleric, meta = NwRules.MetaExtend, domain = 1 })
            }.Write());
            f.Tick(8);
        }
        Request();
        Assert.True(cleric.Memorized[2][0].Ready);
        Assert.DoesNotContain(f.Pc.Effects, e => e.SpellId == 102);
        cleric.Memorized[2][0] = (102, NwRules.MetaExtend, true);
        Request();
        Assert.False(cleric.Memorized[2][0].Ready);
        Assert.Contains(f.Pc.Effects, e => e.SpellId == 102);
        Assert.Equal(0, CreatureRules.SpellUsesLeft(f.World, f.Pc, ClassCleric, 102, NwRules.MetaExtend, 1));
        NoScriptErrors(f);
    }

    [DataFact]
    public void ClearingPreparedCastPreservesCommittedReleaseButEarlyClearAndDeathDoNot()
    {
        foreach (int scenario in new[] { 0, 1, 2 })
        {
            var f = Login();
            f.Unarmour();
            var wizard = f.AddClass(ClassWizard, 1);
            wizard.Memorized[1].Add((102, NwRules.MetaNone, true));
            f.Pc.LastSpell = null;
            f.Pc.ActionModes.Clear();
            f.Pc.Actions.Add(new CastSpellAction
            { Spell = 102, Target = f.Pc.Id, Class = ClassWizard, MetaMagic = NwRules.MetaNone });
            f.Tick(scenario == 0 ? 1.0f : 1.7f);
            Assert.Null(f.Pc.LastSpell);
            Assert.Equal(scenario == 0, wizard.Memorized[1][0].Ready);
            ActionQueues.Clear(f.World, f.Pc);
            if (scenario == 2) f.Pc.IsDead = true;
            f.Tick(0.1f);
            Assert.Null(f.Pc.LastSpell); // Clearing does not release immediately.
            f.Tick(1.0f);
            if (scenario == 1)
            {
                Assert.Equal(102, f.Pc.LastSpell!.SpellId);
                Assert.DoesNotContain(f.Notes, n => n.Kind == "spell-interrupted");
            }
            else Assert.Null(f.Pc.LastSpell);
            Assert.Equal(scenario == 0, wizard.Memorized[1][0].Ready);
            NoScriptErrors(f);
        }
    }

    [DataFact]
    public void QueueClearRetainsCommittedCastAndRecoveryBeforeNewCommands()
    {
        var f = Login();
        // This control measures the ordinary 1.5/.5/1 sequence. The saved test
        // character owns a Haste item, whose timing is covered separately.
        foreach (var item in f.Pc.Equipment.Values.Distinct().ToArray()) f.World.Inventory.Unequip(f.Pc, item);
        Assert.False(CreatureRules.IsHasted(f.Pc));
        ActionQueues.Clear(f.World, f.Pc);
        var cast = new CastSpellAction { Spell = SpellBless, Target = f.Pc.Id, Cheat = true };
        var discarded = new WaitAction { Remaining = 60 };
        f.Pc.Actions.Add(cast);
        f.Pc.Actions.Add(discarded);
        f.Tick(1.6f);
        Assert.Same(cast, f.Pc.Actions[0]);
        ActionQueues.Clear(f.World, f.Pc);
        Assert.Same(cast, Assert.Single(f.Pc.Actions));
        var next = new WaitAction { Remaining = 1 };
        f.Pc.Actions.Add(next);
        f.Tick(0.7f);
        Assert.Same(cast, f.Pc.Actions[0]);
        Assert.Equal(SpellBless, f.Pc.LastSpell!.SpellId);
        // Clearing again during recovery still cannot start the replacement early.
        ActionQueues.Clear(f.World, f.Pc);
        Assert.Same(cast, Assert.Single(f.Pc.Actions));
        f.Pc.Actions.Add(next);
        f.Tick(0.5f);
        Assert.Same(cast, f.Pc.Actions[0]);
        f.Tick(0.7f);
        Assert.Same(next, Assert.Single(f.Pc.Actions));
        Assert.DoesNotContain(discarded, f.Pc.Actions);
        f.Tick(0.7f);
        Assert.DoesNotContain(next, f.Pc.Actions);
        NoScriptErrors(f);
    }

    [DataFact]
    public void ReleasePrecedesCastRecoveryAndMissingConjureTimeIsZero()
    {
        var f = Login();
        // This control measures the ordinary 1.5/.5/1 sequence. The saved test
        // character owns a Haste item, whose timing is covered separately.
        foreach (var item in f.Pc.Equipment.Values.Distinct().ToArray()) f.World.Inventory.Unequip(f.Pc, item);
        Assert.False(CreatureRules.IsHasted(f.Pc));
        f.Pc.LastSpell = null;
        var cast = new CastSpellAction { Spell = SpellBless, Target = f.Pc.Id, Cheat = true };
        Assert.Equal(ActionStatus.InProgress, cast.Step(f.World, f.Pc, 0));
        cast.Step(f.World, f.Pc, 1.5f);
        f.Drain();
        var release = Assert.Single(f.Notes, n => n.Kind == "spell-cast").Data;
        Assert.Equal(0.5f, release.GetProperty("duration").GetSingle());
        Assert.Equal(1.0f, release.GetProperty("recovery").GetSingle());
        cast.Step(f.World, f.Pc, 0.49f);
        Assert.Null(f.Pc.LastSpell);
        Assert.Equal(ActionStatus.InProgress, cast.Step(f.World, f.Pc, 0.02f));
        Assert.Equal(SpellBless, f.Pc.LastSpell!.SpellId);
        Assert.Equal(ActionStatus.InProgress, cast.Step(f.World, f.Pc, 0.9f));
        Assert.Equal(ActionStatus.Done, cast.Step(f.World, f.Pc, 0.11f));

        // Suck Brain has no ConjTime and CastTime=3100: native releases at 0.5s,
        // then recovers for 3.1s. The table's recovery must not delay release.
        Assert.Equal(0, SpellTable.ConjureSeconds(f.World, 716));
        Assert.Equal(3.1f, SpellTable.CastSeconds(f.World, 716));
    }

    [DataFact]
    public void SummonedFamiliarArrivesBeforeItsAppearanceAndBurstEvents()
    {
        var f = Login();
        f.AddClass(ClassWizard, 1);
        f.Drain();
        var familiar = CreatureRules.SummonAssociate(f.World, f.Pc, NwRules.AssociateFamiliar);
        Assert.NotNull(familiar);
        var order = new List<string>();
        while (f.Client.TryReceive(out var bytes))
        {
            var r = new MessageReader(bytes);
            if (r.Type == MessageType.Spawn && SpawnMessage.Read(r).ObjectId == familiar.Id) order.Add("spawn");
            if (r.Type != MessageType.Notify) continue;
            var n = NotifyMessage.Read(r);
            using var data = JsonDocument.Parse(n.Json);
            if (n.Kind == "appear" && n.Subject == familiar.Id) order.Add("appear");
            if (n.Kind == "vfx" && data.RootElement.GetProperty("vfx").GetInt32() == 36) order.Add("burst");
        }
        Assert.Equal(new[] { "spawn", "appear", "burst" }, order);
    }

    [DataFact]
    public void FirstEffectNotificationIsSentOnceAfterSpawnAndRespectsVisibility()
    {
        var f = Login();
        f.Drain();
        var target = f.Spawn("new_effect_target", 4);
        var k = EffectConstants.Of(f.World);
        var vfx = new Effect { Type = k.VisualEffect, Creator = f.Pc.Id };
        vfx.Ints[0] = 9; vfx.Floats[0] = 1;
        f.World.System<EffectSystem>().Apply(vfx, k.Temporary, target, 10);
        var messages = new List<string>();
        while (f.Client.TryReceive(out var bytes))
        {
            var r = new MessageReader(bytes);
            if (r.Type == MessageType.Spawn && SpawnMessage.Read(r).ObjectId == target.Id) messages.Add("spawn");
            else if (r.Type == MessageType.Notify)
            {
                var n = NotifyMessage.Read(r);
                if (n.Subject == target.Id && n.Kind == "vfx") messages.Add("vfx");
            }
        }
        Assert.Equal(new[] { "spawn", "vfx" }, messages);

        var (_, hiddenConnection) = LoopbackConnection.CreatePair();
        var hidden = new Creature { DMOwner = new PlayerSession { Connection = hiddenConnection }, DMHidden = true };
        f.World.AddToArea(hidden, f.Pc.Area!, f.Pc.Position + new Vector3(6, 0, 0), 0);
        f.Server.Notify("appear", hidden, new { animation = 1 });
        Assert.False(f.Client.TryReceive(out _));
        var elsewhere = new Creature();
        f.World.AddToArea(elsewhere, f.World.Areas.First(a => a != f.Pc.Area), Vector3.Zero, 0);
        f.Server.Notify("appear", elsewhere, new { animation = 1 });
        f.Server.Notify("appear", elsewhere, new { animation = 1 }, f.Pc);
        Assert.False(f.Client.TryReceive(out _));
    }

    [DataFact]
    public void PlayerRequestInfersDomainOnlyWhenTheArgumentIsOmitted()
    {
        foreach (int meta in new[] { NwRules.MetaExtend, 255 })
        {
            var f = Login();
            var cleric = f.AddClass(ClassCleric, 20);
            cleric.Domain1 = 0;
            cleric.Domain2 = 13; // Magic: Mage Armor at base level 1.
            f.Pc.Abilities[NwRules.AbilityWis] = 20;
            f.Pc.Feats.Add(12);
            cleric.Memorized[2].Add((102, NwRules.MetaExtend, true));
            void Request(object data)
            {
                f.Client.Send(new ClientRequestMessage { Kind = "cast-spell", Json = JsonSerializer.Serialize(data) }.Write());
                f.Tick(8);
            }
            Request(new { spell = 102, @class = ClassCleric, meta, domain = 0 });
            Assert.True(cleric.Memorized[2][0].Ready); // An explicit ordinary cast is rejected.
            Request(new { spell = 102, @class = ClassCleric, meta });
            Assert.False(cleric.Memorized[2][0].Ready);
            Assert.Contains(f.Pc.Effects, e => e.SpellId == 102);
            Assert.Equal(1, f.Pc.LastSpell!.DomainLevel);
            Assert.Equal(1, f.Pc.LastSpell.SpellLevel);
            NoScriptErrors(f);
        }
    }

    [DataFact]
    public void SpellTalentCastsADomainOnlySpellFromItsDomainSlot()
    {
        var f = Login();
        var cleric = f.AddClass(ClassCleric, 20);
        cleric.Domain1 = 0;
        cleric.Domain2 = 13; // Magic: Mage Armor at base level 1, not on the cleric list.
        f.Pc.Abilities[NwRules.AbilityWis] = 20;
        cleric.Memorized[1].Add((102, NwRules.MetaNone, true));
        var host = f.World.Scripts!;
        var program = new NcsProgram("t",
            System.Text.Encoding.ASCII.GetBytes("NCS V1.0B\0\0\0\0").Concat(new byte[] { 0x20, 0 }).ToArray());
        ((IScriptEngine)host).Call(new ScriptThread(host.VM, program, f.Pc.Id), host.Definitions.ByName["ActionUseTalentOnObject"],
            new object?[] { new Talent { Type = NwRules.TalentSpell, Id = 102 }, f.Pc.Id });
        f.Tick(8);
        Assert.False(cleric.Memorized[1][0].Ready);
        Assert.Contains(f.Pc.Effects, e => e.SpellId == 102);
        Assert.Equal(1, f.Pc.LastSpell!.DomainLevel);
        NoScriptErrors(f);
    }

    [DataFact]
    public void DefensiveCastingChecksAfterConjuringAwayFromEnemiesAndSpendsFailedSpell()
    {
        var f = Login();
        f.Unarmour();
        var wizard = f.AddClass(ClassWizard, 1);
        wizard.Memorized[1].Add((SpellMagicMissile, 0, true));
        f.Pc.ActionModes.Add(10);
        // Guaranteed failure independent of the random d20. The target is outside concentration range.
        f.Pc.Skills[1] = -100;
        var target = f.Spawn("defensive_target", 10f);
        var action = new CastSpellAction { Spell = SpellMagicMissile, Target = target.Id, Class = ClassWizard, MetaMagic = 0 };
        Assert.Equal(ActionStatus.InProgress, action.Step(f.World, f.Pc, 0.1f));
        f.Drain();
        Assert.True(wizard.Memorized[1][0].Ready);
        Assert.Contains(f.Notes, n => n.Kind == "spell-conjure");
        Assert.DoesNotContain(f.Notes, n => n.Kind == "spell-interrupted");

        Assert.Equal(ActionStatus.Failed, action.Step(f.World, f.Pc, 2f));
        f.Drain();
        Assert.False(wizard.Memorized[1][0].Ready);
        Assert.Equal(100, target.CurrentHitPoints);
        Assert.DoesNotContain(f.Notes, n => n.Kind == "spell-cast");
        Assert.Contains(f.Notes, n => n.Kind == "spell-interrupted" && n.Data.GetProperty("reason").GetString() == "defensive-casting");
        NoScriptErrors(f);
    }

    [DataFact]
    public void DefensiveCastingSuccessReleasesTheSpellAndDisablingBypassesTheCheck()
    {
        var f = Login();
        f.Unarmour();
        var wizard = f.AddClass(ClassWizard, 1);
        var target = f.Spawn("defensive_target", 10f);
        foreach (bool defensive in new[] { true, false })
        {
            f.Tick(6f); // Independent cast: allow the previous cast eligibility interval to end.
            wizard.Memorized[1].Clear();
            wizard.Memorized[1].Add((SpellMagicMissile, 0, true));
            f.Notes.Clear();
            if (defensive) f.Pc.ActionModes.Add(10); else f.Pc.ActionModes.Remove(10);
            f.Pc.Skills[1] = defensive ? 100 : -100;
            var action = new CastSpellAction { Spell = SpellMagicMissile, Target = target.Id, Class = ClassWizard, MetaMagic = 0 };
            action.Step(f.World, f.Pc, 0.1f);
            action.Step(f.World, f.Pc, 2f);
            action.Step(f.World, f.Pc, 2f);
            f.Drain();
            Assert.False(wizard.Memorized[1][0].Ready);
            Assert.Contains(f.Notes, n => n.Kind == "spell-cast");
            Assert.DoesNotContain(f.Notes, n => n.Kind == "spell-interrupted");
        }
        NoScriptErrors(f);
    }

    [DataFact]
    public void CombatCastingRemovesOnlyTheNearbyEnemyDefensiveCastingPenalty()
    {
        var f = Login();
        f.Unarmour();
        var wizard = f.AddClass(ClassWizard, 1);
        f.Pc.ActionModes.Add(10);
        f.Pc.Skills[1] = 100;
        var foe = f.Spawn("defensive_foe", 2, faction: FactionTable.Hostile);
        // Its hit points are rolled now, as in the original, and two Magic Missiles could kill it before the last cast.
        foe.IsImmortal = true;
        foreach (var (feat, distance, dc) in new[] { (false, 2f, 20), (true, 2f, 16), (false, 10f, 16) })
        {
            f.Tick(6f); // Each DC comparison is a separate eligible cast.
            if (feat) f.Pc.Feats.Add(7); else f.Pc.Feats.Remove(7);
            foe.Position = f.Pc.Position + new Vector3(distance, 0, 0);
            wizard.Memorized[1].Clear();
            wizard.Memorized[1].Add((SpellMagicMissile, 0, true));
            f.Drain();
            f.Messages.Clear();
            var cast = new CastSpellAction { Spell = SpellMagicMissile, Target = foe.Id, Class = ClassWizard, MetaMagic = 0 };
            cast.Step(f.World, f.Pc, 0.1f);
            cast.Step(f.World, f.Pc, 2f);
            f.Drain();
            // The fixture module renames the Concentration skill; compare the native rule's DC.
            Assert.Contains(f.Messages, m => m.Contains($"vs. DC: {dc})"));
        }
    }

    [DataFact]
    public void MagicMissileDamagesSpendsTheSlotAndFiresSpellCastAt()
    {
        var f = Login();
        f.Unarmour();
        var wiz = f.AddClass(ClassWizard, 5);
        wiz.Memorized[1].Add((SpellMagicMissile, 0, true));
        var target = f.Spawn("spell_target", 4f);
        target.Scripts["ScriptSpellAt"] = "spells_at";
        Assert.Equal(1, CreatureRules.SpellCount(f.World, f.Pc, SpellMagicMissile));

        f.Run("spells_mm");
        Assert.Equal(4, f.M.Ints["mm_action"]); // ACTION_CASTSPELL
        f.Tick(1.0f);
        // Still conjuring: nothing spent or hit yet.
        Assert.True(wiz.Memorized[1][0].Ready);
        Assert.Equal(100, target.CurrentHitPoints);
        f.Tick(5f);

        int damage = 100 - target.CurrentHitPoints;
        _out.WriteLine($"magic missile did {damage}");
        Assert.InRange(damage, 6, 15); // caster level 5: three missiles of d4+1
        Assert.False(wiz.Memorized[1][0].Ready);
        Assert.Equal(0, CreatureRules.SpellCount(f.World, f.Pc, SpellMagicMissile));
        Assert.Empty(f.Pc.Actions);

        // The spell script signalled the target's OnSpellCastAt.
        Assert.Equal(1, f.M.Ints["sat_count"]);
        Assert.Equal(SpellMagicMissile, f.M.Ints["sat_spell"]);
        Assert.Equal(f.Pc.Id, f.M.Objects["sat_caster"]);
        Assert.Equal(1, f.M.Ints["sat_harmful"]);

        // Spell context afterwards.
        f.Run("spells_ctx");
        Assert.Equal(SpellMagicMissile, f.M.Ints["ctx_spell"]);
        Assert.Equal(target.Id, f.M.Objects["ctx_target"]);
        Assert.Equal(ClassWizard, f.M.Ints["ctx_class"]);
        Assert.Equal(1, f.M.Ints["ctx_level"]);
        Assert.Equal(0, f.M.Ints["ctx_spont"]);
        Assert.Equal(5, f.M.Ints["ctx_cl"]);
        Assert.Equal(0, f.M.Ints["ctx_meta"]);
        Assert.Equal(10 + 1 + CreatureRules.AbilityModifier(f.World, f.Pc, NwRules.AbilityInt), f.M.Ints["ctx_dc"]);

        // Clients saw the conjuring, the cast and the missiles.
        Assert.Contains(f.Notes, n => n.Kind == "spell-conjure" && n.Subject == f.Pc.Id && n.Data.GetProperty("spell").GetInt32() == SpellMagicMissile);
        Assert.Contains(f.Notes, n => n.Kind == "spell-cast" && n.Subject == f.Pc.Id && n.Data.GetProperty("animation").GetString() == "out");
        Assert.Contains(f.Notes, n => n.Kind == "vfx" && n.Subject == target.Id);

        // No slot left: a second cast fails without effect.
        f.Run("spells_mm");
        f.Tick(4f);
        Assert.Equal(100 - damage, target.CurrentHitPoints);
        NoScriptErrors(f);
    }

    [DataFact]
    public void SpontaneousCureHealsAndRestRestoresSlots()
    {
        var f = Login();
        var cleric = f.AddClass(ClassCleric, 3);
        cleric.Memorized[1].Add((SpellBless, 0, true));
        f.World.Module.Scripts["Mod_OnPlrRest"] = "spells_rest";
        CreatureRules.SyncHitPoints(f.World, f.Pc);
        int max = f.Pc.MaxHitPoints;
        f.Pc.CurrentHitPoints = max - 30;

        f.Run("spells_cure");
        f.Tick(6f);
        int healed = f.Pc.CurrentHitPoints - (max - 30);
        _out.WriteLine($"cure light wounds healed {healed}");
        Assert.InRange(healed, 1 + 3, 8 + 3); // d8 + caster level 3
        Assert.False(cleric.Memorized[1][0].Ready); // the Bless slot was converted
        f.Run("spells_ctx");
        Assert.Equal(SpellCureLight, f.M.Ints["ctx_spell"]);
        Assert.Equal(1, f.M.Ints["ctx_spont"]);
        Assert.Equal(ClassCleric, f.M.Ints["ctx_class"]);
        Assert.Equal(f.Pc.Id, f.M.Objects["ctx_target"]);

        // Rest through the client's rest button: OnPlayerRest started, then finished; slots and HP back.
        f.Client.Send(new ClientRequestMessage { Kind = "rest" }.Write());
        f.Tick(0.5f);
        Assert.True(f.Pc.IsResting);
        Assert.Equal("1", f.M.Strings["rest_events"]);
        Assert.Equal(1, f.M.Ints["rest_resting"]);
        Assert.Contains(f.Notes, n => n.Kind == "rest" && n.Data.GetProperty("state").GetString() == "started");
        f.Tick(RestAction.Duration(f.World, f.Pc) + 1f);
        Assert.False(f.Pc.IsResting);
        Assert.Equal("12", f.M.Strings["rest_events"]);
        Assert.Equal(f.Pc.Id, f.M.Objects["rest_pc"]);
        Assert.True(cleric.Memorized[1][0].Ready);
        Assert.Equal(f.Pc.MaxHitPoints, f.Pc.CurrentHitPoints);

        // Resting with an enemy close by is refused; being interrupted cancels.
        var enemy = f.Spawn("spell_enemy", 6f, faction: FactionTable.Hostile);
        f.Client.Send(new ClientRequestMessage { Kind = "rest" }.Write());
        f.Tick(0.5f);
        Assert.False(f.Pc.IsResting);
        Assert.Equal("12", f.M.Strings["rest_events"]);
        f.World.Destroy(enemy);
        f.Client.Send(new ClientRequestMessage { Kind = "rest" }.Write());
        f.Tick(0.5f);
        Assert.True(f.Pc.IsResting);
        ActionQueues.Clear(f.World, f.Pc);
        f.Tick(0.2f);
        Assert.False(f.Pc.IsResting);
        Assert.Equal("1213", f.M.Strings["rest_events"]);
        NoScriptErrors(f);
    }

    [DataFact]
    public void CheatCastNeedsNoSlotAndAnUnknownSpellFails()
    {
        var f = Login();
        var target = f.Spawn("spell_target", 3f);
        Assert.Equal(0, CreatureRules.SpellCount(f.World, f.Pc, SpellMagicMissile));
        f.Run("spells_cheat");
        f.Tick(8f);
        var k = EffectConstants.Of(f.World);
        var bulls = f.Pc.Effects.Where(e => e.SpellId == SpellBullsStrength).ToList();
        Assert.Contains(bulls, e => e.Type == k["EFFECT_TYPE_ABILITY_INCREASE"] && e.Ints[0] == NwRules.AbilityStr);
        Assert.All(bulls, e => Assert.True(e.CasterLevel > 0));
        Assert.Equal(f.Pc.Id, bulls[0].Creator);
        // The barbarian has no Magic Missile: that cast failed.
        Assert.Equal(100, target.CurrentHitPoints);
        Assert.Empty(f.Pc.Actions);

        // Instant cheat casts go off on the next step, with no animation time.
        f.World.System<EffectSystem>().RemoveWhere(f.Pc, e => e.SpellId == SpellBullsStrength);
        f.Pc.Actions.Add(new CastSpellAction { Spell = SpellBullsStrength, Target = f.Pc.Id, Cheat = true, Instant = true });
        f.Tick(0.1f);
        Assert.Contains(f.Pc.Effects, e => e.SpellId == SpellBullsStrength);

        // Feat spells (feat.2da SPELLID) are cast at the feat's target: Barbarian Rage.
        const int FeatBarbarianRage = 293, SpellAbilityBarbarianRage = 307;
        Assert.Equal(SpellAbilityBarbarianRage, f.World.Resources.TwoDA("feat").GetInt(FeatBarbarianRage, "SPELLID", -1));
        Assert.True(CreatureRules.HasFeat(f.World, f.Pc, FeatBarbarianRage));
        f.Pc.Actions.Add(new OpenWinter.Game.Scripting.Functions.UseFeatAction { Feat = FeatBarbarianRage, Target = f.Pc.Id });
        f.Tick(4f);
        // (The module's own rage script decides what the rage does, even refunding the use; the engine part is the cast and its context.)
        Assert.Equal(SpellAbilityBarbarianRage, f.Pc.LastSpell!.SpellId);
        Assert.Equal(FeatBarbarianRage, f.Pc.LastSpell.FeatId);
        Assert.Equal(f.Pc.Id, f.Pc.LastSpell.Target);
        NoScriptErrors(f);
    }

    [DataFact]
    public void PlaceablesCastWithoutSpellbooks()
    {
        var f = Login();
        var target = f.Spawn("spell_target", 3f);
        var trap = new Placeable { Tag = "spell_trap" };
        f.World.AddToArea(trap, f.Pc.Area!, f.Pc.Position + new Vector3(-3, 0, 0), 0);
        // Not cheat: placeables still cast (they have no slots), at the minimum caster level for the spell.
        trap.Actions.Add(new CastSpellAction { Spell = SpellBullsStrength, Target = target.Id });
        f.Tick(5f);
        Assert.Contains(target.Effects, e => e.SpellId == SpellBullsStrength && e.Creator == trap.Id && e.CasterLevel == 3);
        Assert.Equal(3, trap.LastSpell!.CasterLevel); // second level spell: caster level 3
        NoScriptErrors(f);
    }

    [DataFact]
    public void ResistanceImmunityAndAbsorption()
    {
        var f = Login();
        foreach (var tag in new[] { "spell_sr", "spell_imm", "spell_mantle", "spell_globe", "spell_plain" }) f.Spawn(tag, 2f);
        f.Pc.LastSpell = new SpellCastInfo { SpellId = SpellMagicMissile, CasterLevel = 5, SpellLevel = 1, ClassType = ClassWizard };
        f.Run("spells_resist");
        var m = f.M.Ints;
        Assert.Equal(1, m["r_sr"]);
        Assert.Equal(2, m["r_imm"]);
        Assert.Equal(3, m["r_mantle"]);
        Assert.Equal(9, m["r_mantle_left"]); // a level-1 spell took one of ten levels
        Assert.Equal(2, m["r_globe"]);
        Assert.Equal(0, m["r_plain"]);
        Assert.Equal(0, m["r_invalid"]);
        Assert.Equal(1, m["c_sr"]);
        Assert.Equal(0, m["c_sr_plain"]);
        Assert.Equal(1, m["c_imm"]);
        Assert.Equal(0, m["c_imm_other"]);
        Assert.Equal(1, m["c_globe"]);
        Assert.Equal(0, m["c_globe_high"]); // a 5th level spell gets through a minor globe
        Assert.Equal(1, m["c_mantle_keep"]);
        Assert.Equal(9, m["c_mantle_left"]); // bRemoveLevels FALSE keeps the levels
        Assert.Contains(m["reflex"], new[] { 10, 20 });
        NoScriptErrors(f);
    }

    [DataFact]
    public void GreaseCreatesAnAreaOfEffectWithTheSpellContext()
    {
        var f = Login();
        var target = f.Spawn("spell_target", 5f);
        f.Run("spells_grease");
        f.Tick(4f);
        var aoe = f.World.System<EffectSystem>().AreasOfEffect.Single();
        Assert.Equal(f.Pc.Id, aoe.Creator);
        Assert.Equal(SpellGrease, aoe.SpellId);
        Assert.NotNull(aoe.LastSpell);
        Assert.Equal(SpellGrease, aoe.LastSpell!.SpellId);
        Assert.True(aoe.CasterLevel > 0);
        Assert.True(aoe.LastSpell.SaveDC >= 10);
        Assert.True(Vector3.Distance(aoe.Position, target.Position) < 0.01f);
        Assert.Contains(target.Id, aoe.Inside);
        Assert.Contains(f.Notes, n => n.Kind == "aoe-created");
        NoScriptErrors(f);
    }

    [DataFact]
    public void PotionCastsItsSpellThroughSpellcasting()
    {
        var f = Login();
        var potion = f.World.Inventory.CreateOn("nw_it_mpotion001", f.Pc, 1, "spell_potion")!;
        potion.Properties.Clear();
        // Cure Light Wounds (5), single use.
        potion.Properties.Add(new ItemProperty { Type = 15, SubType = 67, CostTable = 3, CostTableValue = 1, DurationType = 2 });
        CreatureRules.SyncHitPoints(f.World, f.Pc);
        int max = f.Pc.MaxHitPoints;
        f.Pc.CurrentHitPoints = max - 30;
        f.Tick(0.2f);
        f.Run("spells_item");
        Assert.Equal(19, f.Pc.Actions[0].ActionType); // ACTION_ITEMCASTSPELL
        f.Tick(6f);
        int healed = f.Pc.CurrentHitPoints - (max - 30);
        _out.WriteLine($"potion healed {healed}");
        Assert.InRange(healed, 1 + 5, 8 + 5); // d8 + caster level 5 from iprp_spells
        Assert.True(potion.IsDestroyed); // single use
        Assert.Equal(SpellCureLight, f.Pc.LastSpell!.SpellId);
        Assert.Equal(5, f.Pc.LastSpell.CasterLevel);
        Assert.Equal(potion.Id, f.Pc.LastSpell.CastItem);
        Assert.Contains(f.Notes, n => n.Kind == "spell-cast" && n.Data.GetProperty("item").GetUInt32() == potion.Id);
        NoScriptErrors(f);
    }

    [DataFact]
    public void SpellTargetingOverrideIsSentToThePlayer()
    {
        var f = Login();
        f.World.Scripts!.Run("spells_ctx", f.Pc.Id); // harmless: no spell yet
        Assert.Equal(-1, f.M.Ints["ctx_spell"]);
        Assert.Equal(255, f.M.Ints["ctx_class"]);
        f.World.System<SpellSystem>().SetTargeting(f.Pc, SpellGrease, 1, 5f, 0f, 1);
        f.Drain();
        Assert.Contains(f.Notes, n => n.Kind == "spell-targeting" && n.Data.GetProperty("spell").GetInt32() == SpellGrease);
        Assert.Equal(5f, f.World.System<SpellSystem>().Targeting(f.Pc, SpellGrease)!.Value.SizeX);
    }
}

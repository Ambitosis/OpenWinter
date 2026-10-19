using System.Text.Json;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Game.Persistence;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Requests;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Game.Society;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class FamiliarPossessionTests
{
    static Creature Familiar(World w, Creature master, int type = 3)
    {
        var f = w.Creatures.FromBlueprint("nw_fm_bat01")!;
        f.Scripts.Clear(); w.AddToArea(f, master.Area!, master.Position, 0);
        AssociateService.Add(w, master, f, type); return f;
    }
    static void Request(LoopbackConnection client, string kind, object? data = null) =>
        client.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(data ?? new { }) }.Write());
    static void Script(World w, string body)
    {
        var code = NssCompiler.Compile("void main(){" + body + "}", "possess_probe", w.Scripts!.Definitions, _ => null, out var error);
        Assert.True(code != null, error);
        w.Scripts.Register("possess_probe", new NcsProgram("possess_probe", code!));
        w.Scripts.Run("possess_probe", w.Module.Id);
    }

    [DataFact]
    public void RequestsBindToOwnFamiliarAndScriptIdentityMatchesNative()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (a, client) = f.Join("Alice"); var (b, _) = f.Join("Bob");
        var w = f.Server.World; var master = a.Creature!; master.Tag = "master";
        w.SetTag(master, "master");
        var own = Familiar(w, master); w.SetTag(own, "familiar");
        var foreign = Familiar(w, b.Creature!);
        Request(client, "possess-familiar", new { target = foreign.Id, owner = b.Creature!.Id }); f.Server.Tick(.01f);
        Assert.Same(master, a.Creature);
        int faction = master.Faction;
        Request(client, "possess-familiar", new { target = own.Id }); f.Server.Tick(.01f);
        Assert.Same(own, a.Creature); Assert.Same(master, a.PlayerCreature);
        Assert.False(master.IsPC); Assert.True(own.IsPC);
        Assert.Equal(faction, PartyService.Of(w).Ensure(own));
        Assert.Equal(new[] { master }, FamiliarPossession.Players(w).Where(c => c == master || c == own));
        Script(w, "object m=GetObjectByTag(\"master\"); object f=GetObjectByTag(\"familiar\"); SetLocalInt(OBJECT_SELF,\"masterpc\",GetIsPC(m)); SetLocalInt(OBJECT_SELF,\"familiarpc\",GetIsPC(f)); SetLocalInt(OBJECT_SELF,\"possessed\",GetIsPossessedFamiliar(f)); SetLocalInt(OBJECT_SELF,\"first\",GetFirstPC()==m); UnpossessFamiliar(m);");
        Assert.Equal(0, w.Module.Locals.Ints["masterpc"]); Assert.Equal(1, w.Module.Locals.Ints["familiarpc"]);
        Assert.Equal(1, w.Module.Locals.Ints["possessed"]); Assert.Equal(1, w.Module.Locals.Ints["first"]);
        Assert.Same(own, a.Creature);
        Request(client, "unpossess-familiar"); f.Server.Tick(.01f);
        Assert.Same(master, a.Creature); Assert.True(master.IsPC); Assert.False(own.IsPC);
        Assert.Null(a.FamiliarMaster); Assert.Null(master.FamiliarController);
    }

    [DataFact]
    public void InventoryIsDeniedWhilePossessedButCharacterSheetUsesFamiliar()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (session, client) = f.Join("Alice");
        var w = f.Server.World; var master = session.Creature!; var familiar = Familiar(w, master);
        Assert.True(w.System<FamiliarPossession>().Possess(session, familiar));
        ModuleTransitionTests.Fixture.Drain(client);
        void Request(string kind) => ClientRequests.Handle(w, session.Creature!, kind, "{}");
        List<NotifyMessage> Notes() => ModuleTransitionTests.Fixture.Drain(client)
            .Select(b => new MessageReader(b)).Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read).ToList();
        Request("inventory"); Request("character");
        var notes = Notes();
        Assert.DoesNotContain(notes, n => n.Kind == "inventory-open");
        Assert.Equal(familiar.Id, Assert.Single(notes, n => n.Kind == "inventory-denied").Subject);
        var sheet = JsonDocument.Parse(Assert.Single(notes, n => n.Kind == "character-sheet").Json).RootElement;
        Assert.Equal(familiar.Id, sheet.GetProperty("creature").GetUInt32());
        w.System<FamiliarPossession>().Unpossess(familiar); Notes(); Request("inventory");
        Assert.Equal(master.Id, Assert.Single(Notes(), n => n.Kind == "inventory-open").Subject);
    }

    [DataFact]
    public void HudSwitchesSelfAndOmitsControlledFamiliarFromPartyThenResendsUnchangedMaster()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (session, client) = f.Join("Alice");
        var w = f.Server.World; var master = session.Creature!; var familiar = Familiar(w, master);
        f.Server.Tick(.3f); ModuleTransitionTests.Fixture.Drain(client);
        Assert.True(w.System<FamiliarPossession>().Possess(session, familiar)); f.Server.Tick(.01f);
        JsonElement Hud()
        {
            var messages = ModuleTransitionTests.Fixture.Drain(client).Select(b => new MessageReader(b))
                .Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read).Where(n => n.Kind == "hud").ToList();
            return JsonDocument.Parse(Assert.Single(messages).Json).RootElement.Clone();
        }
        var possessed = Hud(); Assert.Equal(familiar.Id, possessed.GetProperty("self").GetProperty("id").GetUInt32());
        Assert.Equal(master.Id, Assert.Single(possessed.GetProperty("party").EnumerateArray()).GetProperty("id").GetUInt32());
        w.System<FamiliarPossession>().Unpossess(familiar); f.Server.Tick(.01f);
        var released = Hud(); Assert.Equal(master.Id, released.GetProperty("self").GetProperty("id").GetUInt32());
        Assert.Equal(familiar.Id, Assert.Single(released.GetProperty("party").EnumerateArray()).GetProperty("id").GetUInt32());
    }

    [DataFact]
    public void CampaignAndVaultIdentityRemainDistinctFromControl()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (session, _) = f.Join("Named Player");
        var w = f.Server.World; var master = session.Creature!; master.Gold = 735;
        var familiar = Familiar(w, master);
        string identity = CampaignDatabase.PlayerId(w, master);
        Assert.NotEmpty(identity); Assert.Equal("", CampaignDatabase.PlayerId(w, familiar));
        Assert.True(w.System<FamiliarPossession>().Possess(session, familiar));
        Assert.Equal("", CampaignDatabase.PlayerId(w, master));
        Assert.Equal(identity, CampaignDatabase.PlayerId(w, familiar));
        string fromMaster = ServerVault.Export(w, master), fromControlled = ServerVault.Export(w, familiar);
        Assert.Equal(fromMaster, fromControlled);
        Assert.Equal("NamedPlayer", Path.GetFileName(Path.GetDirectoryName(fromMaster)));
        var bic = Gff.Read(File.ReadAllBytes(fromMaster));
        Assert.Equal(735, bic.GetInt("Gold"));
        Assert.Equal(master.AppearanceType, bic.GetInt("Appearance_Type"));
        Assert.Empty(familiar.VaultFile);
        w.System<FamiliarPossession>().Unpossess(familiar);
        Assert.Equal(identity, CampaignDatabase.PlayerId(w, master));
        Assert.Equal("", CampaignDatabase.PlayerId(w, familiar));
    }

    [DataFact]
    public void ScriptCannotDestroyPossessedFamiliarButEngineRemovalRestoresMaster()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (s, _) = f.Join("Alice");
        var w = f.Server.World; var master = s.Creature!; var familiar = Familiar(w, master);
        w.SetTag(familiar, "controlled");
        Assert.True(w.System<FamiliarPossession>().Possess(s, familiar));
        Script(w, "DestroyObject(GetObjectByTag(\"controlled\"));"); w.Step(.01f);
        Assert.False(familiar.IsDestroyed); Assert.Same(familiar, s.Creature);
        w.Destroy(familiar);
        Assert.Same(master, s.Creature); Assert.Null(s.FamiliarMaster);
    }

    [DataFact]
    public void WrongAssociateDeadLockedAndDmRequestsAreRejected()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (s, _) = f.Join("Alice");
        var w = f.Server.World; var master = s.Creature!; var familiar = Familiar(w, master, 2);
        var system = w.System<FamiliarPossession>();
        Assert.False(system.Possess(s, familiar)); familiar.AssociateType = 3;
        familiar.IsDead = true; Assert.False(system.Possess(s, familiar)); familiar.IsDead = false;
        master.Commandable = false; Assert.False(system.Possess(s, familiar)); master.Commandable = true;
        s.IsDM = true; Assert.False(system.Possess(s, familiar)); s.IsDM = false;
        Assert.True(system.Possess(s, familiar)); Assert.False(system.Possess(s, familiar));
    }

    [DataFact]
    public void DeathAndAssociateRemovalReturnControlWithoutChangingCharacter()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (s, _) = f.Join("Alice");
        var w = f.Server.World; var master = s.Creature!; var familiar = Familiar(w, master);
        var system = w.System<FamiliarPossession>(); Assert.True(system.Possess(s, familiar));
        HitPoints.Die(w, familiar, master.Id);
        Assert.Same(master, s.Creature); Assert.Null(s.FamiliarMaster);
        w.Step(.01f); Assert.Equal(GameObject.InvalidId, familiar.Master);
        var replacement = Familiar(w, master); Assert.True(system.Possess(s, replacement));
        AssociateService.Remove(w, master, replacement);
        Assert.Same(master, s.Creature); Assert.Null(replacement.Player);
    }

    [DataFact]
    public void SnapshotPreservesOriginalIdentityAndRestoresControlLink()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (s, _) = f.Join("Alice");
        var w = f.Server.World; var master = s.Creature!; var familiar = Familiar(w, master);
        Assert.True(w.System<FamiliarPossession>().Possess(s, familiar));
        var snapshot = WorldSnapshot.Capture(w, out _);
        Assert.Equal("Alice", snapshot["players"]![0]!["player"]!.GetValue<string>());
        using var rm = OpenWinter.Aurora.Resources.ResMan.ForModule(w.Resources.Paths, f.Module("source"));
        var restoredWorld = new World(rm); restoredWorld.EnableScripts(); restoredWorld.LoadModule();
        var restored = WorldSnapshot.Apply(restoredWorld, snapshot);
        var pc = Assert.Single(restored.Players).Creature;
        Assert.Equal(master.Id, pc.Id); Assert.Equal(familiar.Id, pc.PossessedFamiliar);
        f.Server.SwapWorld(restoredWorld, _ => pc);
        Assert.Equal(familiar.Id, s.Creature!.Id); Assert.Same(pc, s.PlayerCreature);
        Assert.True(s.Creature.IsPC); Assert.False(pc.IsPC);
    }

    [DataFact]
    public void DisconnectReturnsOwnershipBeforeRemovingOriginalPlayer()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (s, client) = f.Join("Alice");
        var w = f.Server.World; var master = s.Creature!; var familiar = Familiar(w, master);
        Assert.True(w.System<FamiliarPossession>().Possess(s, familiar));
        client.Disconnect(); f.Server.Tick(.01f);
        Assert.True(master.IsDestroyed); Assert.Null(familiar.Player); Assert.Null(s.FamiliarMaster);
    }

    static void Register(World w, string name, string body)
    {
        var code = NssCompiler.Compile("void main(){" + body + "}", name, w.Scripts!.Definitions, _ => null, out var error);
        Assert.True(code != null, error);
        w.Scripts.Register(name, new NcsProgram(name, code!));
    }

    [DataFact]
    public void MasterHurtOrKilledWhilePossessedTakesControlBackAndDiesAsPlayer()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (session, _) = f.Join("Alice");
        var w = f.Server.World; var master = session.Creature!; var familiar = Familiar(w, master);
        var possession = w.System<FamiliarPossession>();
        Register(w, "possess_dying", "SetLocalInt(GetModule(),\"dying\",1);");
        Register(w, "possess_death", "SetLocalInt(GetModule(),\"died\",1);");
        w.Module.Scripts["Mod_OnPlrDying"] = "possess_dying";
        w.Module.Scripts["Mod_OnPlrDeath"] = "possess_death";
        master.CurrentHitPoints = Math.Max(master.CurrentHitPoints, 5);

        Assert.True(possession.Possess(session, familiar));
        HitPoints.Damage(w, master, GameObject.InvalidId, 1, 8);
        Assert.Same(master, session.Creature); Assert.True(master.IsPC); Assert.Null(master.FamiliarController);
        Assert.False(familiar.IsPC);

        // A lethal blow while possessed: the master is a player character again before death is decided.
        Assert.True(possession.Possess(session, familiar));
        HitPoints.Damage(w, master, GameObject.InvalidId, master.CurrentHitPoints + 50, 8);
        f.Server.Tick(.01f);
        Assert.True(master.IsDead); Assert.True(master.IsPC); Assert.Same(master, session.Creature);
        Assert.Null(session.FamiliarMaster); Assert.Null(familiar.Player);
        Assert.Equal(1, w.Module.Locals.Ints.GetValueOrDefault("died"));
        // A dead player character is never decayed into a body bag.
        for (int i = 0; i < 80; i++) f.Server.Tick(.1f);
        Assert.False(master.IsDestroyed); Assert.Same(master, w.Get<Creature>(master.Id));
    }

    [DataFact]
    public void ScriptsReachThePossessingPlayerThroughItsCharacter()
    {
        using var f = new ModuleTransitionTests.Fixture(); var (session, client) = f.Join("Alice");
        var w = f.Server.World; var master = session.Creature!; var familiar = Familiar(w, master);
        var db = w.System<PersistenceSystem>().ObjectDb(master);
        Assert.True(w.System<FamiliarPossession>().Possess(session, familiar));
        ModuleTransitionTests.Fixture.Drain(client);
        Script(w, "object pc=GetFirstPC(); SendMessageToPC(pc,\"to master\"); SetLocalString(OBJECT_SELF,\"name\",GetPCPlayerName(pc)); SetLocalString(OBJECT_SELF,\"key\",GetPCPublicCDKey(pc));");
        Assert.Contains(ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Chat)
            .Select(p => ChatMessage.Read(new MessageReader(p))), m => m.Text == "to master");
        Assert.Equal("Alice", w.Module.Locals.Strings["name"]); Assert.Equal("key-Alice", w.Module.Locals.Strings["key"]);
        Assert.Same(db, w.System<PersistenceSystem>().ObjectDb(master));
        Assert.Same(db, w.System<PersistenceSystem>().ObjectDb(familiar));
        Script(w, "BootPC(GetFirstPC(),\"bye\");");
        Assert.False(session.Connection.IsConnected);
    }
}

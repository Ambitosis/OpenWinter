using System.Numerics;
using System.Text.Json;
using OpenWinter.Net;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Society;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Party management (docs/PARTY.md): invitations, joining, leaving, kicking and the leadership with the original's
/// talk-table lines and who reads them; One Party Only; the area's PvP setting in the attack rules; the kill
/// experience split between players of different levels; SetCommandingPlayer.
/// </summary>
public sealed class PartyTests
{
    sealed class Log : IWorldOutput
    {
        public readonly List<(string To, string Text)> Lines = new();
        public readonly List<(string Kind, string To, object Data)> Notifies = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) => Lines.Add((pc.Tag, text));
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) => Notifies.Add((kind, player?.Tag ?? "", data));
        public string[] To(string tag) => Lines.Where(l => l.To == tag).Select(l => l.Text).ToArray();
        public void Clear() { Lines.Clear(); Notifies.Clear(); }
    }

    sealed class Scene
    {
        public readonly World World;
        public readonly Area Area;
        public readonly Log Log = new();
        public PartyService Parties => PartyService.Of(World);

        public Scene(bool oneParty = false)
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources) { OneParty = oneParty };
            World.Register(World.Module);
            Area = new Area { ResRef = "party", Width = 8, Height = 8 };
            World.Register(Area); World.Areas.Add(Area);
            World.Output = Log;
        }

        public Creature Player(string name, float x = 20, (int Class, int Level)? cls = null, int xp = 0)
        {
            var c = World.Creatures.FromTemplate(new GffStruct());
            c.Classes.Add(new CreatureClass { Class = cls?.Class ?? 4, Level = cls?.Level ?? 1 });
            c.Race = 6; c.AppearanceType = 6; c.Tag = name;
            c.Name = new LocString(name);
            c.Experience = xp;
            c.MaxHitPoints = c.CurrentHitPoints = 50;
            c.Player = new PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };
            World.Register(c); CreatureRules.EnsureInit(World, c);
            World.AddToArea(c, Area, new Vector3(x, 20, 0), 0, exact: true);
            return c;
        }

        public Creature Goblin(Vector3 at)
        {
            var c = World.Creatures.FromTemplate(new GffStruct());
            c.Classes.Add(new CreatureClass { Class = 12, Level = 1 });
            c.Race = 6; c.AppearanceType = 6; c.Tag = "Goblin"; c.ChallengeRating = 0.5f;
            c.MaxHitPoints = c.CurrentHitPoints = 1;
            World.Register(c); CreatureRules.EnsureInit(World, c);
            World.AddToArea(c, Area, at, 0, exact: true);
            return c;
        }

        public string Tlk(int strref, Creature? about = null) =>
            World.TlkStringOverridden(strref).Replace("<CUSTOM0>", about?.Tag ?? "<CUSTOM0>");
    }

    [DataFact]
    public void AnInvitationAcceptedJoinsTheInvitersPartyAndTellsEveryone()
    {
        var s = new Scene();
        var a = s.Player("Anna"); var b = s.Player("Bors", 24); var c = s.Player("Cara", 28);
        s.Parties.ListenToPlayerList(c);
        Assert.True(s.Parties.Invite(a, b));
        Assert.Contains(s.Tlk(PartyService.SentInvitation, b), s.Log.To("Anna"));
        Assert.Contains(s.Tlk(PartyService.ReceivedInvitation, a), s.Log.To("Bors"));
        Assert.Contains(s.Log.Notifies, n => n.Kind == "party-invitation" && n.To == "Bors");
        Assert.Equal(a.Id, s.Parties.InvitedBy(b));
        s.Log.Clear();
        Assert.True(s.Parties.Answer(b, a, "accept"));
        Assert.True(s.Parties.SameParty(a, b));
        Assert.Equal(a, s.Parties.Leader(a.Faction));
        Assert.Equal(new[] { a, b }, s.Parties.PartyOf(b));
        Assert.Contains(s.Tlk(PartyService.JoinedParty, b), s.Log.To("Anna"));
        Assert.Contains(s.Tlk(PartyService.JoinedParty, b), s.Log.To("Bors"));
        Assert.Empty(s.Log.To("Cara"));
        // The list of every player who keeps one is refreshed.
        Assert.Contains(s.Log.Notifies, n => n.Kind == "player-list" && n.To == "Cara");
        Assert.Equal(GameObject.InvalidId, s.Parties.InvitedBy(b));
    }

    [DataFact]
    public void OnlyTheLeaderInvitesAndOnlyPlayersOnTheirOwn()
    {
        var s = new Scene();
        var a = s.Player("Anna"); var b = s.Player("Bors", 24); var c = s.Player("Cara", 28); var d = s.Player("Dana", 32);
        s.Parties.Join(b, a);
        s.Log.Clear();
        Assert.False(s.Parties.Invite(b, c));
        Assert.Equal(new[] { s.Tlk(PartyService.LeaderOnly) }, s.Log.To("Bors"));
        Assert.False(s.Parties.Invite(c, b));
        Assert.Equal(new[] { s.Tlk(PartyService.AlreadyInParty, b) }, s.Log.To("Cara"));
        // One open invitation at a time: a second inviter is told the player is already considering one.
        Assert.True(s.Parties.Invite(a, c));
        s.Log.Clear();
        Assert.False(s.Parties.Invite(d, c));
        Assert.Equal(new[] { s.Tlk(PartyService.AlreadyConsidering, c) }, s.Log.To("Dana"));
    }

    [DataFact]
    public void RejectingAndIgnoringTellBothAndIgnoringBlocksTheInviter()
    {
        var s = new Scene();
        var a = s.Player("Anna"); var b = s.Player("Bors", 24);
        s.Parties.Invite(a, b);
        s.Log.Clear();
        Assert.True(s.Parties.Answer(b, a, "reject"));
        Assert.Equal(new[] { s.Tlk(PartyService.YouRejected, a) }, s.Log.To("Bors"));
        Assert.Equal(new[] { s.Tlk(PartyService.RejectedYours, b) }, s.Log.To("Anna"));
        Assert.False(s.Parties.SameParty(a, b));
        s.Parties.Invite(a, b);
        s.Log.Clear();
        Assert.True(s.Parties.Answer(b, a, "ignore"));
        Assert.Equal(new[] { s.Tlk(PartyService.YouIgnore, a) }, s.Log.To("Bors"));
        Assert.Equal(new[] { s.Tlk(PartyService.IgnoringYou, b) }, s.Log.To("Anna"));
        s.Log.Clear();
        Assert.False(s.Parties.Invite(a, b));
        Assert.Equal(new[] { s.Tlk(PartyService.IgnoringYou, b) }, s.Log.To("Anna"));
        Assert.Empty(s.Log.To("Bors"));
    }

    [DataFact]
    public void AnInvitationExpires()
    {
        var s = new Scene();
        var a = s.Player("Anna"); var b = s.Player("Bors", 24);
        s.Parties.Invite(a, b);
        for (float t = 0; t <= PartyService.InvitationLifetime + 1; t += 0.5f) s.World.Step(0.5f);
        Assert.Equal(GameObject.InvalidId, s.Parties.InvitedBy(b));
        s.Log.Clear();
        Assert.False(s.Parties.Answer(b, a, "accept"));
        Assert.Equal(new[] { s.Tlk(PartyService.Expired, a) }, s.Log.To("Bors"));
        Assert.False(s.Parties.SameParty(a, b));
    }

    [DataFact]
    public void LeavingKickingAndPassingTheLeadership()
    {
        var s = new Scene();
        var a = s.Player("Anna"); var b = s.Player("Bors", 24); var c = s.Player("Cara", 28);
        s.Parties.Join(b, a); s.Parties.Join(c, a);
        s.Log.Clear();
        // The leader passes the leadership: everyone is told.
        Assert.False(s.Parties.TransferLeadership(b, c));
        Assert.Equal(new[] { s.Tlk(PartyService.LeaderOnly) }, s.Log.To("Bors"));
        s.Log.Clear();
        Assert.True(s.Parties.TransferLeadership(a, b));
        Assert.Equal(b, s.Parties.Leader(a.Faction));
        foreach (var who in new[] { "Anna", "Bors", "Cara" }) Assert.Equal(new[] { s.Tlk(PartyService.NowLeader, b) }, s.Log.To(who));
        s.Log.Clear();
        // Only the leader kicks.
        Assert.False(s.Parties.Kick(a, c));
        Assert.True(s.Parties.Kick(b, c));
        Assert.False(s.Parties.SameParty(b, c));
        Assert.Equal(new[] { s.Tlk(PartyService.YouWereKicked) }, s.Log.To("Cara"));
        Assert.Contains(s.Tlk(PartyService.WasKicked, c), s.Log.To("Bors"));
        Assert.Contains(s.Tlk(PartyService.WasKicked, c), s.Log.To("Anna"));
        s.Parties.Join(c, b);
        s.Log.Clear();
        // The leader leaves: the member who joined first leads.
        Assert.True(s.Parties.LeaveByPlayer(b));
        Assert.Equal(new[] { s.Tlk(PartyService.YouLeft) }, s.Log.To("Bors"));
        Assert.Equal(new[] { s.Tlk(PartyService.LeftParty, b), s.Tlk(PartyService.NowLeader, a) }, s.Log.To("Anna"));
        Assert.Equal(new[] { s.Tlk(PartyService.LeftParty, b), s.Tlk(PartyService.NowLeader, a) }, s.Log.To("Cara"));
        Assert.Equal(a, s.Parties.Leader(c.Faction));
        Assert.Equal(new[] { a, c }, s.Parties.PartyOf(a));
        // GetFirst/NextFactionMember (PCs only) walks the party leader first.
        Assert.Equal(new[] { a, c }, s.World.System<SocietyService>().FactionMembers(a.Faction, pcOnly: true));
    }

    [DataFact]
    public void APlayerLeavingTheServerLeavesHerParty()
    {
        var s = new Scene();
        var a = s.Player("Anna"); var b = s.Player("Bors", 24); var c = s.Player("Cara", 28);
        s.Parties.Join(b, a); s.Parties.Join(c, a);
        s.Log.Clear();
        s.World.Destroy(a);
        Assert.Equal(b, s.Parties.Leader(b.Faction));
        Assert.Equal(new[] { s.Tlk(PartyService.LeftParty, a), s.Tlk(PartyService.NowLeader, b) }, s.Log.To("Cara"));
    }

    [DataFact]
    public void OnePartyOnlyPutsEveryoneInOnePartyAndNobodyLeaves()
    {
        var s = new Scene(oneParty: true);
        var a = s.Player("Anna"); var b = s.Player("Bors", 24);
        Assert.True(s.Parties.SameParty(a, b));
        Assert.Equal(a, s.Parties.Leader(b.Faction));
        s.Log.Clear();
        Assert.False(s.Parties.LeaveByPlayer(b));
        Assert.Equal(new[] { s.Tlk(PartyService.OnePartyCantLeave) }, s.Log.To("Bors"));
        Assert.False(s.Parties.Kick(a, b));
        Assert.Equal(new[] { s.Tlk(PartyService.OnePartyCantKick) }, s.Log.To("Anna"));
        Assert.True(s.Parties.SameParty(a, b));
    }

    [DataFact]
    public void TheAreasPvpSettingDecidesWhoMayAttackWhom()
    {
        var s = new Scene();
        var a = s.Player("Anna"); var b = s.Player("Bors", 24); var c = s.Player("Cara", 28);
        s.Parties.Join(b, a);
        s.World.Pvp = PvpSetting.Full;
        Assert.True(s.World.PvpAllows(a, b));
        s.Area.Are.SetByte("PlayerVsPlayer", 1);
        Assert.False(s.World.PvpAllows(a, b));   // party protected: her own party
        Assert.True(s.World.PvpAllows(a, c));
        s.Area.Are.SetByte("PlayerVsPlayer", 0);
        Assert.False(s.World.PvpAllows(a, c));   // no PvP here
        // Never more than the server's setting.
        s.World.Pvp = PvpSetting.Party;
        s.Area.Are.SetByte("PlayerVsPlayer", 2);
        Assert.False(s.World.PvpAllows(a, b));
        Assert.True(s.World.PvpAllows(a, c));
    }

    [DataFact]
    public void KillExperienceUsesThePartysAverageRowSharedByThePlayers()
    {
        // A level 15 fighter kills a goblin beside a level 1 wizard in her party. Both take the row of the players'
        // average level, truncated (8: xptable C0 is 7), divided by the players: 3 each (the
        // original's split between two players is not measured, docs/KILL_XP.md).
        var s = new Scene();
        var fighter = s.Player("Fighter", 20, (4, 15), xp: 105000);
        var wizard = s.Player("Wizard", 23, (10, 1));
        s.Parties.Join(wizard, fighter);
        var goblin = s.Goblin(new Vector3(21, 21, 0));
        HitPoints.Kill(s.World, goblin, fighter.Id);
        s.World.Step(0.05f);
        Assert.Equal(105000 + 3, fighter.Experience);
        Assert.Equal(3, wizard.Experience);
    }

    [DataFact]
    public void AlignmentShiftsReachThePartyAtHalfStrength()
    {
        var s = new Scene();
        var a = s.Player("Anna"); var b = s.Player("Bors", 24); var c = s.Player("Cara", 28);
        s.Parties.Join(b, a);
        int before = b.GoodEvil, other = c.GoodEvil;
        CreatureRules.AdjustAlignment(s.World, a, NwRules.AlignGood, 10, allParty: true);
        Assert.Equal(before + 5, b.GoodEvil);
        Assert.Equal(other, c.GoodEvil);
    }

    [DataFact]
    public void SetCommandingPlayerOnlyCountsWithPlayerPartyControl()
    {
        var s = new Scene();
        var a = s.Player("Anna"); var b = s.Player("Bors", 24);
        var hench = s.Goblin(new Vector3(21, 22, 0));
        AssociateService.Add(s.World, a, hench, AssociateService.Henchman);
        hench.CommandingPlayer = b.Id;
        Assert.True(AssociateService.CommandableBy(s.World, a, hench));
        Assert.False(AssociateService.CommandableBy(s.World, b, hench));
        s.World.PlayerPartyControl = true;
        Assert.False(AssociateService.CommandableBy(s.World, a, hench));
        Assert.True(AssociateService.CommandableBy(s.World, b, hench));
        hench.CommandingPlayer = s.World.Module.Id;   // anything but a player: nobody
        Assert.Null(s.World.CommandingPlayer(hench));
        Assert.False(AssociateService.CommandableBy(s.World, a, hench));
    }

    // ------------------------------------------------------------------ through the server, as the clients ask

    sealed class Client
    {
        public required LoopbackConnection Connection;
        public uint Me;
        public readonly List<NotifyMessage> Notifies = new();
        public readonly List<string> Chat = new();

        public void Pump()
        {
            while (Connection.TryReceive(out var m))
            {
                var r = new MessageReader(m);
                if (r.Type == MessageType.ControlObject) Me = ControlObjectMessage.Read(r).ObjectId;
                else if (r.Type == MessageType.Notify) Notifies.Add(NotifyMessage.Read(r));
                else if (r.Type == MessageType.Chat) Chat.Add(ChatMessage.Read(r).Text);
            }
        }

        public void Request(string kind, object? data = null) =>
            Connection.Send(new ClientRequestMessage { Kind = kind, Json = data == null ? "" : JsonSerializer.Serialize(data) }.Write());

        public JsonElement Last(string kind) => JsonDocument.Parse(Notifies.Last(n => n.Kind == kind).Json).RootElement;
    }

    static Client Join(GameServer server, string name)
    {
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = name }.Write());
        return new Client { Connection = client };
    }

    static void Tick(GameServer server, params Client[] clients)
    {
        for (int i = 0; i < 3; i++) server.Tick(1f / 30);
        foreach (var c in clients) c.Pump();
    }

    [DataFact]
    public void ThePartyRequestsAndTheRadialsFacts()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town" });
        var a = Join(server, "Alice");
        Tick(server, a);
        var b = Join(server, "Bob");
        Tick(server, a, b);
        // Both clients keep a player list (the server sends it again whenever it changes).
        a.Request("player-list"); b.Request("player-list");
        Tick(server, a, b);
        // The radial's facts on another player: she can be invited.
        a.Request("abilities", new { target = b.Me });
        Tick(server, a, b);
        var facts = a.Last("abilities").GetProperty("target").GetProperty("party");
        Assert.True(facts.GetProperty("player").GetBoolean());
        Assert.True(facts.GetProperty("canInvite").GetBoolean());
        Assert.False(facts.GetProperty("sameParty").GetBoolean());
        a.Request("party-invite", new { other = b.Me });
        Tick(server, a, b);
        var invitation = b.Last("party-invitation");
        Assert.True(invitation.GetProperty("open").GetBoolean());
        Assert.Equal(a.Me, invitation.GetProperty("from").GetUInt32());
        Assert.Equal(a.Me, b.Last("player-list").GetProperty("invitedBy").GetUInt32());
        b.Request("abilities", new { target = a.Me });
        Tick(server, a, b);
        Assert.True(b.Last("abilities").GetProperty("target").GetProperty("party").GetProperty("invitedMe").GetBoolean());
        b.Request("party-answer", new { from = a.Me, answer = "accept" });
        Tick(server, a, b);
        Assert.False(b.Last("party-invitation").GetProperty("open").GetBoolean());
        var alice = world.Get<Creature>(a.Me)!; var bob = world.Get<Creature>(b.Me)!;
        Assert.True(PartyService.Of(world).SameParty(alice, bob));
        Assert.Contains(b.Chat, line => line.EndsWith("has joined the party."));
        var list = a.Last("player-list");
        Assert.True(list.GetProperty("inParty").GetBoolean());
        Assert.Equal(2, list.GetProperty("players").EnumerateArray().Single(p => p.GetProperty("id").GetUInt32() == b.Me).GetProperty("partySize").GetInt32());
        // The leader may remove her and pass the leadership; Bob may leave.
        a.Request("abilities", new { target = b.Me });
        Tick(server, a, b);
        facts = a.Last("abilities").GetProperty("target").GetProperty("party");
        Assert.True(facts.GetProperty("canKick").GetBoolean());
        Assert.True(facts.GetProperty("canTransfer").GetBoolean());
        b.Request("party-leave");
        Tick(server, a, b);
        Assert.False(PartyService.Of(world).SameParty(alice, bob));
    }

    // ------------------------------------------------------------------ spell-casting lines (castlines, castlines2)

    [DataFact]
    public void CastingLinesNameTheSpellForTheCasterAndBySpellcraftForOthers()
    {
        var s = new Scene();
        var caster = s.Player("Caster", 20, (10, 3));
        var near = s.Player("Near", 25, (10, 1));      // 5 m
        var mid = s.Player("Mid", 52, (10, 1));        // 32 m: "casting" only
        var far = s.Player("Far", 57, (10, 1));        // 37 m: nothing
        var fighter = s.Player("Fighter", 22, (4, 1)); // no Spellcraft ranks: never named
        foreach (var w in new[] { near, mid, far }) w.Skills[OpenWinter.Game.Skills.SkillIds.Spellcraft] = 40;
        const int barkskin = 21;
        string Line(int verb, bool named) => $"Caster {s.World.TlkStringOverridden(verb)} " +
            (named ? s.World.TlkStringOverridden(int.Parse(OpenWinter.Game.Spells.SpellTable.Table(s.World).Get(barkskin, "Name"))) : s.World.TlkStringOverridden(OpenWinter.Game.Spells.CastingLines.UnknownStrref));
        var known = OpenWinter.Game.Spells.CastingLines.Start(s.World, caster, barkskin, _ => 0);
        OpenWinter.Game.Spells.CastingLines.Release(s.World, caster, barkskin, known, _ => 0);
        Assert.Equal(new[] { Line(8127, true), Line(1501, true) }, s.Log.To("Caster"));
        Assert.Equal(new[] { Line(8127, true), Line(1501, true) }, s.Log.To("Near"));
        Assert.Equal(new[] { Line(8127, true) }, s.Log.To("Mid"));
        Assert.Empty(s.Log.To("Far"));
        Assert.Equal(new[] { Line(8127, false), Line(1501, false) }, s.Log.To("Fighter"));
        // d20 + rank against 15 + the innate level (Barkskin 2): rank 7 needs a 10.
        near.Skills[OpenWinter.Game.Skills.SkillIds.Spellcraft] = 7 - CreatureRules.AbilityModifier(s.World, near, 3);
        Assert.Equal(7, CreatureRules.SkillRank(s.World, near, OpenWinter.Game.Skills.SkillIds.Spellcraft));
        Assert.False(OpenWinter.Game.Spells.CastingLines.Identifies(s.World, near, caster, barkskin, _ => 8));   // 9 + 7 = 16
        Assert.True(OpenWinter.Game.Spells.CastingLines.Identifies(s.World, near, caster, barkskin, _ => 9));    // 10 + 7 = 17
    }
    // ------------------------------------------------------------------ castlines3: items, abilities, AC lines

    [DataFact]
    public void AcBonusesOfOneKindTellThePlayerTheyDontStack()
    {
        var s = new Scene();
        var pc = s.Player("P");
        var npc = s.Goblin(new Vector3(22, 20, 0));
        var k = OpenWinter.Game.Scripting.EffectConstants.Of(s.World);
        var fx = s.World.System<EffectSystem>();
        void Ac(Creature c, int amount, int type)
        {
            var e = new OpenWinter.Game.Scripting.Effect { Type = NwRules.EffectAcIncrease, SubType = k.Magical };
            e.Ints[0] = amount; e.Ints[1] = type;
            fx.Apply(e, k.Permanent, c, 0);
        }
        string natural = s.World.TlkStringOverridden(66247), deflection = s.World.TlkStringOverridden(66250);
        Ac(pc, 2, NwRules.AcNatural);
        Assert.Empty(s.Log.To("P"));
        Ac(pc, 2, NwRules.AcNatural);
        Ac(pc, 4, NwRules.AcNatural);
        Assert.Equal(new[] { natural, natural }, s.Log.To("P"));
        s.Log.Clear();
        Ac(pc, 2, NwRules.AcDodge);
        Ac(pc, 2, NwRules.AcDodge);
        Assert.Empty(s.Log.To("P"));
        Ac(pc, 2, NwRules.AcDeflection);
        Ac(pc, 1, NwRules.AcDeflection);
        Assert.Equal(new[] { deflection }, s.Log.To("P"));
        s.Log.Clear();
        Ac(npc, 2, NwRules.AcNatural);
        Ac(npc, 2, NwRules.AcNatural);
        Assert.Empty(s.Log.Lines);
    }

    [DataFact]
    public void APotionSaysWhoUsesItNearbyAndGoesWithoutLostItem()
    {
        var s = new Scene();
        var pc = s.Player("P");
        var near = s.Goblin(new Vector3(40, 20, 0));   // 20 m
        var far = s.Goblin(new Vector3(56, 20, 0));    // 36 m
        var inv = s.World.System<OpenWinter.Game.Inventory.InventorySystem>();
        string Uses(string who) => $"{who} {s.World.TlkStringOverridden(63264)} ";
        foreach (var user in new[] { pc, near, far })
        {
            var potion = inv.CreateOn("nw_it_mpotion001", user, 1, "")!;
            s.Log.Clear();
            inv.UseItemProperty(user, potion, potion.Properties[0], user.Id, user.Location, decrement: true);
            for (int i = 0; i < 30; i++) s.World.Step(1f / 30);
            Assert.True(potion.IsDestroyed);
            var lines = s.Log.To("P");
            if (user == far) Assert.DoesNotContain(lines, l => l.StartsWith(Uses(user.Tag)));
            else Assert.Contains(lines, l => l.StartsWith(Uses(user.Tag)));
            Assert.DoesNotContain(lines, l => l.StartsWith("Lost Item"));
        }
        // An unidentified wand is acquired by its base item's name; a single-use wand used up is lost.
        s.Log.Clear();
        var wand = inv.CreateOn("nw_wmgwn013", pc, 1, "")!;
        Assert.Contains("Acquired Item: Magic Wand", s.Log.To("P"));
        wand.Identified = true;
        inv.UseItemProperty(pc, wand, wand.Properties[0], GameObject.InvalidId, new OpenWinter.Game.Objects.Location(s.Area.Id, new Vector3(22, 20, 0), 0), decrement: true);
        for (int i = 0; i < 30; i++) s.World.Step(1f / 30);
        Assert.True(wand.IsDestroyed);
        Assert.Contains(s.Log.To("P"), l => l.StartsWith(Uses("P")));
        Assert.Contains(s.Log.To("P"), l => l.StartsWith("Lost Item"));
        // A carried item a script destroys is lost.
        var other = inv.CreateOn("nw_it_mpotion001", pc, 1, "")!;
        s.Log.Clear();
        s.World.Destroy(other);
        Assert.Contains(s.Log.To("P"), l => l.StartsWith("Lost Item"));
    }

    [DataFact]
    public void AbilitiesAndFeatsSayTheirAltMessageAndNoCastingLines()
    {
        var s = new Scene();
        var pc = s.Player("P");
        var npc = s.Goblin(new Vector3(25, 20, 0));
        const int aura = 203, familiar = 318;
        Assert.Equal(2, OpenWinter.Game.Spells.SpellTable.UserType(s.World, aura));
        Assert.Equal(3, OpenWinter.Game.Spells.SpellTable.UserType(s.World, familiar));
        OpenWinter.Game.Spells.CastingLines.AltMessage(s.World, npc, aura);
        OpenWinter.Game.Spells.CastingLines.AltMessage(s.World, pc, familiar);
        Assert.Equal(new[] { "Goblin is surrounded by an aura.", "P summons familiar." }, s.Log.To("P"));
    }
}

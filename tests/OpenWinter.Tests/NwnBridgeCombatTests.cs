using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAddMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;

namespace OpenWinter.Tests;

/// <summary>
/// How the NWN bridge presents combat, from frames recorded on a stock server running the bridgecombat scene: a
/// level 15 player fighting goblins, given effects, brought to dying, killed and respawned.
/// </summary>
public class NwnBridgeCombatTests
{
    const uint Player = 0x7FFFFFFF, Goblin = 0x14, Area = 1;

    sealed class Data : INwnBridgeData
    {
        static readonly Dictionary<uint, string> Tlk = new()
        {
            [511] = "hit", [514] = "miss", [981] = "Sneak Attack", [5594] = "<CUSTOM0> Physical", [10462] = "<CUSTOM0> Fire",
            [10466] = "<CUSTOM0> damages <CUSTOM1>: <CUSTOM2>", [10474] = "<CUSTOM0> killed <CUSTOM1>",
            [10470] = "<CUSTOM0> attacks <CUSTOM1> : *<CUSTOM2>* : (<CUSTOM3> <CUSTOM4> <CUSTOM5> = <CUSTOM6><CUSTOM7>)",
            [10484] = "<CUSTOM0> : <CUSTOM1> : *<CUSTOM2>* : (<CUSTOM3> <CUSTOM4> <CUSTOM5> = <CUSTOM6> vs. DC: <CUSTOM7>)",
            [519] = "Reflex Save", [520] = "Fortitude Save", [5352] = "success", [5353] = "failure", [8349] = "Someone",
            [52961] = "<CUSTOM0> : Damage Immunity absorbs <CUSTOM1> point(s) of<CUSTOM2>", [12567] = "Goblin",
            [62462] = "<CUSTOM0> : Immune to Sneak Attacks.", [1028] = "Fire",
        };

        public int TwoDARowCount(string table) => table == "baseitems" ? 200 : 0;

        public string TwoDA(string table, int row, string column) => (table, row, column) switch
        {
            ("damagetypes", 8, "DamageTypeGroup") => "6",
            ("damagetypes", _, "DamageTypeGroup") => "0",
            ("damagetypegroups", 0, "FeedbackStrref") => "5594",
            ("damagetypegroups", 6, "FeedbackStrref") => "10462",
            ("visualeffects", 119, "Type_FD") => "F",
            _ => "",
        };

        public string Text(uint strRef, byte gender) => Tlk.GetValueOrDefault(strRef, $"#{strRef}");
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    sealed class Rig
    {
        public readonly List<byte[]> Output = [];
        public readonly NwnWorldMirror World;
        public readonly NwnCombat Combat;

        public Rig()
        {
            var data = new Data();
            World = new NwnWorldMirror(data, Output.Add);
            Combat = new NwnCombat(data, World, Output.Add);
            World.Area(new AreaMessages.AreaClientArea
            {
                ObjectId = Area, Resref = "visual", DisplayName = "Visual", Width = 1, Height = 1, Tileset = "tde01",
                Tiles = [new AreaMessages.Tile()], Location = (24, 24, 0),
            });
            // The player, then the two goblins of the recorded fight (their add, appearance and update).
            List(new GameObjUpdateMessages.GameObjUpdateObjList
            {
                Entries =
                {
                    new GameObjUpdateObjListAddCreature { ObjectId = Player, Position = (24.77f, 24.22f, 0) },
                    new GameObjUpdateObjListAppearanceCreature
                    {
                        ObjectId = Player, Flags = UpdateCreatureAppearanceFlags.Name | UpdateCreatureAppearanceFlags.Race, Race = 3,
                        Name = new NwnTagged(2, new DisplayCreatureName { Name = "Aluvian Darkstar" }),
                    },
                },
            });
            World.Control(Player);
            List(Frame(GoblinsArrive));
            Combat.Tick(0.1f);
            Output.Clear();
        }

        public void List(GameObjUpdateMessages.GameObjUpdateObjList list) => Combat.ObjectList(list, World.ObjectList);

        public void Server(string hex)
        {
            var m = Message(hex);
            if (m is GameObjUpdateMessages.GameObjUpdateObjList list) List(list);
            else if (m is ClientSideMessageFeedbackMessages.ClientSideMessageFeedback feedback) Assert.True(Combat.Feedback(feedback));
            else Assert.True(Combat.ToClient(m));
        }

        IEnumerable<MessageReader> Of(MessageType type) => Output.Select(m => new MessageReader(m)).Where(r => r.Type == type);

        public List<NotifyMessage> Notices(string kind) => Of(MessageType.Notify).Select(NotifyMessage.Read).Where(n => n.Kind == kind).ToList();

        public List<string> Chat() => Of(MessageType.Chat).Select(r => ChatMessage.Read(r).Text).ToList();

        public List<UpdateMessage> Updates(uint id) => Of(MessageType.Update).Select(UpdateMessage.Read).Where(u => u.ObjectId == id).ToList();

        public DespawnMessage Despawn() => DespawnMessage.Read(Assert.Single(Of(MessageType.Despawn)));
    }

    /// <summary>What decoding the recorded frames reads from 2DAs: the goblins' spear is a composite model.</summary>
    static readonly NwnSessionContext Context = new(_ => 0, (table, row, column) => (table, column) == ("baseitems", "ModelType") ? row == 47 ? "2" : "0" : "");

    /// <summary>A recorded server frame (header 'P', major, minor, then the message).</summary>
    static NwnMessage Message(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        var m = NwnMessageRegistry.Create(bytes[1], bytes[2], NwnDirection.ServerToClient)!;
        m.Read(new NwnWsReader(bytes[3..], Context));
        return m;
    }

    static GameObjUpdateMessages.GameObjUpdateObjList Frame(string hex) => (GameObjUpdateMessages.GameObjUpdateObjList)Message(hex);

    static string Encoded(NwnMessage m)
    {
        var w = new NwnWsWriter(Context);
        m.Write(w);
        return $"70{m.Major:X2}{m.Minor:X2}" + Convert.ToHexString(w.ToArray());
    }

    // Recorded frames, in the order the server sent them.
    const string GoblinsArrive = "500501044911FFFFFF7F05002007000000000702000000111400000002010201020011160000000201020102000441030511140000000B99E8F3410BBED4CE410B000000000B6E2676BF0B40A88CBE0B000000000504000800000000080000000004500305111400000005FFFF020112010017310000120000000000055600030003000500000700000000070000000003000300030003000313050000050000050000050000050000050000050000050000050000050000050000050000050000050000050000050000050000050000050000052C0008FFFFFFFF030003060444110000007F07020000000444110000007F07010000000444110000007F07200000000444110000007F07100000000444110000007F0740000000044111150000000710000000072F000000050B00050B00050B00030008000000000800000000020112010005060000020107100000000701000000020102000200020008000000000E000000000300030003FF0300030003000300030003000300030004550305111400000007673900000B99E8F3410BBED4CE410B0000000002000B0AE4D342020005DE000B0000803F050400030005010002000BCE89CA410BCD02C34105FFFF03020500000302020111FFFFFF7F0B00C0DA440B00C05A450300050C000E000000000E000000000300060000063200020002000301030E0301110000007F050000020002000200";
    const string PlayerAttacks = "50120511FFFFFF7F111400000004140312030103000200020102000200020002000200";
    const string PlayerSwings = "5005010455030511FFFFFF7F07070000000B5DDAC5410B1CACC1410B0000000002000B9A122836020111140000000B0000803F050900030111140000000501000500000594030301050000052800020002010301030005000004550305111600000007070000000B9408CF410B68B4BB410B0000000002000B71E66342020111FFFFFF7F0B0000803F0501000300050000";
    const string BlowLands = "50120311FFFFFF7F1114000000020002000200020002000200020002000200020002000200020105280002000200020002000200020002000200020002000200020002000200020002000200020002000200";
    const string GoblinKilled = "50120411FFFFFF7F1114000000";
    const string GoblinFalls = "50050104550305111400000007470800000B8E8BCF410B0871C4410B0000000002000B26E4D34202000B0000803F05080003000500000500000300050000030202010305";
    const string ReflexSave = "50120711FFFFFF7F031E04120304030203070200";
    const string FireImmunity = "50120B053E0011FFFFFF7F080A0000000800010000";
    const string PlayerDying = "5005010455030511FFFFFF7F07070C00000B5DDAC5410B97C083410B0000000002000B9C914D4202000B0000803F050800030005000006FDFF0649000600000676000305044911FFFFFF7F0508000700000000";
    const string PlayerDead = "5005010455030511FFFFFF7F07474C00000B5DDAC5410B97C083410B0000000002000B9C914D4202000B0000803F050800030005000005000003000500000301020006ECFF06490006000006760002010200020102010200020002000305";
    const string DeathPanel = "5024010201020107000000000E00000000";

    [Fact]
    public void AnAttackBecomesTheSwingItsDamageAndTheCombatLog()
    {
        var rig = new Rig();
        // The goblins are hostile (their AI state's reaction 2), so the client colours them and a click attacks.
        rig.Combat.Tick(0.1f);
        Assert.Equal(NwnCombat.Hostile, rig.Combat.Reaction(Goblin));
        Assert.Equal("70060211" + "14000000", Encoded(rig.Combat.Click(Goblin)!));
        rig.Server(PlayerAttacks);
        rig.Server(PlayerSwings);
        rig.Server(BlowLands);
        rig.Server(GoblinKilled);
        var attack = System.Text.Json.JsonDocument.Parse(rig.Notices("attack")[0].Json).RootElement;
        Assert.Equal((Player, Goblin, true, false, 40, true), (attack.GetProperty("attacker").GetUInt32(), attack.GetProperty("target").GetUInt32(),
            attack.GetProperty("hit").GetBoolean(), attack.GetProperty("critical").GetBoolean(), attack.GetProperty("damage").GetInt32(),
            attack.GetProperty("killed").GetBoolean()));
        // Empty hands with no game data: an unarmed swing.
        Assert.StartsWith("nw", attack.GetProperty("animation").GetString());
        // The blow's damage floats with its attack; its hp-change is marked as combat damage.
        Assert.Equal("""{"target":20,"source":2147483647,"amount":-40,"type":0,"combat":true}""", Assert.Single(rig.Notices("hp-change")).Json);
        Assert.Equal(
        [
            "Sneak Attack : Aluvian Darkstar attacks Goblin : *hit* : (18 + 20 = 38)",
            "Aluvian Darkstar damages Goblin: 40 (40 Physical)",
            "Aluvian Darkstar killed Goblin",
        ], rig.Chat());
    }

    [Fact]
    public void AKilledCreatureLiesDeadAndItsCorpseFades()
    {
        var rig = new Rig();
        rig.Server(GoblinFalls);
        Assert.Equal(ObjectFlags.Dead, rig.Updates(Goblin).Last().Flags & (ObjectFlags.Dead | ObjectFlags.Dying));
        rig.Combat.Tick(0.1f);
        Assert.Contains("\"damageLevel\":5", rig.Notices("creature-labels").Last().Json);
        Assert.Equal(NwnCombat.Hostile, rig.Combat.Reaction(Goblin));
        Assert.Null(rig.Combat.Click(Goblin));
        rig.Output.Clear();
        rig.List(new GameObjUpdateMessages.GameObjUpdateObjList { Entries = { new GameObjUpdateDeleteMessages.ObjListDeleteCreature { ObjectId = Goblin, InstantDelete = true } } });
        Assert.True(rig.Despawn().DecayingCorpse);
    }

    [Fact]
    public void ThePlayerIsDyingAboveMinusTenAndDeadBelowWithTheDeathPanelAndRespawn()
    {
        var rig = new Rig();
        rig.Server(PlayerDying);
        Assert.Equal(ObjectFlags.Dying, rig.Updates(Player).Last().Flags & (ObjectFlags.Dead | ObjectFlags.Dying));
        rig.Combat.Tick(0.1f);
        var hud = System.Text.Json.JsonDocument.Parse(rig.Notices("hud").Last().Json).RootElement.GetProperty("self");
        Assert.Equal((-3, 118, false), (hud.GetProperty("hp").GetInt32(), hud.GetProperty("maxHp").GetInt32(), hud.GetProperty("dead").GetBoolean()));
        rig.Server(PlayerDead);
        Assert.Equal(ObjectFlags.Dead, rig.Updates(Player).Last().Flags & (ObjectFlags.Dead | ObjectFlags.Dying));
        rig.Server(DeathPanel);
        Assert.Equal("""{"respawn":true,"waitForHelp":true,"help":""}""", Assert.Single(rig.Notices("gui-death")).Json);
        // Respawning (or a raise, or any heal) has the server close the panel.
        Assert.True(rig.Combat.ToClient(new PopupGuiPanelMessages.PopUpGUIPanelDestroyPlayerDeathPanel()));
        Assert.Single(rig.Notices("gui-death-close"));
        // The panel's respawn button sends no arguments; the server reads an empty respawn message.
        Assert.Equal("702501", Encoded(rig.Combat.FromClient("respawn", "")!));
        Assert.Equal("70060211" + "14000000", Encoded(rig.Combat.FromClient("attack", """{"target":20}""")!));
    }

    static GameObjUpdateMessages.GameObjUpdateObjList Creature(uint id, Action<GameObjUpdateUpdateMessages.GameObjUpdateObjListUpdateCreature> set)
    {
        var u = new GameObjUpdateUpdateMessages.GameObjUpdateObjListUpdateCreature { ObjectId = id };
        set(u);
        return new GameObjUpdateMessages.GameObjUpdateObjList { Entries = { u } };
    }

    static GameObjUpdateAnimationMessages.AnimTypeData Attacks(params GameObjUpdateAnimationMessages.AttackRecord[] attacks) =>
        new() { AnimType = GameObjUpdateAnimationMessages.AnimAttack, Attacks = [.. attacks] };

    static GameObjUpdateAnimationMessages.AttackRecord Blow(uint target, ushort damage) =>
        new() { TargetId = target, Result = NwnCombatLog.ResultHit, Damage = damage, AnimLen = 1000, WeaponAttackType = 1 };

    [Fact]
    public void AClickAttacksAHostileCreatureButTheTalkCommandStillTalks()
    {
        var rig = new Rig();
        // A bystander the server rates neutral (reaction 0).
        rig.List(Creature(0x11, u => u.AiReaction = 0));
        NwnMessage Send(uint target, string argument = "") => NwnBridgeConnection.UseOrTalk(
            new ActionRequestMessage { Action = ActionKind.Talk, Target = target, Argument = argument }, ObjectKind.Creature, null, rig.Combat.Click(target));
        Assert.IsType<InputMessages.InputDialog>(Send(Goblin, ActionRequestMessage.TalkCommand));
        Assert.Equal("70060211" + "14000000", Encoded(Send(Goblin)));
        Assert.IsType<InputMessages.InputUseObject>(Send(0x11));
    }

    [Fact]
    public void LyingDownAliveIsNotDeath()
    {
        // As recorded: a living bystander playing ANIMATION_LOOPING_DEAD_FRONT is sent animation 6, DEAD_BACK 8,
        // the codes a dying creature lies in; only the dead damage level (or a player's hit points) is death.
        var rig = new Rig();
        foreach (ushort lying in new ushort[] { 6, 8 })
        {
            rig.List(Creature(Goblin, u => u.AnimTypeData = new() { AnimType = lying }));
            Assert.Equal(ObjectFlags.None, rig.Updates(Goblin).Last().Flags & (ObjectFlags.Dead | ObjectFlags.Dying));
        }
        Assert.Equal("70060211" + "14000000", Encoded(rig.Combat.Click(Goblin)!));
        // A creature that isn't a player character at negative hit points (an associate) is dead, never dying.
        rig.List(Creature(Goblin, u => (u.HpCurrent, u.HpMax, u.DamageLevel) = (-4, 20, 5)));
        Assert.Equal(ObjectFlags.Dead, rig.Updates(Goblin).Last().Flags & (ObjectFlags.Dead | ObjectFlags.Dying));
    }

    [Fact]
    public void SwingsFollowEachOtherAndTheirDamageWaitsForThem()
    {
        var rig = new Rig();
        // Two attacks in one update and a third arriving at once after a stall: one second apart, in order.
        rig.List(Creature(Goblin, u => u.AnimTypeData = Attacks(Blow(Player, 3), Blow(Player, 4))));
        rig.List(Creature(Goblin, u => u.AnimTypeData = Attacks(Blow(Player, 5))));
        int[] Damages() => rig.Notices("attack").Select(n => System.Text.Json.JsonDocument.Parse(n.Json).RootElement.GetProperty("damage").GetInt32()).ToArray();
        Assert.Equal([3], Damages());
        var damage = new ClientSideMessageCombatMessages.ClientSideMessageComplexDamage
        {
            Effector = Goblin, Target = Player,
            Damages = [.. Enumerable.Range(0, 32).Select(i => new ClientSideMessageCombatMessages.DamageEntry { HasDamage = i == 12, Damage = i == 12 ? (ushort)3 : null })],
        };
        rig.Combat.ToClient(damage);
        rig.Combat.ToClient(damage);
        // The first blow's damage goes now, the second's with its swing.
        Assert.Single(rig.Notices("hp-change"));
        for (int i = 0; i < 10; i++) rig.Combat.Tick(0.1f);
        Assert.Equal([3, 4], Damages());
        Assert.Equal(2, rig.Notices("hp-change").Count);
        for (int i = 0; i < 10; i++) rig.Combat.Tick(0.1f);
        Assert.Equal([3, 4, 5], Damages());
        Assert.All(rig.Notices("hp-change"), n => Assert.Contains("\"combat\":true", n.Json));
        // A hit whose damage never came stops waiting: later damage (a spell) floats on its own.
        for (int i = 0; i < 30; i++) rig.Combat.Tick(0.1f);
        rig.Combat.ToClient(damage);
        Assert.Contains("\"combat\":false", rig.Notices("hp-change").Last().Json);
    }

    [Fact]
    public void LastingEffectsAreToldApartAndImpactsOutsideTheWorldAreDropped()
    {
        var rig = new Rig();
        GameObjUpdateSharedMessages.VisualEffectUpdate Change(char change) => new() { ChangeType = (byte)change, EffectId = 15 };
        rig.List(Creature(Player, u => u.Effects = [Change('A'), Change('A')]));
        rig.List(Creature(Player, u => u.Effects = [Change('D'), Change('D')]));
        long Id(NotifyMessage n) => System.Text.Json.JsonDocument.Parse(n.Json).RootElement.GetProperty("id").GetInt64();
        var shown = rig.Notices("vfx").Select(Id).ToList();
        Assert.Equal(2, shown.Distinct().Count());
        Assert.Equal(shown.OrderBy(i => i), rig.Notices("effect-removed").Select(Id).OrderBy(i => i));
        Assert.True(rig.Combat.ToClient(new GameObjUpdateMessages.GameObjUpdateVisEffect { TargetId = 0x99, VisualEffectId = 119, TargetLocation = (float.NaN, 1, 0) }));
        Assert.Equal(2, rig.Notices("vfx").Count);
    }

    [Fact]
    public void ANaturalOneFailsOnlyWhenTheServerSaysSo()
    {
        var rig = new Rig();
        ClientSideMessageCombatMessages.ClientSideMessageSavingThrow Save(bool autoFail) =>
            new() { Target = Player, DifficultyClass = 10, Modifier = 15, DieRoll = 1, AutoFailOn1 = autoFail, SaveType = 2 };
        rig.Combat.ToClient(Save(true));
        rig.Combat.ToClient(Save(false));
        Assert.Equal(
        [
            "Aluvian Darkstar : Reflex Save : *failure* : (1 + 15 = 16 vs. DC: 10)",
            "Aluvian Darkstar : Reflex Save : *success* : (1 + 15 = 16 vs. DC: 10)",
        ], rig.Chat());
    }

    [Fact]
    public void SavesAndDefencesArePrintedAsTheOriginalPrintsThem()
    {
        var rig = new Rig();
        rig.Server(ReflexSave);
        rig.Server(FireImmunity);
        Assert.Equal(
        [
            "Aluvian Darkstar : Reflex Save vs. Fire : *failure* : (4 + 18 = 22 vs. DC: 30)",
            "Aluvian Darkstar : Damage Immunity absorbs 10 point(s) of Fire",
        ], rig.Chat());
    }

    [Fact]
    public void TheHudFollowsTheTargetAndThePlayersEffectIcons()
    {
        var rig = new Rig();
        Assert.Null(rig.Combat.FromClient("hud-target", """{"id":20}"""));
        rig.List(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries =
            {
                new GameObjUpdatePlayerinfoMessages.GameObjUpdateObjListPlayerInfo
                {
                    ObjectId = Player, Flags = GameObjUpdatePlayerinfoMessages.UpdatePlayerCreatureFlags.EffectIcons,
                    EffectIcons = new GameObjUpdatePlayerinfoMessages.EffectIconsUpdate { Add = [new() { Icon = 25 }, new() { Icon = 116 }] },
                },
            },
        });
        rig.Combat.Tick(0.1f);
        var hud = System.Text.Json.JsonDocument.Parse(rig.Notices("hud").Last().Json).RootElement;
        Assert.Equal([25, 116], hud.GetProperty("self").GetProperty("effects").EnumerateArray().Select(e => e.GetInt32()));
        var target = hud.GetProperty("target");
        Assert.Equal(("Goblin", "hostile", 1f, -1), (target.GetProperty("name").GetString(), target.GetProperty("attitude").GetString(),
            target.GetProperty("health").GetSingle(), target.GetProperty("hp").GetInt32()));
    }
}

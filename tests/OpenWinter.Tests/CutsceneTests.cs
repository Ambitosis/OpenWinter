using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Progression;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>Cinematics in the official campaigns, played headlessly through the real server and protocol.</summary>
public class CutsceneTests
{
    readonly ITestOutputHelper _out;
    public CutsceneTests(ITestOutputHelper output) { _out = output; }

    static string? CampaignModule(string name)
    {
        if (TestData.Config == null) return null;
        var path = Path.Combine(TestData.Config.GameRoot, "data", "nwm", name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>A module played by one connected (loopback) player; records what the client is told.</summary>
    sealed class Session
    {
        public World World = null!;
        public GameServer Server = null!;
        public LoopbackConnection Client = null!;
        public readonly List<string> Log = new();
        public readonly List<string> Areas = new(), Fades = new(), Errors = new();
        public readonly List<bool> Cutscene = new();
        public DialogMessage? Dialog;
        public uint Controlled;
        public float Time;

        public void Step(float seconds)
        {
            for (float t = 0; t < seconds; t += 1f / GameServer.TickRate)
            {
                Server.Tick(1f / GameServer.TickRate);
                Time += 1f / GameServer.TickRate;
                Drain();
            }
        }

        public void Drain()
        {
            while (Client.TryReceive(out var data))
            {
                var r = new MessageReader(data);
                switch (r.Type)
                {
                    case MessageType.LoadArea:
                        var area = LoadAreaMessage.Read(r).ResRef;
                        Areas.Add(area);
                        Log.Add($"{Time,6:0.00} LoadArea {area}");
                        break;
                    case MessageType.ControlObject: Controlled = ControlObjectMessage.Read(r).ObjectId; Log.Add($"{Time,6:0.00} Control {Controlled:X}"); break;
                    case MessageType.Notify:
                        var n = NotifyMessage.Read(r);
                        using (var json = System.Text.Json.JsonDocument.Parse(n.Json))
                        {
                            if (n.Kind == "fade") Fades.Add(json.RootElement.GetProperty("op").GetString()!);
                            if (n.Kind == "cutscene") Cutscene.Add(json.RootElement.GetProperty("enabled").GetBoolean());
                        }
                        if (n.Kind is "fade" or "cutscene" or "camera" or "area-transition" or "movie") Log.Add($"{Time,6:0.00} Notify {n.Kind} {n.Json}");
                        break;
                    case MessageType.Dialog:
                        Dialog = DialogMessage.Read(r);
                        Log.Add($"{Time,6:0.00} Dialog ended={Dialog.Ended} '{Trim(Dialog.Text)}' [{string.Join(" | ", Dialog.Replies.Select(x => $"{x.Index}:{Trim(x.Text)}"))}]");
                        break;
                }
            }
        }

        static string Trim(string s) => s.Length > 40 ? s[..40] + "..." : s;

        public Creature Pc => World.Get<Creature>(Controlled)!;
    }

    Session Start(string modulePath, Func<World, GffStruct> character)
    {
        var userRoot = Directory.CreateTempSubdirectory("openwinter-cutscene");
        var paths = new GamePaths(TestData.Config!.GameRoot, userRoot.FullName, TestData.Config.Language);
        var rm = ResMan.ForModule(paths, modulePath);
        var world = new World(rm);
        world.EnableScripts();
        var s = new Session { World = world };
        world.Scripts!.Log = line =>
        {
            if (line.Contains("not implemented")) return;
            s.Log.Add($"{s.Time,6:0.00} {line}");
            if (line.Contains("[script error]")) s.Errors.Add(line);
        };
        world.LoadModule();
        var bic = character(world);
        Directory.CreateDirectory(Path.Combine(userRoot.FullName, "localvault"));
        File.WriteAllBytes(Path.Combine(userRoot.FullName, "localvault", "tester.bic"), Gff.Write(bic, "BIC "));
        var server = new GameServer(world, new ServerOptions { DefaultCharacter = "tester" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        s.Server = server;
        s.Client = client;
        return s;
    }

    void Dump(Session s) { foreach (var l in s.Log) _out.WriteLine(l); }

    /// <summary>Hordes of the Underdark chapter 1 with a new first-level character: the "bedtime" NPC offers to level
    /// you to 15; picking that fades to black, jumps you to the dream area and starts the opening cutscene, which must
    /// fade back in and eventually hand control back.</summary>
    [DataFact]
    public void HotuChapterOneLevelUpStartsTheOpeningCutscene()
    {
        var module = CampaignModule("XP2_Chapter1.nwm");
        if (module == null) return;
        var s = Start(module, w =>
        {
            var result = CharacterFactory.Build(w, ProgressionTests.Spec(w, 6, 9, gender: 1, name: "Sorca"));
            Assert.True(result.Ok, string.Join("; ", result.Errors));
            return result.Bic!;
        });
        s.World.Scripts!.Trace = c =>
        {
            if (c.Function.Name is "FadeToBlack" or "FadeFromBlack" or "BlackScreen" or "StopFade" or "SetCutsceneMode" or "JumpToObject" or "JumpToLocation"
                or "ActionJumpToObject" or "ActionJumpToLocation" or "ActionStartConversation" or "BeginConversation" or "SetXP" or "StartNewModule" or "SetCommandable"
                || c.Function.Name == "ExecuteScript" && !c.S(0).StartsWith("nw_c"))
                s.Log.Add($"{s.Time,6:0.00} [{c.Thread.Program.Name}] {c.Function.Name}({string.Join(", ", c.Args.Select(a => a is uint u ? $"#{u:X}" : a?.ToString()))}) self={c.SelfId:X}");
        };
        s.Step(2);
        // The level-up NPC talks to the player on sight; answer its lines until "level me up to 15" is offered.
        for (int guard = 0; guard < 30 && s.Dialog is not { Ended: false }; guard++) s.Step(1);
        Assert.NotNull(s.Dialog);
        for (int guard = 0; guard < 10 && s.Dialog is { Ended: false } d; guard++)
        {
            var pick = d.Replies.FirstOrDefault(r => r.Text.Contains("Automatic"), d.Replies[0]);
            s.Log.Add($"{s.Time,6:0.00} >>> reply {pick.Index} '{pick.Text}'");
            s.Client.Send(new DialogReplyMessage { Choice = pick.Index }.Write());
            s.Step(0.5f);
        }
        s.Log.Add($"{s.Time,6:0.00} >>> dialogue over; level {s.Pc.TotalLevel}, area {s.Pc.Area?.ResRef}, cutscene {s.Pc.InCutscene}, commandable {s.Pc.Commandable}");
        Dump(s);
        // bk_auto_level levels the PC with LevelUpHenchman; under 12th level cut20_start would send the PC straight back
        // to the inn room and the screen would stay black (the bug this guards against).
        Assert.Equal(15, s.Pc.TotalLevel);
        Assert.Equal(new[] { "area", "yprooms" }, s.Areas);
        Assert.Equal("to-black", s.Fades[^1]);

        // The dream cutscene: black, into the drow area, cutscene mode on, fade back in, ~2.5 minutes of scene,
        // then back to the inn room where control returns and the wake-up conversation starts.
        s.Log.Clear();
        s.Step(200);
        s.Log.Add($"{s.Time,6:0.00} >>> after 200 s; area {s.Pc.Area?.ResRef}, cutscene {s.Pc.InCutscene}, commandable {s.Pc.Commandable}, actions {s.Pc.Actions.Count}");
        Dump(s);
        Assert.Equal(new[] { "area", "yprooms", "area", "yprooms" }, s.Areas);
        Assert.Equal(new[] { true, false }, s.Cutscene);
        Assert.Contains("from-black", s.Fades.SkipWhile(f => f != "to-black"));
        Assert.Equal("from-black", s.Fades[^1]);
        Assert.Equal("yprooms", s.Pc.Area?.ResRef);
        Assert.False(s.Pc.InCutscene);
        Assert.True(s.Pc.Commandable);
        Assert.True(s.Dialog is { Ended: false }, "the wake-up conversation (pre_awake1) should be open");
        Assert.Empty(s.Errors);
    }
}

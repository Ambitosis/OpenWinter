using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Gameplay;

/// <summary>Silent, headless loopback regression for replacement module catalogues.</summary>
public static class ModuleContentProbe
{
    public static async Task<int> Run(Node parent, string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        var (client, server) = LoopbackConnection.CreatePair();
        var session = new ClientSession { Connection = client, Paths = new GamePaths(directory, directory) };
        parent.AddChild(session);
        // Real presentation nodes, with automatic processing disabled so the probe
        // controls time and does not need a camera, world picker, or input mapping.
        var presentation = new Node { ProcessMode = Node.ProcessModeEnum.Disabled };
        parent.AddChild(presentation);
        var hud = new CanvasLayer(); presentation.AddChild(hud);
        var input = new PlayerInput { Session = session }; presentation.AddChild(input);
        var interaction = new Interaction { Session = session }; presentation.AddChild(interaction);
        var dialog = new UI.DialogPanel(); presentation.AddChild(dialog);
        var chat = new UI.ChatLog(); presentation.AddChild(chat);
        var fx = new UI.PlayerFx { Session = session, Hud = hud, Input_ = input,
            Interaction = interaction, Dialog = dialog, Chat = chat };
        presentation.AddChild(fx);
        void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks.Add(label);
        }
        void Send(byte[] data) => server.Send(data);
        void Tick() => session._Process(0);
        void Notice(string kind, object data) => Send(new NotifyMessage
            { Kind = kind, Subject = 77, Json = JsonSerializer.Serialize(data) }.Write());
        try
        {
            var oldWindow = new UI.UiTheme.Window { Visible = true };
            parent.AddChild(oldWindow);
            UI.WindowStack.Activate(oldWindow);
            oldWindow.Closed += () => session.Request("old-close");
            session.ModuleCleared += UI.WindowStack.CloseAll;
            int changes = 0, loaded = 0, notices = 0;
            session.ContentChanged += () => changes++;
            session.AreaLoaded += () => loaded++;
            session.NotifyReceived += _ => notices++;
            session.AreaLoading += _ => { };
            Send(new WelcomeMessage { ModuleName = "first" }.Write());
            Send(new LoadAreaMessage { AreaId = 1, ResRef = "first" }.Write());
            Tick(); Tick(); Tick();
            Check(loaded == 1 && session.Area?.Info.AreaId == 1, "initial area loads after its normal delay");
            var firstContent = session.Content;
            var firstAudio = session.AreaAudio;
            Check(firstAudio != null && firstAudio.Resources == firstContent!.Resources, "initial audio uses first catalogue");
            Send(new ControlObjectMessage { ObjectId = 77 }.Write());
            Notice("rest", new { state = "started", duration = 10f });
            Notice("cutscene", new { enabled = true });
            Tick(); fx._Process(1);
            Notice("pause", new { state = 1 }); Tick(); fx._Process(1);
            Check(!hud.Visible && dialog.Cutscene && !input.IsProcessing()
                && !input.IsProcessingUnhandledInput() && interaction.Suspended
                && !interaction.IsProcessingUnhandledInput(), "actual cutscene disables HUD and player input");
            Check(fx.RestActive && fx.RestDuration == 10 && fx.RestElapsed == 1 && fx.RestPaused,
                "actual rest advances then pause freezes its clock");
            notices = 0;
            Send(new LoadAreaMessage { AreaId = 2, ResRef = "discarded" }.Write());
            Tick();
            Check(session.PendingArea?.AreaId == 2, "old area pending before switch");

            Check(fx.RestActive && fx.RestPaused && dialog.Cutscene,
                "ordinary pending area does not clear within-module presentation");
            // These real server resets arrive behind a pending area; replacement
            // Welcome bypasses the hold and discards them before PlayerFx sees them.
            Notice("cutscene", new { enabled = false });
            Notice("pause", new { state = 0 });
            Notice("rest", new { state = "cancelled" });

            // Unique partial transfer cannot be satisfied by an earlier run's cache.
            var oldHash = Convert.ToHexString(SHA1.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant();
            var old = new ContentFile { Kind = ContentKind.ModuleResources, Name = "module_resources", Sha1 = oldHash, Size = 100 };
            Send(new WelcomeMessage { ModuleName = "interrupted", Files = [old] }.Write());
            Send(new ContentChunkMessage { Sha1 = oldHash, Total = 100, Data = [1, 2] }.Write());
            Send(new LoadAreaMessage { AreaId = 3, ResRef = "held_old" }.Write());
            Send(new NotifyMessage { Kind = "old-module" }.Write());
            Tick();
            Check(session.ModuleName == "interrupted" && session.Content == null, "new Welcome interrupts pending area");
            Check(session.PendingArea == null && session.Area == null && session.ControlledId == 0, "old module scene and pending area cleared");
            Check(hud.Visible && !dialog.Cutscene && input.IsProcessing()
                && input.IsProcessingUnhandledInput() && !interaction.Suspended
                && interaction.IsProcessingUnhandledInput(), "replacement restores actual player controls without held reset notices");
            Check(!fx.RestActive && !fx.RestPaused && fx.RestDuration == 0 && fx.RestElapsed == 0,
                "replacement clears paused/rest presentation and clock");
            Check(notices == 0, "old cutscene pause and rest resets really were discarded");
            Check(!oldWindow.Visible, "old-module window closes during catalogue replacement");
            while (server.TryReceive(out var outgoing))
                Check(new MessageReader(outgoing).Type != MessageType.ClientRequest, "old close callback sends no request into replacement module");
            oldWindow.Free();
            Check(session.AreaAudio == null && !GodotObject.IsInstanceValid(firstAudio), "old catalogue audio released");

            string value = Guid.NewGuid().ToString("N");
            var bytes = Erf.Write([new ErfEntry("probe", ResType.TwoDA, Encoding.UTF8.GetBytes("2DA V2.0\n\nValue\n0 " + value + "\n"))], "ERF ");
            var hash = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
            var current = new ContentFile { Kind = ContentKind.ModuleResources, Name = "module_resources", Sha1 = hash, Size = bytes.Length };
            hud.Visible = false; // A user-hidden HUD is not a stale cutscene state.
            Send(new WelcomeMessage { ModuleName = "replacement", Files = [current] }.Write());
            Send(new ContentChunkMessage { Sha1 = oldHash, Offset = 2, Total = 100, Data = [3] }.Write());
            Send(new LoadAreaMessage { AreaId = 4, ResRef = "replacement" }.Write());
            Tick();
            Check(!hud.Visible, "replacement preserves a hidden HUD when no cutscene is active");
            Check(session.ModuleName == "replacement" && session.Content == null, "Welcome interrupts an active content download");
            Check(notices == 0 && loaded == 1, "held old notices and areas discarded");
            Send(new ContentChunkMessage { Sha1 = hash, Total = bytes.Length, Data = bytes }.Write());
            Tick(); Tick(); Tick();
            Check(session.Content!.Resources.TwoDA("probe").Get(0, "Value") == value, "replacement module table resolves from verified package");
            Check(loaded == 2 && session.Area?.Info.AreaId == 4, "only replacement area is released after download");
            Check(session.AreaAudio != null && session.AreaAudio.Resources == session.Content.Resources, "replacement audio uses new resources");
            Check(changes == 2 && session.Content != firstContent, "content change notification follows verified installation only");
            await parent.ToSignal(parent.GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(firstContent!.Resources.Sources.Count == 0, "retired resource sources disposed after deferred removal frame");
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[module-content] {checks.Count} checks passed");
            return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "failure.txt"), e.ToString());
            GD.PrintErr(e); return 1;
        }
        finally { presentation.Free(); session.Free(); }
    }
}

using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Diagnostics;

/// <summary>Standalone headless scene exercising real Godot timers and session messages.</summary>
public partial class CharacterSheetRefreshProbe : Node
{
    public override async void _Ready()
    {
        int checks = 0;
        var pair = LoopbackConnection.CreatePair();
        var session = new ClientSession { Connection = pair.Client };
        AddChild(session);
        var first = new Control(); var second = new Control();
        AddChild(first); AddChild(second);
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++; GD.Print("[sheet-refresh] PASS " + message);
        }
        async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        List<JsonElement> Drain()
        {
            var requests = new List<JsonElement>();
            while (pair.Server.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type != MessageType.ClientRequest) continue;
                var request = ClientRequestMessage.Read(reader);
                if (request.Kind == "character") requests.Add(JsonSerializer.Deserialize<JsonElement>(request.Json));
            }
            return requests;
        }
        async Task<JsonElement> Next()
        {
            for (int i = 0; i < 40; i++)
            {
                await Wait(.05);
                var requests = Drain();
                if (requests.Count == 0) continue;
                Check(requests.Count == 1, "simultaneous windows send one poll");
                return requests[0];
            }
            throw new InvalidOperationException("visible window did not poll within two seconds");
        }
        void Version(uint subject, string version) => pair.Server.Send(new NotifyMessage
        { Kind = "character-sheet", Subject = subject, Json = JsonSerializer.Serialize(new { version }) }.Write());
        try
        {
            pair.Server.Send(new ControlObjectMessage { ObjectId = 42 }.Write());
            CharacterSheetRefresh.Attach(first, session);
            CharacterSheetRefresh.Attach(second, session);
            Check((await Next()).GetProperty("version").ValueKind == JsonValueKind.Null, "initial poll has no token");
            Version(42, "first"); await Wait(.05);
            Check((await Next()).GetProperty("version").GetString() == "first", "both windows reuse received sheet version");
            first.Hide(); second.Hide();
            await Wait(.65);
            Check(Drain().Count == 0, "hidden windows send no polls");
            second.Show();
            Check((await Next()).GetProperty("version").GetString() == "first", "reopened window resumes conditional refresh");
            pair.Server.Send(new ControlObjectMessage { ObjectId = 43 }.Write()); await Wait(.05);
            Check((await Next()).GetProperty("version").ValueKind == JsonValueKind.Null, "controlled creature change invalidates old token");
            Version(42, "wrong"); Version(43, "second"); await Wait(.05);
            Check((await Next()).GetProperty("version").GetString() == "second", "only current creature updates shared token");
            first.QueueFree(); second.QueueFree();
            await Wait(.65);
            Check(Drain().Count == 0, "freed windows stop polling");
            GD.Print($"[sheet-refresh] {checks} checks passed");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PrintErr("[sheet-refresh] FAIL " + e);
            GetTree().Quit(1);
        }
    }
}

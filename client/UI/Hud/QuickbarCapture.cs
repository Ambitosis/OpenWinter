using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Content;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Checks client edits against the server's quickbar (--capture-quickbar dir, Aluvian).</summary>
public static class QuickbarCapture
{
    public static async Task Run(string dir, ClientSession session, ActionBar bar,
        Func<string, string, Task> shot, Func<double, Task> wait, Action<int> quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        QuickbarSlot?[]? received = null;
        void OnNotify(NotifyMessage message)
        {
            if (message.Kind != "quickbar" || message.Subject != session.ControlledId) return;
            using var doc = JsonDocument.Parse(message.Json);
            received = doc.RootElement.GetProperty("slots").Deserialize<QuickbarSlot?[]>();
        }
        session.NotifyReceived += OnNotify;
        try
        {
            await wait(4);
            await shot(dir, "00_imported");
            if (bar.Get(0, 5) is not { Kind: "item" } dagger)
                throw new InvalidOperationException("Capture requires Aluvian's original quickbar and fresh client data.");
            bar.Set(0, 0, dagger);
            bar.Set(0, 5, new() { Kind = "command", Arg = "rest" });
            bar.Set(0, 1, null);
            bar.Set(1, 3, new() { Kind = "spell", Id = 102, Class = 10, Meta = 1 });
            bar.Set(2, 11, new() { Kind = "emote", Arg = "greeting", Name = "Wave" });
            await wait(0.5);
            received = null;
            session.Request("quickbar");
            for (int i = 0; received == null && i < 50; i++) await wait(0.1);
            var expected = Enumerable.Range(0, 36).Select(i => (QuickbarSlot?)bar.Get(i / 12, i % 12)).ToArray();
            if (JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(received))
                throw new InvalidOperationException("Client/server quickbar differs after editing all three pages.");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "server-slots.json"), JsonSerializer.Serialize(received));
            for (int page = 0; page < 3; page++)
            {
                bar.SetBasePage(page);
                await wait(0.2);
                await shot(dir, $"0{page + 1}_edited_page{page + 1}");
            }
            GD.Print("[capture] PASS: all 36 server slots match the edited client quickbar");
            quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError($"[capture] quickbar: {ex}");
            quit(1);
        }
        finally { session.NotifyReceived -= OnNotify; }
    }
}

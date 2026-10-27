using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-gui dir: on an NWN:EE server running the
    /// bridge_gui test module (parity/suites/bridge_gui), disables, forces and toggles built-in panels by chat command
    /// and opens them as the player would, with a shot after each step. The module answers each attempt to open a
    /// disabled panel with a window naming it. Works in either interface (--ui classic).
    /// </summary>
    async Task CaptureNwnGui(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 600 && (_session.Area == null || _session.Controlled == null); i++) await Wait(0.1);
        if (_session.Controlled == null)
        {
            GD.PrintErr("[nwn] no controlled character after 60 s");
            GetTree().Quit(1);
            return;
        }
        await Wait(4);
        void Say(string text)
        {
            GD.Print($"[nwn] say {text}");
            _session.Send(new ChatSendMessage { Channel = ChatChannel.Talk, Text = text }.Write());
        }
        uint Find(string name) => _session.Objects.Values.FirstOrDefault(o => o.DisplayName == name)?.ObjectId ?? 0;
        async Task Step(string name)
        {
            await Wait(1.5);
            GD.Print($"[nwn] {name}: windows [{string.Join(", ", Nui.Windows.Values.Select(w => w.WindowId))}]");
            await Shot(dir, name);
            Nui.CloseAll();
        }
        await Shot(dir, "01_arrived");
        Say("dis 4");
        await Wait(1);
        _input.ToggleInventory?.Invoke();
        await Step("02_inventory_disabled");
        Say("dis 8 pc");
        await Wait(1);
        _input.ToggleCharacter?.Invoke();
        await Step("03_sheet_disabled");
        _input.ToggleSpellbook?.Invoke();
        await Step("04_book_open");
        Say("flip 7");
        await Step("05_book_flipped");
        _input.ToggleSpellbook?.Invoke();
        await Step("06_book_reopened");
        _input.ToggleSpellbook?.Invoke();
        _input.ToggleJournal?.Invoke();
        await Step("07_journal_open");
        Say("dis 6");
        await Step("08_journal_disabled");
        Say("pop 6");
        await Step("09_journal_forced");
        _input.ToggleJournal?.Invoke();
        await Step("10_journal_closed");
        Say("dis 12 BG_NPC");
        await Wait(1);
        Examine.Examine(Find("Npc One"));
        await Step("11_examine_disabled");
        Examine.Examine(Find("Npc Two"));
        await Step("12_examine_other");
        UI.WindowStack.CloseTop();
        Say("exam BG_NPC");
        await Step("13_examine_forced");
        GetTree().Quit();
    }
}

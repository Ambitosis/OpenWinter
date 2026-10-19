using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Conditional live refresh, shared by every record window in a session.</summary>
public static class CharacterSheetRefresh
{
    static readonly ConditionalWeakTable<ClientSession, Refresh> Sessions = new();

    public static void Attach(Control window, ClientSession session)
    {
        var refresh = Sessions.GetValue(session, s => new Refresh(s));
        var timer = new Godot.Timer { WaitTime = 0.5, Autostart = true };
        timer.Timeout += () => { if (window.IsVisibleInTree()) refresh.Poll(); };
        window.AddChild(timer);
        void ControlChanged(uint _)
        {
            if (window is Classic.ClassicWindow classic) classic.Close();
            else if (window is UiTheme.Window modern) modern.Close();
        }
        session.ControlledChanged += ControlChanged;
        window.TreeExiting += () => session.ControlledChanged -= ControlChanged;
    }

    sealed class Refresh
    {
        readonly ClientSession _session;
        string? _version;
        uint _creature = uint.MaxValue;
        ulong _nextPoll;

        public Refresh(ClientSession session)
        {
            _session = session;
            session.NotifyReceived += Received;
        }

        void Received(NotifyMessage note)
        {
            if (note.Kind != "character-sheet" || note.Subject != _session.ControlledId) return;
            using var json = JsonDocument.Parse(note.Json);
            _version = json.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
            _creature = note.Subject;
        }

        public void Poll()
        {
            ulong now = Time.GetTicksMsec();
            if (now < _nextPoll) return; // Inventory + character record still share one poll.
            _nextPoll = now + 500;
            _session.Request("character", new { version = _creature == _session.ControlledId ? _version : null });
        }
    }
}

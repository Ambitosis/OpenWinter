using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.DM;

/// <summary>
/// The chat entry line under the chat log: Enter to type, Enter to send, Escape to leave.
/// A channel picker, or a prefix: /s shout, /w whisper, /p party, /dm DM channel, /t talk.
/// Players use the DM channel to reach Dungeon Masters; DMs see it from everyone.
/// Other /commands go to the server as typed.
/// </summary>
public partial class ChatInput : HBoxContainer
{
    public ClientSession Session = null!;
    LineEdit _line = null!;
    OptionButton _channel = null!;

    /// <summary>True while the player is typing (movement and camera keys pause).</summary>
    public static bool Typing { get; set; }

    /// <summary>The chat line (this one or the classic talk line) took or lost focus, with the selected channel.</summary>
    public static event Action<bool, ChatChannel>? FocusChanged;
    public static void ReportFocus(bool focused, ChatChannel channel) => FocusChanged?.Invoke(focused, channel);

    static readonly (string Name, ChatChannel Channel)[] Channels =
    {
        ("Talk", ChatChannel.Talk), ("Shout", ChatChannel.Shout), ("Whisper", ChatChannel.Whisper), ("Party", ChatChannel.Party), ("DM", ChatChannel.Dm),
    };

    public override void _Ready()
    {
        AnchorTop = 1; AnchorBottom = 1;
        OffsetLeft = 16; OffsetRight = 424; OffsetTop = -50; OffsetBottom = -16;
        AddThemeConstantOverride("separation", 4);
        _channel = new OptionButton { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(92, 0) };
        foreach (var (name, _) in Channels) _channel.AddItem(name);
        AddChild(_channel);
        _line = new LineEdit { PlaceholderText = "Press Enter to chat", TooltipText = "Channels: /s shout · /w whisper · /p party · /dm Dungeon Master", SizeFlagsHorizontal = SizeFlags.ExpandFill, MaxLength = 1000 };
        _line.TextSubmitted += Submit;
        _line.FocusEntered += () => { Typing = true; ReportFocus(true, Channels[Math.Max(0, _channel.Selected)].Channel); };
        _line.FocusExited += () => { Typing = false; ReportFocus(false, Channels[Math.Max(0, _channel.Selected)].Channel); };
        AddChild(_line);
    }

    /// <summary>Selects a channel (the DM toolbar defaults DMs to the DM channel).</summary>
    public void SetChannel(ChatChannel channel)
    {
        int i = Array.FindIndex(Channels, c => c.Channel == channel);
        if (i >= 0) _channel.Select(i);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!_line.HasFocus() && Preferences.InputBindings.Pressed(e, Preferences.ActionCatalog.Chat))
        {
            _line.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent e)
    {
        if (_line.HasFocus() && e is InputEventKey { Pressed: true, PhysicalKeycode: Key.Escape })
        {
            _line.Text = "";
            _line.ReleaseFocus();
            GetViewport().SetInputAsHandled();
        }
    }

    void Submit(string text)
    {
        _line.Text = "";
        _line.ReleaseFocus();
        Send(text);
    }

    /// <summary>Sends a line on the selected channel, honouring a channel prefix.</summary>
    public void Send(string text)
    {
        var (channel, line) = Parse(text, Channels[Math.Max(0, _channel.Selected)].Channel);
        if (line.Length > 0) Session.Send(new ChatSendMessage { Channel = channel, Text = line }.Write());
    }

    /// <summary>A typed line's channel (a prefix, else <paramref name="channel"/>) and the text that goes out.</summary>
    public static (ChatChannel Channel, string Text) Parse(string text, ChatChannel channel)
    {
        text = text.Trim();
        foreach (var (prefix, ch) in new[] { ("/dm ", ChatChannel.Dm), ("/s ", ChatChannel.Shout), ("/w ", ChatChannel.Whisper), ("/p ", ChatChannel.Party), ("/tk ", ChatChannel.Talk), ("/t ", ChatChannel.Talk) })
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return (ch, text[prefix.Length..].Trim());
        return (channel, text);
    }
}

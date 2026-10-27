using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.ChatMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>NWN chat messages as OpenWinter chat: channel, speaker and text (talk table references resolved).</summary>
static class NwnChat
{
    public static bool ToClient(NwnMessage message, INwnBridgeData data, Action<byte[]> emit)
    {
        string Ref(uint strRef) => data.Text(strRef, 0);
        string Loc(NwnLocString s) => s.IsStrRef ? data.Text(s.StrRef, s.Gender) : s.Text;
        ChatMessage? Said(ChatChannel channel, Speaker s) => new()
        {
            Channel = channel,
            Speaker = s.SpeakerId,
            SpeakerName = s.OriginalName ? $"{(s.FirstName is { } f ? Loc(f) : "")} {(s.LastName is { } l ? Loc(l) : "")}".Trim() : s.DisplayName ?? "",
            Text = s.Message,
        };
        var chat = message switch
        {
            ChatTalk m => new ChatMessage { Channel = ChatChannel.Talk, Speaker = m.SpeakerId, Text = m.Message },
            ChatWhisper m => new ChatMessage { Channel = ChatChannel.Whisper, Speaker = m.SpeakerId, Text = m.Message },
            ChatShout m => Said(ChatChannel.Shout, m.Speaker),
            ChatTell m => Said(ChatChannel.Tell, m.Speaker),
            ChatParty m => Said(ChatChannel.Party, m.Speaker),
            ChatSilentShout m => Said(ChatChannel.Dm, m.Speaker),
            ChatDMTalk m => Said(ChatChannel.Dm, m.Speaker),
            ChatDMWhisper m => Said(ChatChannel.Dm, m.Speaker),
            ChatDMSilentShout m => Said(ChatChannel.Dm, m.Speaker),
            ChatServerTell m => new ChatMessage { Channel = ChatChannel.Server, Text = m.Message },
            ChatTalkRef m => new ChatMessage { Channel = ChatChannel.Talk, Speaker = m.SpeakerId, Text = Ref(m.Strref) },
            ChatShoutRef m => new ChatMessage { Channel = ChatChannel.Shout, Speaker = m.SpeakerId, Text = Ref(m.Strref) },
            ChatWhisperRef m => new ChatMessage { Channel = ChatChannel.Whisper, Speaker = m.SpeakerId, Text = Ref(m.Strref) },
            ChatServerTellRef m => new ChatMessage { Channel = ChatChannel.Server, Speaker = m.SpeakerId, Text = Ref(m.Strref) },
            ChatTokenTalk m => new ChatMessage { Channel = ChatChannel.Talk, Speaker = m.SpeakerId, Text = Loc(m.Message) },
            ChatTokenTalkNoBubble m => new ChatMessage { Channel = ChatChannel.Dialog, Speaker = m.SpeakerId, Text = Loc(m.Message) },
            _ => null,
        };
        if (chat != null) emit(chat.Write());
        return chat != null;
    }
}

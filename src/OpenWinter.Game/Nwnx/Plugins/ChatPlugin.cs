using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Chat: the chat script, sending messages and hearing distances. As measured on nwserver with NWNX (parity
/// suite nwnx_chat): the registered script runs for every chat message the server sends out, a creature's
/// SpeakString and ActionSpeakString as well as players' chat and NWNX_Chat_SendMessage, with OBJECT_SELF the sender
/// (OBJECT_INVALID when there is none). A skipped message reaches no one, listening creatures included.
/// </summary>
public static class ChatPlugin
{
    const string P = "Chat";
    const string KeySkip = "nwnx:chat:skip", KeyChannel = "nwnx:chat:channel", KeyMessage = "nwnx:chat:message",
        KeySender = "nwnx:chat:sender", KeyTarget = "nwnx:chat:target";

    /// <summary>NWNX_CHAT_CHANNEL_*.</summary>
    public const int PlayerTalk = 1, PlayerShout = 2, PlayerWhisper = 3, PlayerTell = 4, ServerMessage = 5, PlayerParty = 6,
        SilentTalk = 13, PlayerDM = 14, DMTalk = 17, DMShout = 18, DMWhisper = 19, DMTell = 20, DMParty = 22, DMDM = 30;

    sealed class ChatState
    {
        public string Script = Environment.GetEnvironmentVariable("NWNX_CHAT_CHAT_SCRIPT") ?? "";
        public bool Running;
        public readonly Dictionary<int, float> Distances = new() { [PlayerTalk] = 20f, [PlayerWhisper] = 3f, [DMTalk] = 20f, [DMWhisper] = 3f };
        public readonly Dictionary<(uint Listener, int Channel), float> Personal = new();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NwnxRegistry, ChatState> _state = new();
    static ChatState State(World w) => _state.GetValue(w.System<NwnxRegistry>(), _ =>
    {
        var st = new ChatState();
        // A player's own distances go with her character.
        w.ObjectRemoved += o =>
        {
            if (st.Personal.Count > 0)
                foreach (var key in st.Personal.Keys.Where(k => k.Listener == o.Id).ToList()) st.Personal.Remove(key);
        };
        return st;
    });

    /// <summary>The NWNX channel of a talk volume (TALKVOLUME_*): talk, whisper, shout, silent talk and shout, party, tell.</summary>
    public static int Channel(int volume, bool dm = false) => volume switch
    {
        1 => dm ? DMWhisper : PlayerWhisper,
        2 => dm ? DMShout : PlayerShout,
        3 => SilentTalk,
        4 => PlayerDM,
        5 => dm ? DMParty : PlayerParty,
        6 => dm ? DMTell : PlayerTell,
        _ => dm ? DMTalk : PlayerTalk,
    };

    /// <summary>
    /// Runs the registered chat script for a message about to go out. Returns true when the script skipped it. A
    /// message the chat script itself sends does not run it again.
    /// </summary>
    public static bool Intercept(World world, int channel, string message, GameObject? sender, GameObject? target = null)
    {
        var st = State(world);
        if (st.Script.Length == 0 || st.Running || world.Scripts == null) return false;
        var ctx = new EventContext().Set(KeyChannel, channel).Set(KeyMessage, message)
            .Set(KeySender, sender?.Id ?? GameObject.InvalidId).Set(KeyTarget, target?.Id ?? GameObject.InvalidId);
        st.Running = true;
        try { world.Scripts.Run(st.Script, sender?.Id ?? GameObject.InvalidId, ctx); }
        finally { st.Running = false; }
        return ctx.Int(KeySkip) != 0;
    }

    /// <summary>
    /// How far a player hears talk or a whisper: her own setting, else the server's (20 m talk, 3 m whisper). Other
    /// channels have no distance.
    /// </summary>
    public static float HearingDistance(World world, GameObject? listener, int channel)
    {
        var st = State(world);
        if (listener != null && st.Personal.TryGetValue((listener.Id, channel), out var own)) return own;
        return st.Distances.GetValueOrDefault(channel);
    }

    [NwnxFunc(P, "SendMessage")]
    static void SendMessage(NwnxCall n)
    {
        int channel = n.PopInt(); var message = n.PopString(); var sender = n.PopObject(); var target = n.PopObject();
        bool tell = channel is PlayerTell or DMTell;
        bool server = channel == ServerMessage;
        // A tell needs a player to hear it: sent to a creature it fails before the chat script sees it, yet a
        // listening creature still hears it (nwnx_chat).
        if (tell && target is not Creature { IsPC: true })
        {
            if (sender != null && target is Creature listener) Society.SocietyService.Hear(n.World, sender, listener, message);
            n.Push(0);
            return;
        }
        if (target != null && target is not Creature { IsPC: true }) { n.Push(0); return; }
        if (sender == null)
        {
            Intercept(n.World, channel, message, null, target);
            n.Push(1);
            return;
        }
        if (server || tell || target is Creature)
        {
            // A chat line from the sender on its channel, not feedback: NWNX_Feedback's filters don't apply.
            if (!Intercept(n.World, channel, message, sender, target))
            {
                var line = new Net.ChatMessage
                {
                    Speaker = sender.Id, Channel = ClientChannel(channel), SpeakerName = server ? "" : sender.Name.Resolve(n.World.Resources.Talk),
                    Text = message,
                }.Write();
                var targets = target is Creature pc ? new[] { pc } : n.World.ConnectedPCs.ToArray();
                foreach (var c in targets) Players.FamiliarPossession.SessionOf(c)?.Send(line);
            }
            n.Push(1);
            return;
        }
        // Talk, shout, whisper and party go out as the sender's speech (the chat script runs there).
        int volume = channel switch
        {
            PlayerShout or DMShout => 2,
            PlayerWhisper or DMWhisper => 1,
            PlayerParty or DMParty => 5,
            PlayerDM or DMDM => 4,
            _ => 0,
        };
        n.World.Speak(sender, message, volume);
        n.Push(1);
    }

    /// <summary>The chat window channel a message on an NWNX channel shows in.</summary>
    static Net.ChatChannel ClientChannel(int channel) => channel switch
    {
        PlayerShout or DMShout => Net.ChatChannel.Shout,
        PlayerWhisper or DMWhisper => Net.ChatChannel.Whisper,
        PlayerTell or DMTell => Net.ChatChannel.Tell,
        PlayerParty or DMParty => Net.ChatChannel.Party,
        PlayerDM or DMDM => Net.ChatChannel.Dm,
        ServerMessage => Net.ChatChannel.Server,
        _ => Net.ChatChannel.Talk,
    };

    [NwnxFunc(P, "RegisterChatScript")] static void Register(NwnxCall n) => State(n.World).Script = n.PopString();
    [NwnxFunc(P, "SkipMessage")] static void Skip(NwnxCall n) => n.Host?.CurrentEvent.Set(KeySkip, 1);
    [NwnxFunc(P, "GetChannel")] static void Channel(NwnxCall n) => n.Push(n.Host?.CurrentEvent.Int(KeyChannel) ?? 0);
    [NwnxFunc(P, "GetMessage")] static void Message(NwnxCall n) => n.Push(n.Host?.CurrentEvent.Str(KeyMessage) ?? "");

    // Outside a chat script the sender and target read as the module (nwnx_chat).
    [NwnxFunc(P, "GetSender")] static void Sender(NwnxCall n) => n.PushObject(ChatObject(n, KeySender));
    [NwnxFunc(P, "GetTarget")] static void Target(NwnxCall n) => n.PushObject(ChatObject(n, KeyTarget));

    static uint ChatObject(NwnxCall n, string key) =>
        n.Host?.CurrentEvent is { } e && e.Values.ContainsKey(key) ? e.Obj(key) : n.World.Module.Id;

    [NwnxFunc(P, "SetChatHearingDistance")]
    static void SetDistance(NwnxCall n)
    {
        float distance = n.PopFloat(); var listener = n.PopObject(); int channel = n.PopInt();
        if (distance < 0) return;
        var st = State(n.World);
        // Personal distances are for players; any other listener changes nothing (nwnx_chat).
        if (listener == null) st.Distances[channel] = distance;
        else if (listener is Creature { IsPC: true }) st.Personal[(listener.Id, channel)] = distance;
    }

    [NwnxFunc(P, "GetChatHearingDistance")]
    static void GetDistance(NwnxCall n)
    {
        var listener = n.PopObject(); int channel = n.PopInt();
        n.Push(HearingDistance(n.World, listener, channel));
    }
}

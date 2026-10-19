using System.Text.Json;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Gameplay;

/// <summary>
/// Multiplayer sync harness, client side (<c>--mp-trace &lt;dir&gt;</c>; tools/multiplayer, docs/MULTIPLAYER_HARNESS.md).
/// Records what this client shows of the world in the same form the server's <c>--harness</c> records what it holds:
/// <list type="bullet">
/// <item><c>samples.jsonl</c>: every 0.2 s, each creature and door: the last position the server sent and the drawn
/// one, facing, animation, HP bar.</item>
/// <item><c>checkpoints.jsonl</c>: everything shown (objects, HP bars, door states, ground items, effects, party,
/// chat since the last checkpoint) when the server says a checkpoint was reached (notify <c>mp-checkpoint</c>).</item>
/// <item><c>events.jsonl</c>: chat lines, notifies and animations as they arrive.</item>
/// </list>
/// The server's <c>mp-do</c> notify drives this client's input (keys, clicks, chat, requests) and <c>mp-done</c>
/// ends the run.
/// </summary>
public partial class MultiplayerTrace : Node
{
    public ClientSession Session = null!;
    public Interaction Interaction = null!;
    public EffectPlayer Effects = null!;
    public UI.HudParts.HudState Hud = null!;
    public string Directory = "";
    public Action? Quit;

    StreamWriter _samples = null!, _checkpoints = null!, _events = null!;
    double _nextSample;
    /// <summary>Duration visual effects the server said are on each object (vfx notify with an id, until effect-removed).</summary>
    readonly Dictionary<long, (uint Subject, int Vfx)> _vfx = new();
    readonly List<object> _chatSince = new();
    readonly List<object> _floatSince = new();
    JsonElement? _party, _playerList;

    static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public override void _Ready()
    {
        System.IO.Directory.CreateDirectory(Directory);
        _samples = new StreamWriter(System.IO.Path.Combine(Directory, "samples.jsonl")) { AutoFlush = true };
        _checkpoints = new StreamWriter(System.IO.Path.Combine(Directory, "checkpoints.jsonl")) { AutoFlush = true };
        _events = new StreamWriter(System.IO.Path.Combine(Directory, "events.jsonl")) { AutoFlush = true };
        Session.ChatReceived += OnChat;
        Session.NotifyReceived += OnNotify;
        Session.AnimationReceived += OnAnimation;
        Session.AreaLoading += area => Event(new { type = "load-area", area = area.ResRef });
    }

    public override void _ExitTree()
    {
        _samples?.Dispose();
        _checkpoints?.Dispose();
        _events?.Dispose();
    }

    void Event(object data) => _events.WriteLine(JsonSerializer.Serialize(new { t = Now, e = data }));

    void OnChat(ChatMessage m)
    {
        var line = new { channel = m.Channel.ToString(), speaker = m.Speaker, speakerName = UI.Kit.NwnText.Strip(m.SpeakerName), text = UI.Kit.NwnText.Strip(m.Text) };
        _chatSince.Add(line);
        Event(new { type = "chat", line.channel, line.speaker, line.speakerName, line.text });
    }

    void OnAnimation(PlayAnimationMessage m) => Event(new { type = "animation", id = m.ObjectId, m.Animation, m.Gesture, m.Duration });

    void OnNotify(NotifyMessage m)
    {
        // The HUD feed repeats several times a second; checkpoints carry its state.
        if (m.Kind is not ("hud" or "creature-visibility" or "map-exploration"))
            _events.WriteLine($"{{\"t\":{Now},\"e\":{{\"type\":\"notify\",\"kind\":{JsonSerializer.Serialize(m.Kind)},\"subject\":{m.Subject},\"data\":{(m.Json.Length > 0 ? m.Json : "null")}}}}}");
        using var doc = JsonDocument.Parse(m.Json.Length > 0 ? m.Json : "{}");
        var d = doc.RootElement;
        switch (m.Kind)
        {
            case "vfx" when d.TryGetProperty("id", out var id) && id.GetInt64() != 0 && Session.Content is { } content:
            {
                int row = d.GetProperty("vfx").GetInt32();
                bool lasting = content.Resources.TwoDA("visualeffects").Get(row, "Type_FD") == "D" || d.TryGetProperty("duration", out var dur) && dur.GetSingle() != 0;
                if (lasting) _vfx[id.GetInt64()] = (m.Subject, row);
                break;
            }
            case "effect-removed" when d.TryGetProperty("id", out var removed):
                _vfx.Remove(removed.GetInt64());
                break;
            case "float-text":
                _floatSince.Add(new { subject = m.Subject, text = UI.Kit.NwnText.Strip(d.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "") });
                break;
            case "party":
                _party = d.Clone();
                break;
            case "player-list":
                _playerList = d.Clone();
                break;
            case "mp-checkpoint":
                Checkpoint(d.GetProperty("name").GetString() ?? "");
                break;
            case "mp-do":
                Do(d.GetProperty("verb").GetString() ?? "", d.GetProperty("args").EnumerateArray().Select(a => a.GetString() ?? "").ToArray());
                break;
            case "mp-done":
                Event(new { type = "done" });
                GetTree().CreateTimer(1.0).Timeout += () => Quit?.Invoke();
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (Session.Area == null) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (now < _nextSample) return;
        _nextSample = now + 0.2;
        _samples.WriteLine(JsonSerializer.Serialize(new
        {
            t = Now,
            controlled = Session.ControlledId,
            objects = Session.Objects.Values.Where(v => v.Kind is ObjectKind.Creature or ObjectKind.Door && IsInstanceValid(v)).Select(v => v.Kind == ObjectKind.Creature
                ? (object)new { id = v.ObjectId, net = V(v.NetPosition), pos = V(v.Position), facing = Deg(v.NetFacing), anim = Animation(v), hp = Interaction.LabelDamage(v.ObjectId) }
                : new { id = v.ObjectId, open = (v.Flags & ObjectFlags.Open) != 0 }).ToList(),
        }));
    }

    static float[] V(Vector3 v) => [MathF.Round(v.X, 3), MathF.Round(v.Y, 3), MathF.Round(v.Z, 3)];
    static float Deg(float radians) => MathF.Round((radians * 180f / MathF.PI % 360f + 360f) % 360f, 1);

    /// <summary>What the creature's model is doing: locomotion (Stand, Walk, Run...), stance and the one-shot clip playing.</summary>
    static object Animation(ObjectView v) => v is CreatureView { Animator: { } a }
        ? new { drive = a.DriveMode.ToString(), stance = a.Stance, action = a.CaptureAction()?.Clip ?? "", dead = (v.Flags & ObjectFlags.Dead) != 0 }
        : new { drive = "", stance = "", action = "", dead = (v.Flags & ObjectFlags.Dead) != 0 };

    void Checkpoint(string name)
    {
        var effects = Effects.ActiveEffectIds.ToHashSet();
        var state = new
        {
            controlled = Session.ControlledId,
            area = Session.Area?.Info.ResRef,
            objects = Session.Objects.Values.Where(v => IsInstanceValid(v) && v.Kind is ObjectKind.Creature or ObjectKind.Door or ObjectKind.Item or ObjectKind.Placeable).Select(v => new
            {
                id = v.ObjectId,
                kind = v.Kind.ToString().ToLowerInvariant(),
                tag = v.Tag,
                name = v.DisplayName,
                net = V(v.NetPosition),
                pos = V(v.Position),
                facing = Deg(v.NetFacing),
                flags = v.Flags.ToString(),
                open = (v.Flags & ObjectFlags.Open) != 0,
                dead = (v.Flags & ObjectFlags.Dead) != 0,
                anim = v.Kind == ObjectKind.Creature ? Animation(v) : null,
                hp = v.Kind == ObjectKind.Creature ? Interaction.LabelDamage(v.ObjectId) : -1,
                reaction = v.Kind == ObjectKind.Creature ? Interaction.Reaction(v) : 0,
                // Told (the server's notifies) and drawn (the effect player's live nodes).
                vfx = _vfx.Where(e => e.Value.Subject == v.ObjectId).Select(e => e.Value.Vfx).OrderBy(x => x).ToList(),
                vfxDrawn = _vfx.Where(e => e.Value.Subject == v.ObjectId && effects.Contains(e.Key)).Select(e => e.Value.Vfx).OrderBy(x => x).ToList(),
            }).OrderBy(o => o.id).ToList(),
            party = _party,
            playerList = _playerList,
            invitationWindow = GetTree().Root.FindChild("PartyInvitation", true, false) != null
                || GetTree().Root.FindChild("Popup_party_invitation", true, false) != null,
            hudParty = Hud.Party.Select(p => new { id = p.Id, name = p.Name, hp = p.Hp, maxHp = p.MaxHp }).ToList(),
            self = Hud.Self is { } self ? new { id = self.Id, hp = self.Hp, maxHp = self.MaxHp, xp = Hud.Xp, gold = Hud.Gold } : null,
            chat = _chatSince.ToList(),
            floating = _floatSince.ToList(),
        };
        _chatSince.Clear();
        _floatSince.Clear();
        _checkpoints.WriteLine(JsonSerializer.Serialize(new { t = Now, name, state }));
        GD.Print($"[mp] checkpoint {name}");
        if (Screenshots) _ = Shot(name);
    }

    /// <summary>Saves what the window shows at a checkpoint (JPEG, to keep runs small).</summary>
    public bool Screenshots = true;

    async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SaveJpg(System.IO.Path.Combine(Directory, name + ".jpg"), 0.8f);
    }

    /// <summary>Client input the scene asked for, through the same paths as the real controls.</summary>
    void Do(string verb, string[] args)
    {
        Event(new { type = "do", verb, args });
        string Arg(int i) => i < args.Length ? args[i] : "";
        switch (verb)
        {
            // key|<input action>|down|up|tap: the keyboard bindings (move_forward, walk, turn_left ...).
            case "key" when InputMap.HasAction(Arg(0)):
                if (Arg(1) is "down" or "tap") Input.ActionPress(Arg(0));
                if (Arg(1) == "up") Input.ActionRelease(Arg(0));
                if (Arg(1) == "tap") GetTree().CreateTimer(0.1).Timeout += () => Input.ActionRelease(Arg(0));
                break;
            // moveto|x,y[,z]|walk: a click on the ground.
            case "moveto":
            {
                var xy = Arg(0).Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Session.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = new System.Numerics.Vector3(xy[0], xy[1], xy.Length > 2 ? xy[2] : 0), Run = Arg(1) != "walk" }.Write());
                break;
            }
            // click|<tag>: a left click on an object (doors open, items are picked up, hostile creatures attacked).
            case "click" when Session.Objects.Values.FirstOrDefault(v => v.Tag.Equals(Arg(0), StringComparison.OrdinalIgnoreCase)) is { } target:
                Session.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = target.ObjectId }.Write());
                break;
            // chat|<talk|shout|whisper|party>|text
            case "chat":
                Session.Send(new ChatSendMessage { Channel = Enum.TryParse<ChatChannel>(Arg(0), true, out var channel) ? channel : ChatChannel.Talk, Text = string.Join('|', args.Skip(1)) }.Write());
                break;
            // request|<kind>|<json>: any client request (party, inventory, spellcasting ...). "@tag" in the JSON becomes that object's id.
            case "request":
            {
                string json = string.Join('|', args.Skip(1));
                foreach (var v in Session.Objects.Values) json = json.Replace("\"@" + v.Tag + "\"", v.ObjectId.ToString(), StringComparison.OrdinalIgnoreCase);
                Session.Send(new ClientRequestMessage { Kind = Arg(0), Json = json }.Write());
                break;
            }
            // ui|<invite|kick|leader|like|dislike>|<tag or hex id>, ui|<accept|reject|leave|list>: the player list's buttons, the
            // invitation window's Accept / Reject and opening the list, through the windows' own actions (docs/PARTY.md).
            case "ui" when GetTree().Root.FindChildren("*", "", true, false).OfType<UI.PlayerListPanel>().FirstOrDefault() is { } list:
            {
                // A tag, or an object id as ObjectToString writes it (hex): a player's tag set later isn't sent to clients.
                uint Id() => Session.Objects.Values.FirstOrDefault(v => v.Tag.Equals(Arg(1), StringComparison.OrdinalIgnoreCase))?.ObjectId
                    ?? (uint.TryParse(Arg(1), System.Globalization.NumberStyles.HexNumber, null, out var hex) ? hex : 0);
                switch (Arg(0))
                {
                    case "invite": list.Invite(Id()); break;
                    case "kick": list.Kick(Id()); break;
                    case "leader": list.Transfer(Id()); break;
                    case "like": list.SetLike(Id(), true); break;
                    case "dislike": list.SetLike(Id(), false); break;
                    case "accept" or "reject": list.AnswerOpenInvitation(Arg(0)); break;
                    case "leave": list.Leave(); break;
                    case "list": list.Toggle(); break;
                }
                break;
            }
            // portal|<cancel|join>: the portal prompt's buttons (ActivatePortal, scene mp_portal).
            case "portal" when GetTree().Root.FindChildren("*", "", true, false).OfType<UI.PortalPrompt>().FirstOrDefault() is { } portal:
                portal.Press(Arg(0));
                break;
            // quit: the player closes the game (the server sees her leave).
            case "quit":
                Quit?.Invoke();
                break;
            default:
                GD.PrintErr($"[mp] unknown or impossible input: {verb}|{string.Join('|', args)}");
                break;
        }
    }
}

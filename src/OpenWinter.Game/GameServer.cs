using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;

namespace OpenWinter.Game;

public sealed class PlayerSession
{
    /// <summary>The last HUD payload sent for each creature it plays (Players.HudFeed), kept off the saved world.</summary>
    public readonly Dictionary<uint, string> LastHud = new();
    public required IConnection Connection;
    public string PlayerName = "";
    public Creature? Creature;
    public Creature? FamiliarMaster;
    public Creature? PlayerCreature => FamiliarMaster ?? Creature;
    public Area? Area;
    public bool IsDM;
    public string PublicKey = "";
    public string Address = "";
    /// <summary>The waypoint a portal sent this player to (HelloMessage.PortalWaypoint), until the player is placed.</summary>
    public string PortalWaypoint = "";
    public bool AreaReadyHandshake;
    /// <summary>The client has said Hello; a second Hello on the same connection is ignored.</summary>
    public bool HelloReceived;
    /// <summary>Server running time when the connection was accepted (for the Hello deadline).</summary>
    internal double AcceptedAt;
    public bool IsAreaLoading;
    /// <summary>
    /// A death panel with its Respawn button is up (PopUpDeathGUIPanel, PopUpGUIPanel(GUI_PANEL_PLAYER_DEATH)). Its
    /// press runs OnPlayerRespawn whether the player is dead or not (dying scene); it goes with the press or a raise.
    /// </summary>
    public bool DeathPanelRespawn;
    internal bool CommittingArea;
    internal uint LoadToken;
    internal (Creature Creature, Area Area, Vector3 Position, float Facing, bool Exact, bool EnterEvent)? PendingEntry;
    /// <summary>Objects this client has been told about.</summary>
    public readonly HashSet<uint> Known = new();

    public void Send(byte[] message, bool reliable = true) => Connection.Send(message, reliable);
}

public sealed class ServerOptions
{
    /// <summary>Overrides the module's entry area (development).</summary>
    public string? StartArea;
    /// <summary>Appearance row used when a player has no character file (development).</summary>
    public int DefaultAppearance = -1;
    /// <summary>Character file (localvault resref) used when the client doesn't name one (development).</summary>
    public string? DefaultCharacter;
    /// <summary>Enables /commands in chat for testing (single-player development only).</summary>
    public bool DevCommands;
    /// <summary>Players pick a character after connecting (off: log straight in with a default, for tests).</summary>
    public bool CharacterSelection;
    /// <summary>Server vault: characters live on the server (servervault/&lt;player&gt;/) and are saved on logout. Local vault: clients bring their own.</summary>
    public VaultMode Vault = VaultMode.Server;
    /// <summary>Offer the module's haks and custom TLK for download (dedicated servers).</summary>
    public bool ServeContent;
    /// <summary>Most players connected at once (0 = unlimited).</summary>
    public int MaxPlayers;
    /// <summary>Exports every player character to the vault this often (seconds, 0 = only on logout and by script).</summary>
    public float ExportInterval;
    /// <summary>Players may create new characters on the selection screen.</summary>
    public bool AllowCharacterCreation = true;
    /// <summary>Uploaded local-vault characters must pass the creation and level-up rules (ELC).</summary>
    public bool EnforceLegalCharacters;
    /// <summary>Every level gives the full hit die (otherwise levels above ruleset MIN_LEVEL_FOR_MAX_HP roll).</summary>
    public bool MaxHitPointsPerLevel;
    /// <summary>DM login password: null takes no DMs, "" lets any DM in (local games).</summary>
    public string? DMPassword;
    /// <summary>Single-player save games: save/load requests and DoSinglePlayerAutoSave (see SaveGames/).</summary>
    public bool SaveGames;
    /// <summary>Players may pause a multiplayer game (the original's server.allow-player-pausing, PauseAndPlay; off by
    /// default). Single player and Dungeon Masters can always pause.</summary>
    public bool AllowPlayerPausing;
    /// <summary>The module file or folder this server runs (stored in save games so a menu can continue them).</summary>
    public string? ModulePath;
    /// <summary>A save folder to load when the first player logs in, instead of character selection (Load Game from the main menu).</summary>
    public string? LoadOnLogin;
    /// <summary>The server's name, for a portal prompt's server check ("server-info").</summary>
    public string ServerName = "";
}

/// <summary>
/// Runs the world and talks to clients. Single-player hosts this in-process
/// over a loopback connection; the dedicated server hosts it over the network.
/// </summary>
public sealed partial class GameServer : IWorldOutput
{
    public const float TickRate = 30f;

    readonly List<PlayerSession> _sessions = new();
    readonly HashSet<GameObject> _dirty = new();
    readonly ServerOptions _options;
    Players.CommandReticles _commandReticles = null!;

    /// <summary>Shows the player's walk-to marker at a spot an order goes to (dropping onto the ground) while it lasts.</summary>
    public void TrackOrder(Creature pc, GameAction action, Vector3 position) => _commandReticles.Track(pc, action, position);

    public World World { get; private set; }
    public IReadOnlyList<PlayerSession> Sessions => _sessions;
    public ServerOptions Options => _options;
    readonly List<Action> _deferred = new();

    /// <summary>Hak/TLK downloads for players who lack them (when ServerOptions.ServeContent).</summary>
    public Distribution.ContentServer? Content { get; private set; }

    public GameServer(World world, ServerOptions? options = null)
    {
        World = world;
        _options = options ?? new ServerOptions();
        if (_options.ServeContent) Content = new Distribution.ContentServer(world);
        Attach(world);
    }

    /// <summary>Hooks the server up to a world's events (constructor, and after a saved game replaced the world).</summary>
    void Attach(World world)
    {
        _commandReticles = new Players.CommandReticles(world);
        var progression = world.System<Progression.ProgressionSystem>();
        progression.AllowCharacterCreation = _options.AllowCharacterCreation;
        progression.EnforceLegalCharacters = _options.EnforceLegalCharacters;
        progression.AlwaysMaxHitPoints = _options.MaxHitPointsPerLevel;
        world.ObjectChanged += o => _dirty.Add(o);
        world.ObjectRemoved += OnRemoved;
        world.Output = this;
        world.DeferAreaEntry = DeferAreaEntry;
        var convo = world.System<Conversations.ConversationService>();
        convo.Show += (pc, view) => pc.Player?.Send(new DialogMessage
        {
            Owner = view.Owner, Speaker = view.Speaker, SpeakerName = view.SpeakerName, Text = view.Text, Sound = view.Sound,
            Ended = view.Ended, Replies = view.Replies.ToList(),
        }.Write());
        // The original posts every conversation line and chosen answer to the player's chat window (convoend).
        convo.Line += (pc, speaker, text) => pc.Player?.Send(new ChatMessage
        {
            Speaker = speaker.Id, Channel = ChatChannel.Dialog, SpeakerName = speaker.Name.Resolve(World.Resources.Talk), Text = text,
        }.Write());
        world.AreaTransition += (o, area) =>
        {
            if (o is Creature { Player: { } s } c && s.Area != area) EnterArea(s, area, c.Position, c.Facing, alreadyPlaced: true);
        };
    }

    /// <summary>Runs an action after the current tick, outside any script or world step (saving and loading games).</summary>
    public void Defer(Action action) => _deferred.Add(action);

    void ResetWorld(World next)
    {
        foreach (var s in _sessions)
        {
            foreach (var id in s.Known) s.Send(new DespawnMessage { ObjectId = id }.Write());
            s.Known.Clear();
            s.Area = null;
            s.Creature = null;
            s.FamiliarMaster = null;
            s.PendingEntry = null;
            s.IsAreaLoading = false;
            s.CommittingArea = false;
        }
        _dirty.Clear();
        _looks.Clear();
        _nextLookCheck.Clear();
        _pendingLooks.Clear();
        _nextClock = 0;
        if (World.Output == this) World.Output = null;
        World = next;
        Attach(next);
    }

    /// <summary>
    /// Replaces the running world (loading a saved game). Every player is moved onto the creature
    /// <paramref name="creatureFor"/> picks in the new world and sent its area, as on an area
    /// transition; no module or area enter events run.
    /// </summary>
    public void SwapWorld(World next, Func<PlayerSession, Creature?> creatureFor)
    {
        ResetWorld(next);
        foreach (var s in _sessions)
        {
            if (creatureFor(s) is not { Area: { } area } c) continue;
            c.Player = s;
            s.Creature = c;
            EnterArea(s, area, c.Position, c.Facing, alreadyPlaced: true);
            if (World.Get<Creature>(c.PossessedFamiliar) is { } familiar)
                World.System<Players.FamiliarPossession>().Possess(s, familiar);
        }
    }

    public PlayerSession Accept(IConnection connection)
    {
        var s = new PlayerSession { Connection = connection, AcceptedAt = _running };
        _sessions.Add(s);
        return s;
    }

    /// <summary>Seconds a connection may stay without saying Hello before it is dropped.</summary>
    public const float HelloTimeout = 30;
    double _running;

    double _nextClock;

    TimeMessage Clock() => new()
    {
        Hour = World.Clock.Hour, Minute = World.Clock.Minute, Second = World.Clock.Second,
        MinutesPerHour = World.Clock.MinutesPerHour, DawnHour = World.Clock.DawnHour, DuskHour = World.Clock.DuskHour,
    };

    public void Tick(float dt)
    {
        _running += dt;
        if (World.Time >= _nextClock)
        {
            _nextClock = World.Time + 10;
            var clock = Clock().Write();
            foreach (var session in _sessions) if (session.Area != null) session.Send(clock, reliable: false);
        }
        foreach (var s in _sessions.ToList())
        {
            while (s.Connection.TryReceive(out var data))
            {
                // Clients are untrusted: a malformed message drops the connection, and a failure while handling
                // one is logged rather than taking the server down.
                try { Handle(s, new MessageReader(data), data); }
                catch (Exception e) when (e is EndOfStreamException or InvalidDataException or FormatException)
                {
                    Console.WriteLine($"[server] malformed message from client {s.Connection.Id}; disconnecting: {e.Message}");
                    s.Connection.Disconnect();
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[server] message {(data.Length > 0 ? (MessageType)data[0] : 0)} from client {s.Connection.Id} failed: {e}");
                }
            }
            // A connection that never logs in would hold a place on the server.
            if (!s.HelloReceived && _running - s.AcceptedAt > HelloTimeout) s.Connection.Disconnect();
            if (!s.Connection.IsConnected) Disconnect(s);
        }
        if (_options.ExportInterval > 0 && _options.Vault == VaultMode.Server && (_exportTimer += dt) >= _options.ExportInterval)
        {
            _exportTimer = 0;
            foreach (var pc in _sessions.Select(x => x.PlayerCreature).OfType<Creature>().Where(c => !c.IsDestroyed))
                try { Persistence.ServerVault.Export(World, pc); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Console.WriteLine($"[server] periodic save of {pc.Name.FirstString} failed: {e.Message}"); }
        }
        Content?.Pump();
        // A paused game (SetPause / timestop) keeps talking to clients but stops simulating.
        if (!World.IsPaused) World.Step(dt);
        FlushUpdates();
        SyncConcealed();
        _commandReticles.Flush();
        if (_deferred.Count > 0)
        {
            var run = _deferred.ToList();
            _deferred.Clear();
            foreach (var action in run) action();
        }
        if (World.PendingModule is { } next) { World.PendingModule = null; StartModule(next); }
    }

    void Handle(PlayerSession s, MessageReader r, byte[] raw)
    {
        if (r.Type == MessageType.ClientRequest)
        {
            var request = ClientRequestMessage.Read(new MessageReader(raw));
            if (request.Kind == "area-ready") { CompleteAreaEntry(s, request.Json); return; }
            if (s.IsAreaLoading && request.Kind is not ("inventory" or "character" or "journal" or "abilities" or "item-info" or "player-list" or "quickbar" or "spellbook" or "examine")) return;
        }
        if (s.IsAreaLoading && r.Type is MessageType.MoveInput or MessageType.DriveInput or MessageType.ActionRequest or MessageType.DialogReply) return;
        switch (r.Type)
        {
            case MessageType.Hello when !s.HelloReceived:
                s.HelloReceived = true;
                Login(s, HelloMessage.Read(r));
                break;
            case MessageType.ContentRequest when s.HelloReceived:
                Content?.Request(s, ContentRequestMessage.Read(r).Sha1);
                break;
            case MessageType.CharacterChoice when s.HelloReceived:
                ChooseCharacter(s, CharacterChoiceMessage.Read(r));
                break;
            case MessageType.ClientRequest when s.Creature != null:
            {
                var req = ClientRequestMessage.Read(r);
                Requests.ClientRequests.Handle(World, s.Creature, req.Kind, req.Json);
                break;
            }
            case MessageType.ClientRequest:
            {
                // Before choosing a character: character creation, or a portal prompt asking what this server is.
                var req = ClientRequestMessage.Read(r);
                if (req.Kind == "server-info") { s.Send(ServerInfo()); break; }
                if (s.HelloReceived) Progression.ChargenRequests.HandlePreLogin(this, s, req.Kind, req.Json);
                break;
            }
            case MessageType.MoveInput when s.Creature != null:
                s.Creature.DriveInput = null;
                var m = MoveInputMessage.Read(r);
                // A dying, dead or otherwise uncommandable player can't move.
                if (s.Creature.IsDying || !ActionQueues.Accepts(s.Creature)) break;
                if (m.Direction.LengthSquared() > 0.0001f)
                {
                    // Steering by keys overrides click-to-move and other queued actions.
                    if (s.Creature.Actions.Count > 0) ActionQueues.Clear(World, s.Creature);
                    s.Creature.MoveDirection = m.Direction.LengthSquared() > 1 ? Vector3.Normalize(m.Direction) : m.Direction;
                    s.Creature.WalkRequested = m.Walk;
                }
                else if (s.Creature.Actions.Count == 0)
                {
                    s.Creature.MoveDirection = Vector3.Zero;
                    s.Creature.WalkRequested = m.Walk;
                }
                break;
            case MessageType.ChatSend when s.Creature != null:
                PlayerChat(s, ChatSendMessage.Read(r));
                break;
            case MessageType.DriveInput when s.Creature != null:
                Driving.Receive(World, s.Creature, DriveInputMessage.Read(r));
                break;
            case MessageType.ActionRequest when s.Creature != null:
                HandleAction(s, ActionRequestMessage.Read(r));
                break;
            case MessageType.DialogReply when s.Creature != null:
                var reply = DialogReplyMessage.Read(r);
                var conversations = World.System<Conversations.ConversationService>();
                if (reply.Choice < 0) conversations.Dismiss(s.Creature);
                else conversations.Choose(s.Creature, reply.Choice);
                break;
        }
    }

    /// <summary>
    /// An emote from the radial or quickbar (docs/QUICKBAR_RADIAL.md): it replaces what the player was doing and plays
    /// as ActionPlayAnimation would (moving ends a looping one), speaking its voice-set line when it has one.
    /// </summary>
    void Emote(Creature pc, OpenWinter.Content.Emotes.Emote emote)
    {
        if (pc.IsDead || !ActionQueues.Accepts(pc)) return;
        ActionQueues.Clear(World, pc);
        pc.Actions.Add(new AnimationAction { Animation = emote.Animation, Duration = emote.Seconds });
        if (emote.Voice >= 0) WorldObjects.VoiceService.Play(World, pc, emote.Voice);
    }

    void HandleAction(PlayerSession s, ActionRequestMessage a)
    {
        switch (a.Action)
        {
            case ActionKind.Emote:
                if (OpenWinter.Content.Emotes.All.FirstOrDefault(e => e.Key == a.Argument) is { } emote) Emote(s.Creature!, emote);
                else Broadcast(s.Area, new PlayAnimationMessage { ObjectId = s.Creature!.Id, Animation = a.Argument, Gesture = true }.Write());
                break;
            case ActionKind.ToggleStance:
                s.Creature!.InCombat = !s.Creature.InCombat;
                World.MarkChanged(s.Creature);
                break;
            case ActionKind.Attack:
                Broadcast(s.Area, new PlayAnimationMessage { ObjectId = s.Creature!.Id, Animation = a.Argument }.Write());
                break;
            case ActionKind.MoveTo:
                if (s.Creature!.IsDying || !ActionQueues.Accepts(s.Creature)) break;
                ActionQueues.Clear(World, s.Creature!);
                Movement.Stop(s.Creature!);
                var move = new MoveAction { Target = a.Position, Run = a.Run, Range = 0.3f, AllowBackward = true };
                s.Creature!.Actions.Add(move);
                _commandReticles.Track(s.Creature, move, a.Position);
                // Movement runs before action queues in World.Step. Prime the new route now,
                // so replacing a destination cannot insert an idle tick and restart locomotion.
                s.Creature.MoveBlockedBy = GameObject.InvalidId;
                ActionQueues.Step(World, s.Creature, 0);
                break;
            case ActionKind.Talk:
            case ActionKind.Use:
                if (s.Creature!.IsDying || !ActionQueues.Accepts(s.Creature)) break;
                Interact(s.Creature!, World.Get(a.Target), a.Action == ActionKind.Talk);
                break;
            case ActionKind.OpenDoor:
            case ActionKind.CloseDoor:
                if (s.Creature is { } pc && World.Get<Door>(a.Target) is { } door && door.Area == pc.Area
                    && ActionQueues.Accepts(pc) && !pc.IsDead)
                {
                    if (!WorldObjects.DoorRecoveryAction.Pending(pc, door.Id)) ActionQueues.Clear(World, pc);
                    pc.Actions.Add(a.Action == ActionKind.CloseDoor
                        ? new WorldObjects.CloseDoorAction { Target = door.Id, Run = true }
                        : new WorldObjects.OpenDoorAction { Target = door.Id, Run = true });
                }
                break;
        }
    }

    /// <summary>Runs the module's OnPlayerChat (which may change or swallow the message), then delivers it and lets listeners hear it.</summary>
    void PlayerChat(PlayerSession s, ChatSendMessage chat)
    {
        var pc = s.Creature!;
        if (_options.DevCommands && chat.Text.StartsWith('/') && DevCommand(pc, chat.Text)) return;
        if (chat.Channel != ChatChannel.Dm && World.System<DungeonMaster.DungeonMasterSystem>().IsMuted(s)) { SendMessage(pc, "You have been muted by a Dungeon Master."); return; }
        // TALKVOLUME_*: the DM channel is TALKVOLUME_SILENT_SHOUT (4), tells TALKVOLUME_TELL (6).
        int volume = chat.Channel switch { ChatChannel.Whisper => 1, ChatChannel.Shout => 2, ChatChannel.Party => 5, ChatChannel.Tell => 6, ChatChannel.Dm => 4, _ => 0 };
        var ctx = new Scripting.EventContext().Set("ChatSpeaker", pc.Id).Set("ChatMessage", chat.Text).Set("ChatVolume", volume);
        World.RunEvent(World.Module, "Mod_OnPlrChat", ctx);
        var text = ctx.Str("ChatMessage");
        if (text.Length == 0) return;
        var channel = ctx.Int("ChatVolume") switch { 1 => ChatChannel.Whisper, 2 => ChatChannel.Shout, 5 => ChatChannel.Party, 6 => ChatChannel.Tell, 4 => ChatChannel.Dm, _ => ChatChannel.Talk };
        var msg = new ChatMessage { Speaker = pc.Id, Channel = channel, SpeakerName = pc.Name.Resolve(World.Resources.Talk), Text = text }.Write();
        // The DM channel reaches every DM (and echoes to the sender); players never see each other's DM messages.
        if (channel == ChatChannel.Dm) { foreach (var x in _sessions) if (x.IsDM || x == s) x.Send(msg); }
        else if (channel == ChatChannel.Party)
        {
            // Party membership belongs to the original character while a familiar
            // supplies the controlled speaker. Party chat has no area restriction.
            var party = Society.PartyService.Of(World);
            var members = party.Members(party.Ensure(s.PlayerCreature!));
            foreach (var x in _sessions)
                if (x == s || x.PlayerCreature is { } member && members.Contains(member)) x.Send(msg);
        }
        else if (channel == ChatChannel.Shout) Broadcast(null, msg);
        else BroadcastHeard(pc, channel == ChatChannel.Whisper ? 1 : 0, msg);
        Society.SocietyService.OnChat(World, pc, text, ctx.Int("ChatVolume"));
    }

    bool DevCommand(Creature pc, string text)
    {
        var parts = text[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        var nearest = pc.Area?.Objects.OfType<Creature>().Where(c => c != pc && !c.IsDead)
            .OrderBy(c => Vector3.Distance(c.Position, pc.Position)).FirstOrDefault();
        switch (parts[0].ToLowerInvariant())
        {
            case "vfx" when parts.Length > 1 && int.TryParse(parts[1], out var vfx):
                Notify("vfx", pc, new { vfx, duration = 0f, id = 0L });
                return true;
            case "attack" when nearest != null:
                ActionQueues.Clear(World, pc);
                pc.Actions.Add(new Combat.AttackAction { Target = nearest.Id });
                return true;
            case "spawn" when parts.Length > 1 && pc.Area != null:
            {
                // Creates a creature from a blueprint a few metres ahead: /spawn <resref> [faction id].
                var ahead = pc.Position + new Vector3(MathF.Cos(pc.Facing), MathF.Sin(pc.Facing), 0) * 3f;
                if (Scripting.Functions.ObjectFunctions.Spawn(World, 1, parts[1], pc.Area, ahead, pc.Facing + MathF.PI) is Creature spawned
                    && parts.Length > 2 && int.TryParse(parts[2], out var faction))
                    World.Factions.SetObjectFaction(spawned, faction);
                return true;
            }
            case "xp" when parts.Length > 1 && int.TryParse(parts[1], out var xp):
                Rules.CreatureRules.GiveXP(World, pc, xp);
                return true;
            case "time" when parts.Length > 1 && int.TryParse(parts[1], out var hour):
                World.Clock.Set(hour, 0, 0, 0);
                _nextClock = 0;
                return true;
            case "store":
            {
                // Opens the store with the given tag, or the nearest one in the module.
                var store = parts.Length > 1 ? World.ByTag(parts[1]).OfType<Store>().FirstOrDefault()
                    : World.AllObjects.OfType<Store>().OrderBy(s => s.Area == pc.Area ? Vector3.Distance(s.Position, pc.Position) : float.MaxValue).FirstOrDefault();
                if (store != null) World.Inventory.OpenStore(store, pc, 0, 0);
                return true;
            }
            case "journal":
            {
                // Adds a journal entry: /journal [tag state]; without arguments, the module's first quest.
                var journal = World.System<Players.JournalService>();
                if (parts.Length > 1 && parts[1] == "demo")
                {
                    // Development: walks a few quests through their entries (journal history), finishing the last one.
                    var quests = journal.Categories.Values.Where(c => c.Entries.Count > 1).Take(5).ToList();
                    for (int q = 0; q < quests.Count; q++)
                    {
                        var entries = quests[q].Entries.Values.ToList();
                        int upto = q == quests.Count - 1 ? entries.Count : Math.Max(1, Math.Min(entries.Count - 1, 3));
                        foreach (var e in entries.Take(upto).Where(e => !e.End || q == quests.Count - 1))
                            journal.Add(quests[q].Tag, e.Id, pc, false, false, true);
                    }
                    return true;
                }
                var cat = parts.Length > 1 ? journal.Categories.GetValueOrDefault(parts[1]) : journal.Categories.Values.FirstOrDefault(c => c.Entries.Count > 0);
                if (cat == null) return true;
                int state = parts.Length > 2 && int.TryParse(parts[2], out var st) ? st : cat.Entries.Keys.Min();
                journal.Add(cat.Tag, state, pc, false, false, true);
                return true;
            }
            case "cast" when parts.Length > 1 && int.TryParse(parts[1], out var castSpell):
            {
                // Cheat-casts a spell (spells.2da row) at the nearest creature, or yourself: /cast 107
                ActionQueues.Clear(World, pc);
                pc.Actions.Add(new Spells.CastSpellAction { Spell = castSpell, Cheat = true, Target = parts.Length > 2 && nearest != null ? nearest.Id : pc.Id });
                return true;
            }
            case "loot":
            {
                // Goes to the nearest unlocked container in the area and opens it (treasure scripts fill many on open).
                var chest = pc.Area?.Objects.OfType<Placeable>().Where(p => p.HasInventory && !p.Locked && p.Useable)
                    .OrderBy(p => Vector3.Distance(p.Position, pc.Position)).FirstOrDefault();
                if (chest == null) return true;
                ActionQueues.Clear(World, pc);
                World.Jump(pc, new Location(chest.Area!.Id, chest.Position + new Vector3(1.2f, 0, 0), 180));
                pc.Actions.Add(new Conversations.InteractAction { Target = chest.Id });
                return true;
            }
            case "kill":
                Rules.HitPoints.Kill(World, pc, pc.Id);
                return true;
            case "god":
            {
                // Development (playtesting): immortal (hit points never drop below 1) and fully healed; "/god off" ends it.
                pc.IsImmortal = !(parts.Length > 1 && parts[1].Equals("off", StringComparison.OrdinalIgnoreCase));
                Heal(pc);
                SendMessage(pc, pc.IsImmortal ? "God mode on." : "God mode off.");
                return true;
            }
            case "heal":
                Heal(pc);
                return true;
            case "effect" when parts.Length > 1:
            {
                // Applies a temporary effect by EFFECT_TYPE_* name: /effect haste [seconds]
                if (World.Scripts?.Definitions.Constants.TryGetValue("EFFECT_TYPE_" + parts[1].ToUpperInvariant(), out var c) != true || !int.TryParse(c.Value, out int type)) return true;
                float seconds = parts.Length > 2 && float.TryParse(parts[2], out var sec) ? sec : 60;
                World.System<Rules.EffectSystem>().Apply(new Scripting.Effect { Type = type, Ints = [2, 2, 0, 0, 0, 0, 0, 0] }, 1, pc, seconds);
                return true;
            }
            case "explore" when pc.Area is { } exploreArea:
            {
                // Development: reveals the whole area map (ExploreAreaForPlayer).
                var env = WorldObjects.AreaSettings.Of(World, exploreArea);
                env.Explored[pc.Id] = Enumerable.Repeat(true, Math.Max(0, exploreArea.Width * exploreArea.Height)).ToArray();
                return true;
            }
            case "script" when parts.Length > 1:
                World.Scripts?.Run(parts[1], pc.Id);
                return true;
        }
        return false;
    }

    /// <summary>Restores a character to full hit points (dev commands).</summary>
    void Heal(Creature pc)
    {
        int max = Rules.CreatureRules.MaxHitPoints(World, pc);
        if (pc.CurrentHitPoints < max) pc.CurrentHitPoints = max;
        World.MarkChanged(pc);
    }

    /// <summary>The player clicked an object: walk up to it, then talk to it or use it.</summary>
    void Interact(Creature pc, GameObject? target, bool talk)
    {
        if (target == null || target.Area != pc.Area) return;
        if (target is Creature { IsDead: true } corpse)
        {
            if (World.System<Rules.CorpseSystem>().LootFor(corpse) is not { } loot) return;
            target = loot;
            talk = false;
        }
        if (talk || target is not Door || !WorldObjects.DoorRecoveryAction.Pending(pc, target.Id))
            ActionQueues.Clear(World, pc);
        // Clicking the container already being looted closes it without another approach/open.
        // Keep scripted ActionInteractObject behavior separate from this player click toggle.
        if (!talk && target is Placeable && World.System<Requests.ContainerService>().OpenFor(pc) == target)
        {
            World.System<Requests.ContainerService>().Close(pc);
            return;
        }
        if (target is Item item)
        {
            if (item.Possessor == GameObject.InvalidId && item.UseableOnGround && ActionQueues.Accepts(pc))
                pc.Actions.Add(new Inventory.PickUpItemAction { ItemId = item.Id });
            return;
        }
        if (target is Trigger || target is Door && !talk)
        {
            // Doors open, close or lead elsewhere; the transition rules decide.
            var approach = new MoveAction { TargetObject = target.Id, Run = true, Range = 1.8f };
            pc.Actions.Add(approach);
            _commandReticles.Track(pc, approach, target.Position);
            World.System<WorldObjects.TransitionService>().Click(pc, target);
            return;
        }
        // Like the original, clicking a creature that is hostile to you (by faction reputation and the PvP
        // setting) attacks it; anyone else is talked to.
        if (target is Creature { IsDead: false } foe && World.System<Players.ReactionFeed>().Of(foe, pc) == Players.ReactionFeed.Hostile)
        {
            if (pc.IsDead || !pc.Commandable) return;
            // On the action queue, so the player's next order ends the fight (see Combat.AttackAction).
            var attack = new Combat.AttackAction { Target = foe.Id };
            pc.Actions.Add(attack);
            _commandReticles.Track(pc, attack, foe.Position, foe.Id);
            World.System<Combat.CombatSystem>().Attack(pc, foe);
            return;
        }
        // Only containers have the measured close approach. If their use point is blocked, retain
        // the previous 1.6 m reach instead of repeatedly trying to walk into furniture.
        bool container = target is Placeable { HasInventory: true };
        var walk = new MoveAction
        {
            TargetObject = target.Id, Run = true,
            Range = target is Creature ? 2.0f : container ? 0.8f : 1.6f,
            GiveUpWithin = container ? 1.6f : 0,
        };
        pc.Actions.Add(walk);
        if (target is not Creature) _commandReticles.Track(pc, walk, target.Position);
        pc.Actions.Add(new Conversations.InteractAction { Target = target.Id, Talk = talk, Type = talk || target is Creature ? 7 : 15, Clicked = true });
    }

    float _exportTimer;

    /// <summary>
    /// What a portal prompt shows of this server before the player joins (the original lists the server name, module,
    /// players and levels, whether it needs a password and whether it takes local characters; mp_portal). Asked on a
    /// connection that has not logged in.
    /// </summary>
    public byte[] ServerInfo() => new NotifyMessage
    {
        Kind = "server-info",
        Json = System.Text.Json.JsonSerializer.Serialize(new
        {
            name = _options.ServerName,
            module = World.Module.Name.Resolve(World.Resources.Talk),
            players = _sessions.Count(x => x.Creature != null && !x.IsDM),
            maxPlayers = _options.MaxPlayers,
            // Levels allowed: OpenWinter servers set no limits, so the game's whole range (nwserver's default 1 - 40).
            minLevel = 1,
            maxLevel = 40,
            password = false, // OpenWinter servers take no player password
            localCharacters = _options.Vault == VaultMode.Local,
        }),
    }.Write();

    void Login(PlayerSession s, HelloMessage hello)
    {
        if (_options.MaxPlayers > 0 && _sessions.Count(x => x != s && x.Creature != null) >= _options.MaxPlayers)
        {
            s.Send(new ChatMessage { Channel = ChatChannel.Server, Text = "The server is full." }.Write());
            s.Connection.Disconnect();
            return;
        }
        s.PlayerName = hello.PlayerName;
        s.AreaReadyHandshake = hello.AreaReadyHandshake;
        s.PublicKey = hello.PlayerKey;
        s.PortalWaypoint = hello.PortalWaypoint;
        if (hello.DM && !DungeonMaster.DungeonMasterSystem.PasswordAccepted(_options.DMPassword, hello.DMPassword))
        {
            s.Send(new NotifyMessage { Kind = "login-refused", Json = "{\"reason\":\"Invalid DM password.\"}" }.Write());
            s.Connection.Disconnect();
            return;
        }
        if (!hello.DM && _options.Vault == VaultMode.Server && _options.CharacterSelection
            && !Persistence.ServerVault.ClaimFolder(World, hello.PlayerName, hello.PlayerKey))
        {
            s.Send(new ChatMessage { Channel = ChatChannel.Server, Text = "That player name is in use with a different key." }.Write());
            s.Connection.Disconnect();
            return;
        }
        s.IsDM = hello.DM;
        // Naming a local-vault character skips selection only for DMs and on servers without a selection screen.
        if (!IsPlainFileName(hello.CharacterResRef) || (_options.CharacterSelection && !s.IsDM)) hello.CharacterResRef = "";
        var ifo = World.Module.Ifo;
        s.Send(new WelcomeMessage
        {
            ModuleName = World.Module.Name.Resolve(World.Resources.Talk),
            CustomTlk = ifo.GetString("Mod_CustomTlk"),
            Haks = ResMan.ModuleHaks(ifo).ToList(),
            Files = Content?.Files.ToList() ?? new(),
        }.Write());

        // Loading a saved game from the main menu: the player takes over the saved character.
        if (_options.LoadOnLogin is { Length: > 0 } saveFolder && _options.SaveGames && !s.IsDM)
        {
            _options.LoadOnLogin = null;
            Defer(() =>
            {
                try { SaveGames.SaveGameRequests.LoadInto(this, saveFolder); }
                catch (Exception e)
                {
                    Console.WriteLine($"[server] loading {saveFolder} failed: {e}");
                    s.Send(new ChatMessage { Channel = ChatChannel.Server, Text = "The saved game could not be loaded: " + e.Message }.Write());
                }
                if (s.Creature == null) Login(s, hello); // nothing to take over: choose a character as usual
            });
            return;
        }
        // DMs skip the vault list: they enter as the named character or a default DM avatar.
        if (_options.CharacterSelection && !s.IsDM && string.IsNullOrEmpty(hello.CharacterResRef) && string.IsNullOrEmpty(_options.DefaultCharacter))
        {
            s.Send(new CharacterListMessage
            {
                Mode = _options.Vault,
                AllowNew = _options.AllowCharacterCreation,
                Characters = _options.Vault == VaultMode.Server ? Persistence.ServerVault.List(World, s.PlayerName) : new(),
            }.Write());
            return;
        }
        EnterWorld(s, CreatePlayerCreature(hello));
    }

    static bool IsPlainFileName(string name) =>
        name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.IndexOfAny(['/', '\\', ':']) < 0 && !name.Contains("..");

    /// <summary>The player chose a character on the selection screen.</summary>
    void ChooseCharacter(PlayerSession s, CharacterChoiceMessage choice)
    {
        if (s.Creature != null) return;
        Creature? c = null;
        try
        {
            if (_options.Vault == VaultMode.Server && choice.File.Length > 0)
            {
                if (Persistence.ServerVault.Load(World, s.PlayerName, choice.File) is { } bic)
                {
                    c = World.Creatures.FromTemplate(bic);
                    c.VaultFile = Path.GetFileNameWithoutExtension(choice.File);
                }
            }
            else if (_options.Vault == VaultMode.Local && choice.Bic.Length > 0)
            {
                var bic = Gff.Read(choice.Bic);
                if (_options.EnforceLegalCharacters && Progression.LegalCharacterCheck.Check(World, bic) is { Count: > 0 } illegal)
                {
                    Console.WriteLine($"[server] {s.PlayerName}: illegal character: {string.Join("; ", illegal.Take(5))}");
                    s.Send(new ChatMessage { Channel = ChatChannel.Server, Text = "That character is not legal on this server: " + string.Join(" ", illegal.Take(5).Select(e => e.Message)) }.Write());
                    return;
                }
                c = World.Creatures.FromTemplate(bic);
            }
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or IOException)
        {
            Console.WriteLine($"[server] {s.PlayerName}: unreadable character: {e.Message}");
        }
        if (c == null)
        {
            s.Send(new ChatMessage { Channel = ChatChannel.Server, Text = "That character could not be loaded." }.Write());
            return;
        }
        SetPlayerSpeeds(c);
        EnterWorld(s, c);
    }

    /// <summary>Enters the game with a character made on the selection screen (character creation).</summary>
    public void EnterWithNewCharacter(PlayerSession s, Creature c)
    {
        if (s.Creature != null) return;
        SetPlayerSpeeds(c);
        EnterWorld(s, c);
    }

    void EnterWorld(PlayerSession s, Creature creature)
    {
        creature.Player = s;
        s.Creature = creature;
        if (s.IsDM) World.System<DungeonMaster.DungeonMasterSystem>().MakeAvatar(s, creature);

        var area = (_options.StartArea != null ? World.FindArea(_options.StartArea) : null) ?? World.FindArea(World.Module.EntryArea) ?? World.Areas.FirstOrDefault();
        if (area == null) return;
        var pos = World.Module.EntryPosition;
        float facing = MathF.Atan2(World.Module.EntryDirection.Y, World.Module.EntryDirection.X);
        if (_options.StartArea != null || area.ResRef != World.Module.EntryArea)
            pos = SpawnPoint(area);
        // Through a portal: at the waypoint it named (the tag matched exactly: the game's release notes say
        // ActivatePortal's sWaypointTag is case sensitive); the module's start when there is none.
        if (s.PortalWaypoint is { Length: > 0 } tag
            && World.ByTag(tag).OfType<Objects.Waypoint>().FirstOrDefault(w => w.Tag == tag && w.Area != null) is { } waypoint)
        {
            area = waypoint.Area!;
            pos = waypoint.Position;
            facing = waypoint.Facing;
        }
        s.PortalWaypoint = "";
        if (area.Walkmesh.HighestGround(pos.X, pos.Y) is { } z) { if (MathF.Abs(z - pos.Z) > 1.5f) pos.Z = z; }
        // An entry point off the walkable ground (inside a building's footprint) moves to the nearest ground, as in the original.
        else if (area.Walkmesh.NearestWalkable(pos) is { } ground) pos = ground;
        World.Register(creature);
        World.RunEvent(World.Module, "Mod_OnClientEntr", new Scripting.EventContext().Set("Entering", creature.Id));
        World.Inventory.FireLoginAcquires(creature);
        // The join line comes before the first area's PvP line (mp_join), after the area itself is on its way.
        _joining = s;
        try { EnterArea(s, area, pos, facing); }
        finally { _joining = null; }
        AnnouncePlayer(s, joined: true);
        if (!s.IsAreaLoading && s.Area == area) AnnounceArea(s, area);
        World.System<DungeonMaster.DungeonMasterSystem>().NotifyState(s);
    }

    Creature CreatePlayerCreature(HelloMessage hello)
    {
        Creature? c = null;
        var character = string.IsNullOrEmpty(hello.CharacterResRef) ? _options.DefaultCharacter : hello.CharacterResRef;
        if (!string.IsNullOrEmpty(character))
        {
            // Characters made in development runs live in the sandboxed local vault; then the real one.
            var bic = new[] { World.System<Persistence.PersistenceSystem>().UserRoot, World.Resources.Paths.UserRoot }
                .Select(root => Path.Combine(root, "localvault", character + ".bic")).FirstOrDefault(File.Exists);
            if (bic != null) c = World.Creatures.FromTemplate(Gff.Read(File.ReadAllBytes(bic)));
        }
        if (c == null)
        {
            c = new Creature { Tag = "player", Name = new LocString(string.IsNullOrEmpty(hello.PlayerName) ? "Player" : hello.PlayerName), WalkRate = 2f, RunRate = 4f };
            c.AppearanceType = _options.DefaultAppearance >= 0 ? _options.DefaultAppearance : Math.Max(0, World.Resources.TwoDA("appearance").Find("RACE", "c_bugbeara"));
            // A real player always has at least one level; scripts divide by hit dice and the like.
            c.Classes.Add(new CreatureClass { Class = 4, Level = 1 }); // fighter
            c.CurrentHitPoints = c.MaxHitPoints = 10;
        }
        SetPlayerSpeeds(c);
        return c;
    }

    void SetPlayerSpeeds(Creature c)
    {
        var speeds = World.Resources.TwoDA("creaturespeed");
        c.WalkRate = speeds.GetFloat(0, "WALKRATE", 2f);
        c.RunRate = speeds.GetFloat(0, "RUNRATE", 4f);
    }

    /// <summary>Walkable point nearest the area's centre.</summary>
    static Vector3 SpawnPoint(Area area)
    {
        var centre = new Vector2(area.Width * Area.TileSize / 2, area.Height * Area.TileSize / 2);
        Vector3 best = new(centre, 0);
        float bestD = float.MaxValue;
        foreach (var t in area.Walkmesh.Triangles)
        {
            if (!t.Walkable) continue;
            var c = (t.A + t.B + t.C) / 3;
            float d = Vector2.Distance(new Vector2(c.X, c.Y), centre);
            if (d < bestD && area.Walkmesh.GroundHeight(c.X, c.Y, c.Z) != null) { bestD = d; best = c; }
        }
        return best;
    }

    public void EnterArea(PlayerSession s, Area area, Vector3 position, float facing, bool alreadyPlaced = false)
    {
        // A creature already standing in the area (save loads, ending possession) stays in the world:
        // only a creature arriving waits for the client.
        if (s.AreaReadyHandshake && !s.CommittingArea && !alreadyPlaced)
        {
            StageAreaEntry(s, area, position, facing, exact: false, enterEvent: true);
            return;
        }
        SettleAreaEntry(s);
        bool arriving = s.Area != area;
        if (s.Area != null && s.Area != area)
        {
            foreach (var id in s.Known) s.Send(new DespawnMessage { ObjectId = id }.Write());
            s.Known.Clear();
        }
        s.Area = area;
        World.System<Players.CreatureVisibilityFeed>().Invalidate(s);
        // Keep a valid restored snapshot and its script schedule. Only another area's
        // lists need invalidating; the private feed still resends to recreated views.
        if (s.Creature is { } viewer && viewer.Society.PerceptionArea != area.Id)
        {
            viewer.Society.PerceptionArea = GameObject.InvalidId;
            viewer.Society.NextPerception = -1;
        }
        s.Send(area.Describe(World.Resources.Talk).Write());
        s.Send(Clock().Write());
        if (!alreadyPlaced) World.AddToArea(s.Creature!, area, position, facing);
        foreach (var o in area.Objects) SendSpawn(s, o);
        s.Send(new ControlObjectMessage { ObjectId = s.Creature!.Id }.Write());
        World.System<WorldObjects.MapExploration>().Sync(s.Creature);
        // The feed only sends changes, and what it last sent is part of a loaded save: a fresh area (a load included)
        // must get the whole HUD again, or the portrait stays black until something changes.
        World.System<Players.HudFeed>().Resend(s.Creature, now: false);
        foreach (var other in _sessions)
            if (other != s && other.Area == area) SendSpawn(other, s.Creature);
        if (arriving && _joining != s) AnnounceArea(s, area);
    }

    /// <summary>The session entering the game in <see cref="EnterWorld"/>, whose first area line waits for the join line.</summary>
    PlayerSession? _joining;

    /// <summary>
    /// "&lt;player&gt; has joined as a player." / "... as a game master." (dialog.tlk 6610/6611) to everyone on the server
    /// when a player enters the game, and "has left as ..." (6612/6613) when she leaves. Measured: the original told the
    /// joining player herself "MPA has joined as a player.." (the line gains a second full stop); the leaving line is
    /// built the same way (not measured: two original clients can't share a server, docs/MULTIPLAYER_HARNESS.md).
    /// </summary>
    void AnnouncePlayer(PlayerSession s, bool joined)
    {
        uint strref = (uint)(joined ? (s.IsDM ? 6611 : 6610) : (s.IsDM ? 6613 : 6612));
        string text = World.Resources.TlkString(strref).Replace("<PlayerName>", s.PlayerName) + ".";
        var msg = new ChatMessage { Channel = ChatChannel.Server, Text = text }.Write();
        foreach (var x in _sessions)
            if (x.Creature != null && (joined || x != s)) x.Send(msg);
    }

    /// <summary>
    /// "[Server] You are now in a Party PVP area." (dialog.tlk 8013 full, 8014 party, 8015 none) each time a player
    /// enters an area, by the PvP in force there (World.AreaPvp). Measured with mp_join on the original.
    /// </summary>
    internal void AnnounceArea(PlayerSession s, Area area)
    {
        if (s.Creature == null || s.IsDM) return;
        uint strref = World.AreaPvp(area) switch { PvpSetting.Full => 8013u, PvpSetting.None => 8015u, _ => 8014u };
        s.Send(new ChatMessage { Channel = ChatChannel.Server, Text = "[Server] " + World.Resources.TlkString(strref) }.Write());
    }

    static bool IsVisibleKind(GameObject o) => o is Creature or Placeable or Door or Item or Sound;

    /// <summary>
    /// What a player's client isn't told about: hidden DM avatars (except to DMs and their owner), and creatures in
    /// stealth or invisible that her character neither sees nor hears. The original draws nothing of those and gives
    /// them no Tab label or highlight (visual scene stealthview), so their positions aren't sent at all.
    /// </summary>
    /// <summary>
    /// Whether a player may be told about an object: in the player's area and not hidden from them (the rule that
    /// decides which objects their client is sent). True without a session or server to ask.
    /// </summary>
    public static bool Perceives(World world, Creature player, GameObject o)
    {
        if (o.Area == null || o.Area != player.Area) return false;
        return world.Output is not GameServer server || player.Player is not { } s || !server.HiddenFrom(s, o);
    }

    bool HiddenFrom(PlayerSession s, GameObject o)
    {
        if (o is not Creature c) return false;
        if (c.IsDMUndetectable) return !s.IsDM && c.DMOwner != s;
        return ConcealedFrom(s, c);
    }

    bool ConcealedFrom(PlayerSession s, Creature t)
    {
        if (s.IsDM || s.Creature is not { } viewer || viewer == t || !Concealed(t) || t.IsDead) return false;
        if (viewer.Area == null || viewer.Area != t.Area) return false;
        if (World.System<Skills.SkillSystem>().AlwaysPerceives(viewer, t)) return false;
        // Until the viewer's first perception update in this area there is nothing to show yet.
        if (viewer.Society.PerceptionArea != viewer.Area.Id) return true;
        return !viewer.Society.Seen.Contains(t.Id) && !viewer.Society.Heard.Contains(t.Id);
    }

    /// <summary>In stealth mode, or under an effect that hides it from sight (invisibility, darkness, sanctuary, etherealness).</summary>
    static bool Concealed(Creature t)
    {
        if (t.StealthMode) return true;
        foreach (var e in Society.PerceptionSystem.Effects(t))
            if (e.Type is Combat.CombatK.EffectInvisibility or Combat.CombatK.EffectImprovedInvisibility or Combat.CombatK.EffectSanctuary
                or Combat.CombatK.EffectEthereal or Combat.CombatK.EffectDarkness) return true;
        return false;
    }

    /// <summary>
    /// Spawns concealed creatures for players who have just perceived them and removes those who have just lost them.
    /// Perception changes without the creature moving, so dirty-object updates alone wouldn't catch it.
    /// </summary>
    void SyncConcealed()
    {
        foreach (var s in _sessions)
        {
            if (s.Area is not { } area || s.Creature == null || s.IsDM) continue;
            foreach (var o in area.Objects)
            {
                if (o is not Creature c || c.IsDestroyed || c == s.Creature) continue;
                if (c.Player?.IsAreaLoading == true) continue;
                bool hidden = HiddenFrom(s, c);
                if (hidden && s.Known.Remove(c.Id)) s.Send(new DespawnMessage { ObjectId = c.Id }.Write());
                else if (!hidden && !s.Known.Contains(c.Id)) SendSpawn(s, c);
            }
        }
    }

    void SendSpawn(PlayerSession s, GameObject o, long skipVisualId = 0)
    {
        if (o is Creature { Player.IsAreaLoading: true } loading && s.Creature != loading) return;
        if (!IsVisibleKind(o) || HiddenFrom(s, o) || !s.Known.Add(o.Id)) return;
        var spawn = BuildSpawn(o);
        // Compare later mutations with the appearance actually sent in the spawn.
        // Another observer arriving must not replace an older observer's baseline.
        if (o is Creature) _looks.TryAdd(o.Id, AppearanceKey(spawn.Appearance));
        s.Send(spawn.Write());
        // Effects already running on it (auras, glows, beams) start for this player too.
        // Script-set UI hints (SetObjectMouseCursor, SetObjectHiliteColor) reach players who arrive later too.
        if (o.MouseCursor >= 0)
            s.Send(new NotifyMessage { Kind = "mouse-cursor", Subject = o.Id, Json = System.Text.Json.JsonSerializer.Serialize(new { cursor = o.MouseCursor }) }.Write());
        if (o is Creature { HiliteColor: >= 0 } lit)
            s.Send(new NotifyMessage { Kind = "hilite-color", Subject = o.Id, Json = System.Text.Json.JsonSerializer.Serialize(new { color = lit.HiliteColor }) }.Write());
        if (o.Effects.Count > 0)
            foreach (var (kind, data) in World.System<Rules.EffectSystem>().LastingVisuals(o))
            {
                if (skipVisualId != 0 && System.Text.Json.JsonSerializer.SerializeToElement(data).GetProperty("id").GetInt64() == skipVisualId) continue;
                s.Send(new NotifyMessage { Kind = kind, Subject = o.Id, Json = System.Text.Json.JsonSerializer.Serialize(data) }.Write());
            }
    }

    /// <summary>The facing players see: a conversation partner's turn toward the other shows before scripts see it.</summary>
    float ShownFacing(GameObject o) => Convo.ShownFacing(o) ?? o.Facing;

    Conversations.ConversationService Convo
    {
        get
        {
            if (_convoWorld != World) { _convo = World.System<Conversations.ConversationService>(); _convoWorld = World; }
            return _convo!;
        }
    }
    Conversations.ConversationService? _convo;
    World? _convoWorld;

    /// <summary>
    /// Conditions clients show on a creature's body (parity/visual scene conditions): asleep (lying face down),
    /// knocked down (sitting up), held (animation stopped), petrified (stone) and stunned (stars); and, to its own
    /// player only, invisibility, sanctuary or etherealness (translucent) and blindness or darkness (a dark view).
    /// </summary>
    ObjectFlags ConditionFlags(Creature c)
    {
        var flags = ObjectFlags.None;
        foreach (var e in c.Effects)
        {
            // A creature knocked down from behind falls forwards (the player, with the effect's creator behind her,
            // ends kneeling; a bandit facing it falls back and sits up).
            if (e.Type == Combat.CombatK.EffectKnockdown && World.Get(e.Creator) is { } from && from != c && from.Area == c.Area
                && MathF.Cos(c.Facing) * (from.Position.X - c.Position.X) + MathF.Sin(c.Facing) * (from.Position.Y - c.Position.Y) < 0)
                flags |= ObjectFlags.KnockedForward;
            flags |= e.Type switch
            {
                Rules.NwRules.EffectSleep => ObjectFlags.Asleep,
                Combat.CombatK.EffectKnockdown => ObjectFlags.KnockedDown,
                Rules.NwRules.EffectParalyze => ObjectFlags.Held,
                Rules.NwRules.EffectPetrify => ObjectFlags.Petrified,
                Rules.NwRules.EffectStunned => ObjectFlags.Stunned,
                Combat.CombatK.EffectInvisibility or Combat.CombatK.EffectImprovedInvisibility or Combat.CombatK.EffectSanctuary
                    or Combat.CombatK.EffectEthereal => ObjectFlags.Translucent,
                Combat.CombatK.EffectBlindness or Combat.CombatK.EffectDarkness => ObjectFlags.Blinded,
                _ => ObjectFlags.None,
            };
        }
        return flags;
    }


    SpawnMessage BuildSpawn(GameObject o)
    {
        var msg = new SpawnMessage
        {
            ObjectId = o.Id,
            Tag = o.Tag,
            Name = o.Name.Resolve(World.Resources.Talk),
            Position = o.Position,
            Facing = ShownFacing(o),
        };
        switch (o)
        {
            case Creature c:
                msg.Kind = ObjectKind.Creature;
                msg.Appearance = new Appearance
                {
                    Type = c.AppearanceType, Gender = c.Gender, Phenotype = c.Phenotype, BodyParts = c.BodyParts, Colors = c.Colors,
                    Tail = c.Tail, Wings = c.Wings,
                    Equipment = c.Equipment.Select(kv => ItemFactory.Visual(kv.Key, kv.Value)).ToList(),
                };
                msg.WalkSpeed = c.WalkRate;
                msg.RunSpeed = c.RunRate;
                msg.Portrait = Portraits.Display(World, c);
                msg.Flags = (!c.IsDead || c.SelectableWhenDead || World.System<Rules.CorpseSystem>().LootFor(c) != null ? ObjectFlags.Selectable : 0)
                    | (World.System<Rules.CorpseSystem>().LootFor(c) != null ? ObjectFlags.Useable : 0) | (c.IsDead ? ObjectFlags.Dead : 0) | (Convo.IsInConversation(c) ? ObjectFlags.Talking : 0)
                    | (c.IsDead ? 0 : ConditionFlags(c)) | (c.IsDying ? ObjectFlags.Dying : 0);
                break;
            case Placeable p:
                msg.Kind = ObjectKind.Placeable;
                msg.Appearance = new Appearance { Type = p.Appearance };
                // AnimationState: 0 default, 1 open, 2 closed, 3 destroyed, 4 activated, 5 deactivated.
                int animState = (int)p.Template.GetInt("AnimationState");
                msg.Flags = (p.Static ? ObjectFlags.Static : 0) | (p.Useable ? ObjectFlags.Useable | ObjectFlags.Selectable : 0)
                    | (p.IsOpen || animState == 1 ? ObjectFlags.Open : 0) | (animState == 4 ? ObjectFlags.Activated : 0)
                    | (animState == 3 ? ObjectFlags.Dead : 0);
                break;
            case Door d:
                msg.Kind = ObjectKind.Door;
                msg.Appearance = new Appearance { Type = d.Appearance, GenericDoorType = d.GenericType };
                msg.Flags = ObjectFlags.Selectable | (d.IsOpen ? ObjectFlags.Open : 0) | (d.Locked ? ObjectFlags.Locked : 0)
                    | (d.IsTransition || d.Script("OnClick").Length > 0 ? ObjectFlags.Transition : 0)
                    | (d.OpenedSide == 2 ? ObjectFlags.OpenSide2 : 0) | (d.Broken ? ObjectFlags.Dead : 0);
                break;
            case Item i:
                msg.Kind = ObjectKind.Item;
                // The ground model is the item's own, built from its parts and colours (docs/GROUND_ITEMS.md).
                msg.Appearance = new Appearance { Type = i.BaseItem, Equipment = { ItemFactory.Visual(0, i) } };
                msg.Flags = ObjectFlags.Selectable;
                break;
            case Sound snd:
                msg.Kind = ObjectKind.Sound;
                var t = snd.Template;
                msg.Extra = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sounds = snd.Sounds,
                    active = snd.Active,
                    continuous = snd.Continuous,
                    looping = snd.Looping,
                    positional = snd.Positional,
                    random = t.GetInt("Random") != 0,
                    randomPosition = t.GetInt("RandomPosition") != 0,
                    rangeX = t.GetFloat("RandomRangeX"),
                    rangeY = t.GetFloat("RandomRangeY"),
                    elevation = t.GetFloat("Elevation"),
                    interval = (int)t.GetInt("Interval"),
                    intervalVariation = (int)t.GetInt("IntervalVrtn"),
                    pitchVariation = t.GetFloat("PitchVariation"),
                    volume = snd.Volume, // SoundObjectSetVolume's, not the blueprint's
                    volumeVariation = (int)t.GetInt("VolumeVrtn"),
                    minDistance = snd.MinDistance,
                    maxDistance = snd.MaxDistance,
                    times = (int)t.GetInt("Times", 3), // 0 the hours in Hours, 1 day, 2 night, 3 always (measured: soundobj)
                    hours = (uint)t.GetInt("Hours"),
                });
                break;
        }
        return msg;
    }

    readonly Dictionary<uint, string> _looks = new();

    /// <summary>Sends AppearanceMessage when a creature's look changed since it was last sent.</summary>
    void CheckAppearance(Creature c, SpawnMessage spawn)
    {
        var look = AppearanceKey(spawn.Appearance);
        if (_looks.TryGetValue(c.Id, out var before) && before == look) return;
        _looks[c.Id] = look;
        Broadcast(c.Area, new AppearanceMessage { ObjectId = c.Id, Appearance = spawn.Appearance }.Write());
    }

    static string AppearanceKey(Appearance appearance)
    {
        var w = new MessageWriter(MessageType.Appearance);
        appearance.Write(w);
        return System.Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(w.ToArray()));
    }

    readonly Dictionary<uint, double> _nextLookCheck = new();
    readonly HashSet<uint> _pendingLooks = new();

    void FlushUpdates()
    {
        foreach (var o in _dirty)
        {
            if (o.IsDestroyed || o.Area == null) continue;
            // Gone to another area (a creature through a transition, a jump): players it left behind lose it.
            foreach (var s in _sessions)
                if (s.Area != o.Area && s.Creature != o && s.Known.Remove(o.Id)) s.Send(new DespawnMessage { ObjectId = o.Id }.Write());
            // Nobody to tell: changes in areas without players cost nothing (spawns carry current state later).
            bool watched = false;
            foreach (var session in _sessions) if (session.Area == o.Area) { watched = true; break; }
            if (!watched) continue;
            var spawn = BuildSpawn(o);
            if (o is Creature) _pendingLooks.Add(o.Id);
            var update = o is Creature c
                ? new UpdateMessage
                {
                    ObjectId = c.Id, Position = c.Position, Facing = spawn.Facing, Speed = c.Speed, Mode = c.Mode, InCombat = c.InCombat, Flags = spawn.Flags,
                    Surface = c.Area?.Walkmesh.SurfaceAt(c.Position) ?? 0,
                    FootstepType = c.FootstepOverride >= 0 ? c.FootstepOverride : World.Resources.TwoDA("appearance").GetInt(c.AppearanceType, "FOOTSTEPTYPE", -1)
                }
                : new UpdateMessage { ObjectId = o.Id, Position = o.Position, Facing = spawn.Facing, Flags = spawn.Flags };
            byte[]? bytes = null;
            foreach (var s in _sessions)
            {
                if (s.Area != o.Area) continue;
                if (HiddenFrom(s, o)) { if (s.Known.Remove(o.Id)) s.Send(new DespawnMessage { ObjectId = o.Id }.Write()); continue; }
                if (!s.Known.Contains(o.Id)) { SendSpawn(s, o); continue; }
                s.Send(bytes ??= update.Write(), reliable: false);
            }
        }
        _dirty.Clear();
        foreach (uint id in _pendingLooks.ToArray())
        {
            if (World.Get<Creature>(id) is not { Area: not null } creature)
            {
                _pendingLooks.Remove(id);
                continue;
            }
            if (World.Time < _nextLookCheck.GetValueOrDefault(id)) continue;
            _nextLookCheck[id] = World.Time + 1;
            _pendingLooks.Remove(id);
            // Retain a throttled look check even if this stationary object never
            // generates another position update after equipping or changing body parts.
            CheckAppearance(creature, BuildSpawn(creature));
        }
    }

    void OnRemoved(GameObject o)
    {
        _looks.Remove(o.Id);
        _nextLookCheck.Remove(o.Id);
        _pendingLooks.Remove(o.Id);
        foreach (var s in _sessions)
            if (s.Known.Remove(o.Id)) s.Send(new DespawnMessage { ObjectId = o.Id, DecayingCorpse = o is Creature { IsDestroyed: true, IsDead: true } }.Write());
    }

    void Broadcast(Area? area, byte[] message)
    {
        foreach (var s in _sessions)
            if (area == null || s.Area == area) s.Send(message);
    }

    void Disconnect(PlayerSession s)
    {
        if (s.Creature != null) AnnouncePlayer(s, joined: false);
        _sessions.Remove(s);
        Content?.Forget(s);
        World.System<Players.FamiliarPossession>().Release(s);
        World.System<DungeonMaster.DungeonMasterSystem>().BeforeDisconnect(s);
        if (s.Creature != null)
        {
            World.RunEvent(World.Module, "Mod_OnClientLeav", new Scripting.EventContext().Set("Exiting", s.Creature.Id));
            // Server-vault characters are saved as they leave, like the original server does.
            if (_options.Vault == VaultMode.Server && _options.CharacterSelection && !s.IsDM)
                try { Persistence.ServerVault.Export(World, s.Creature); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Console.WriteLine($"[server] saving {s.PlayerName}'s character failed: {e.Message}"); }
            World.Destroy(s.Creature);
        }
    }

    // IWorldOutput: speech, messages and animations raised by scripts.

    public void Speak(GameObject speaker, string text, int volume)
    {
        // TALKVOLUME_SILENT_TALK / SILENT_SHOUT reach listening creatures only, never a player's chat.
        if (volume is 3 or 4) { Society.SocietyService.OnSpeech(World, speaker, text, volume); return; }
        var msg = new ChatMessage
        {
            Speaker = speaker.Id,
            Channel = volume switch { 1 => ChatChannel.Whisper, 2 => ChatChannel.Shout, _ => ChatChannel.Talk },
            SpeakerName = speaker.Name.Resolve(World.Resources.Talk),
            Text = text,
        }.Write();
        if (volume == 2) Broadcast(null, msg);
        else BroadcastHeard(speaker, volume, msg);
        Society.SocietyService.OnSpeech(World, speaker, text, volume);
    }

    /// <summary>
    /// Talk reaches the players within <see cref="Society.SocietyService.TalkRange"/> of the speaker and a whisper
    /// those within <see cref="Society.SocietyService.WhisperRange"/>, in the same area (parity/visual/scenes/mp_delivery:
    /// talk heard from 20 m, not 25; a whisper from 2.5 m, not 3.5; a shout from 65 m).
    /// </summary>
    void BroadcastHeard(GameObject speaker, int volume, byte[] message)
    {
        float range = volume == 1 ? Society.SocietyService.WhisperRange : Society.SocietyService.TalkRange;
        foreach (var s in _sessions)
            if (s.Area == speaker.Area && s.Area != null && s.Creature is { } listener
                && (listener == speaker || Vector3.Distance(listener.Position, speaker.Position) <= range))
                s.Send(message);
    }

    public void PlayAnimation(GameObject o, int animation, float speed, float duration)
    {
        var (name, gesture) = Animations.Name(animation);
        if (name.Length == 0) return;
        // A fire-and-forget animation (ANIMATION_FIREFORGET_*) plays once whatever time it's given.
        if (animation is >= 100 and < 200) duration = 0;
        Broadcast(o.Area, new PlayAnimationMessage { ObjectId = o.Id, Animation = name, Speed = speed, Duration = duration, Gesture = gesture }.Write());
    }

    public void Notify(string kind, GameObject? subject, object data, Creature? player = null)
    {
        // A draw needs the completed equipment appearance in this tick,
        // before the authored model-switch event; ordinary look checks remain throttled.
        if (kind == "equipment-draw" && subject is Creature drawing)
        {
            _nextLookCheck.Remove(drawing.Id);
            _pendingLooks.Add(drawing.Id);
        }
        string json = System.Text.Json.JsonSerializer.Serialize(data);
        var msg = new NotifyMessage { Kind = kind, Subject = subject?.Id ?? GameObject.InvalidId, Json = json }.Write();
        // Private player state (HUD/labels/targeting) remains deliverable during area transitions.
        if (player != null && subject == player) { player.Player?.Send(msg); return; }
        var area = subject as Area ?? subject?.Area;
        void Send(PlayerSession session, bool playerControl = false)
        {
            // Newly spawned creatures can raise appear/VFX events in the creating script,
            // before the end-of-tick dirty flush. Deliver their identity first.
            if (subject is { IsDestroyed: false, Area: not null } && IsVisibleKind(subject))
            {
                // Hidden DM identities remain private even for addressed data.
                if (HiddenFrom(session, subject)) return;
                bool inView = session.Area == subject.Area;
                // Private UI control/data can outlive its subject's area (e.g.
                // closing a container after EnterArea). Do not spawn that old
                // subject. World visuals still require visibility, even when
                // addressed to one player.
                if (!inView && !playerControl) return;
                if (inView && !session.Known.Contains(subject.Id))
                {
                    long skipVisual = 0;
                    if (kind is "vfx" or "beam")
                    {
                        using var visual = System.Text.Json.JsonDocument.Parse(json);
                        if (visual.RootElement.TryGetProperty("id", out var id)) skipVisual = id.GetInt64();
                    }
                    // Other existing auras still replay; this event is sent exactly once below.
                    SendSpawn(session, subject, skipVisual);
                }
            }
            session.Send(msg);
        }
        if (player != null)
        {
            if (player.Player is { } session)
                Send(session, playerControl: kind is "container-close" or "store-close" or "examine" or "dm-examine" or "item-identified");
            return;
        }
        foreach (var session in _sessions)
            if (area == null || session.Area == area) Send(session);
    }

    public void SendMessage(Creature pc, string text) =>
        Players.FamiliarPossession.SessionOf(pc)?.Send(new ChatMessage { Channel = ChatChannel.Server, Text = text }.Write());

    /// <summary>Floating text broadcast to a faction reaches its members this close (mp_delivery: a henchman's 25 m, not 35 m).</summary>
    public const float FloatingTextFactionRange = 30f;

    /// <summary>
    /// FloatingTextStringOnCreature: without bBroadcastToFaction only the creature's own player sees it; with it, the
    /// players whose creature is in its faction (a PC's party) within <see cref="FloatingTextFactionRange"/> in the same
    /// area (mp_delivery: a commoner's text reached nobody; her own and her henchman's at 5 and 25 m reached her).
    /// </summary>
    public void FloatingText(Creature over, string text, bool toFaction)
    {
        var msg = new ChatMessage { Speaker = over.Id, Channel = ChatChannel.Server, SpeakerName = "", Text = text }.Write();
        var own = Players.FamiliarPossession.SessionOf(over);
        if (!toFaction) { own?.Send(msg); return; }
        foreach (var s in _sessions)
            if (s == own || s.Creature is { } member && member.Area == over.Area && member.Area != null && member.Faction == over.Faction
                && Vector3.Distance(member.Position, over.Position) <= FloatingTextFactionRange)
                s.Send(msg);
    }
}

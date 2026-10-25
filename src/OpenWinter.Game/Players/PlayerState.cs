using System.Numerics;
using OpenWinter.Game.Players;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Players
{
    /// <summary>What a connected client advertised about itself (build, device, language, latency).</summary>
    public sealed class ClientInfo
    {
        /// <summary>The "8193" of "87.8193.37-17 abcdef01"; 0 when unknown.</summary>
        public int BuildMajor = 8193;
        public int BuildMinor = 37;
        public int BuildPostfix = 17;
        public string BuildSha1 = "";
        /// <summary>PLAYER_DEVICE_PLATFORM_*.</summary>
        public int Platform = 2;
        /// <summary>LANGUAGE_* (0 = English), -1 when unknown.</summary>
        public int Language = 0;
        public bool Relayed;
        public int LatencyMs, LatencySmoothedMs;
        /// <summary>PLAYER_DEVICE_PROPERTY_* values; missing ones read as -1.</summary>
        public readonly Dictionary<string, int> DeviceProperties = new(StringComparer.Ordinal);
    }

    /// <summary>One journal quest as a player sees it.</summary>
    public sealed class JournalState
    {
        public required string Tag;
        public int State;
        public int Day, Time;
        /// <summary>The earlier entries this player reached in this quest, oldest first (the journal window shows them under the current one).</summary>
        public List<(int State, int Day, int Time)> Earlier = new();
    }

    /// <summary>SetEnterTargetingModeData: spell targeting shape for the next EnterTargetingMode.</summary>
    public sealed class SpellTargetingData
    {
        public int Shape, Flags, Spell = -1, Feat = -1;
        public float SizeX, SizeY, Range;
    }
}

namespace OpenWinter.Game.Objects
{
    /// <summary>Per-player state owned by the player package (camera, cutscenes, journal, GUI, client info).</summary>
    public sealed partial class Creature
    {
        public readonly ClientInfo Client = new();

        // Cutscenes and camera.
        public bool InCutscene, CutsceneLeftClick;
        /// <summary>Plot flag to restore when cutscene mode ends.</summary>
        public bool PlotBeforeCutscene;
        public float CutsceneCameraMoveRate = 1f;
        public int CameraMode;
        public float CameraHeight;
        public int CameraFlags;
        public uint CameraAttachedTo = GameObject.InvalidId;
        /// <summary>StoreCameraFacing snapshot: direction, distance, pitch (all -1 when unset).</summary>
        public (float Direction, float Distance, float Pitch)? StoredCamera;

        /// <summary>Journal quests by plot tag.</summary>
        public readonly Dictionary<string, JournalState> Journal = new(StringComparer.Ordinal);

        /// <summary>GUI_PANEL_* disabled with SetGuiPanelDisabled (panel to target object).</summary>
        public readonly Dictionary<int, uint> DisabledGuiPanels = new();
        public SpellTargetingData? PendingTargetingData;
        /// <summary>EnterTargetingMode is waiting for this player's pick; OnPlayerTarget runs only for an awaited pick.</summary>
        public bool AwaitingTarget;

        public bool ExploresMinimap = true;
        /// <summary>SetPCLike/SetPCDislike: players this one dislikes.</summary>
        public readonly HashSet<uint> Dislikes = new();
        /// <summary>SetCommandingPlayer override (InvalidId = regular party control).</summary>
        public uint CommandingPlayer = GameObject.InvalidId;
        public bool IsPlayerDM;
    }

    public abstract partial class GameObject
    {
        /// <summary>OBJECT_UI_DISCOVERY_* mask (-1 = default).</summary>
        public int UiDiscoveryMask = -1;
        /// <summary>MOUSECURSOR_* override (-1 = default).</summary>
        public int MouseCursor = -1;
        public int TextBubbleMode;
        public string TextBubbleText = "";
        /// <summary>Local cassowary references (Get/SetLocalCassowary).</summary>
        public readonly Dictionary<string, Cassowary> LocalCassowaries = new(StringComparer.Ordinal);
    }
}

namespace OpenWinter.Game
{
    /// <summary>Server-wide state owned by the player package.</summary>
    public sealed partial class World
    {
        /// <summary>GAME_DIFFICULTY_* (the server's setting, or single player's option): 1 Easy by default, as the
        /// original's settings.tml has it and nwserver reads without -difficulty (parity suite difficulty_default).
        /// See docs/DIFFICULTY.md for what it changes.</summary>
        public int GameDifficulty = 1;
        /// <summary>GetGamePauseState bits: 1 = timestop, 2 = active (player) pause.</summary>
        public int PauseState;
        public bool IsPaused => PauseState != 0;
        /// <summary>StartNewModule target, for the server to act on.</summary>
        public string? PendingModule;
        /// <summary>Data for GetLastGuiEvent* and GetTargetingModeSelected* (see <see cref="PlayerEvents"/>).</summary>
        public GuiEvent? LastGuiEvent;
        public TargetSelection? LastTarget;
        /// <summary>SetTlkOverride strings by strref.</summary>
        public readonly Dictionary<int, string> TlkOverrides = new();

        /// <summary>A strref's text honouring SetTlkOverride.</summary>
        public string TlkStringOverridden(int strref) =>
            TlkOverrides.TryGetValue(strref, out var s) ? s : Resources.TlkString(unchecked((uint)strref));

        /// <summary>Connected player characters.</summary>
        public IEnumerable<Objects.Creature> ConnectedPCs =>
            AllObjects.OfType<Objects.Creature>().Where(c => c.IsPC && !c.IsDestroyed).OrderBy(c => c.Id);

        /// <summary>Sends a notify to every connected player.</summary>
        public void NotifyAllPlayers(string kind, object data)
        {
            foreach (var pc in ConnectedPCs.ToList()) Output?.Notify(kind, pc, data, pc);
        }
    }
}

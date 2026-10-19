namespace OpenWinter.Game.Objects
{
    public sealed partial class Creature
    {
        /// <summary>Perception lists, listening, associates and other social state (society package).</summary>
        public readonly OpenWinter.Game.Society.CreatureSociety Society = new();
    }
}

namespace OpenWinter.Game.Society
{
    using OpenWinter.Game.Objects;

    /// <summary>Per-creature state for perception, listening, associates and hostility tracking.</summary>
    public sealed class CreatureSociety
    {
        /// <summary>Creatures this creature currently sees (GetObjectSeen).</summary>
        public readonly HashSet<uint> Seen = new();
        /// <summary>Creatures this creature currently hears (GetObjectHeard).</summary>
        public readonly HashSet<uint> Heard = new();
        /// <summary>
        /// The perception list: every creature this one has noticed, in the order it was noticed. An entry stays while
        /// the creature is in the same area and within perception range, even with neither sense left (an invisible,
        /// silenced creature nearby); passes go through it newest first (nwserver sched_percept).
        /// </summary>
        public readonly List<uint> Listed = new();
        /// <summary>World time of the next perception update; negative until first scheduled.</summary>
        public double NextPerception = -1;
        /// <summary>An NPC's last perception update (or its appearance), and whether it has had one yet.</summary>
        public double PerceptionFrom = -1;
        public bool PerceivedOnce;
        /// <summary>The area whose players this creature's last perception pass saw (keeps it at AI_LEVEL_LOW until the next).</summary>
        public uint PlayersAtPass = GameObject.InvalidId;
        /// <summary>Area whose perception snapshot was evaluated; invalid until initialized.</summary>
        public uint PerceptionArea = GameObject.InvalidId;
        /// <summary>
        /// Creatures this one must check again at its next pass even if it sees and hears them (a creature entering
        /// stealth with Hide in Plain Sight).
        /// </summary>
        public readonly HashSet<uint> MustRecheck = new();
        /// <summary>Cached perception ranges (metres); NaN until resolved from ranges.2da.</summary>
        public float SightRange = float.NaN, HearingRange = float.NaN;
        /// <summary>The PerceptionRange row the cached ranges came from.</summary>
        public int RangesRow = -1;

        /// <summary>SetListening.</summary>
        public bool Listening;
        /// <summary>SetListenPattern entries in the order they were added (number, pattern).</summary>
        public readonly List<ListenPattern> ListenPatterns = new();
        /// <summary>SetAssociateListenPatterns was called: associate commands reach the OnConversation script.</summary>
        public bool AssociatePatterns;
        /// <summary>GetLastAssociateCommand (ASSOCIATE_COMMAND_*).</summary>
        public int LastAssociateCommand;

        /// <summary>Associates in the order they were added (henchmen, summons, familiars, companions, dominated).</summary>
        public readonly List<uint> Associates = new();
        /// <summary>Faction to return to when released from service (RemoveHenchman); null when not an associate.</summary>
        public int? OriginalFaction;

        /// <summary>GetLastHostileActor.</summary>
        public uint LastHostileActor = GameObject.InvalidId;
    }

    public sealed record ListenPattern(int Number, string Pattern);
}

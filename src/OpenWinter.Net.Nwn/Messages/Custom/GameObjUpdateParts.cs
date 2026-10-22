using System.Runtime.CompilerServices;
using OpenWinter.Net.Nwn.Wire;

namespace OpenWinter.Net.Nwn.Messages;

public static partial class GameObjUpdateAnimationMessages
{
    public const ushort AnimAttack = 9;
    static readonly ushort[] CastAnims = [17, 18, 19, 20, 61];
    static readonly ushort[] ConjureAnims = [15, 16];

    /// <summary>The animation an update starts, with the attacks of an attack animation or the spell of a cast.</summary>
    public sealed class AnimTypeData : INwnStruct
    {
        [NwnField("anim_type")]
        public ushort AnimType { get; set; }
        [NwnField("attacks")]
        public List<AttackRecord> Attacks { get; set; } = new();
        [NwnField("spell")]
        public CastAnimData? Spell { get; set; }
        [NwnField("cast_target")]
        public CastTargetData? CastTarget { get; set; }

        static bool IsCast(ushort anim) => Array.IndexOf(CastAnims, anim) >= 0;
        static bool IsConjureOrCast(ushort anim) => IsCast(anim) || Array.IndexOf(ConjureAnims, anim) >= 0;

        public void Read(NwnReader r)
        {
            AnimType = r.ReadWord();
            Attacks = new();
            Spell = null;
            CastTarget = null;
            if (AnimType == AnimAttack)
            {
                int n = r.ReadByte(2);
                for (int i = 0; i < n; i++) Attacks.Add(r.ReadStruct<AttackRecord>());
            }
            if (IsConjureOrCast(AnimType)) Spell = r.ReadStruct<CastAnimData>();
            if (IsCast(AnimType)) CastTarget = r.ReadStruct<CastTargetData>();
        }

        public void Write(NwnWriter w)
        {
            w.WriteWord(AnimType);
            if (AnimType == AnimAttack)
            {
                w.WriteByte((byte)Attacks.Count, 2);
                foreach (var a in Attacks) w.WriteStruct(a);
            }
            if (IsConjureOrCast(AnimType) && Spell != null) w.WriteStruct(Spell);
            if (IsCast(AnimType) && CastTarget != null) w.WriteStruct(CastTarget);
        }
    }

    /// <summary>A path: the client path number and its waypoints, with the slide factor when driven.</summary>
    public sealed class AnimPathData : INwnStruct
    {
        [NwnField("client_path_number")]
        public byte ClientPathNumber { get; set; }
        [NwnField("waypoints")]
        public List<(float, float)> Waypoints { get; set; } = new();
        [NwnField("drive_mode")]
        public bool DriveMode { get; set; }
        [NwnField("drive_slide_factor")]
        public float DriveSlideFactor { get; set; } = 1f;

        public void Read(NwnReader r)
        {
            ClientPathNumber = r.ReadByte();
            int n = r.ReadWord();
            DriveMode = false;
            DriveSlideFactor = 1f;
            Waypoints = new(n);
            if (n == 0) return;
            DriveMode = r.ReadBool();
            if (DriveMode) DriveSlideFactor = r.ReadFloat();
            for (int i = 0; i < n; i++) Waypoints.Add((r.ReadFloat(), r.ReadFloat()));
        }

        public void Write(NwnWriter w)
        {
            w.WriteByte(ClientPathNumber);
            w.WriteWord((ushort)Waypoints.Count);
            if (Waypoints.Count == 0) return;
            w.WriteBool(DriveMode);
            if (DriveMode) w.WriteFloat(DriveSlideFactor);
            foreach (var (x, y) in Waypoints)
            {
                w.WriteFloat(x);
                w.WriteFloat(y);
            }
        }
    }
}

public static partial class GameObjUpdateAppearanceMessages
{
    public const int ArmorPartCount = 19;

    /// <summary>The 19 armour or body part numbers; sent sparsely (index, value) when fewer than ten are set.</summary>
    public sealed class CreaturePartInfo : INwnStruct
    {
        [NwnField("parts")]
        public List<ushort> Parts { get; set; } = Enumerable.Repeat((ushort)0, ArmorPartCount).ToList();

        public void Read(NwnReader r)
        {
            int n = r.ReadByte();
            Parts = Enumerable.Repeat((ushort)0, ArmorPartCount).ToList();
            if (n == 0) return;
            if (n < 10)
            {
                for (int i = 0; i < n; i++)
                {
                    int index = r.ReadByte();
                    var value = r.ReadWord();
                    if (index < ArmorPartCount) Parts[index] = value;
                }
                return;
            }
            for (int i = 0; i < ArmorPartCount; i++) Parts[i] = r.ReadWord();
        }

        public void Write(NwnWriter w)
        {
            if (Parts.Count != ArmorPartCount) throw new NwnWireException($"part list must have {ArmorPartCount} entries");
            var set = Parts.Select((v, i) => (i, v)).Where(p => p.v != 0).ToList();
            if (set.Count == 0)
            {
                w.WriteByte(0);
                return;
            }
            if (set.Count < 10)
            {
                w.WriteByte((byte)set.Count);
                foreach (var (i, v) in set)
                {
                    w.WriteByte((byte)i);
                    w.WriteWord(v);
                }
                return;
            }
            w.WriteByte(ArmorPartCount);
            foreach (var v in Parts) w.WriteWord(v);
        }
    }
}

public static partial class GameObjUpdatePlayerinfoMessages
{
    /// <summary>
    /// Explored map tiles, sent one of three ways: every tile explored, or a list of explored tiles (explore_all);
    /// a list of changed tiles; or the raw explored bits (bitstream).
    /// </summary>
    public sealed class AutoMapUpdate : INwnStruct
    {
        [NwnField("explore_all")]
        public bool ExploreAll { get; set; }
        [NwnField("bitstream")]
        public bool Bitstream { get; set; }
        /// <summary>(x, y) tiles when <see cref="ExploreAll"/>; (explored, x, y) changes otherwise.</summary>
        [NwnField("tile_coords")]
        public List<ITuple> TileCoords { get; set; } = new();
        [NwnField("raw_bytes")]
        public byte[]? RawBytes { get; set; }

        public void Read(NwnReader r)
        {
            ExploreAll = r.ReadBool();
            Bitstream = r.ReadBool();
            TileCoords = new();
            RawBytes = null;
            if (!Bitstream)
            {
                var n = r.ReadDword();
                for (uint i = 0; i < n; i++)
                    TileCoords.Add(ExploreAll ? (r.ReadByte(), r.ReadByte()) : (r.ReadBool(), r.ReadByte(), r.ReadByte()));
                return;
            }
            ExploreAll = false;
            int count = r.ReadByte();
            RawBytes = new byte[count];
            for (int i = 0; i < count; i++) RawBytes[i] = r.ReadByte();
        }

        public void Write(NwnWriter w)
        {
            w.WriteBool(ExploreAll);
            w.WriteBool(Bitstream);
            if (!Bitstream)
            {
                w.WriteDword((uint)TileCoords.Count);
                foreach (var t in TileCoords)
                {
                    if (ExploreAll)
                    {
                        w.WriteByte((byte)t[0]!);
                        w.WriteByte((byte)t[1]!);
                    }
                    else
                    {
                        w.WriteBool((bool)t[0]!);
                        w.WriteByte((byte)t[1]!);
                        w.WriteByte((byte)t[2]!);
                    }
                }
                return;
            }
            var raw = RawBytes ?? [];
            w.WriteByte((byte)raw.Length);
            foreach (var b in raw) w.WriteByte(b);
        }
    }
}

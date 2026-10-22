using System.Buffers.Binary;
using System.Text;
using OpenWinter.Net.Nwn.Wire;

namespace OpenWinter.Net.Nwn.Session;

/// <summary>Who is connecting: a player or a DM.</summary>
public enum NwnConnectionType : byte
{
    Player = 0x10,
    DungeonMaster = 0x20,
}

/// <summary>Why the server refused a login.</summary>
public enum NwnAuthResult : byte
{
    Ok = 0,
    Unknown = 1,
    ServerVersionMismatch = 2,
    PasswordIncorrect = 3,
    ServerNotMultiplayer = 4,
    ServerFull = 5,
    PlayerNameInUse = 6,
    ReplyTimeout = 7,
    PlayerNameRefused = 8,
    CdKeyInUse = 9,
    Banned = 10,
    ExpansionPackWrong = 11,
    CdKeyUnauthorized = 12,
    DmConnectionRefused = 13,
    AdminConnectionRefused = 14,
    LanguageVersionMismatch = 15,
    MasterServerCdKeyInUse = 16,
    MasterServerNotResponding = 17,
    DefaultPlayerNameMissing = 18,
    CryptoHandshakeFailed = 19,
}

/// <summary>
/// The packets before the message window opens: WSNetLayer's hello (G...) and the login exchange (BN..). Plain
/// little-endian fields after a four-letter head; strings carry a one-, two- or four-byte length.
/// </summary>
public abstract record NwnPacket
{
    public abstract string Head { get; }

    public byte[] ToBytes()
    {
        var w = new PacketWriter();
        w.Bytes(Encoding.ASCII.GetBytes(Head));
        WriteBody(w);
        return w.ToArray();
    }

    protected abstract void WriteBody(PacketWriter w);

    /// <summary>The packet in a frame that starts with B or G; null when the head is unknown.</summary>
    public static NwnPacket? Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 4) return null;
        var head = Encoding.ASCII.GetString(frame[..4]);
        var r = new PacketReader(frame[4..].ToArray());
        return head switch
        {
            "GNHI" => new GnhiPacket(r.U8(), r.U64()),
            "BNES" => new BnesPacket(r.U16(), r.U8()),
            "BNER" => BnerPacket.Read(r),
            "BNXI" => BnxiPacket.Read(r),
            "BNXR" => BnxrPacket.Read(r),
            "BNDS" => new BndsPacket(r.U16()),
            "BNDR" => BndrPacket.Read(r),
            "BNCS" => BncsPacket.Read(r),
            "BNCR" => BncrPacket.Read(r),
            "BNVS" => BnvsPacket.Read(r),
            "BNVR" => BnvrPacket.Read(r),
            "BNLM" => new BnlmPacket(r.U16(), r.U8(), r.U32()),
            "BNLR" => new BnlrPacket(r.U16(), r.U8(), r.U32()),
            _ => null,
        };
    }
}

/// <summary>WSNetLayer's hello: its protocol version and capability bits.</summary>
public sealed record GnhiPacket(byte ProtocolVersion = 1, ulong Caps = 0) : NwnPacket
{
    public override string Head => "GNHI";
    protected override void WriteBody(PacketWriter w) => w.U8(ProtocolVersion).U64(Caps);
}

/// <summary>Asks for the server's session name.</summary>
public sealed record BnesPacket(ushort Port = 5121, byte EnumerateType = 0) : NwnPacket
{
    public override string Head => "BNES";
    protected override void WriteBody(PacketWriter w) => w.U16(Port).U8(EnumerateType);
}

/// <summary>The server's session name.</summary>
public sealed record BnerPacket(byte EnumerateType, string SessionName) : NwnPacket
{
    public override string Head => "BNER";
    protected override void WriteBody(PacketWriter w) => w.Bytes("U"u8).U16(5121).U8(EnumerateType).Str8(SessionName);

    internal static BnerPacket Read(PacketReader r)
    {
        if (r.U8() != (byte)'U') throw new NwnWireException("BNER doesn't start with U");
        r.U16();
        return new BnerPacket(r.U8(), r.Str8());
    }
}

/// <summary>Asks for the server's settings, with the player's name, key and build.</summary>
public sealed record BnxiPacket(string PlayerName, string PublicKey, string LegacyPublicKey, byte Language,
    NwnConnectionType ConnectionType, byte Platform, string BuildNumber, string BuildRevision, string BuildPostfix,
    string BuildGitCommit) : NwnPacket
{
    public override string Head => "BNXI";

    protected override void WriteBody(PacketWriter w) => w.U16(5121).Str8(PlayerName).Str8(PublicKey).Str8(LegacyPublicKey)
        .U8(Language).U8((byte)ConnectionType).U8(Platform).Str8(BuildNumber).Str8(BuildRevision).Str8(BuildPostfix)
        .Str8(BuildGitCommit);

    internal static BnxiPacket Read(PacketReader r)
    {
        r.U16();
        return new BnxiPacket(r.Str8(), r.Str8(), r.Str8(), r.U8(), (NwnConnectionType)r.U8(), r.U8(), r.Str8(),
            r.Str8(), r.Str8(), r.Str8());
    }
}

/// <summary>One extra NWSync manifest (a language's).</summary>
public sealed record NwnSyncManifest(byte Flags, byte LanguageId, string Hash);

/// <summary>The server's settings: levels, players, PvP, character rules, module and NWSync source.</summary>
public sealed record BnxrPacket : NwnPacket
{
    public override string Head => "BNXR";
    public bool Passworded { get; init; }
    public byte MinLevel { get; init; }
    public byte MaxLevel { get; init; } = 40;
    public byte CurrentPlayers { get; init; }
    public byte MaxPlayers { get; init; } = 0xFF;
    public bool AllowLocalVault { get; init; } = true;
    public byte PvpSetting { get; init; }
    public bool PauseAndPlay { get; init; }
    public bool OnePartyOnly { get; init; } = true;
    public bool Elc { get; init; } = true;
    public bool Ilr { get; init; }
    public byte ExpansionPack { get; init; }
    public string ModuleName { get; init; } = "";
    public string NwSyncUrl { get; init; } = "";
    public string NwSyncPrimaryManifest { get; init; } = "";
    public IReadOnlyList<NwnSyncManifest> NwSyncAdditionalManifests { get; init; } = [];
    public string AdvancedVersion { get; init; } = "";

    protected override void WriteBody(PacketWriter w)
    {
        w.U16(0).U8(0xFD).Bool(Passworded).U8(MinLevel).U8(MaxLevel).U8(CurrentPlayers).U8(MaxPlayers)
            .Bool(AllowLocalVault).U8(PvpSetting).Bool(PauseAndPlay).Bool(OnePartyOnly).Bool(Elc).Bool(Ilr)
            .U8(ExpansionPack).Str8(ModuleName);
        if (NwSyncUrl.Length == 0 && NwSyncPrimaryManifest.Length == 0) w.U8(2).U8(0);
        else
        {
            w.U8(2).U8(1).Str8(NwSyncUrl).Str8(NwSyncPrimaryManifest).U8((byte)NwSyncAdditionalManifests.Count);
            foreach (var m in NwSyncAdditionalManifests) w.U8(m.Flags).U8(m.LanguageId).Str8(m.Hash);
        }
        w.Str16(AdvancedVersion);
    }

    internal static BnxrPacket Read(PacketReader r)
    {
        r.U16();
        var version = r.U8();
        if (version != 0xFD) throw new NwnWireException($"BNXR version {version:X2}, expected FD");
        var p = new BnxrPacket
        {
            Passworded = r.U8() != 0,
            MinLevel = r.U8(),
            MaxLevel = r.U8(),
            CurrentPlayers = r.U8(),
            MaxPlayers = r.U8(),
            AllowLocalVault = r.U8() != 0,
            PvpSetting = r.U8(),
            PauseAndPlay = r.U8() != 0,
            OnePartyOnly = r.U8() != 0,
            Elc = r.U8() != 0,
            Ilr = r.U8() != 0,
            ExpansionPack = r.U8(),
            ModuleName = r.Str8(),
        };
        if (!r.AtEnd && r.U8() == 2 && !r.AtEnd && r.U8() != 0)
        {
            var url = r.Str8();
            var primary = r.Str8();
            var extra = new List<NwnSyncManifest>();
            int n = r.U8();
            for (int i = 0; i < n; i++) extra.Add(new NwnSyncManifest(r.U8(), r.U8(), r.Str8()));
            p = p with { NwSyncUrl = url, NwSyncPrimaryManifest = primary, NwSyncAdditionalManifests = extra };
        }
        if (!r.AtEnd) p = p with { AdvancedVersion = r.Str16() };
        return p;
    }
}

/// <summary>Asks for the server's description.</summary>
public sealed record BndsPacket(ushort Port = 5121) : NwnPacket
{
    public override string Head => "BNDS";
    protected override void WriteBody(PacketWriter w) => w.U16(Port);
}

/// <summary>The server's game details, module description and build.</summary>
public sealed record BndrPacket(string GameDetails, string ModuleDescription, string Build, short GameType = 0) : NwnPacket
{
    public override string Head => "BNDR";
    protected override void WriteBody(PacketWriter w) =>
        w.U16(5121).Str32(GameDetails).Str32(ModuleDescription).Str32(Build).I16(GameType);

    internal static BndrPacket Read(PacketReader r)
    {
        r.U16();
        return new BndrPacket(r.Str32(), r.Str32(), r.Str32(), r.I16());
    }
}

/// <summary>The connection request: player name, public key and the client's version.</summary>
public sealed record BncsPacket(NwnConnectionType ConnectionType, uint VersionNumber, byte Language, uint AppId,
    string PlayerName, string CdKey, string LegacyCdKey, byte Platform, string Psid, int PatchRevision,
    string GitCommit, int PatchPostfix) : NwnPacket
{
    public override string Head => "BNCS";

    protected override void WriteBody(PacketWriter w) => w.U16(5121).U8((byte)ConnectionType).U32(VersionNumber)
        .U16(0xFFFF).U8(Language).U32(AppId).Str8(PlayerName).Str8(CdKey).Str8(LegacyCdKey).U8(Platform).Str8(Psid)
        .I32(PatchRevision).Str8(GitCommit).I32(PatchPostfix);

    internal static BncsPacket Read(PacketReader r)
    {
        r.U16();
        var type = (NwnConnectionType)r.U8();
        var version = r.U32();
        r.U16();
        return new BncsPacket(type, version, r.U8(), r.U32(), r.Str8(), r.Str8(), r.Str8(), r.U8(), r.Str8(), r.I32(),
            r.Str8(), r.I32());
    }
}

/// <summary>The server's answer to a connection request: refused, or the challenges to answer.</summary>
public sealed record BncrPacket(char Status, NwnAuthResult Rejection, byte[] GamePasswordChallenge,
    byte[] CdKeyChallenge, byte[] MasterPasswordChallenge) : NwnPacket
{
    public const char Rejected = 'R', PasswordRequired = 'P', Accepted = 'V';
    public override string Head => "BNCR";

    protected override void WriteBody(PacketWriter w)
    {
        w.U16(0).U8((byte)Status);
        if (Status == Rejected)
        {
            w.U8((byte)Rejection);
            return;
        }
        if (Status == PasswordRequired) w.Bytes8(GamePasswordChallenge);
        w.Bytes8(CdKeyChallenge).Bytes8(MasterPasswordChallenge);
    }

    internal static BncrPacket Read(PacketReader r)
    {
        r.U16();
        var status = (char)r.U8();
        if (status == Rejected) return new BncrPacket(status, (NwnAuthResult)r.U8(), [], [], []);
        var game = status == PasswordRequired ? r.Bytes8() : [];
        return new BncrPacket(status, NwnAuthResult.Ok, game, r.Bytes8(), r.Bytes8());
    }
}

/// <summary>The answers to the server's challenges.</summary>
public sealed record BnvsPacket(byte[] CdKeyResponse, byte[] MasterPasswordResponse, byte[] GamePasswordResponse) : NwnPacket
{
    public override string Head => "BNVS";

    protected override void WriteBody(PacketWriter w)
    {
        w.U8(GamePasswordResponse.Length > 0 ? (byte)'P' : (byte)'V').U8(CdKeyResponse.Length > 0 ? (byte)1 : (byte)0);
        if (CdKeyResponse.Length > 0) w.Bytes8(CdKeyResponse);
        w.Bytes8(MasterPasswordResponse);
        if (GamePasswordResponse.Length > 0) w.Bytes8(GamePasswordResponse);
    }

    internal static BnvsPacket Read(PacketReader r)
    {
        var withPassword = r.U8() == (byte)'P';
        var key = r.U8() == 1 ? r.Bytes8() : [];
        var master = r.Bytes8();
        return new BnvsPacket(key, master, withPassword ? r.Bytes8() : []);
    }
}

/// <summary>The login's outcome: refused with a reason, or accepted with the server's version.</summary>
public sealed record BnvrPacket(bool Accepted, NwnAuthResult Rejection, uint AppId, uint Build, uint Patch, uint Postfix) : NwnPacket
{
    public override string Head => "BNVR";

    protected override void WriteBody(PacketWriter w)
    {
        if (!Accepted)
        {
            w.U8((byte)'R').U8((byte)Rejection);
            return;
        }
        w.U8((byte)'A').U32(AppId).U32(Build).U32(Patch).U32(Postfix);
    }

    internal static BnvrPacket Read(PacketReader r)
    {
        if (r.U8() == (byte)'R') return new BnvrPacket(false, (NwnAuthResult)r.U8(), 0, 0, 0, 0);
        var appId = r.U32();
        uint build = r.AtEnd ? 0 : r.U32(), patch = r.AtEnd ? 0 : r.U32(), postfix = r.AtEnd ? 0 : r.U32();
        return new BnvrPacket(true, NwnAuthResult.Ok, appId, build, patch, postfix);
    }
}

/// <summary>The server's keep-alive; answered with <see cref="BnlrPacket"/>.</summary>
public sealed record BnlmPacket(ushort Port, byte MessageNo, uint SessionId) : NwnPacket
{
    public override string Head => "BNLM";
    protected override void WriteBody(PacketWriter w) => w.U16(Port).U8(MessageNo).U32(SessionId);
}

/// <summary>The answer to a keep-alive.</summary>
public sealed record BnlrPacket(ushort Port, byte MessageNo, uint SessionId) : NwnPacket
{
    public override string Head => "BNLR";
    protected override void WriteBody(PacketWriter w) => w.U16(Port).U8(MessageNo).U32(SessionId);
}

/// <summary>Little-endian packet fields; strings in the game's code page.</summary>
public sealed class PacketWriter
{
    readonly MemoryStream _ms = new();
    static Encoding Text => OpenWinter.Aurora.Text.Windows1252;

    public PacketWriter Bytes(ReadOnlySpan<byte> b)
    {
        _ms.Write(b);
        return this;
    }

    public PacketWriter U8(byte v) => Bytes([v]);
    public PacketWriter Bool(bool v) => U8(v ? (byte)1 : (byte)0);

    public PacketWriter U16(ushort v)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, v);
        return Bytes(b);
    }

    public PacketWriter I16(short v) => U16(unchecked((ushort)v));

    public PacketWriter U32(uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        return Bytes(b);
    }

    public PacketWriter I32(int v) => U32(unchecked((uint)v));

    public PacketWriter U64(ulong v)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(b, v);
        return Bytes(b);
    }

    public PacketWriter Bytes8(byte[] v) => U8((byte)v.Length).Bytes(v);
    public PacketWriter Str8(string v) => Bytes8(Text.GetBytes(v ?? ""));
    public PacketWriter Str16(string v)
    {
        var b = Text.GetBytes(v ?? "");
        return U16((ushort)b.Length).Bytes(b);
    }

    public PacketWriter Str32(string v)
    {
        var b = Text.GetBytes(v ?? "");
        return U32((uint)b.Length).Bytes(b);
    }

    public byte[] ToArray() => _ms.ToArray();
}

/// <summary>Reads <see cref="PacketWriter"/>'s fields. Strings lose surrounding NULs, as the original pads some.</summary>
public sealed class PacketReader(byte[] data)
{
    static Encoding Text => OpenWinter.Aurora.Text.Windows1252;
    int _pos;

    public bool AtEnd => _pos >= data.Length;

    ReadOnlySpan<byte> Take(int n)
    {
        if (_pos + n > data.Length) throw new NwnWireException($"packet truncated at {_pos}");
        var s = data.AsSpan(_pos, n);
        _pos += n;
        return s;
    }

    public byte U8() => Take(1)[0];
    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    public short I16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    public int I32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    public ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
    public byte[] Bytes8() => Take(U8()).ToArray();
    public string Str8() => Text.GetString(Take(U8())).Trim('\0');
    public string Str16() => Text.GetString(Take(U16())).Trim('\0');
    public string Str32() => Text.GetString(Take((int)U32())).Trim('\0');
}

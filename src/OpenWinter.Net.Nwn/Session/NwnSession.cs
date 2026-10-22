using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;

namespace OpenWinter.Net.Nwn.Session;

/// <summary>A game version as the protocol states it: build.patch.postfix (8193.37.17).</summary>
public readonly record struct NwnVersion(uint Build, uint Patch, uint Postfix)
{
    /// <summary>The only version the message definitions describe.</summary>
    public static readonly NwnVersion Supported = new(8193, 37, 17);

    public bool Satisfies(int build, int patch, int postfix) =>
        Build == build && (Patch > patch || (Patch == patch && Postfix >= postfix));

    public override string ToString() => $"{Build}.{Patch}.{Postfix}";
}

/// <summary>How to log in: the player's name and CD key, and the server's password if it has one.</summary>
public sealed record NwnLogin(string PlayerName, NwnCdKey Key, string Password = "",
    NwnConnectionType ConnectionType = NwnConnectionType.Player, string DmPassword = "", byte Language = 0);

/// <summary>Game data for decoding, from the client's resources, plus the server's version once it is known.</summary>
public sealed class NwnSessionContext(Func<string, int> rowCount, Func<string, int, string, string> cell) : INwnContext
{
    public NwnVersion ServerVersion { get; set; } = NwnVersion.Supported;
    public int TwoDARowCount(string table) => rowCount(table);
    public string TwoDA(string table, int row, string column) => cell(table, row, column);
    public bool Satisfies(int build, int patch, int postfix) => ServerVersion.Satisfies(build, patch, postfix);
    public Dictionary<uint, bool> TriggerIsTrap { get; } = [];
}

public enum NwnSessionState
{
    Connecting,
    LoggingIn,
    Ready,
    Failed,
}

/// <summary>A frame that couldn't be decoded, kept for diagnostics.</summary>
public sealed record NwnDecodeError(byte[] Frame, string Error);

/// <summary>
/// One connection to an NWN:EE server over WSNetLayer: the login exchange, then messages in both directions.
/// Keep-alives and echo requests are answered here. Driven by <see cref="Pump"/> on the owner's thread.
/// </summary>
public sealed class NwnSession(INwnTransport transport, NwnLogin login, NwnSessionContext context)
{
    const byte ServerToClient = (byte)'P';
    const byte ClientToServer = (byte)'p';

    string _awaiting = "";
    readonly uint _appId = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public NwnSessionState State { get; private set; } = NwnSessionState.Connecting;
    public string? Failure { get; private set; }
    public NwnAuthResult Rejection { get; private set; }
    public NwnSessionContext Context => context;
    public BnerPacket? SessionInfo { get; private set; }
    public BnxrPacket? Settings { get; private set; }
    public BndrPacket? Description { get; private set; }
    public NwnVersion ServerVersion { get; private set; }
    public List<NwnDecodeError> DecodeErrors { get; } = [];
    /// <summary>Every frame received and sent, for recordings; null unless requested.</summary>
    public Action<bool, byte[]>? FrameObserver { get; set; }

    /// <summary>Advances the login and decodes what has arrived; decoded messages go to <paramref name="received"/>.</summary>
    public void Pump(ICollection<NwnMessage> received)
    {
        if (State == NwnSessionState.Failed) return;
        if (State == NwnSessionState.Connecting)
        {
            if (transport.IsOpen) Begin();
            else if (transport.CloseReason is { } why) Fail(why);
            return;
        }
        while (transport.TryReceive(out var frame))
        {
            FrameObserver?.Invoke(false, frame);
            if (frame.Length == 0) continue;
            if (frame[0] == ServerToClient) HandleMessage(frame, received);
            else if (frame[0] is (byte)'B' or (byte)'G') HandlePacket(frame);
            else DecodeErrors.Add(new NwnDecodeError(frame, $"unknown frame type {frame[0]}"));
            if (State == NwnSessionState.Failed) return;
        }
        if (!transport.IsOpen && transport.CloseReason is { } reason) Fail(reason);
    }

    public void Send(NwnMessage message)
    {
        if (State != NwnSessionState.Ready) throw new InvalidOperationException("not logged in");
        var w = new NwnWsWriter(context);
        message.Write(w);
        SendFrame([ClientToServer, message.Major, message.Minor, .. w.ToArray()]);
    }

    public void Close(string reason) => transport.Close(reason);

    void SendFrame(byte[] frame)
    {
        FrameObserver?.Invoke(true, frame);
        transport.Send(frame);
    }

    void SendPacket(NwnPacket packet, string awaiting)
    {
        _awaiting = awaiting;
        SendFrame(packet.ToBytes());
    }

    void Begin()
    {
        State = NwnSessionState.LoggingIn;
        SendPacket(new GnhiPacket(), "GNHI");
    }

    void Fail(string reason)
    {
        if (State == NwnSessionState.Failed) return;
        State = NwnSessionState.Failed;
        Failure = reason;
        transport.Close(reason);
    }

    void HandlePacket(byte[] frame)
    {
        NwnPacket? packet;
        try
        {
            packet = NwnPacket.Parse(frame);
        }
        catch (NwnWireException e)
        {
            DecodeErrors.Add(new NwnDecodeError(frame, e.Message));
            if (State == NwnSessionState.LoggingIn) Fail($"malformed login packet: {e.Message}");
            return;
        }
        if (packet is BnlmPacket keepAlive)
        {
            SendFrame(new BnlrPacket(keepAlive.Port, keepAlive.MessageNo, keepAlive.SessionId).ToBytes());
            return;
        }
        if (State != NwnSessionState.LoggingIn || packet == null || packet.Head != _awaiting) return;
        Advance(packet);
    }

    void Advance(NwnPacket packet)
    {
        var version = NwnVersion.Supported;
        switch (packet)
        {
            case GnhiPacket hello:
                if (hello.ProtocolVersion != 1 || hello.Caps != 0)
                {
                    Fail($"the server's WSNetLayer protocol {hello.ProtocolVersion} (caps {hello.Caps:X}) isn't supported");
                    return;
                }
                SendPacket(new BnesPacket(), "BNER");
                break;
            case BnerPacket info:
                SessionInfo = info;
                SendPacket(new BnxiPacket(login.PlayerName, login.Key.PublicKey, "", login.Language, login.ConnectionType,
                    PlatformId(), version.Build.ToString(), version.Patch.ToString(), version.Postfix.ToString(), ""), "BNXR");
                break;
            case BnxrPacket settings:
                Settings = settings;
                SendPacket(new BndsPacket(), "BNDR");
                break;
            case BndrPacket description:
                Description = description;
                SendPacket(new BncsPacket(login.ConnectionType, version.Build, login.Language, _appId, login.PlayerName,
                    login.Key.PublicKey, "", PlatformId(), "", (int)version.Patch, "", (int)version.Postfix), "BNCR");
                break;
            case BncrPacket challenge:
                Answer(challenge);
                break;
            case BnvrPacket result:
                Finish(result);
                break;
        }
    }

    void Answer(BncrPacket challenge)
    {
        if (challenge.Status == BncrPacket.Rejected)
        {
            Rejection = challenge.Rejection;
            Fail($"the server refused the connection: {challenge.Rejection}");
            return;
        }
        byte[] gamePassword = [];
        if (challenge.Status == BncrPacket.PasswordRequired)
        {
            if (login.Password.Length == 0)
            {
                Rejection = NwnAuthResult.PasswordIncorrect;
                Fail("the server needs a password");
                return;
            }
            gamePassword = NwnCdKey.Md5Hex([.. OpenWinter.Aurora.Text.Windows1252.GetBytes(login.Password), .. challenge.GamePasswordChallenge]);
        }
        var master = NwnCdKey.Md5Hex([.. OpenWinter.Aurora.Text.Windows1252.GetBytes(login.DmPassword), .. challenge.MasterPasswordChallenge]);
        SendPacket(new BnvsPacket(login.Key.ChallengeResponse(challenge.CdKeyChallenge), master, gamePassword), "BNVR");
    }

    void Finish(BnvrPacket result)
    {
        if (!result.Accepted)
        {
            Rejection = result.Rejection;
            Fail($"the server refused the login: {result.Rejection}");
            return;
        }
        ServerVersion = new NwnVersion(result.Build, result.Patch, result.Postfix);
        context.ServerVersion = ServerVersion;
        if (ServerVersion != NwnVersion.Supported)
        {
            Rejection = NwnAuthResult.ServerVersionMismatch;
            Fail($"the server runs NWN:EE {ServerVersion}; this client speaks {NwnVersion.Supported}");
            return;
        }
        State = NwnSessionState.Ready;
        _awaiting = "";
    }

    void HandleMessage(byte[] frame, ICollection<NwnMessage> received)
    {
        if (State != NwnSessionState.Ready || frame.Length < 3)
        {
            DecodeErrors.Add(new NwnDecodeError(frame, "message before login completed"));
            return;
        }
        var message = NwnMessageRegistry.Create(frame[1], frame[2], NwnDirection.ServerToClient);
        if (message == null)
        {
            DecodeErrors.Add(new NwnDecodeError(frame, $"unknown message {frame[1]:X2}.{frame[2]:X2}"));
            return;
        }
        var reader = new NwnWsReader(frame.AsMemory(3), context);
        try
        {
            message.Read(reader);
        }
        catch (Exception e) when (e is NwnWireException or InvalidCastException or FormatException or OverflowException)
        {
            DecodeErrors.Add(new NwnDecodeError(frame, $"{message.GetType().Name} at byte {reader.Position}: {e.Message}"));
            return;
        }
        if (!reader.AtEnd)
            DecodeErrors.Add(new NwnDecodeError(frame, $"{message.GetType().Name}: {reader.Length - reader.Position} bytes unread"));
        if (message is DeviceMessages.DeviceEchoRequest echo) Send(new DeviceMessages.DeviceEchoResponse { Data = echo.Data });
        received.Add(message);
    }

    static byte PlatformId() => (OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS(),
        System.Runtime.InteropServices.RuntimeInformation.OSArchitecture) switch
    {
        (true, _, _, System.Runtime.InteropServices.Architecture.X64) => 2,
        (true, _, _, _) => 1,
        (_, true, _, System.Runtime.InteropServices.Architecture.X64) => 11,
        (_, true, _, System.Runtime.InteropServices.Architecture.Arm64) => 13,
        (_, _, true, System.Runtime.InteropServices.Architecture.Arm64) => 22,
        (_, _, true, _) => 21,
        _ => 0,
    };
}

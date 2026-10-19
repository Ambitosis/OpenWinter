using System.Reflection;
using LiteNetLib.Utils;

namespace OpenWinter.Net;

/// <summary>Stable, bounded transport handshake, independent of the gameplay message layouts.</summary>
public sealed record NetworkVersion(int Protocol, string Engine, string Build)
{
    // Keep this envelope readable when the gameplay protocol changes. Legacy openwinter/1 peers fail closed.
    public const string HandshakeKey = "openwinter/handshake/1";
    public const int CurrentProtocol = 3;
    public static NetworkVersion Current { get; } = new(CurrentProtocol, Metadata("OpenWinterVersion"), Metadata("OpenWinterBuild"));

    static string Metadata(string key) => typeof(NetworkVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == key)?.Value ?? "";

    public bool Valid => Protocol > 0 && Engine.Length is > 0 and <= 32 && Build.Length == 64 && Build.All(char.IsAsciiHexDigit);
    public string Display => $"{Engine} (protocol {Protocol}, build {Build[..Math.Min(Build.Length, 12)]})";

    public string? Mismatch(NetworkVersion server)
    {
        if (Valid && server.Valid && this == server) return null;
        string kind = Protocol != server.Protocol ? "Network protocol mismatch." : "Engine build mismatch.";
        return $"{kind}\nYour client: {Display}\nServer: {server.Display}\nInstall the same OpenWinter build as the server, then reconnect.";
    }

    internal NetDataWriter Write()
    {
        var w = new NetDataWriter();
        w.Put(HandshakeKey);
        w.Put(Protocol);
        w.Put(Engine);
        w.Put(Build);
        return w;
    }

    internal static bool TryRead(NetDataReader reader, out NetworkVersion version)
    {
        version = new(0, "unknown", "");
        // Never feed malformed or legacy handshakes to gameplay deserializers.
        if (reader.AvailableBytes > 256) return false;
        if (!reader.TryGetString(out var key) || key != HandshakeKey || !reader.TryGetInt(out var protocol)
            || !reader.TryGetString(out var engine) || !reader.TryGetString(out var build) || reader.AvailableBytes != 0) return false;
        version = new(protocol, engine, build);
        return version.Valid;
    }
}

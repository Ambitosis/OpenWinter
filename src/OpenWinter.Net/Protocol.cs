using System.Numerics;
using System.Text;

namespace OpenWinter.Net;

public enum MessageType : byte
{
    // client → server
    Hello = 1,
    MoveInput = 2,
    ChatSend = 3,
    ActionRequest = 4,
    DialogReply = 5,
    ClientRequest = 6,
    CharacterChoice = 7,
    ContentRequest = 8,
    DriveInput = 9,

    // server → client
    Welcome = 64,
    LoadArea = 65,
    Spawn = 66,
    Update = 67,
    Despawn = 68,
    Chat = 69,
    PlayAnimation = 70,
    ControlObject = 71,
    Notify = 72,
    Dialog = 73,
    Time = 74,
    Appearance = 75,
    CharacterList = 76,
    ContentChunk = 77,
}

/// <summary>Binary message body writer (little-endian, length-prefixed UTF-8 strings).</summary>
public sealed class MessageWriter
{
    readonly MemoryStream _ms = new();
    readonly BinaryWriter _w;

    public MessageWriter(MessageType type)
    {
        _w = new BinaryWriter(_ms, Encoding.UTF8, leaveOpen: true);
        _w.Write((byte)type);
    }

    public MessageWriter U8(byte v) { _w.Write(v); return this; }
    public MessageWriter Bool(bool v) { _w.Write(v); return this; }
    public MessageWriter U16(ushort v) { _w.Write(v); return this; }
    public MessageWriter I32(int v) { _w.Write(v); return this; }
    public MessageWriter I64(long v) { _w.Write(v); return this; }
    public MessageWriter U32(uint v) { _w.Write(v); return this; }
    public MessageWriter F32(float v) { _w.Write(v); return this; }
    public MessageWriter Str(string v) { _w.Write(v ?? ""); return this; }
    public MessageWriter V3(Vector3 v) { _w.Write(v.X); _w.Write(v.Y); _w.Write(v.Z); return this; }
    public MessageWriter Bytes(byte[] v) { _w.Write(v.Length); _w.Write(v); return this; }
    public MessageWriter U16s(ushort[] v) { _w.Write(v.Length); foreach (var x in v) _w.Write(x); return this; }

    public byte[] ToArray()
    {
        _w.Flush();
        return _ms.ToArray();
    }
}

public sealed class MessageReader
{
    readonly BinaryReader _r;
    public MessageType Type { get; }
    /// <summary>Bytes not yet read (lets newer fields be optional).</summary>
    public long Remaining => _r.BaseStream.Length - _r.BaseStream.Position;

    public MessageReader(byte[] data)
    {
        _r = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
        Type = (MessageType)_r.ReadByte();
    }

    public byte U8() => _r.ReadByte();
    public bool Bool() => _r.ReadBoolean();
    public ushort U16() => _r.ReadUInt16();
    public int I32() => _r.ReadInt32();
    public long I64() => _r.ReadInt64();
    public uint U32() => _r.ReadUInt32();
    public float F32() => _r.ReadSingle();
    public string Str() => _r.ReadString();
    public Vector3 V3() => new(_r.ReadSingle(), _r.ReadSingle(), _r.ReadSingle());
    public byte[] Bytes() => _r.ReadBytes(_r.ReadInt32());
    public ushort[] U16s()
    {
        var v = new ushort[_r.ReadInt32()];
        for (int i = 0; i < v.Length; i++) v[i] = _r.ReadUInt16();
        return v;
    }
}

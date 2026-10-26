using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace OpenWinter.Net.Nwn.Wire;

/// <summary>Reads message fields. The field order is the protocol's; how each field is encoded is the transport's.</summary>
public abstract class NwnReader(INwnContext context)
{
    public INwnContext Context { get; } = context;
    public abstract int Position { get; }
    public abstract int Length { get; }
    public bool AtEnd => Position >= Length;

    public abstract bool ReadBool();
    /// <summary>Any integer field; <paramref name="bits"/> is the width a bit-packed transport would use.</summary>
    public abstract long ReadInteger(NwnWireType type, int bits = 0);
    public abstract float ReadFloat(NwnFloatEncoding encoding = default);
    public abstract double ReadDouble();
    public abstract string ReadResRef();
    public abstract string ReadString();
    public abstract byte[] ReadVoid();
    public abstract string ReadJson();
    public abstract NwnLocString ReadLocString();

    public byte ReadByte(int bits = 0) => (byte)ReadInteger(NwnWireType.Byte, bits);
    public sbyte ReadChar(int bits = 0) => (sbyte)ReadInteger(NwnWireType.Char, bits);
    public ushort ReadWord(int bits = 0) => (ushort)ReadInteger(NwnWireType.Word, bits);
    public short ReadShort(int bits = 0) => (short)ReadInteger(NwnWireType.Short, bits);
    public uint ReadDword(int bits = 0) => (uint)ReadInteger(NwnWireType.Dword, bits);
    public int ReadInt(int bits = 0) => (int)ReadInteger(NwnWireType.Int, bits);
    public ulong ReadDword64(int bits = 0) => unchecked((ulong)ReadInteger(NwnWireType.Dword64, bits));
    public long ReadInt64(int bits = 0) => ReadInteger(NwnWireType.Int64, bits);
    public uint ReadObjectId() => (uint)ReadInteger(NwnWireType.ObjectId);

    public T ReadStruct<T>() where T : INwnStruct, new()
    {
        var value = new T();
        value.Read(this);
        return value;
    }
}

/// <summary>Writes message fields; the counterpart of <see cref="NwnReader"/>.</summary>
public abstract class NwnWriter(INwnContext context)
{
    public INwnContext Context { get; } = context;

    public abstract void WriteBool(bool value);
    public abstract void WriteInteger(NwnWireType type, long value, int bits = 0);
    public abstract void WriteFloat(float value, NwnFloatEncoding encoding = default);
    public abstract void WriteDouble(double value);
    public abstract void WriteResRef(string value);
    public abstract void WriteString(string value);
    public abstract void WriteVoid(byte[] value);
    public abstract void WriteJson(string value);
    public abstract void WriteLocString(NwnLocString value);
    public abstract byte[] ToArray();

    public void WriteByte(byte value, int bits = 0) => WriteInteger(NwnWireType.Byte, value, bits);
    public void WriteChar(sbyte value, int bits = 0) => WriteInteger(NwnWireType.Char, value, bits);
    public void WriteWord(ushort value, int bits = 0) => WriteInteger(NwnWireType.Word, value, bits);
    public void WriteShort(short value, int bits = 0) => WriteInteger(NwnWireType.Short, value, bits);
    public void WriteDword(uint value, int bits = 0) => WriteInteger(NwnWireType.Dword, value, bits);
    public void WriteInt(int value, int bits = 0) => WriteInteger(NwnWireType.Int, value, bits);
    public void WriteDword64(ulong value, int bits = 0) => WriteInteger(NwnWireType.Dword64, unchecked((long)value), bits);
    public void WriteInt64(long value, int bits = 0) => WriteInteger(NwnWireType.Int64, value, bits);
    public void WriteObjectId(uint value) => WriteInteger(NwnWireType.ObjectId, value);
    public void WriteStruct(INwnStruct value) => value.Write(this);
}

/// <summary>
/// The WSNetLayer encoding: each field is its type byte followed by the value, little-endian; floats are full width;
/// strings and blobs carry a length (one byte for resrefs, four otherwise).
/// </summary>
public sealed class NwnWsReader(ReadOnlyMemory<byte> data, INwnContext context) : NwnReader(context)
{
    static Encoding Text => OpenWinter.Aurora.Text.Windows1252;
    int _pos;

    public override int Position => _pos;
    public override int Length => data.Length;
    public ReadOnlyMemory<byte> Remaining => data[_pos..];
    public void Skip(int count) => Take(count);

    ReadOnlySpan<byte> Take(int n)
    {
        if (n < 0 || _pos + n > data.Length)
            throw new NwnWireException($"message truncated: need {n} bytes at {_pos}, have {data.Length - _pos}");
        var span = data.Span.Slice(_pos, n);
        _pos += n;
        return span;
    }

    void Expect(NwnWireType type)
    {
        var got = Take(1)[0];
        if (got != (byte)type) throw new NwnWireException($"expected {type} at {_pos - 1}, found type byte {got}");
    }

    uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    public override bool ReadBool()
    {
        Expect(NwnWireType.Bool);
        return (Take(1)[0] & 1) != 0;
    }

    public override long ReadInteger(NwnWireType type, int bits = 0)
    {
        Expect(type);
        return type switch
        {
            NwnWireType.Bool => Take(1)[0] & 1,
            NwnWireType.Byte => Take(1)[0],
            NwnWireType.Char => (sbyte)Take(1)[0],
            NwnWireType.Word => BinaryPrimitives.ReadUInt16LittleEndian(Take(2)),
            NwnWireType.Short => BinaryPrimitives.ReadInt16LittleEndian(Take(2)),
            NwnWireType.Dword or NwnWireType.ObjectId => U32(),
            NwnWireType.Int => BinaryPrimitives.ReadInt32LittleEndian(Take(4)),
            NwnWireType.Dword64 or NwnWireType.Int64 => BinaryPrimitives.ReadInt64LittleEndian(Take(8)),
            _ => throw new NwnWireException($"{type} is not an integer field"),
        };
    }

    public override float ReadFloat(NwnFloatEncoding encoding = default)
    {
        Expect(NwnWireType.Float);
        return BinaryPrimitives.ReadSingleLittleEndian(Take(4));
    }

    public override double ReadDouble()
    {
        Expect(NwnWireType.Double);
        return BinaryPrimitives.ReadDoubleLittleEndian(Take(8));
    }

    public override string ReadResRef()
    {
        Expect(NwnWireType.ResRef);
        return Text.GetString(Take(Take(1)[0]));
    }

    public override string ReadString()
    {
        Expect(NwnWireType.String);
        return Text.GetString(Take((int)U32()));
    }

    public override byte[] ReadVoid()
    {
        Expect(NwnWireType.VoidPtr);
        return Take((int)U32()).ToArray();
    }

    // JSON is its type byte, a length and the text, with no string type byte of its own.
    public override string ReadJson()
    {
        Expect(NwnWireType.Json);
        return Text.GetString(Take((int)U32()));
    }

    public override NwnLocString ReadLocString()
    {
        Expect(NwnWireType.LocString);
        if (Take(1)[0] != 0)
        {
            var gender = Take(1)[0];
            return NwnLocString.FromStrRef(U32(), gender);
        }
        return NwnLocString.FromText(Text.GetString(Take((int)U32())));
    }
}

/// <summary>Writes the WSNetLayer encoding; see <see cref="NwnWsReader"/>.</summary>
public sealed class NwnWsWriter(INwnContext context) : NwnWriter(context)
{
    static Encoding Text => OpenWinter.Aurora.Text.Windows1252;
    readonly ArrayBufferWriter<byte> _buf = new();

    void Type(NwnWireType type) => _buf.Write([(byte)type]);

    void U32(uint value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        _buf.Write(b);
    }

    public override void WriteBool(bool value)
    {
        Type(NwnWireType.Bool);
        _buf.Write([value ? (byte)1 : (byte)0]);
    }

    public override void WriteInteger(NwnWireType type, long value, int bits = 0)
    {
        Type(type);
        Span<byte> b = stackalloc byte[8];
        switch (type)
        {
            case NwnWireType.Bool:
            case NwnWireType.Byte:
            case NwnWireType.Char:
                _buf.Write([unchecked((byte)value)]);
                break;
            case NwnWireType.Word:
            case NwnWireType.Short:
                BinaryPrimitives.WriteUInt16LittleEndian(b, unchecked((ushort)value));
                _buf.Write(b[..2]);
                break;
            case NwnWireType.Dword:
            case NwnWireType.ObjectId:
            case NwnWireType.Int:
                BinaryPrimitives.WriteUInt32LittleEndian(b, unchecked((uint)value));
                _buf.Write(b[..4]);
                break;
            case NwnWireType.Dword64:
            case NwnWireType.Int64:
                BinaryPrimitives.WriteInt64LittleEndian(b, value);
                _buf.Write(b);
                break;
            default:
                throw new NwnWireException($"{type} is not an integer field");
        }
    }

    public override void WriteFloat(float value, NwnFloatEncoding encoding = default)
    {
        Type(NwnWireType.Float);
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(b, value);
        _buf.Write(b);
    }

    public override void WriteDouble(double value)
    {
        Type(NwnWireType.Double);
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(b, value);
        _buf.Write(b);
    }

    public override void WriteResRef(string value)
    {
        var bytes = Text.GetBytes(value ?? "");
        if (bytes.Length > 16) throw new NwnWireException($"resref longer than 16 characters: {value}");
        Type(NwnWireType.ResRef);
        _buf.Write([(byte)bytes.Length]);
        _buf.Write(bytes);
    }

    public override void WriteString(string value)
    {
        var bytes = Text.GetBytes(value ?? "");
        Type(NwnWireType.String);
        U32((uint)bytes.Length);
        _buf.Write(bytes);
    }

    public override void WriteVoid(byte[] value)
    {
        Type(NwnWireType.VoidPtr);
        U32((uint)value.Length);
        _buf.Write(value);
    }

    public override void WriteJson(string value)
    {
        var bytes = Text.GetBytes(value ?? "");
        Type(NwnWireType.Json);
        U32((uint)bytes.Length);
        _buf.Write(bytes);
    }

    // Literal text is written as a string field after the flag but read back without the string's type byte; this
    // follows nwn-message.py until it is measured against a server.
    public override void WriteLocString(NwnLocString value)
    {
        Type(NwnWireType.LocString);
        if (value.IsStrRef)
        {
            _buf.Write([(byte)1]);
            _buf.Write([value.Gender]);
            U32(value.StrRef);
            return;
        }
        _buf.Write([(byte)0]);
        WriteString(value.Text);
    }

    public override byte[] ToArray() => _buf.WrittenSpan.ToArray();
}

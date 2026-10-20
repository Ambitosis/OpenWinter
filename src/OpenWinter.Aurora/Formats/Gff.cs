using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace OpenWinter.Aurora.Formats;

public enum GffType : uint
{
    Byte, Char, Word, Short, DWord, Int, DWord64, Int64, Float, Double,
    ExoString, ResRef, LocString, Void, Struct, List, Orientation, Vector,
}

/// <summary>Localised string: a talk-table reference and/or embedded strings keyed by language*2+gender.</summary>
public sealed class LocString
{
    public int StrRef = -1;
    public readonly SortedDictionary<int, string> Strings = new();

    public LocString() { }
    public LocString(string text) { Strings[0] = text; }

    public string FirstString => Strings.Count > 0 ? Strings.First().Value : "";

    /// <summary>Embedded text for the language if present, else the talk table, else any embedded text.</summary>
    public string Resolve(TalkTable? tlk, int language = 0)
    {
        if (Strings.TryGetValue(language * 2, out var s) && s.Length > 0) return s;
        if (StrRef >= 0 && tlk != null)
        {
            var t = tlk.Get((uint)StrRef);
            if (t.Length > 0) return t;
        }
        return FirstString;
    }

    public LocString Clone()
    {
        var c = new LocString { StrRef = StrRef };
        foreach (var kv in Strings) c.Strings[kv.Key] = kv.Value;
        return c;
    }
}

public sealed class GffField
{
    public string Label;
    public GffType Type;
    public object Value;

    public GffField(string label, GffType type, object value)
    {
        Label = label;
        Type = type;
        Value = value;
    }
}

/// <summary>A GFF struct: an ordered set of labelled, typed fields.</summary>
public sealed class GffStruct
{
    public uint StructType;
    public readonly List<GffField> Fields = new();

    public GffStruct(uint structType = 0) { StructType = structType; }

    public GffField? Field(string label)
    {
        foreach (var f in Fields)
            if (string.Equals(f.Label, label, StringComparison.Ordinal)) return f;
        foreach (var f in Fields)
            if (string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase)) return f;
        return null;
    }

    public bool Has(string label) => Field(label) != null;
    public object? this[string label] => Field(label)?.Value;

    public long GetInt(string label, long fallback = 0) => Field(label)?.Value switch
    {
        byte v => v,
        sbyte v => v,
        ushort v => v,
        short v => v,
        uint v => v,
        int v => v,
        ulong v => (long)v,
        long v => v,
        float v => (long)v,
        double v => (long)v,
        _ => fallback,
    };

    public float GetFloat(string label, float fallback = 0f) => Field(label)?.Value switch
    {
        float v => v,
        double v => (float)v,
        byte v => v,
        short v => v,
        int v => v,
        uint v => v,
        ushort v => v,
        _ => fallback,
    };

    public string GetString(string label, string fallback = "") => Field(label)?.Value switch
    {
        string s => s,
        LocString l => l.FirstString,
        _ => fallback,
    };

    public LocString GetLocString(string label) => Field(label)?.Value switch
    {
        LocString l => l,
        string s => new LocString(s),
        _ => new LocString(),
    };

    public List<GffStruct> GetList(string label) => Field(label)?.Value as List<GffStruct> ?? new List<GffStruct>();
    public GffStruct? GetStruct(string label) => Field(label)?.Value as GffStruct;
    public Vector3 GetVector(string label) => Field(label)?.Value is Vector3 v ? v : Vector3.Zero;
    public byte[] GetBytes(string label) => Field(label)?.Value as byte[] ?? Array.Empty<byte>();

    public GffStruct Set(string label, GffType type, object value)
    {
        var f = Field(label);
        if (f == null) Fields.Add(new GffField(label, type, value));
        else { f.Type = type; f.Value = value; }
        return this;
    }

    public GffStruct SetByte(string label, int v) => Set(label, GffType.Byte, (byte)v);
    public GffStruct SetWord(string label, int v) => Set(label, GffType.Word, (ushort)v);
    public GffStruct SetShort(string label, int v) => Set(label, GffType.Short, (short)v);
    public GffStruct SetDWord(string label, uint v) => Set(label, GffType.DWord, v);
    public GffStruct SetInt(string label, int v) => Set(label, GffType.Int, v);
    public GffStruct SetFloat(string label, float v) => Set(label, GffType.Float, v);
    public GffStruct SetString(string label, string v) => Set(label, GffType.ExoString, v);
    public GffStruct SetResRef(string label, string v) => Set(label, GffType.ResRef, v);
    public GffStruct SetLocString(string label, LocString v) => Set(label, GffType.LocString, v);
    public GffStruct SetList(string label, List<GffStruct> v) => Set(label, GffType.List, v);
    public GffStruct SetStruct(string label, GffStruct v) => Set(label, GffType.Struct, v);

    public bool Remove(string label) => Fields.RemoveAll(f => f.Label == label) > 0;

    public GffStruct DeepClone()
    {
        var c = new GffStruct(StructType);
        foreach (var f in Fields)
        {
            object v = f.Value switch
            {
                GffStruct s => s.DeepClone(),
                List<GffStruct> l => l.Select(x => x.DeepClone()).ToList(),
                LocString ls => ls.Clone(),
                byte[] b => (byte[])b.Clone(),
                _ => f.Value,
            };
            c.Fields.Add(new GffField(f.Label, f.Type, v));
        }
        return c;
    }
}

/// <summary>GFF V3.2 reader and writer.</summary>
public static class Gff
{
    public static GffStruct Read(byte[] buf) => Read(buf, out _);

    public static GffStruct Read(byte[] buf, out string fileType)
    {
        fileType = buf.Length >= 4 ? Encoding.ASCII.GetString(buf, 0, 4) : "";
        if (buf.Length < 56) return new GffStruct();
        return new Reader(buf).ReadStruct(0);
    }

    sealed class Reader
    {
        readonly byte[] _b;
        readonly int _structs, _fields, _labels, _data, _indices, _lists;

        public Reader(byte[] b)
        {
            _b = b;
            _structs = I(8);
            _fields = I(16);
            _labels = I(24);
            _data = I(32);
            _indices = I(40);
            _lists = I(48);
        }

        int I(int o) => (int)BinaryPrimitives.ReadUInt32LittleEndian(_b.AsSpan(o));

        public GffStruct ReadStruct(int index)
        {
            int s = _structs + index * 12;
            var result = new GffStruct((uint)I(s));
            int data = I(s + 4);
            int count = I(s + 8);
            if (count == 1) ReadField(data, result);
            else
                for (int i = 0; i < count; i++)
                    ReadField(I(_indices + data + i * 4), result);
            return result;
        }

        void ReadField(int index, GffStruct into)
        {
            int f = _fields + index * 12;
            var type = (GffType)I(f);
            string label = Bin.FixedString(_b, _labels + I(f + 4) * 16, 16);
            int raw = f + 8;
            int off = _data + I(raw);
            var span = _b.AsSpan();
            object value = type switch
            {
                GffType.Byte => _b[raw],
                GffType.Char => (sbyte)_b[raw],
                GffType.Word => BinaryPrimitives.ReadUInt16LittleEndian(span[raw..]),
                GffType.Short => BinaryPrimitives.ReadInt16LittleEndian(span[raw..]),
                GffType.DWord => BinaryPrimitives.ReadUInt32LittleEndian(span[raw..]),
                GffType.Int => BinaryPrimitives.ReadInt32LittleEndian(span[raw..]),
                GffType.Float => BinaryPrimitives.ReadSingleLittleEndian(span[raw..]),
                GffType.DWord64 => BinaryPrimitives.ReadUInt64LittleEndian(span[off..]),
                GffType.Int64 => BinaryPrimitives.ReadInt64LittleEndian(span[off..]),
                GffType.Double => BinaryPrimitives.ReadDoubleLittleEndian(span[off..]),
                GffType.ExoString => Bin.Text.GetString(_b, off + 4, I(off)),
                GffType.ResRef => Bin.Text.GetString(_b, off + 1, _b[off]),
                GffType.LocString => ReadLoc(off),
                GffType.Void => _b.AsSpan(off + 4, I(off)).ToArray(),
                GffType.Struct => ReadStruct(I(raw)),
                GffType.List => ReadList(_lists + I(raw)),
                GffType.Orientation => new Quaternion(F(off), F(off + 4), F(off + 8), F(off + 12)),
                GffType.Vector => new Vector3(F(off), F(off + 4), F(off + 8)),
                _ => 0,
            };
            into.Fields.Add(new GffField(label, type, value));
        }

        float F(int o) => BinaryPrimitives.ReadSingleLittleEndian(_b.AsSpan(o));

        LocString ReadLoc(int off)
        {
            var l = new LocString { StrRef = (int)BinaryPrimitives.ReadUInt32LittleEndian(_b.AsSpan(off + 4)) };
            int n = I(off + 8);
            int p = off + 12;
            for (int i = 0; i < n; i++)
            {
                int id = I(p);
                int len = I(p + 4);
                l.Strings[id] = Bin.Text.GetString(_b, p + 8, len);
                p += 8 + len;
            }
            return l;
        }

        List<GffStruct> ReadList(int p)
        {
            int n = I(p);
            var list = new List<GffStruct>(n);
            for (int i = 0; i < n; i++) list.Add(ReadStruct(I(p + 4 + i * 4)));
            return list;
        }
    }

    public static byte[] Write(GffStruct root, string fileType)
    {
        var w = new Writer();
        w.AddStruct(root);
        return w.Build(fileType);
    }

    sealed class Writer
    {
        readonly List<(uint Type, uint Data, uint Count)> _structs = new();
        readonly List<(uint Type, uint Label, uint Data)> _fields = new();
        readonly List<string> _labels = new();
        readonly Dictionary<string, int> _labelIndex = new();
        readonly MemoryStream _data = new();
        readonly List<uint> _fieldIndices = new();
        readonly List<uint> _listIndices = new();

        public int AddStruct(GffStruct s)
        {
            int index = _structs.Count;
            _structs.Add(default);
            var fieldIdx = new List<uint>(s.Fields.Count);
            foreach (var f in s.Fields) fieldIdx.Add((uint)AddField(f));
            if (fieldIdx.Count == 1) _structs[index] = (s.StructType, fieldIdx[0], 1);
            else
            {
                uint offset = (uint)(_fieldIndices.Count * 4);
                _fieldIndices.AddRange(fieldIdx);
                _structs[index] = (s.StructType, fieldIdx.Count == 0 ? 0xFFFFFFFF : offset, (uint)fieldIdx.Count);
            }
            return index;
        }

        int Label(string label)
        {
            if (label.Length > 16) label = label[..16];
            if (!_labelIndex.TryGetValue(label, out int i))
            {
                i = _labels.Count;
                _labels.Add(label);
                _labelIndex[label] = i;
            }
            return i;
        }

        int AddField(GffField f)
        {
            int index = _fields.Count;
            _fields.Add(default);
            uint data = 0;
            switch (f.Type)
            {
                case GffType.Byte: data = Convert.ToByte(f.Value); break;
                case GffType.Char: data = (uint)(byte)Convert.ToSByte(f.Value); break;
                case GffType.Word: data = Convert.ToUInt16(f.Value); break;
                case GffType.Short: data = (uint)(ushort)Convert.ToInt16(f.Value); break;
                case GffType.DWord: data = Convert.ToUInt32(f.Value); break;
                case GffType.Int: data = (uint)Convert.ToInt32(f.Value); break;
                case GffType.Float: data = BitConverter.SingleToUInt32Bits(Convert.ToSingle(f.Value)); break;
                case GffType.DWord64: data = Data(b => b.Write(Convert.ToUInt64(f.Value))); break;
                case GffType.Int64: data = Data(b => b.Write(Convert.ToInt64(f.Value))); break;
                case GffType.Double: data = Data(b => b.Write(Convert.ToDouble(f.Value))); break;
                case GffType.ExoString:
                    data = Data(b => { var s = Bin.Text.GetBytes((string)f.Value); b.Write(s.Length); b.Write(s); });
                    break;
                case GffType.ResRef:
                    data = Data(b => { var s = Bin.Text.GetBytes((string)f.Value); int n = Math.Min(s.Length, 255); b.Write((byte)n); b.Write(s, 0, n); });
                    break;
                case GffType.LocString:
                    data = Data(b =>
                    {
                        var l = (LocString)f.Value;
                        var parts = l.Strings.Select(kv => (kv.Key, Bytes: Bin.Text.GetBytes(kv.Value))).ToList();
                        int size = 8 + parts.Sum(p => 8 + p.Bytes.Length);
                        b.Write(size);
                        b.Write(l.StrRef);
                        b.Write(parts.Count);
                        foreach (var p in parts) { b.Write(p.Key); b.Write(p.Bytes.Length); b.Write(p.Bytes); }
                    });
                    break;
                case GffType.Void:
                    data = Data(b => { var v = (byte[])f.Value; b.Write(v.Length); b.Write(v); });
                    break;
                case GffType.Orientation:
                    data = Data(b => { var q = (Quaternion)f.Value; b.Write(q.X); b.Write(q.Y); b.Write(q.Z); b.Write(q.W); });
                    break;
                case GffType.Vector:
                    data = Data(b => { var v = (Vector3)f.Value; b.Write(v.X); b.Write(v.Y); b.Write(v.Z); });
                    break;
                case GffType.Struct:
                    data = (uint)AddStruct((GffStruct)f.Value);
                    break;
                case GffType.List:
                {
                    var list = (List<GffStruct>)f.Value;
                    int start = _listIndices.Count;
                    data = (uint)(start * 4);
                    _listIndices.Add((uint)list.Count);
                    for (int i = 0; i < list.Count; i++) _listIndices.Add(0);
                    for (int i = 0; i < list.Count; i++) _listIndices[start + 1 + i] = (uint)AddStruct(list[i]);
                    break;
                }
            }
            _fields[index] = ((uint)f.Type, (uint)Label(f.Label), data);
            return index;
        }

        uint Data(Action<BinaryWriter> write)
        {
            uint offset = (uint)_data.Length;
            using var bw = new BinaryWriter(_data, Encoding.ASCII, leaveOpen: true);
            write(bw);
            return offset;
        }

        public byte[] Build(string fileType)
        {
            var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            const int header = 56;
            int structOff = header;
            int fieldOff = structOff + _structs.Count * 12;
            int labelOff = fieldOff + _fields.Count * 12;
            int dataOff = labelOff + _labels.Count * 16;
            int indicesOff = dataOff + (int)_data.Length;
            int listOff = indicesOff + _fieldIndices.Count * 4;
            w.Write(Encoding.ASCII.GetBytes(fileType.PadRight(4)[..4]));
            w.Write(Encoding.ASCII.GetBytes("V3.2"));
            w.Write(structOff); w.Write(_structs.Count);
            w.Write(fieldOff); w.Write(_fields.Count);
            w.Write(labelOff); w.Write(_labels.Count);
            w.Write(dataOff); w.Write((int)_data.Length);
            w.Write(indicesOff); w.Write(_fieldIndices.Count * 4);
            w.Write(listOff); w.Write(_listIndices.Count * 4);
            foreach (var s in _structs) { w.Write(s.Type); w.Write(s.Data); w.Write(s.Count); }
            foreach (var f in _fields) { w.Write(f.Type); w.Write(f.Label); w.Write(f.Data); }
            foreach (var l in _labels)
            {
                var b = new byte[16];
                Encoding.ASCII.GetBytes(l, 0, Math.Min(16, l.Length), b, 0);
                w.Write(b);
            }
            w.Write(_data.ToArray());
            foreach (var i in _fieldIndices) w.Write(i);
            foreach (var i in _listIndices) w.Write(i);
            return ms.ToArray();
        }
    }
}

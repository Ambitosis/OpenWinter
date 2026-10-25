using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Aurora.Textures;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests.Formats;

/// <summary>
/// Deterministic fuzzing of every file-format reader. Each test builds a small valid file in code, then feeds the
/// reader truncations, seeded byte flips, hostile counts and offsets, and self-references. A reader must either
/// succeed or throw its documented format exception, within a time and allocation bound.
/// </summary>
public class MalformedInputTests
{
    const int RandomMutations = 600;
    // Generous bounds: they catch hangs and runaway work, not slow disks or a busy CI machine.
    static readonly TimeSpan PerInput = TimeSpan.FromSeconds(10);
    static readonly TimeSpan PerSuite = TimeSpan.FromSeconds(120);

    // ---------------------------------------------------------------- harness

    internal static IEnumerable<byte[]> Mutations(byte[] valid, int seed, int random = RandomMutations, bool everyTruncation = true)
    {
        yield return valid;
        int step = everyTruncation ? 1 : Math.Max(1, valid.Length / 128);
        for (int n = 0; n < valid.Length; n += step) yield return valid[..n];

        uint[] hostile = { 0, 1, 2, 0xFFFFFFFF, 0xFFFFFFFE, 0x7FFFFFFF, 0x80000000, 0x40000000, 0x10000000, (uint)valid.Length, (uint)valid.Length + 1, (uint)valid.Length - 4 };
        var rng = new Random(seed);
        for (int i = 0; i < random; i++)
        {
            var b = (byte[])valid.Clone();
            int edits = 1 + rng.Next(3);
            for (int e = 0; e < edits && b.Length >= 4; e++)
            {
                switch (rng.Next(5))
                {
                    case 0:
                        b[rng.Next(b.Length)] ^= (byte)(1 << rng.Next(8));
                        break;
                    case 1:
                        b[rng.Next(b.Length)] = (byte)rng.Next(256);
                        break;
                    case 2:
                    {
                        int at = rng.Next(b.Length / 4) * 4;
                        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), hostile[rng.Next(hostile.Length)]);
                        break;
                    }
                    case 3:
                    {
                        int at = rng.Next(b.Length - 3);
                        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), hostile[rng.Next(hostile.Length)]);
                        break;
                    }
                    default:
                    {
                        // Copy one word over another: turns counts into offsets and indices into self-references.
                        int from = rng.Next(b.Length / 4) * 4, to = rng.Next(b.Length / 4) * 4;
                        b.AsSpan(from, 4).CopyTo(b.AsSpan(to));
                        break;
                    }
                }
            }
            yield return b;
        }
    }

    /// <summary>Runs the reader over every input on a worker thread, failing on hangs, blowups and stray exceptions.</summary>
    internal static void Survives(string format, IEnumerable<byte[]> inputs, Action<byte[]> read, params Type[] allowed)
    {
        var worker = Task.Factory.StartNew(() =>
        {
            int index = 0;
            foreach (var input in inputs)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var clock = Stopwatch.StartNew();
                try
                {
                    read(input);
                }
                catch (Exception e) when (allowed.Contains(e.GetType()))
                {
                }
                catch (Exception e)
                {
                    throw new Xunit.Sdk.XunitException($"{format} input #{index} ({input.Length} bytes) threw {e.GetType().Name}: {e}");
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (clock.Elapsed > PerInput)
                    throw new Xunit.Sdk.XunitException($"{format} input #{index} took {clock.Elapsed.TotalSeconds:F1} s");
                if (allocated > 64L * 1024 * 1024 + 256L * input.Length)
                    throw new Xunit.Sdk.XunitException($"{format} input #{index} ({input.Length} bytes) allocated {allocated / (1024 * 1024)} MB");
                index++;
            }
        }, TaskCreationOptions.LongRunning);
        Assert.True(worker.Wait(PerSuite), $"{format}: reader did not finish within {PerSuite.TotalSeconds} s");
        worker.GetAwaiter().GetResult();
    }

    internal static void Put32(byte[] b, int at, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), v);
    internal static void Put16(byte[] b, int at, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at), v);
    internal static uint Get32(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    static void PutF(byte[] b, int at, float v) => BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(at), v);
    internal static void PutText(byte[] b, int at, string s) => Encoding.ASCII.GetBytes(s, b.AsSpan(at));

    // ---------------------------------------------------------------- GFF

    static byte[] ValidGff()
    {
        var loc = new LocString { StrRef = 12 };
        loc.Strings[0] = "Hello";
        loc.Strings[2] = "Bonjour";
        var root = new GffStruct()
            .SetByte("B", 7).SetWord("W", 600).SetInt("I", -3).SetFloat("F", 1.5f)
            .Set("I64", GffType.Int64, -(1L << 40)).Set("D", GffType.Double, 2.5)
            .SetString("Str", "text").SetResRef("Ref", "nw_it_gold001").SetLocString("Loc", loc)
            .Set("V", GffType.Void, new byte[] { 1, 2, 3 })
            .Set("Vec", GffType.Vector, new Vector3(1, 2, 3)).Set("Ori", GffType.Orientation, Quaternion.Identity)
            .SetStruct("Child", new GffStruct(5).SetInt("A", 1))
            .SetList("List", new List<GffStruct> { new GffStruct(1).SetInt("X", 1), new GffStruct(2), new GffStruct(3).SetString("Y", "z").SetInt("Z", 9) });
        return Gff.Write(root, "UTC ");
    }

    [Fact]
    public void GffSurvivesMutation() =>
        Survives("GFF", Mutations(ValidGff(), 1), b => Gff.Read(b), typeof(InvalidDataException));

    [Fact]
    public void GffRejectsSelfReferencingStructsAndLists()
    {
        var valid = ValidGff();
        int fieldsAt = (int)Get32(valid, 16), fieldCount = (int)Get32(valid, 20);
        int listsAt = (int)Get32(valid, 48);

        // A struct field whose struct is the root.
        var structCycle = (byte[])valid.Clone();
        for (int i = 0; i < fieldCount; i++)
            if (Get32(structCycle, fieldsAt + i * 12) == (uint)GffType.Struct) Put32(structCycle, fieldsAt + i * 12 + 8, 0);
        Assert.Throws<InvalidDataException>(() => Gff.Read(structCycle));

        // A list whose first element is the root.
        var listCycle = (byte[])valid.Clone();
        Put32(listCycle, listsAt + 4, 0);
        Assert.Throws<InvalidDataException>(() => Gff.Read(listCycle));
    }

    [Fact]
    public void GffRejectsExponentiallySharedStructs()
    {
        // A chain of lists [next, empty] 40 deep; pointing both entries at "next" would expand to 2^40 structs.
        var leaf = new GffStruct().SetInt("Leaf", 1);
        var node = leaf;
        for (int d = 0; d < 40; d++) node = new GffStruct().SetList("L", new List<GffStruct> { node, new GffStruct() });
        var b = Gff.Write(node, "GFF ");
        int listsAt = (int)Get32(b, 48), listsSize = (int)Get32(b, 52);
        for (int p = 0; p < listsSize;)
        {
            int n = (int)Get32(b, listsAt + p);
            if (n == 2) Put32(b, listsAt + p + 8, Get32(b, listsAt + p + 4));
            p += 4 + 4 * n;
        }
        Survives("GFF shared structs", new[] { b }, x => Gff.Read(x), typeof(InvalidDataException));
        Assert.Throws<InvalidDataException>(() => Gff.Read(b));
    }

    [Fact]
    public void GuiSurvivesMutation()
    {
        var button = new GffStruct().SetString("Obj_Tag", "OK").SetInt("Obj_Type", 6).SetFloat("Obj_X", 10)
            .SetStruct("Obj_Caption", new GffStruct().SetString("AurString_Text", "OK").SetFloat("AurString_ColorR", 1));
        var panel = new GffStruct().SetString("Obj_Tag", "PANEL").SetList("Obj_ChildList", new List<GffStruct> { button, new GffStruct().SetString("Obj_Tag", "B") });
        Survives("GUI", Mutations(Gff.Write(panel, "GUI "), 2, 300), b => GuiReader.Read(b, "test"), typeof(InvalidDataException));
    }

    // ---------------------------------------------------------------- ERF / KEY / BIF

    static byte[] ValidErf(string type = "MOD ") => Erf.Write(new List<ErfEntry>
    {
        new("module", 2014, Encoding.ASCII.GetBytes("IFO V3.2 module info")),
        new("area001", 2012, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
        new("script", 2010, Encoding.ASCII.GetBytes("NCS V1.0")),
    }, type, 7, new Dictionary<int, string> { [0] = "A module" }, new DateTime(2020, 1, 1));

    [Fact]
    public void ErfSurvivesMutation() =>
        Survives("ERF", Mutations(ValidErf(), 3), b => Erf.Read(b, out _), typeof(InvalidDataException));

    [Fact]
    public void ErfSourceSurvivesMutation()
    {
        string dir = Directory.CreateTempSubdirectory("ow-fuzz-erf").FullName;
        try
        {
            int n = 0;
            Survives("ERF source", Mutations(ValidErf("HAK "), 4, 200, everyTruncation: false), b =>
            {
                string path = Path.Combine(dir, $"{n++}.hak");
                File.WriteAllBytes(path, b);
                using var source = new ErfSource(path);
                foreach (var key in source.Keys.ToList()) source.Read(key);
            }, typeof(InvalidDataException));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    internal static (byte[] Key, byte[] Bif) ValidKeyBif(string bifName = "data\\test.bif")
    {
        var payloads = new[] { Encoding.ASCII.GetBytes("2DA V2.0\n\n   Label\n0  one\n"), new byte[] { 9, 8, 7, 6 } };
        int tableAt = 20, dataAt = tableAt + payloads.Length * 16;
        var bif = new byte[dataAt + payloads.Sum(p => p.Length)];
        PutText(bif, 0, "BIFFV1  ");
        Put32(bif, 8, (uint)payloads.Length);
        Put32(bif, 16, (uint)tableAt);
        for (int i = 0, at = dataAt; i < payloads.Length; at += payloads[i].Length, i++)
        {
            Put32(bif, tableAt + i * 16, (uint)i);
            Put32(bif, tableAt + i * 16 + 4, (uint)at);
            Put32(bif, tableAt + i * 16 + 8, (uint)payloads[i].Length);
            Put32(bif, tableAt + i * 16 + 12, 2017);
            payloads[i].CopyTo(bif, at);
        }

        int fileTable = 64, nameAt = fileTable + 12, keyTable = nameAt + bifName.Length + 1;
        var key = new byte[keyTable + 2 * 22];
        PutText(key, 0, "KEY V1  ");
        Put32(key, 8, 1);
        Put32(key, 12, 2);
        Put32(key, 16, (uint)fileTable);
        Put32(key, 20, (uint)keyTable);
        Put32(key, fileTable, (uint)bif.Length);
        Put32(key, fileTable + 4, (uint)nameAt);
        Put16(key, fileTable + 8, (ushort)(bifName.Length + 1));
        PutText(key, nameAt, bifName);
        PutText(key, keyTable, "classes");
        Put16(key, keyTable + 16, 2017);
        Put32(key, keyTable + 18, 0);
        PutText(key, keyTable + 22, "other");
        Put16(key, keyTable + 22 + 16, 2017);
        Put32(key, keyTable + 22 + 18, 1);
        return (key, bif);
    }

    [Fact]
    public void KeyBifSurvivesMutation()
    {
        var (key, bif) = ValidKeyBif();
        string dir = Directory.CreateTempSubdirectory("ow-fuzz-key").FullName;
        try
        {
            int n = 0;
            void Run(byte[] k, byte[] b)
            {
                string root = Path.Combine(dir, (n++).ToString());
                Directory.CreateDirectory(Path.Combine(root, "data"));
                File.WriteAllBytes(Path.Combine(root, "data", "test.bif"), b);
                File.WriteAllBytes(Path.Combine(root, "test.key"), k);
                var source = new KeyBifSource(Path.Combine(root, "test.key"), root);
                foreach (var resKey in source.Keys.ToList()) source.Read(resKey);
            }
            Run(key, bif);
            Survives("KEY", Mutations(key, 5, 150, everyTruncation: false), k => Run(k, bif), typeof(InvalidDataException));
            Survives("BIF", Mutations(bif, 6, 150, everyTruncation: false), b => Run(key, b), typeof(InvalidDataException));
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // ---------------------------------------------------------------- text formats

    [Fact]
    public void TwoDASurvivesMutation()
    {
        var text = Encoding.ASCII.GetBytes("2DA V2.0\n\n   Label   Name    Cost\n0  Sword   \"Long Sword\"  10\n1  ****  Axe 5\n5  Bow Bow ****\n");
        Survives("2DA", Mutations(text, 7), b =>
        {
            var t = TwoDA.Parse(b);
            for (int r = -1; r < t.RowCount + 1; r++) t.Get(r, "Name");
        });
        Survives("2DA hostile row", new[] { Encoding.ASCII.GetBytes("2DA V2.0\n\nA B\n2147483647 x y\n2000000000 z\n") }, b => TwoDA.Parse(b));
    }

    [Fact]
    public void AsciiModelSurvivesMutation()
    {
        var text = Encoding.ASCII.GetBytes(string.Join('\n',
            "newmodel test", "setsupermodel test null", "classification character", "setanimationscale 1",
            "beginmodelgeom test",
            "node dummy test", "  parent NULL", "endnode",
            "node trimesh body", "  parent test", "  position 0 0 1", "  orientation 0 0 1 0.5", "  bitmap tex",
            "  verts 3", "    0 0 0", "    1 0 0", "    0 1 0",
            "  tverts 3", "    0 0 0", "    1 0 0", "    0 1 0",
            "  faces 1", "    0 1 2 1 0 1 2 1",
            "endnode",
            "node light lamp", "  parent test", "  color 1 1 1", "  radius 5", "endnode",
            "endmodelgeom test",
            "newanim walk test", "  length 1", "  transtime 0.25", "  animroot test", "  event 0.5 hit",
            "  node dummy test", "    parent NULL", "    positionkey 2", "      0 0 0 0", "      1 1 0 0", "  endnode",
            "doneanim walk test", "donemodel test", ""));
        Survives("ASCII MDL", Mutations(text, 8, 400, everyTruncation: false), b => MdlAsciiReader.Read(b), typeof(InvalidDataException));
        var hostile = Encoding.ASCII.GetBytes("newmodel x\nnode trimesh m\n verts 2000000000\n 0 0 0\n faces -5\nendnode\nnewanim a x\n node dummy x\n  positionkey 2000000000\n endnode\ndoneanim a x\n");
        Survives("ASCII MDL hostile counts", new[] { hostile }, b => MdlAsciiReader.Read(b), typeof(InvalidDataException));
        Survives("model cache", Mutations(text, 9, 200, everyTruncation: false), b => ModelCache.Load(b));
    }

    // ---------------------------------------------------------------- binary MDL

    const int ModelAt = 0, RootAt = 0x100, MeshAt = 0x200, ControllersAt = 0x520, AnimTableAt = 0x560, AnimAt = 0x580, AnimNodeAt = 0x660;

    /// <summary>A model with a root node (one position key), a mesh child with one triangle and one animation.</summary>
    static byte[] ValidBinaryModel()
    {
        const int modelSize = 0x700;
        var raw = new byte[3 * 12 + 3 * 8 + 3 * 12];
        for (int i = 0; i < 3; i++)
        {
            PutF(raw, i * 12, i == 1 ? 1 : 0);
            PutF(raw, i * 12 + 4, i == 2 ? 1 : 0);
            PutF(raw, 36 + i * 8, i * 0.5f);
            PutF(raw, 60 + i * 12 + 8, 1);
        }
        var m = new byte[modelSize];
        PutText(m, ModelAt + 0x08, "test");
        Put32(m, ModelAt + 0x48, RootAt);
        m[ModelAt + 0x72] = 0x04;
        Put32(m, ModelAt + 0x78, AnimTableAt);
        Put32(m, ModelAt + 0x7C, 1);
        PutF(m, ModelAt + 0xA4, 1);
        PutText(m, ModelAt + 0xA8, "NULL");

        // Root: one child, a position controller.
        PutText(m, RootAt + 0x20, "test");
        Put32(m, RootAt + 0x48, RootAt + 0xF0);
        Put32(m, RootAt + 0x4C, 1);
        Put32(m, RootAt + 0xF0, MeshAt);
        Put32(m, RootAt + 0x54, ControllersAt);
        Put32(m, RootAt + 0x58, 1);
        Put32(m, RootAt + 0x60, ControllersAt + 12);
        Put32(m, RootAt + 0x64, 4);
        Put32(m, ControllersAt, 8);
        Put16(m, ControllersAt + 4, 1);
        Put16(m, ControllersAt + 6, 0);
        Put16(m, ControllersAt + 8, 1);
        m[ControllersAt + 10] = 3;
        PutF(m, ControllersAt + 12 + 4, 1);

        // Mesh: three vertices, uvs and normals in the raw block, one face, no index lists.
        PutText(m, MeshAt + 0x20, "body");
        Put32(m, MeshAt + 0x6C, 0x21);
        Put32(m, MeshAt + 0x78, MeshAt + 0x2D0);
        Put32(m, MeshAt + 0x7C, 1);
        PutText(m, MeshAt + 0xE8, "tex");
        Put32(m, MeshAt + 0x204, 0xFFFFFFFF);
        Put32(m, MeshAt + 0x210, 0xFFFFFFFF);
        Put32(m, MeshAt + 0x22C, 0);
        Put16(m, MeshAt + 0x230, 3);
        Put32(m, MeshAt + 0x234, 36);
        Put32(m, MeshAt + 0x244, 60);
        Put16(m, MeshAt + 0x2D0 + 26, 0);
        Put16(m, MeshAt + 0x2D0 + 28, 1);
        Put16(m, MeshAt + 0x2D0 + 30, 2);

        // Animation "walk" with one node carrying the root's position key.
        Put32(m, AnimTableAt, AnimAt);
        PutText(m, AnimAt + 0x08, "walk");
        Put32(m, AnimAt + 0x48, AnimNodeAt);
        PutF(m, AnimAt + 0x70, 1);
        PutText(m, AnimAt + 0x78, "test");
        PutText(m, AnimNodeAt + 0x20, "test");
        Put32(m, AnimNodeAt + 0x54, ControllersAt);
        Put32(m, AnimNodeAt + 0x58, 1);
        Put32(m, AnimNodeAt + 0x60, ControllersAt + 12);
        Put32(m, AnimNodeAt + 0x64, 4);

        var file = new byte[12 + modelSize + raw.Length];
        Put32(file, 4, modelSize);
        Put32(file, 8, (uint)raw.Length);
        m.CopyTo(file, 12);
        raw.CopyTo(file, 12 + modelSize);
        return file;
    }

    [Fact]
    public void BinaryModelReadsTheSyntheticModel()
    {
        var model = MdlBinaryReader.Read(ValidBinaryModel());
        Assert.Equal(2, model.Parts.Count);
        Assert.Equal(3, model.Parts[1].Mesh!.Vertices.Length);
        Assert.Equal(3, model.Parts[1].Mesh!.Indices.Length);
        Assert.Equal(new Vector3(1, 0, 0), model.Parts[0].Position);
        Assert.True(model.Animations.ContainsKey("walk"));
    }

    [Fact]
    public void BinaryModelSurvivesMutation()
    {
        Survives("binary MDL", Mutations(ValidBinaryModel(), 10, 800, everyTruncation: false),
            b => MdlBinaryReader.Read(b), typeof(InvalidDataException));
        Survives("binary MDL via cache", Mutations(ValidBinaryModel(), 11, 200, everyTruncation: false), b => ModelCache.Load(b));
    }

    [Fact]
    public void BinaryModelSurvivesNodeCycles()
    {
        // Root lists itself twice as a child: without a guard the walk grows as 2^depth.
        var cycle = ValidBinaryModel();
        Put32(cycle, 12 + RootAt + 0x4C, 2);
        Put32(cycle, 12 + RootAt + 0xF0, RootAt);
        Put32(cycle, 12 + RootAt + 0xF4, RootAt);
        var animCycle = ValidBinaryModel();
        Put32(animCycle, 12 + AnimNodeAt + 0x48, AnimNodeAt + 0x90);
        Put32(animCycle, 12 + AnimNodeAt + 0x4C, 2);
        Put32(animCycle, 12 + AnimNodeAt + 0x90, AnimNodeAt);
        Put32(animCycle, 12 + AnimNodeAt + 0x94, AnimNodeAt);
        Survives("binary MDL cycles", new[] { cycle, animCycle }, b => MdlBinaryReader.Read(b), typeof(InvalidDataException));
    }

    // ---------------------------------------------------------------- textures

    static byte[] Tga(int type, int bpp, bool rle, int w = 4, int h = 4)
    {
        var data = new List<byte>();
        var header = new byte[18];
        header[2] = (byte)type;
        Put16(header, 12, (ushort)w);
        Put16(header, 14, (ushort)h);
        header[16] = (byte)bpp;
        if (type == 1 || type == 9)
        {
            header[1] = 1;
            Put16(header, 5, 4);
            header[7] = 24;
        }
        data.AddRange(header);
        if (type == 1 || type == 9) for (int i = 0; i < 12; i++) data.Add((byte)(i * 20));
        int bytesPer = (bpp + 7) / 8;
        if (rle)
        {
            for (int px = 0; px < w * h; px += 4)
            {
                data.Add(0x83);
                for (int k = 0; k < bytesPer; k++) data.Add((byte)(px + k));
            }
        }
        else for (int i = 0; i < w * h * bytesPer; i++) data.Add((byte)i);
        return data.ToArray();
    }

    [Fact]
    public void TgaSurvivesMutation()
    {
        int seed = 20;
        foreach (var tga in new[] { Tga(2, 32, false), Tga(10, 24, true), Tga(1, 8, false), Tga(3, 8, false), Tga(11, 16, true), Tga(2, 16, false) })
            Survives("TGA", Mutations(tga, seed++, 300), b => TextureDecoder.DecodeTga(b));
        // Maximum dimensions with only a few bytes of pixel data must not allocate a full image.
        var huge = new[] { Tga(2, 32, false)[..64], Tga(10, 32, true)[..30] };
        foreach (var h in huge)
        {
            Put16(h, 12, 16384);
            Put16(h, 14, 16384);
        }
        Survives("TGA huge", huge, b => Assert.Null(TextureDecoder.DecodeTga(b)));
    }

    [Fact]
    public void DdsSurvivesMutation()
    {
        // BioWare DDS: 8x8 DXT5 with its full mip chain.
        int size = 4 * 16 + 16 + 16 + 16;
        var bioware = new byte[20 + size];
        Put32(bioware, 0, 8);
        Put32(bioware, 4, 8);
        Put32(bioware, 8, 4);
        Put32(bioware, 12, 64);
        for (int i = 20; i < bioware.Length; i++) bioware[i] = (byte)(i * 7);

        var standard = new byte[128 + 8 * 8 / 2];
        PutText(standard, 0, "DDS ");
        Put32(standard, 4, 124);
        Put32(standard, 12, 8);
        Put32(standard, 16, 8);
        Put32(standard, 28, 1);
        Put32(standard, 76, 32);
        Put32(standard, 80, 4);
        PutText(standard, 84, "DXT1");

        var rgba = new byte[128 + 4 * 4 * 4];
        PutText(rgba, 0, "DDS ");
        Put32(rgba, 12, 4);
        Put32(rgba, 16, 4);
        Put32(rgba, 80, 0x41);
        Put32(rgba, 88, 32);
        Put32(rgba, 92, 0xFF0000);
        Put32(rgba, 96, 0xFF00);
        Put32(rgba, 100, 0xFF);
        Put32(rgba, 104, 0xFF000000);

        Survives("BioWare DDS", Mutations(bioware, 30, 300), b => TextureDecoder.DecodeDds(b));
        Survives("DDS", Mutations(standard, 31, 300), b => TextureDecoder.DecodeDds(b));
        Survives("DDS RGBA", Mutations(rgba, 32, 300), b => TextureDecoder.DecodeDds(b));
    }

    [Fact]
    public void PltSurvivesMutation()
    {
        var plt = new byte[24 + 4 * 4 * 2];
        PutText(plt, 0, "PLT V1  ");
        Put32(plt, 16, 4);
        Put32(plt, 20, 4);
        for (int i = 24; i < plt.Length; i++) plt[i] = (byte)i;
        Survives("PLT", Mutations(plt, 40), b => TextureDecoder.DecodePlt(b));
        var huge = (byte[])plt.Clone();
        Put32(huge, 16, 65536);
        Put32(huge, 20, 65536);
        Assert.Null(TextureDecoder.DecodePlt(huge));
    }

    // ---------------------------------------------------------------- talk tables, sound sets, name generators, fonts

    [Fact]
    public void TalkTableSurvivesMutation()
    {
        var strings = new[] { "Hello", "", "World" };
        int stringsAt = 20 + strings.Length * 40;
        var tlk = new byte[stringsAt + strings.Sum(s => s.Length)];
        PutText(tlk, 0, "TLK V3.0");
        Put32(tlk, 12, (uint)strings.Length);
        Put32(tlk, 16, (uint)stringsAt);
        for (int i = 0, offset = 0; i < strings.Length; offset += strings[i].Length, i++)
        {
            int e = 20 + i * 40;
            Put32(tlk, e, 7);
            PutText(tlk, e + 4, "vs_hello");
            Put32(tlk, e + 28, (uint)offset);
            Put32(tlk, e + 32, (uint)strings[i].Length);
            PutText(tlk, stringsAt + offset, strings[i]);
        }
        Assert.Equal("World", new TalkTable(tlk, null).Get(2));
        Survives("TLK", Mutations(tlk, 50), b =>
        {
            var t = new TalkTable(b, b);
            foreach (uint r in new uint[] { 0, 1, 2, 3, 0x00FFFFFF, 0x01000000, 0x01000002, 0x7FFFFFFF, 0xFFFFFFFE })
            {
                t.Get(r);
                t.Sound(r);
            }
        });
    }

    [Fact]
    public void SoundSetSurvivesMutation()
    {
        var ssf = new byte[40 + 3 * 4 + 3 * 20];
        PutText(ssf, 0, "SSF V1.0");
        Put32(ssf, 8, 3);
        Put32(ssf, 12, 40);
        for (int i = 0; i < 3; i++)
        {
            int at = 52 + i * 20;
            Put32(ssf, 40 + i * 4, (uint)at);
            PutText(ssf, at, "vs_attack");
            Put32(ssf, at + 16, (uint)i);
        }
        Survives("SSF", Mutations(ssf, 60), b => SoundSet.Read(b), typeof(InvalidDataException));
    }

    [Fact]
    public void LtrSurvivesMutation()
    {
        int n = 26;
        var ltr = new byte[9 + 3 * n * 4 * (1 + n + n * n)];
        PutText(ltr, 0, "LTR V1.0");
        ltr[8] = (byte)n;
        for (int i = 9; i + 4 <= ltr.Length; i += 4) PutF(ltr, i, (i % 104) / 104f);
        Survives("LTR", Mutations(ltr, 70, 200, everyTruncation: false), b => Ltr.Parse(b).Generate(new Random(1)), typeof(InvalidDataException));
    }

    static byte[] BigEndianFont()
    {
        var tables = new (string Tag, byte[] Data)[]
        {
            ("cmap", CmapFormat12()), ("head", new byte[54]), ("hhea", new byte[36]), ("hmtx", new byte[3 * 4]),
        };
        BinaryPrimitives.WriteUInt16BigEndian(tables[1].Data.AsSpan(18), 1000);
        BinaryPrimitives.WriteUInt16BigEndian(tables[2].Data.AsSpan(34), 3);
        for (int i = 0; i < 3; i++) BinaryPrimitives.WriteUInt16BigEndian(tables[3].Data.AsSpan(i * 4), (ushort)(500 + i));
        int at = 12 + tables.Length * 16;
        var font = new byte[at + tables.Sum(t => t.Data.Length)];
        BinaryPrimitives.WriteUInt32BigEndian(font, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(4), (ushort)tables.Length);
        for (int i = 0; i < tables.Length; i++)
        {
            int rec = 12 + i * 16;
            PutText(font, rec, tables[i].Tag);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(rec + 8), (uint)at);
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(rec + 12), (uint)tables[i].Data.Length);
            tables[i].Data.CopyTo(font, at);
            at += tables[i].Data.Length;
        }
        return font;

        static byte[] CmapFormat12()
        {
            var c = new byte[12 + 16 + 12];
            BinaryPrimitives.WriteUInt16BigEndian(c.AsSpan(2), 1);
            BinaryPrimitives.WriteUInt16BigEndian(c.AsSpan(4), 3);
            BinaryPrimitives.WriteUInt16BigEndian(c.AsSpan(6), 10);
            BinaryPrimitives.WriteUInt32BigEndian(c.AsSpan(8), 12);
            BinaryPrimitives.WriteUInt16BigEndian(c.AsSpan(12), 12);
            BinaryPrimitives.WriteUInt32BigEndian(c.AsSpan(12 + 4), 28);
            BinaryPrimitives.WriteUInt32BigEndian(c.AsSpan(12 + 12), 1);
            BinaryPrimitives.WriteUInt32BigEndian(c.AsSpan(12 + 16), 'A');
            BinaryPrimitives.WriteUInt32BigEndian(c.AsSpan(12 + 20), 'C');
            BinaryPrimitives.WriteUInt32BigEndian(c.AsSpan(12 + 24), 1);
            return c;
        }
    }

    [Fact]
    public void TrueTypeMetricsSurviveMutation()
    {
        var font = BigEndianFont();
        Assert.Equal(501, TrueTypeMetrics.Read(font)!.AdvanceUnits('A'));
        Survives("TTF", Mutations(font, 80), b => TrueTypeMetrics.Read(b)?.AdvanceUnits('B'));
    }

    // ---------------------------------------------------------------- NCS

    [Fact]
    public void NcsSurvivesMutation()
    {
        var code = new List<byte>();
        code.AddRange(Encoding.ASCII.GetBytes("NCS V1.0"));
        code.Add(0x42);
        code.AddRange(new byte[4]);
        code.AddRange(new byte[] { 0x02, 0x03 });                         // RSADDI
        code.AddRange(new byte[] { 0x04, 0x03, 0, 0, 0, 5 });             // CONSTI 5
        code.AddRange(new byte[] { 0x04, 0x05, 0, 2, (byte)'h', (byte)'i' }); // CONSTS "hi"
        code.AddRange(new byte[] { 0x01, 0x01, 0xFF, 0xFF, 0xFF, 0xF8, 0, 4 }); // CPDOWNSP
        code.AddRange(new byte[] { 0x05, 0x00, 0, 1, 1 });                // ACTION 1 (1 arg)
        code.AddRange(new byte[] { 0x1D, 0x00, 0, 0, 0, 6 });             // JMP +6
        code.AddRange(new byte[] { 0x2C, 0x00, 0, 0, 0, 1, 0, 0, 0, 2 }); // STORE_STATE
        code.AddRange(new byte[] { 0x20, 0x00 });                         // RETN
        var file = code.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(9), (uint)file.Length);
        Assert.Equal(8, new NcsProgram("test", file).Instructions.Length);
        Survives("NCS", Mutations(file, 90), b => new NcsProgram("test", b), typeof(InvalidDataException));
    }
}

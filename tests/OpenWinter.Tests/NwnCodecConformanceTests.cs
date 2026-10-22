using System.Collections;
using System.IO.Compression;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// The C# message codec against nwn-message.py: every fixture frame (written by tools/nwnproto/fixtures.py) decodes to
/// the reference library's value, consumes every byte, and re-encodes to the same bytes.
/// </summary>
public class NwnCodecConformanceTests
{
    /// <summary>The fixtures' fake game data: every 2DA has four rows and a cell holds its row number, except that
    /// even visual effects are projectiles.</summary>
    sealed class FakeContext : INwnContext
    {
        public int TwoDARowCount(string table) => 4;
        public string TwoDA(string table, int row, string column) =>
            table == "visualeffects" && column == "Type_FD" ? (row % 2 == 0 ? "P" : "F") : row.ToString();
        public bool Satisfies(int build, int patch, int postfix) => build == 8193 && (37 > patch || (37 == patch && 17 >= postfix));
        public Dictionary<uint, bool> TriggerIsTrap { get; } = [];
    }

    sealed record Fixture(string Type, byte[] Frame, JsonNode? Value);

    static List<Fixture> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "nwn-message-fixtures.jsonl.gz");
        using var reader = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
        var list = new List<Fixture>();
        while (reader.ReadLine() is { } line)
        {
            var row = JsonNode.Parse(line)!;
            list.Add(new Fixture((string)row["type"]!, Convert.FromHexString((string)row["hex"]!), row["value"]));
        }
        return list;
    }

    static Type ClrType(string name) =>
        typeof(AreaMessages).Assembly.GetType("OpenWinter.Net.Nwn.Messages." + name.Replace('.', '+'))
        ?? throw new InvalidOperationException($"no generated type {name}");

    [Fact]
    public void EveryMessageMatchesTheReference()
    {
        var fixtures = Load();
        Assert.True(fixtures.Count > 2000, $"only {fixtures.Count} fixtures");
        var failures = new List<string>();
        foreach (var f in fixtures)
        {
            var problem = Check(f);
            if (problem != null) failures.Add($"{f.Type}: {problem}");
        }
        var covered = fixtures.Select(f => f.Type).Distinct().Count();
        Assert.True(failures.Count == 0,
            $"{failures.Count} of {fixtures.Count} fixtures ({covered} message types) differ:\n" +
            string.Join("\n", failures.Distinct().Take(40)));
    }

    static string? Check(Fixture f)
    {
        var direction = f.Frame[0] switch
        {
            (byte)'p' => NwnDirection.ClientToServer,
            (byte)'P' => NwnDirection.ServerToClient,
            var b => throw new InvalidOperationException($"frame prefix {b}"),
        };
        var created = NwnMessageRegistry.Create(f.Frame[1], f.Frame[2], direction);
        if (created == null || created.GetType() != ClrType(f.Type))
            return $"registry gives {created?.GetType().Name ?? "nothing"} for {f.Frame[1]:X2}.{f.Frame[2]:X2}";

        var payload = f.Frame.AsMemory(3);
        var reader = new NwnWsReader(payload, new FakeContext());
        try
        {
            created.Read(reader);
        }
        catch (Exception e) when (e is NwnWireException or InvalidCastException or NotImplementedException)
        {
            return $"read failed at byte {reader.Position}: {e.Message}";
        }
        if (!reader.AtEnd) return $"{reader.Length - reader.Position} bytes left after reading";

        var errors = new List<string>();
        Compare(f.Value, Normalize(created), "", errors);
        if (errors.Count > 0) return string.Join("; ", errors.Take(3));

        var writer = new NwnWsWriter(new FakeContext());
        try
        {
            created.Write(writer);
        }
        catch (Exception e)
        {
            return $"write failed: {e.Message}";
        }
        var written = writer.ToArray();
        if (!written.AsSpan().SequenceEqual(payload.Span))
        {
            var at = 0;
            while (at < written.Length && at < payload.Length && written[at] == payload.Span[at]) at++;
            return $"re-encoding differs at byte {at} ({written.Length} vs {payload.Length} bytes)";
        }
        return null;
    }

    /// <summary>The same shape fixtures.py gives a decoded value.</summary>
    static object? Normalize(object? v) => v switch
    {
        null => null,
        NwnTagged t => Normalize(t.Value),
        GameObjUpdateMessages.ObjListGuiEntry g => Normalize(g.Value),
        NwnLocString l => l.IsStrRef
            ? new Dictionary<string, object?> { ["strref"] = (BigInteger)l.StrRef, ["gender"] = (BigInteger)l.Gender }
            : new Dictionary<string, object?> { ["text"] = l.Text },
        Enum e => (BigInteger)Convert.ToInt64(e),
        bool b => b,
        string s => s,
        byte[] bytes => Convert.ToHexString(bytes).ToLowerInvariant(),
        float x => (double)x,
        double x => x,
        ulong u => (BigInteger)u,
        IConvertible c when v is byte or sbyte or short or ushort or int or uint or long => (BigInteger)c.ToInt64(null),
        ITuple tuple => Enumerable.Range(0, tuple.Length).Select(i => Normalize(tuple[i])).ToList(),
        IList list => list.Cast<object?>().Select(Normalize).ToList(),
        INwnStruct s => s.GetType().GetProperties()
            .Where(p => p.GetCustomAttribute<NwnFieldAttribute>() != null)
            .ToDictionary(p => p.GetCustomAttribute<NwnFieldAttribute>()!.Name, p => Normalize(p.GetValue(s))),
        _ => throw new InvalidOperationException($"can't normalize {v.GetType().Name}"),
    };

    static void Compare(JsonNode? expected, object? actual, string path, List<string> errors)
    {
        switch (expected, actual)
        {
            case (null, null):
                return;
            case (JsonObject o, Dictionary<string, object?> d):
                foreach (var (key, value) in o)
                {
                    if (!d.TryGetValue(key, out var a)) errors.Add($"{path}.{key} missing");
                    else Compare(value, a, $"{path}.{key}", errors);
                }
                foreach (var key in d.Keys.Where(k => !o.ContainsKey(k))) errors.Add($"{path}.{key} unexpected");
                return;
            case (JsonArray arr, List<object?> list):
                if (arr.Count != list.Count)
                {
                    errors.Add($"{path} has {list.Count} items, expected {arr.Count}");
                    return;
                }
                for (int i = 0; i < arr.Count; i++) Compare(arr[i], list[i], $"{path}[{i}]", errors);
                return;
            case (JsonValue jv, double d):
                if (jv.GetValue<double>() != d) errors.Add($"{path} = {d}, expected {jv}");
                return;
            case (JsonValue jv, BigInteger n):
                if (BigInteger.Parse(jv.ToJsonString()) != n) errors.Add($"{path} = {n}, expected {jv}");
                return;
            case (JsonValue jv, bool b):
                if (jv.GetValue<bool>() != b) errors.Add($"{path} = {b}, expected {jv}");
                return;
            case (JsonValue jv, string s):
                if (jv.GetValue<string>() != s) errors.Add($"{path} = \"{s}\", expected {jv}");
                return;
            default:
                errors.Add($"{path}: {actual?.GetType().Name ?? "null"} where {expected?.ToJsonString() ?? "null"} expected");
                return;
        }
    }
}

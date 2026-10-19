/// <summary>Run validity is separate from equality: two aborted scripts do not prove parity.</summary>
internal static class ParityResultGuard
{
    static string Record(string line)
    {
        int at = line.IndexOf("PARITY|", StringComparison.Ordinal);
        return at < 0 ? "" : line[(at + 7)..].TrimEnd('\r', '\n');
    }
    public static bool IsComplete(string line) => Record(line) == "__done__|1";
    public static IReadOnlyList<string> Failures(IEnumerable<string> source)
    {
        var lines = source.ToArray();
        var failures = new List<string>();
        if (!lines.Any(IsComplete)) failures.Add("missing PDone completion marker (__done__|1)");
        foreach (string line in lines)
            if (Record(line).StartsWith("error.", StringComparison.Ordinal)
                || line.Contains("Script error:", StringComparison.Ordinal)
                || line.Contains("[script error]", StringComparison.Ordinal)
                || line.Contains("[script] not implemented:", StringComparison.Ordinal))
                failures.Add(line.Trim());
        return failures.Distinct().ToArray();
    }
}

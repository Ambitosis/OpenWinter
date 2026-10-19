using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenWinter.Client.Playtest;

/// <summary>A problem the autopilot ran into. Bugs of the same kind and key are counted, not repeated.</summary>
public sealed class PlaytestBug
{
    public string Kind { get; set; } = "";
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Area { get; set; } = "";
    public double Time { get; set; }
    public int Count { get; set; } = 1;
    public string? Screenshot { get; set; }
    public List<string> Log { get; set; } = new();
}

public sealed class PlaytestArea
{
    public string ResRef { get; set; } = "";
    public string Name { get; set; } = "";
    public double FirstEntered { get; set; }
    public int Visits { get; set; }
    public double Seconds { get; set; }
    public string? Screenshot { get; set; }
    public int Talked { get; set; }
    public int Used { get; set; }
    public int Fought { get; set; }
    public int Transitions { get; set; }
}

public sealed class PlaytestConversation
{
    public string Speaker { get; set; } = "";
    public string Area { get; set; } = "";
    public string FirstLine { get; set; } = "";
    public double Time { get; set; }
    public int Nodes { get; set; }
    public int Replies { get; set; }
    public int Times { get; set; } = 1;
    public string? Screenshot { get; set; }
}

public sealed class PlaytestQuest
{
    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
    public List<int> States { get; set; } = new();
    public string LastText { get; set; } = "";
    public bool Finished { get; set; }
    public double FirstTime { get; set; }
    public double LastTime { get; set; }
}

public sealed class PlaytestEvent
{
    public double Time { get; set; }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>What a playtest run found: areas, conversations, quests, bugs and a timeline. Written as JSON, Markdown and HTML.</summary>
public sealed class PlaytestReport
{
    public string Module { get; set; } = "";
    public string Character { get; set; } = "";
    public string Started { get; set; } = "";
    public double Seconds { get; set; }
    public string Outcome { get; set; } = "running";
    public bool GodMode { get; set; }
    public Dictionary<string, int> Stats { get; set; } = new();
    public List<PlaytestArea> Areas { get; set; } = new();
    public List<PlaytestQuest> Quests { get; set; } = new();
    public List<PlaytestConversation> Conversations { get; set; } = new();
    public List<PlaytestBug> Bugs { get; set; } = new();
    public List<PlaytestEvent> Timeline { get; set; } = new();

    public void Count(string stat, int by = 1) => Stats[stat] = Stats.GetValueOrDefault(stat) + by;

    public void Event(double time, string kind, string text)
    {
        Timeline.Add(new PlaytestEvent { Time = Math.Round(time, 1), Kind = kind, Text = text });
        if (Timeline.Count > 3000) Timeline.RemoveAt(0);
    }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public void Write(string dir)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "report.json"), JsonSerializer.Serialize(this, Json));
        File.WriteAllText(Path.Combine(dir, "report.md"), Markdown());
        File.WriteAllText(Path.Combine(dir, "report.html"), Html());
    }

    static string T(double seconds) => $"{(int)(seconds / 60):00}:{(int)(seconds % 60):00}";

    IEnumerable<IGrouping<string, PlaytestBug>> BugGroups() =>
        Bugs.GroupBy(b => b.Kind).OrderBy(g => KindOrder(g.Key)).ThenByDescending(g => g.Sum(b => b.Count));

    /// <summary>Most serious kinds first.</summary>
    static int KindOrder(string kind) => kind switch
    {
        "exception" => 0, "stuck-screen" => 1, "black-screen" => 1, "loading" => 1, "cutscene" => 2, "no-control" => 2,
        "dialog-loop" => 3, "dialog" => 3, "ui" => 4, "script-error" => 5, "stuck" => 6, "combat" => 6, "transition" => 6,
        "module-switch" => 7, "unimplemented" => 8, "content" => 9, "frame-spike" => 10, _ => 11,
    };

    string Markdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Playtest: {Module}");
        sb.AppendLine();
        sb.AppendLine($"- Character: {Character}{(GodMode ? " (god mode)" : "")}");
        sb.AppendLine($"- Started: {Started}, ran {T(Seconds)} — outcome: **{Outcome}**");
        sb.AppendLine($"- Areas: {Areas.Count}, conversations: {Conversations.Count}, quests touched: {Quests.Count}, bugs: {Bugs.Count} distinct ({Bugs.Sum(b => b.Count)} occurrences)");
        if (Stats.Count > 0) sb.AppendLine("- Stats: " + string.Join(", ", Stats.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")));
        sb.AppendLine();
        sb.AppendLine("## Bugs");
        foreach (var g in BugGroups())
        {
            sb.AppendLine();
            sb.AppendLine($"### {g.Key} ({g.Count()} distinct, {g.Sum(b => b.Count)} total)");
            foreach (var b in g.OrderBy(b => b.Time))
            {
                sb.AppendLine();
                sb.AppendLine($"- **{Escape(b.Title)}** — {b.Area} at {T(b.Time)}{(b.Count > 1 ? $", ×{b.Count}" : "")}");
                if (b.Detail.Length > 0) sb.AppendLine($"  {Escape(b.Detail).Replace("\n", "\n  ")}");
                if (b.Screenshot != null) sb.AppendLine($"  ![{b.Kind}]({b.Screenshot})");
                if (b.Log.Count > 0)
                {
                    sb.AppendLine("  ```");
                    foreach (var l in b.Log) sb.AppendLine("  " + l);
                    sb.AppendLine("  ```");
                }
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Areas");
        sb.AppendLine();
        sb.AppendLine("| Area | ResRef | First | Visits | Time | Talked | Used | Fought | Exits taken |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var a in Areas) sb.AppendLine($"| {Escape(a.Name)} | {a.ResRef} | {T(a.FirstEntered)} | {a.Visits} | {T(a.Seconds)} | {a.Talked} | {a.Used} | {a.Fought} | {a.Transitions} |");
        sb.AppendLine();
        sb.AppendLine("## Quests");
        sb.AppendLine();
        foreach (var q in Quests)
            sb.AppendLine($"- **{Escape(q.Name)}** ({q.Tag}) states {string.Join(" → ", q.States)}{(q.Finished ? " — finished" : "")}: {Escape(Short(q.LastText, 200))}");
        sb.AppendLine();
        sb.AppendLine("## Conversations");
        sb.AppendLine();
        foreach (var c in Conversations)
            sb.AppendLine($"- {T(c.Time)} **{Escape(c.Speaker)}** ({c.Area}) ×{c.Times}, {c.Nodes} lines, {c.Replies} replies: “{Escape(Short(c.FirstLine, 120))}”");
        sb.AppendLine();
        sb.AppendLine("## Timeline");
        sb.AppendLine();
        foreach (var e in Timeline) sb.AppendLine($"- {T(e.Time)} `{e.Kind}` {Escape(e.Text)}");
        return sb.ToString();
    }

    static string Short(string s, int n) { s = s.Replace('\n', ' ').Trim(); return s.Length > n ? s[..(n - 1)] + "…" : s; }
    static string Escape(string s) => s.Replace("|", "\\|").Replace("<", "&lt;").Replace(">", "&gt;");

    string Html()
    {
        static string H(string s) => WebUtility.HtmlEncode(s);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><meta charset=utf-8><title>Playtest ").Append(H(Module)).Append("</title><style>")
          .Append("body{font:14px system-ui,sans-serif;background:#111418;color:#ddd;margin:24px;max-width:1300px}h1,h2,h3{color:#e3c27a}")
          .Append("a{color:#9cf}.bug{border:1px solid #333;border-radius:8px;padding:10px;margin:10px 0;background:#181c22;overflow:hidden}")
          .Append(".bug img,.thumbs img{max-width:320px;max-height:180px;border:1px solid #444;border-radius:4px;float:right;margin-left:12px}")
          .Append("pre{background:#0b0d10;padding:8px;overflow:auto;font-size:12px;max-height:260px;clear:both}table{border-collapse:collapse}td,th{border:1px solid #333;padding:4px 8px}")
          .Append(".thumbs{display:flex;flex-wrap:wrap;gap:10px}.thumbs figure{margin:0;width:330px}.thumbs img{float:none}figcaption{font-size:12px;color:#aaa}")
          .Append(".k{color:#e3c27a;font-weight:600}</style>");
        sb.Append("<h1>Playtest: ").Append(H(Module)).Append("</h1><p>Character ").Append(H(Character)).Append(GodMode ? " (god mode)" : "")
          .Append(" · started ").Append(H(Started)).Append(" · ran ").Append(T(Seconds)).Append(" · outcome <b>").Append(H(Outcome)).Append("</b><br>")
          .Append($"{Areas.Count} areas · {Conversations.Count} conversations · {Quests.Count} quests · {Bugs.Count} distinct bugs ({Bugs.Sum(b => b.Count)} total)</p>");
        if (Stats.Count > 0) sb.Append("<p>").Append(H(string.Join(" · ", Stats.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")))).Append("</p>");
        sb.Append("<h2>Bugs</h2>");
        foreach (var g in BugGroups())
        {
            sb.Append($"<h3>{H(g.Key)} ({g.Count()})</h3>");
            foreach (var b in g.OrderBy(b => b.Time))
            {
                sb.Append("<div class=bug>");
                if (b.Screenshot != null) sb.Append($"<a href=\"{H(b.Screenshot)}\"><img src=\"{H(b.Screenshot)}\" loading=lazy></a>");
                sb.Append($"<span class=k>{H(b.Title)}</span><br>{H(b.Area)} at {T(b.Time)}{(b.Count > 1 ? $" · ×{b.Count}" : "")}");
                if (b.Detail.Length > 0) sb.Append("<p>").Append(H(b.Detail).Replace("\n", "<br>")).Append("</p>");
                if (b.Log.Count > 0) sb.Append("<pre>").Append(H(string.Join("\n", b.Log))).Append("</pre>");
                sb.Append("</div>");
            }
        }
        sb.Append("<h2>Areas</h2><div class=thumbs>");
        foreach (var a in Areas)
        {
            sb.Append("<figure>");
            if (a.Screenshot != null) sb.Append($"<a href=\"{H(a.Screenshot)}\"><img src=\"{H(a.Screenshot)}\" loading=lazy></a>");
            sb.Append($"<figcaption><b>{H(a.Name)}</b> ({H(a.ResRef)}) first {T(a.FirstEntered)}, {a.Visits} visit(s), {T(a.Seconds)}; talked {a.Talked}, used {a.Used}, fought {a.Fought}, exits {a.Transitions}</figcaption></figure>");
        }
        sb.Append("</div><h2>Quests</h2><ul>");
        foreach (var q in Quests) sb.Append($"<li><b>{H(q.Name)}</b> ({H(q.Tag)}) {H(string.Join(" → ", q.States))}{(q.Finished ? " — finished" : "")}<br><small>{H(q.LastText)}</small></li>");
        sb.Append("</ul><h2>Conversations</h2><div class=thumbs>");
        foreach (var c in Conversations)
        {
            sb.Append("<figure>");
            if (c.Screenshot != null) sb.Append($"<a href=\"{H(c.Screenshot)}\"><img src=\"{H(c.Screenshot)}\" loading=lazy></a>");
            sb.Append($"<figcaption>{T(c.Time)} <b>{H(c.Speaker)}</b> ({H(c.Area)}) ×{c.Times}, {c.Nodes} lines, {c.Replies} replies<br>“{H(Short(c.FirstLine, 160))}”</figcaption></figure>");
        }
        sb.Append("</div><h2>Timeline</h2><pre>");
        foreach (var e in Timeline) sb.Append(H($"{T(e.Time)} {e.Kind,-12} {e.Text}")).Append('\n');
        sb.Append("</pre>");
        return sb.ToString();
    }
}

using System.Net;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Persistence;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>NWNX_SQL, NWNX_Core, NWNX_HTTPClient, NWNX_WebHook and NWNX_NWSQLiteExtensions without a script.</summary>
public class NwnxMiscPluginTests
{
    static (World World, NwnxRegistry Registry) Setup(Dictionary<string, string>? env = null)
    {
        var world = new World(new ResMan(TestData.Paths));
        world.System<PersistenceSystem>().RootOverride = Path.Combine(Path.GetTempPath(), "openwinter-tests-nwnx", Guid.NewGuid().ToString("N"));
        var registry = world.System<NwnxRegistry>();
        registry.Log = _ => { };
        env ??= new() { ["NWNX_SQL_TYPE"] = "SQLITE", ["NWNX_SQL_DATABASE"] = "test" };
        registry.EnvironmentVariable = name => env.GetValueOrDefault(name);
        return (world, registry);
    }

    /// <summary>Calls a plugin function with arguments in the order the script pushes them.</summary>
    static NwnxValue? Call(NwnxRegistry r, string plugin, string function, params NwnxValue[] pushed)
    {
        foreach (var v in pushed) r.Stack.Push(v);
        Assert.True(r.Call(plugin, function));
        return r.Stack.Count > 0 ? r.Stack.Pop() : null;
    }

    static NwnxValue S(string s) => NwnxValue.String(s);
    static NwnxValue I(int i) => NwnxValue.Int(i);
    static int Int(NwnxValue? v) => v?.Value is int i ? i : throw new InvalidOperationException("no int");
    static string Str(NwnxValue? v) => v?.Value as string ?? "";

    [DataFact]
    public void SqlitePreparedQueriesBindTextAndReadRowsAsNwnxDoes()
    {
        var (world, r) = Setup();
        try
        {
            Assert.Equal("SQLITE", Str(Call(r, "SQL", "GetDatabaseType")));
            Assert.Equal(-1, Int(Call(r, "SQL", "GetPreparedQueryParamCount")));
            Assert.Equal(1, Int(Call(r, "SQL", "PrepareQuery", S("CREATE TABLE t (a, b)"))));
            Assert.Equal(1, Int(Call(r, "SQL", "ExecutePreparedQuery")));
            Assert.Equal(1, Int(Call(r, "SQL", "PrepareQuery", S("INSERT INTO t VALUES (?, ?)"))));
            Assert.Equal(2, Int(Call(r, "SQL", "GetPreparedQueryParamCount")));
            Call(r, "SQL", "PreparedInt", I(5), I(0));
            Call(r, "SQL", "PreparedFloat", NwnxValue.Float(0.1f), I(1));
            Assert.Equal(2, Int(Call(r, "SQL", "ExecutePreparedQuery")));
            Assert.Equal(1, Int(Call(r, "SQL", "GetAffectedRows")));
            // The binds are kept for the next run.
            Assert.Equal(3, Int(Call(r, "SQL", "ExecutePreparedQuery")));
            Call(r, "SQL", "DestroyPreparedQuery");
            Assert.Equal(1, Int(Call(r, "SQL", "PrepareQuery", S("SELECT a, b, typeof(a) FROM t"))));
            Assert.Equal(4, Int(Call(r, "SQL", "ExecutePreparedQuery")));
            Assert.Equal(-1, Int(Call(r, "SQL", "GetAffectedRows")));
            Assert.Equal(1, Int(Call(r, "SQL", "ReadyToReadNextRow")));
            Call(r, "SQL", "ReadNextRow");
            Assert.Equal("5", Str(Call(r, "SQL", "ReadDataInActiveRow", I(0))));
            Assert.Equal("0.100000", Str(Call(r, "SQL", "ReadDataInActiveRow", I(1))));
            Assert.Equal("text", Str(Call(r, "SQL", "ReadDataInActiveRow", I(2))));
            Assert.Null(Call(r, "SQL", "ReadDataInActiveRow", I(3)));
            Call(r, "SQL", "ReadNextRow");
            Assert.Equal(0, Int(Call(r, "SQL", "ReadyToReadNextRow")));
            // Past the end, and into the next query, the active row stays.
            Call(r, "SQL", "ReadNextRow");
            Call(r, "SQL", "PrepareQuery", S("SELECT 'x'"));
            Assert.Equal("5", Str(Call(r, "SQL", "ReadDataInActiveRow", I(0))));

            // A failed prepare takes no query id; a failed run does. The error is kept until read once.
            Assert.Equal(0, Int(Call(r, "SQL", "PrepareQuery", S("SELEC"))));
            Assert.Equal(-1, Int(Call(r, "SQL", "GetPreparedQueryParamCount")));
            Call(r, "SQL", "PrepareQuery", S("SELECT 1"));
            Assert.Equal("near \"SELEC\": syntax error", Str(Call(r, "SQL", "GetLastError")));
            Assert.Equal("", Str(Call(r, "SQL", "GetLastError")));
            Call(r, "SQL", "PrepareQuery", S("CREATE TABLE u (k INTEGER PRIMARY KEY)"));
            Call(r, "SQL", "ExecutePreparedQuery");
            Call(r, "SQL", "PrepareQuery", S("INSERT INTO u VALUES (1)"));
            Assert.Equal(6, Int(Call(r, "SQL", "ExecutePreparedQuery")));
            Assert.Equal(0, Int(Call(r, "SQL", "ExecutePreparedQuery")));
            Assert.Equal(-1, Int(Call(r, "SQL", "GetAffectedRows")));
            Assert.Equal("UNIQUE constraint failed: u.k", Str(Call(r, "SQL", "GetLastError")));
            Call(r, "SQL", "PrepareQuery", S("SELECT 1"));
            Assert.Equal(8, Int(Call(r, "SQL", "ExecutePreparedQuery")));
            Assert.True(File.Exists(Path.Combine(world.System<PersistenceSystem>().DatabaseDir, "test" + NwnxSqliteTarget.Extension)));
        }
        finally { SqlPlugin.Close(r); }
    }

    [DataFact]
    public void Utf8ConversionTreatsScriptTextAsLatin1()
    {
        var (_, r) = Setup(new() { ["NWNX_SQL_TYPE"] = "sqlite", ["NWNX_SQL_USE_UTF8"] = "true" });
        try
        {
            Call(r, "SQL", "PrepareQuery", S("SELECT hex(?), char(8364)"));
            Call(r, "SQL", "PreparedString", S("café"), I(0));
            Call(r, "SQL", "ExecutePreparedQuery");
            Call(r, "SQL", "ReadNextRow");
            Assert.Equal("636166C3A9", Str(Call(r, "SQL", "ReadDataInActiveRow", I(0))));
            Assert.Equal("?", Str(Call(r, "SQL", "ReadDataInActiveRow", I(1))));
        }
        finally { SqlPlugin.Close(r); }
    }

    [Theory]
    [InlineData("SELECT ?, '?', \"?\", `?` -- ?\n, ? /* ? */", NwnxAdoTarget.Kind.MySql, 2)]
    [InlineData("SELECT 'it''s ?', 'a\\'?', ?", NwnxAdoTarget.Kind.MySql, 1)]
    [InlineData("SELECT $1, $3, '$4', $2", NwnxAdoTarget.Kind.PostgreSql, 3)]
    [InlineData("SELECT ?", NwnxAdoTarget.Kind.PostgreSql, 0)]
    public void PlaceholdersAreCountedOutsideQuotesAndComments(string query, NwnxAdoTarget.Kind kind, int expected) =>
        Assert.Equal(expected, NwnxAdoTarget.CountPlaceholders(query, kind));

    [Theory]
    [InlineData("NWNX_Creature", true)]
    [InlineData("nwnx_sql", true)]
    [InlineData("NWNX_Tweaks", true)]
    [InlineData("NWNX_Core", true)]
    [InlineData("Creature", false)]
    [InlineData("NWNX_", false)]
    [InlineData("NWNX_Ruby", false)]
    [InlineData("NWNX_Redis", false)]
    public void PluginExistsNeedsThePrefixAndIgnoresCase(string name, bool exists) => Assert.Equal(exists, CorePlugin.PluginExists(name));

    [Fact]
    public void MersenneTwisterMatchesTheStandardSequence()
    {
        var mt = new MersenneTwister(5489);
        Assert.Equal([3499211612u, 581869302u, 3890346734u], new[] { mt.Next(), mt.Next(), mt.Next() });
        var one = new MersenneTwister(1);
        Assert.Equal(1791095845u, one.Next());
        Assert.Equal(4282876139u, one.Next());
    }

    [Fact]
    public void WebHookTextIsWrappedAndJsonIsSentAsItIs()
    {
        Assert.Equal("{\"text\":\"hi\",\"username\":\"bot\",\"mrkdwn\":false}", WebHookPlugin.Body("hi", "bot", false));
        Assert.Equal("{\"text\":\"hi\"}", WebHookPlugin.Body("hi", "", true));
        Assert.Equal("{\"text\": \"x\"}", WebHookPlugin.Body("{\"text\": \"x\"}", "bot", true));
        Assert.Equal("https://discord.com/api/webhooks/1/slack", NwnxHttp.Url("discord.com", 443, "/api/webhooks/1/slack").ToString());
        Assert.Equal("http://localhost:8080/x", NwnxHttp.Url("http://localhost", 8080, "x").ToString());
        Assert.Equal("http://example.org/", NwnxHttp.Url("example.org", 80, "/").ToString());
    }

    sealed class Recorder : HttpMessageHandler
    {
        public readonly List<(HttpRequestMessage Request, string Body)> Seen = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request, request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("pong") };
            response.Headers.Add("X-RateLimit-Remaining", "4");
            return response;
        }
    }

    [DataFact]
    public void HttpClientSendsTheDescribedRequestAndKeepsItForGetRequest()
    {
        var (world, r) = Setup();
        var recorder = new Recorder();
        HTTPClientPlugin.Client = new HttpMessageInvoker(recorder);
        try
        {
            // Pushed as nwnx_httpclient.nss pushes the struct: custom3 first, the tag last.
            var id = Int(Call(r, "HTTPClient", "SendRequest", S("c3"), S("c2"), S("c1"), S("Accept: application/json|X-Test: 1"),
                I(0), S("pw"), S("user"), I(1), S("{\"a\":1}"), I(2), S("/issues"), S("api.example.org"), I(1), S("TAG")));
            Assert.Equal(1, id);
            for (int i = 0; i < 200 && recorder.Seen.Count == 0; i++) Thread.Sleep(10);
            var (req, body) = Assert.Single(recorder.Seen);
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("https://api.example.org/issues", req.RequestUri!.ToString());
            Assert.Equal("Basic", req.Headers.Authorization!.Scheme);
            Assert.Equal("dXNlcjpwdw==", req.Headers.Authorization.Parameter);
            Assert.Equal("1", req.Headers.GetValues("X-Test").Single());
            Assert.Equal("application/json", req.Content!.Headers.ContentType!.MediaType);
            Assert.Equal("{\"a\":1}", body);
            r.Step(0.05f);

            var popped = new List<object?> { Call(r, "HTTPClient", "GetRequest", I(id))?.Value };
            while (r.Stack.Pop() is { } v) popped.Add(v.Value);
            Assert.Equal(new object?[] { "TAG", 1, "api.example.org", "/issues", 2, "{\"a\":1}", 1, "user", "pw", 0, "Accept: application/json|X-Test: 1", "c1", "c2", "c3" }, popped);
        }
        finally { HTTPClientPlugin.Client = null; }
    }
}

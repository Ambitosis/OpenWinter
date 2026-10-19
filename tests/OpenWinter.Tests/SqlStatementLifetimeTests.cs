using System.Runtime.CompilerServices;
using OpenWinter.Game.Persistence;
using SQLitePCL;
using Xunit;

namespace OpenWinter.Tests;

// The regression briefly holds the finalizer thread; do not run it beside other tests.
[CollectionDefinition("SQL finalization", DisableParallelization = true)]
public class SqlFinalizationCollection { }

[Collection("SQL finalization")]
public class SqlStatementLifetimeTests
{
    sealed class FinalizerGate(ManualResetEventSlim entered, ManualResetEventSlim release)
    {
        ~FinalizerGate() { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void QueueGate(ManualResetEventSlim entered, ManualResetEventSlim release) => _ = new FinalizerGate(entered, release);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference<SqlStatement> AbandonCursor(SqlDb db)
    {
        var statement = db.PrepareInternal("SELECT value FROM data");
        Assert.Equal(raw.SQLITE_ROW, statement.Step());
        return new WeakReference<SqlStatement>(statement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollectedActiveCursorReleasesLockBeforeNativeFinalizerRuns(bool endOfScripts)
    {
        using var db = SqlDb.OpenMemory();
        db.Exec("CREATE TABLE data(value); INSERT INTO data VALUES(1),(2)");
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        QueueGate(entered, release);
        GC.Collect();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(3)), "Finalizer control did not start");
            var weak = AbandonCursor(db);
            GC.Collect();
            Assert.False(weak.TryGetTarget(out _), "The script's query wrapper must be collected for this control");
            if (endOfScripts)
            {
                db.EndOfScripts(null);
                db.Exec("DROP TABLE data");
            }
            else db.DropSchema();
            using var count = db.PrepareInternal("SELECT count(*) FROM sqlite_master WHERE name='data'");
            Assert.Equal(raw.SQLITE_ROW, count.Step());
            Assert.Equal(0, count.Int(0));
        }
        finally
        {
            release.Set();
            GC.WaitForPendingFinalizers();
        }
    }

    [Fact]
    public void DroppingSchemaKeepsLivePreparedHandlesAndBindingsReusable()
    {
        using var db = SqlDb.OpenMemory();
        db.Exec("CREATE TABLE data(value); INSERT INTO data VALUES(1),(2)");
        using var query = db.PrepareInternal("SELECT value + @add FROM data");
        query.BindInt(query.ParameterIndex("@add"), 10);
        Assert.Equal(raw.SQLITE_ROW, query.Step());
        Assert.Equal(11, query.Int(0));
        db.DropSchema();
        Assert.True(query.IsValid);
        Assert.False(query.HasRow);
        db.Exec("CREATE TABLE data(value); INSERT INTO data VALUES(3)");
        Assert.Equal(raw.SQLITE_OK, query.Reset(clearBinds: false));
        Assert.Equal(raw.SQLITE_ROW, query.Step());
        Assert.Equal(13, query.Int(0));
    }
}

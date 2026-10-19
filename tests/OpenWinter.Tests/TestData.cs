using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>A test that needs local game data (config/local.json); skipped when it isn't available.</summary>
public sealed class DataFactAttribute : FactAttribute
{
    public DataFactAttribute()
    {
        if (TestData.Config == null) Skip = "No config/local.json with game data";
    }
}

/// <summary>A theory that needs local game data (config/local.json); skipped when it isn't available.</summary>
public sealed class DataTheoryAttribute : TheoryAttribute
{
    public DataTheoryAttribute()
    {
        if (TestData.Config == null) Skip = "No config/local.json with game data";
    }
}

public static class TestData
{
    public static readonly LocalConfig? Config = Init();

    static LocalConfig? Init()
    {
        var c = LocalConfig.Find();
        // Tests never write into the real user folder.
        OpenWinter.Game.Persistence.PersistenceSystem.SandboxRoot = Path.Combine(Path.GetTempPath(), "openwinter-tests-userdata");
        return c;
    }
    static ResMan? _module;

    public static GamePaths Paths => new(Config!.GameRoot, Config.UserRoot, Config.Language);
    public static ResMan Module => _module ??= ResMan.ForModule(Paths, Config!.Module);

    /// <summary>A neverwinter.nim tool from the configured nimTools folder, or "" when none is configured.</summary>
    public static string NimTool(string exe) => Config?.NimTools is { Length: > 0 } dir ? Path.Combine(dir, exe) : "";
}

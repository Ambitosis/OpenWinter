using OpenWinter.Aurora.Formats;

namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>Public EE progfx MIRV parameters; independent of rendering and of effect row numbers.</summary>
public sealed record SpellProjectile(string Model, string Orientation, string Path, string Timing)
{
    public static SpellProjectile? FromVisual(TwoDA visuals, TwoDA programs, int visual)
    {
        int row = visuals.GetInt(visual, "ProgFX_Impact", -1);
        if (row < 0 || programs.GetInt(row, "Type", -1) != 10) return null;
        string model = programs.Get(row, "Param1");
        if (model.Length == 0) return null;
        // Param2 has no measured spells.2da mapping (all stock MIRVs use 340). It is
        // unmeasured; sounds belong to the original visualeffects row instead.
        return new(model,
            programs.GetInt(row, "Param3") switch { 1 => "target", 2 => "path", _ => "none" },
            programs.GetInt(row, "Param4") switch
            {
                2 => "ballistic",
                3 => "highballistic",
                4 => "burstup",
                5 => "accelerating",
                6 => "spiral",
                7 => "linked",
                8 => "bounce",
                9 => "burst",
                10 => "linkedburstup",
                11 => "triplehit",
                12 => "triplemiss",
                13 => "doubleballistic",
                _ => "homing",
            }, programs.Get(row, "Param5").ToLowerInvariant());
    }

    public float FlightSeconds(float distance) => Timing switch
    {
        "linear" => Math.Max(0.1f, distance / 50f),
        "linear2" => Math.Max(0.1f, distance / 25f),
        _ => OpenWinter.Game.Spells.SpellTable.ProjectileFlightTime(distance),
    };
}

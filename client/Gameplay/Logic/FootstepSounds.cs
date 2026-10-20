namespace OpenWinter.Client.Gameplay;

/// <summary>Installed surfacemat sound families select the three sample columns in footstepsounds.2da.</summary>
public static class FootstepSounds
{
    public static string Column(string family, int surface) => family switch
    {
        "DT" => "Dirt",
        "GR" => "Grass",
        "ST" => "Stone",
        "WD" => "Wood",
        "WT" => surface is 11 or 12 or 13 ? "Puddles" : "Water",
        "CP" => "Carpet",
        "MT" => "Metal",
        "LV" => "Leaves",
        "SD" => "Sand",
        "SW" => "Snow",
        _ => "",
    };
}

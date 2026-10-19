namespace OpenWinter.Client.Gameplay;

/// <summary>Held-pointer activation and the original's sixteen directional arrow frames.</summary>
public static class MouseDriving
{
    // Native captures retain the pressed gauntlet at 100 ms and show the arrow at 200 ms.
    public const double HoldSeconds = 0.2;
    // Arrow length switches independently of walking: the native mouseclips fixture brackets
    // it at 128-144 px; mouseground measures those at 1.868 and 2.101 metres.
    public const float LongArrowDistance = 2f;

    // Installed gui_mp_ar*00 points up, 04 right: clockwise 22.5-degree steps.
    public static int Cursor(float x, float y, bool shortArrow) => (shortArrow ? 75 : 59)
        + ((int)MathF.Floor(MathF.Atan2(x, -y) * 8 / MathF.PI + 0.5f) + 16) % 16;
}

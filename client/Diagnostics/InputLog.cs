using Godot;

namespace OpenWinter.Client.Diagnostics;

/// <summary>
/// --visual-input-log: every pointer event the window receives (_Input, before the interface) and whether the world
/// got it unhandled, written to input.txt in the capture folder. For checking real pointer input (tools/visual/realdrag.py).
/// </summary>
public partial class InputLog : Node
{
    public string Path = "";
    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    public InputLog() { ProcessMode = ProcessModeEnum.Always; }

    void Write(string stage, InputEvent e)
    {
        string text = e switch
        {
            InputEventMouseButton b => $"button {b.ButtonIndex} {(b.Pressed ? "down" : "up")} at {b.Position} mask {b.ButtonMask}",
            InputEventMouseMotion m => $"motion at {m.Position} mask {m.ButtonMask}",
            _ => "",
        };
        if (text.Length > 0) System.IO.File.AppendAllText(Path, $"{_clock.ElapsedMilliseconds}|{stage}|{text}|focus={GetWindow().HasFocus()}\n");
    }

    public override void _Input(InputEvent e) => Write("input", e);
    public override void _UnhandledInput(InputEvent e) => Write("unhandled", e);
}

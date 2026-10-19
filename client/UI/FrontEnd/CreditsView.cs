using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>
/// Credits and licences: the engine, the fonts (SIL OFL 1.1), the interface icons (CC BY 3.0, read
/// from Assets/Icons/CREDITS.md so the list never goes stale) and the libraries the client links.
/// Shown from the main menu and Options → Credits.
/// </summary>
public partial class CreditsView : ScrollContainer
{
    public override void _Ready()
    {
        HorizontalScrollMode = ScrollMode.Disabled;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var col = Ui.VBox(Palette.Space4);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var pad = Ui.Pad(col, 4, 4, 18, 24);
        pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(pad);

        col.AddChild(Block("OpenWinter",
            "An open-source reimplementation of the Aurora engine that runs original Neverwinter Nights modules and persistent worlds. " +
            "Built with Godot and .NET."));
        col.AddChild(Block("Neverwinter Nights",
            "Neverwinter Nights is a trademark of Wizards of the Coast. All game content shown (models, textures, music, text, modules) is read at runtime " +
            "from your own installation and is not part of this program. This engine is not affiliated with or endorsed by Beamdog, BioWare or Wizards of the Coast."));

        col.AddChild(Ui.Section("Typefaces"));
        col.AddChild(Licence("Cinzel", "Copyright 2020 The Cinzel Project Authors (github.com/NDISCOVER/Cinzel)", "SIL Open Font License 1.1"));
        col.AddChild(Licence("Inter", "Copyright 2020 The Inter Project Authors (github.com/rsms/inter)", "SIL Open Font License 1.1"));
        col.AddChild(Licence("EB Garamond", "Copyright 2017 The EB Garamond Project Authors (github.com/octaviopardo/EBGaramond12)", "SIL Open Font License 1.1"));

        col.AddChild(Ui.Section("Interface icons"));
        var intro = Ui.Text("Icons from game-icons.net, licensed under Creative Commons Attribution 3.0 (creativecommons.org/licenses/by/3.0). " +
            "The black background square was removed so the glyphs can be tinted. The close icon was drawn for this project.", Ui.TextStyle.Small);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(intro);
        foreach (var (author, icons) in IconCredits())
            col.AddChild(Licence(author, icons, ""));

        col.AddChild(Ui.Section("Software"));
        col.AddChild(Licence("Godot Engine", "Copyright (c) 2014-present Godot Engine contributors; (c) 2007-2014 Juan Linietsky, Ariel Manzur", "MIT License"));
        col.AddChild(Licence(".NET", "Copyright (c) .NET Foundation and contributors", "MIT License"));
        col.AddChild(Licence("LiteNetLib", "Copyright (c) 2020 Ruslan Pyrch", "MIT License"));
        col.AddChild(Licence("Microsoft.Data.Sqlite, SQLitePCLRaw", "Copyright (c) .NET Foundation; Eric Sink", "MIT License (SQLite: public domain)"));
    }

    static Control Block(string title, string text)
    {
        var col = Ui.VBox(6);
        col.AddChild(Ui.Text(title, Ui.TextStyle.Title, Palette.AccentBright, 24));
        var body = Ui.Text(text, Ui.TextStyle.Reading, Palette.TextSecondary, 17);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(body);
        return col;
    }

    static Control Licence(string name, string detail, string licence)
    {
        var row = Ui.HBox(Palette.Space4);
        var n = Ui.Text(name, Ui.TextStyle.Strong);
        n.CustomMinimumSize = new Vector2(220, 0);
        n.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(n);
        var d = Ui.Text(detail, Ui.TextStyle.Small);
        d.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        d.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(d);
        if (licence.Length > 0)
        {
            var l = Ui.Text(licence, Ui.TextStyle.Caption, Palette.Accent);
            l.HorizontalAlignment = HorizontalAlignment.Right;
            l.CustomMinimumSize = new Vector2(170, 0);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(l);
        }
        return row;
    }

    /// <summary>The authors table in Assets/Icons/CREDITS.md: (author, icons).</summary>
    public static List<(string Author, string Icons)> IconCredits()
    {
        var list = new List<(string, string)>();
        const string path = "res://Assets/Icons/CREDITS.md";
        if (!Godot.FileAccess.FileExists(path)) return list;
        foreach (var line in Godot.FileAccess.GetFileAsString(path).Split('\n'))
        {
            var cells = line.Split('|', StringSplitOptions.TrimEntries);
            if (cells.Length < 4 || cells[1].Length == 0 || cells[1] == "Authors" || cells[1].StartsWith("---")) continue;
            list.Add((cells[1], cells[2]));
        }
        return list;
    }
}

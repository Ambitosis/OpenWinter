using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-actions dir: on an NWN:EE server, shots of the
    /// quickbar the server keeps for the character, the character sheet (main and feats tabs) and the spellbook;
    /// then turns detect mode on from the bar, casts the first ready spell on the character and uses the first feat
    /// with uses left, with shots of each.
    /// </summary>
    async Task CaptureNwnActions(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 600 && _session.Controlled == null; i++) await Wait(0.1);
        if (_session.Controlled == null)
        {
            GD.PrintErr("[nwn] no controlled character after 60 s");
            GetTree().Quit(1);
            return;
        }
        await Wait(4);
        await Shot(dir, "01_quickbar");
        Character.Toggle();
        await Wait(2);
        await Shot(dir, "02_sheet");
        Character.ShowTab(1);
        await Wait(0.8);
        await Shot(dir, "03_sheet_feats");
        Character.ShowTab(0);
        Character.Close();
        Spellbook.Toggle();
        await Wait(1.5);
        await Shot(dir, "04_spellbook");
        Spellbook.Close();
        var book = ActionBar.Book;
        GD.Print($"[nwn] {book.Spells.Count} spells, {book.Feats.Count} feats, {book.Modes.Count} modes, {book.Skills.Count} skills, {book.Items.Count} items");
        if (book.Modes.Any(m => m.Mode == 0))
        {
            _session.Request("action-mode", new { mode = 0, on = true });
            await Wait(1.5);
            await Shot(dir, "05_detect_mode");
            _session.Request("action-mode", new { mode = 0, on = false });
            await Wait(1);
        }
        if (book.Spells.FirstOrDefault(s => s.Ready > 0 && s.Class != 255) is { } spell)
        {
            GD.Print($"[nwn] casting {spell.Spell} ({spell.Class}, level {spell.Level})");
            _session.Request("cast-spell", new { spell = spell.Spell, @class = spell.Class, meta = spell.Meta, target = _session.ControlledId });
            await Wait(1.2);
            await Shot(dir, "06_casting");
            await Wait(2.5);
            await Shot(dir, "07_cast");
        }
        if (book.Feats.FirstOrDefault(f => f.Uses > 0) is { } feat)
        {
            GD.Print($"[nwn] using feat {feat.Feat} ({feat.Uses} left)");
            _session.Request("use-feat", new { feat = feat.Feat, sub = 0 });
            await Wait(3.5);
            await Shot(dir, "08_feat_used");
        }
        GetTree().Quit();
    }
}

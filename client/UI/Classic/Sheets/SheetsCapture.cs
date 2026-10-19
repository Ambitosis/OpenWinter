using Godot;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>
/// --capture-classic-sheets dir (with --ui classic): opens the classic character record (and its Skills and Feats
/// pages), the spellbook with the memorise panel, the journal and the small and large maps in turn and
/// screenshots each, named like the reference shots of the original (c_character, d_spellbook, e_journal, f_map).
/// </summary>
public static class SheetsCapture
{
    public static async Task Run(string dir, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        await wait(2.0);
        if (ClassicSheets.Character is not { } character) { GD.PrintErr("[classic-sheets] not attached (run with --ui classic)"); quit(); return; }
        // Let the HUD settle (the original shots are taken a while after entering).
        await wait(1.0);
        await shot(dir, "a_hud");
        character.Open();
        await wait(1.2);
        await shot(dir, "c_character");
        character.ShowTab(1);
        await wait(0.6);
        await shot(dir, "c_character_skills");
        character.ShowTab(2);
        await wait(0.6);
        await shot(dir, "c_character_feats");
        character.ShowTab(0);
        // Enough experience for a level: the pulsing Level Up button replaces the experience box.
        if (ClassicGame.Current is { } c0)
        {
            c0.Session.Send(new OpenWinter.Net.ChatSendMessage { Text = "/xp 1000" }.Write());
            await wait(1.5);
            await shot(dir, "c_character_levelup");
        }
        character.Close();

        ClassicSheets.Spellbook!.Open();
        await wait(1.5);
        await shot(dir, "d_spellbook");
        // First level: memorise the first spell offered (spell-memorize, as the modern spellbook sends).
        ClassicSheets.Spellbook.PickLevel(1);
        await wait(0.3);
        ClassicSheets.Spellbook.ClickSpell(0);
        await wait(1.0);
        await shot(dir, "d_spellbook_level1");
        ClassicSheets.Spellbook.Close();

        ClassicSheets.Journal!.Open();
        await wait(1.0);
        await shot(dir, "e_journal");
        ClassicSheets.Journal.UnfoldFirst();
        await wait(0.5);
        await shot(dir, "e_journal_quest");
        // The World page has no tab (as on the original); shown with two sample entries.
        ClassicSheets.Journal.SetWorldEntries(new ClassicJournal.WorldEntry[]
        {
            new("The Harvest Fair", "The fair opens at dawn in the square. Bring your best produce.", 1372 * 336 + 5 * 28 + 2, 8 * 3600000, 0),
            new("Wolves on the Coast Road", "Travellers report wolves along the coast road after dark.", 1372 * 336 + 5 * 28 + 4, 20 * 3600000, 1),
        });
        ClassicSheets.Journal.ShowTab(3);
        await wait(0.5);
        await shot(dir, "e_journal_world");
        ClassicSheets.Journal.ShowTab(0);
        ClassicSheets.Journal.Close();

        ClassicSheets.ToggleMap();
        await wait(1.0);
        await shot(dir, "f_map");
        ClassicSheets.MiniMap!.Enlarge?.Invoke();
        await wait(1.0);
        await shot(dir, "f_map_area");
        ClassicSheets.ToggleMap();

        // Examine the player's own creature.
        if (ClassicGame.Current is { } ctx)
        {
            ctx.Session.Request("examine", new { @object = ctx.Session.ControlledId });
            await wait(1.2);
            await shot(dir, "g_examine");
            ClassicSheets.Examine?.Close();
        }
        quit();
    }
}

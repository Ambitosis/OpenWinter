using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// --capture-classic-dialogs dir (with --ui classic): the classic conversation box (the scene's own conversation,
/// else one with the nearest creature), a reply chosen with its number key, the game menu, each message box and the
/// death panel, at the window's size (run with --resolution 1280x720 to compare with the original's shots in
/// out/visual/shots/vis_classicui_dialogs/nwn).
/// </summary>
public static class ClassicDialogsCapture
{
    public static async Task Run(string dir, ClientSession session, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        Task Shot(string name) => shot(dir, name);
        DialogMessage? last = null;
        session.DialogReceived += m => last = m;
        for (int i = 0; i < 100 && ClassicDialogs.Conversation == null; i++) await wait(0.1);
        if (ClassicDialogs.Conversation is not { } talk || ClassicDialogs.Menu is not { } menu || ClassicDialogs.Death is not { } death || ClassicGame.Current is not { } ctx)
        {
            GD.PrintErr("[classic-dialogs] the classic interface is not attached (run with --ui classic)");
            quit();
            return;
        }
        // The scene's conversation (vis_classicui_dialogs starts one), else talk to the nearest creature.
        for (int i = 0; i < 80 && last == null; i++) await wait(0.1);
        if (last == null && session.Controlled is { } me)
        {
            var npc = session.Objects.Values.Where(v => v.Kind == ObjectKind.Creature && v != me)
                .OrderBy(v => v.GlobalPosition.DistanceTo(me.GlobalPosition)).FirstOrDefault();
            if (npc != null) session.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = npc.ObjectId }.Write());
            for (int i = 0; i < 100 && last == null; i++) await wait(0.1);
        }
        await wait(2.0);
        await Shot("a_dialog");
        if (talk.ReplyPosition(1) is { } at)
        {
            Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
            await wait(0.3);
            await Shot("a2_dialog_hover");
        }
        await Press(Godot.Key.Key1, wait);
        await wait(2.0);
        await Shot("b_dialog_reply");
        await Press(Godot.Key.Escape, wait);
        await wait(1.5);
        await Shot("c_after");

        menu.Open();
        await wait(1.0);
        await Shot("d_menu");
        var host = (Node)menu;
        ClassicPopup.OkCancel(host, ctx.Assets, "Exit to the main menu? Progress since your last save will be lost.", () => { });
        await wait(0.5);
        await Shot("d2_menu_exit_confirm");
        ClassicPopup.Top?.Cancel();
        menu.Close();
        await wait(0.5);

        var popups = ClassicDialogs.PopupHost!;
        ClassicPopup.Ok(popups, ctx.Assets, "You cannot rest so close to enemies.");
        await wait(0.4);
        await Shot("f_okpanel");
        ClassicPopup.Top?.Cancel();
        ClassicPopup.YesNoCancel(popups, ctx.Assets, "Save changes to the character before leaving?", () => { }, () => { });
        await wait(0.4);
        await Shot("g_yesnocancel");
        ClassicPopup.Top?.Cancel();
        ClassicPopup.OkScroll(popups, ctx.Assets, string.Join(" ", Enumerable.Repeat("A long message that the original shows in a scrolling list, as the server's message of the day or a module's notes.", 6)));
        await wait(0.4);
        await Shot("h_okscroll");
        ClassicPopup.Top?.Cancel();
        ClassicPopup.Edit(popups, ctx.Assets, "Name your familiar:", "Wolfie", _ => { });
        await wait(0.4);
        await Shot("i_edit");
        ClassicPopup.Top?.Cancel();
        await wait(0.3);

        death.Show(true, true, "");
        await wait(0.8);
        await Shot("e_death");
        death.Visible = false;

        // The loading screen as an area transition shows it.
        if (ctx.Main.GetChildren().OfType<FrontEnd.LoadingScreen>().FirstOrDefault() is { } loading && session.Area is { } area)
        {
            loading.Present(session.ModuleName, area.Info.Name, session.Content, area.Info.LoadScreen, area.Info.Tileset, false, "Building the area...");
            await wait(1.5);
            await Shot("j_loading");
            loading.SetStatus("Downloading module content  -  48.2 of 120.5 MB", 0.4f);
            await wait(1.0);
            await Shot("k_loading_progress");
            loading.Finish(0);
            await wait(1.0);
        }
        quit();
    }

    static async Task Press(Godot.Key key, Func<double, Task> wait)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        await wait(0.05);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await wait(0.05);
    }
}

// Player package: NUI window, binds and watches.
void main()
{
    object m = GetModule();
    object pc = GetLocalObject(m, "pc");
    json win = JsonParse("{\"version\":1,\"title\":{\"bind\":\"title\"},\"root\":{\"type\":\"col\",\"children\":[{\"type\":\"label\",\"value\":{\"bind\":\"text\"}},{\"type\":\"button\",\"id\":\"ok\",\"label\":\"OK\"}]},\"geometry\":{\"bind\":\"geometry\"}}");
    int token = NuiCreate(pc, win, "testwin", "player_nuievt");
    SetLocalInt(m, "token", token);
    SetLocalInt(m, "found", NuiFindWindow(pc, "testwin") == token);
    SetLocalString(m, "winid", NuiGetWindowId(pc, token));
    SetLocalString(m, "binds", NuiGetNthBind(pc, token, FALSE, 0) + "," + NuiGetNthBind(pc, token, FALSE, 1) + "," + NuiGetNthBind(pc, token, FALSE, 2));
    NuiSetBind(pc, token, "text", JsonString("hello"));
    SetLocalString(m, "text", JsonGetString(NuiGetBind(pc, token, "text")));
    SetLocalInt(m, "watch_ok", NuiSetBindWatch(pc, token, "text", TRUE));
    SetLocalString(m, "watched", NuiGetNthBind(pc, token, TRUE, 0));
    NuiSetBind(pc, token, "text", JsonString("changed"));
    NuiSetUserData(pc, token, JsonParse("{\"u\":1}"));
    SetLocalString(m, "userdata", JsonDump(NuiGetUserData(pc, token)));
    SetLocalInt(m, "nth", NuiGetNthWindow(pc, 0) == token);
    SetLocalInt(m, "bad_create", NuiCreate(m, win));
}

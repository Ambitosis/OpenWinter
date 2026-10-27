#include "visual_inc"
#include "nw_inc_nui"
// Which NUI titles hide a window's title bar. Six windows that can't be closed, collapsed or resized, each holding a
// half-full progress bar with a line of text drawn over it (as a persistent world's stamina bar does): the title an
// empty string (literal and bound), false, a space, a word, and an empty string on a window that can be closed.
json Bar()
{
    json jText = NuiDrawListText(JsonBool(TRUE), NuiColor(255, 255, 255), NuiRect(4.0, 2.0, 120.0, 22.0), JsonString("Stamina: 6/12"));
    json jBar = NuiHeight(NuiWidth(NuiProgress(JsonFloat(0.5)), 150.0), 26.0);
    jBar = NuiStyleForegroundColor(jBar, NuiColor(0, 102, 0));
    jBar = NuiDrawList(jBar, JsonBool(TRUE), JsonArrayInsert(JsonArray(), jText));
    return NuiCol(JsonArrayInsert(JsonArray(), jBar));
}
int Win(object oPC, string sId, json jTitle, float x, float y, int bClosable)
{
    json jWin = NuiWindow(Bar(), jTitle, NuiBind("geometry"), JsonBool(FALSE), JsonBool(FALSE), JsonBool(bClosable), JsonBool(TRUE), JsonBool(FALSE));
    int nToken = NuiCreate(oPC, jWin, sId, "nt_event");
    NuiSetBind(oPC, nToken, "geometry", NuiRect(x, y, 170.0, 60.0));
    NuiSetBindWatch(oPC, nToken, "geometry", TRUE);
    return nToken;
}
void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    Win(oPC, "t_empty", JsonString(""), 100.0, 120.0, FALSE);
    int nBound = Win(oPC, "t_bound", NuiBind("window_title"), 300.0, 120.0, FALSE);
    NuiSetBind(oPC, nBound, "window_title", JsonString(""));
    Win(oPC, "t_false", JsonBool(FALSE), 500.0, 120.0, FALSE);
    Win(oPC, "t_space", JsonString(" "), 100.0, 300.0, FALSE);
    Win(oPC, "t_word", JsonString("Word"), 300.0, 300.0, FALSE);
    Win(oPC, "t_closable", JsonString(""), 500.0, 300.0, TRUE);
    DelayCommand(4.0, VShot("windows"));
    DelayCommand(6.0, VShot("later"));
    DelayCommand(8.0, VDone());
}

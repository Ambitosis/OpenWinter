// NUI renderer test: a list with a row template bound to arrays, plus a group whose
// layout is swapped with NuiSetGroupLayout. Events go to nui_list_ev.
#include "nw_inc_nui"

void main()
{
    object oPC = OBJECT_SELF;

    json jTemplate = JsonArray();
    jTemplate = JsonArrayInsert(jTemplate, NuiListTemplateCell(NuiImage(NuiBind("icons"), JsonInt(NUI_ASPECT_FIT), JsonInt(NUI_HALIGN_CENTER), JsonInt(NUI_VALIGN_MIDDLE)), 28.0, FALSE));
    jTemplate = JsonArrayInsert(jTemplate, NuiListTemplateCell(NuiLabel(NuiBind("names"), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_MIDDLE)), 0.0, TRUE));
    jTemplate = JsonArrayInsert(jTemplate, NuiListTemplateCell(NuiWidth(NuiLabel(NuiBind("levels"), JsonInt(NUI_HALIGN_RIGHT), JsonInt(NUI_VALIGN_MIDDLE)), 40.0), 40.0, FALSE));
    jTemplate = JsonArrayInsert(jTemplate, NuiListTemplateCell(NuiCheck(JsonString(""), NuiBind("done")), 24.0, FALSE));
    jTemplate = JsonArrayInsert(jTemplate, NuiListTemplateCell(NuiId(NuiButton(JsonString("Pick")), "btn_pick"), 64.0, FALSE));

    json jCol = JsonArray();
    jCol = JsonArrayInsert(jCol, NuiList(jTemplate, NuiBind("names"), 30.0));
    json jRow = JsonArray();
    jRow = JsonArrayInsert(jRow, NuiId(NuiWidth(NuiButton(JsonString("Swap group")), 120.0), "btn_swap"));
    jRow = JsonArrayInsert(jRow, NuiLabel(NuiBind("status"), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_MIDDLE)));
    jCol = JsonArrayInsert(jCol, NuiHeight(NuiRow(jRow), 32.0));

    json jInner = JsonArray();
    jInner = JsonArrayInsert(jInner, NuiHeight(NuiLabel(JsonString("Group layout A: a scrolling column"), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_MIDDLE)), 24.0));
    int i;
    for (i = 1; i <= 6; i++)
        jInner = JsonArrayInsert(jInner, NuiHeight(NuiButton(JsonString("Group button " + IntToString(i))), 28.0));
    jCol = JsonArrayInsert(jCol, NuiHeight(NuiId(NuiGroup(NuiCol(jInner), TRUE, NUI_SCROLLBARS_AUTO), "grp"), 120.0));

    json jWin = NuiWindow(NuiCol(jCol), JsonString("NUI List"), NuiRect(700.0, 60.0, 460.0, 560.0), JsonBool(TRUE), JsonNull(), JsonBool(TRUE), JsonBool(FALSE), JsonBool(TRUE));
    int nToken = NuiCreate(oPC, jWin, "list", "nui_list_ev");

    json jNames = JsonArray(), jLevels = JsonArray(), jIcons = JsonArray(), jDone = JsonArray();
    string sIcons = "is_fireball,ir_attack,ir_cast,is_bless,is_barkskin,is_aid";
    for (i = 0; i < 40; i++)
    {
        jNames = JsonArrayInsert(jNames, JsonString("Adventurer #" + IntToString(i + 1)));
        jLevels = JsonArrayInsert(jLevels, JsonInt(1 + (i * 7) % 20));
        int n = i % 6;
        string sIcon = n == 0 ? "is_fireball" : n == 1 ? "ir_attack" : n == 2 ? "ir_cast" : n == 3 ? "is_bless" : n == 4 ? "is_barkskin" : "is_aid";
        jIcons = JsonArrayInsert(jIcons, JsonString(sIcon));
        jDone = JsonArrayInsert(jDone, JsonBool(i % 3 == 0));
    }
    NuiSetBind(oPC, nToken, "names", jNames);
    NuiSetBind(oPC, nToken, "levels", jLevels);
    NuiSetBind(oPC, nToken, "icons", jIcons);
    NuiSetBind(oPC, nToken, "done", jDone);
    NuiSetBind(oPC, nToken, "status", JsonString("Pick a row."));
}

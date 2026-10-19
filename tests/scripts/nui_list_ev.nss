// Event handler for nui_list: row clicks (array index) and group layout swaps.
#include "nw_inc_nui"

void main()
{
    object oPC = NuiGetEventPlayer();
    int nToken = NuiGetEventWindow();
    string sType = NuiGetEventType();
    string sElem = NuiGetEventElement();
    if (sType != "click") return;
    if (sElem == "btn_pick")
    {
        int nRow = NuiGetEventArrayIndex();
        json jNames = NuiGetBind(oPC, nToken, "names");
        NuiSetBind(oPC, nToken, "status", JsonString("Picked row " + IntToString(nRow) + ": " + JsonGetString(JsonArrayGet(jNames, nRow))));
        jNames = JsonArraySet(jNames, nRow, JsonString("** picked **"));
        NuiSetBind(oPC, nToken, "names", jNames);
    }
    else if (sElem == "btn_swap")
    {
        json jRow = JsonArray();
        jRow = JsonArrayInsert(jRow, NuiLabel(JsonString("Layout B"), JsonInt(NUI_HALIGN_CENTER), JsonInt(NUI_VALIGN_MIDDLE)));
        jRow = JsonArrayInsert(jRow, NuiProgress(JsonFloat(0.5)));
        NuiSetGroupLayout(oPC, nToken, "grp", NuiRow(jRow));
        NuiSetBind(oPC, nToken, "status", JsonString("Group swapped."));
    }
}

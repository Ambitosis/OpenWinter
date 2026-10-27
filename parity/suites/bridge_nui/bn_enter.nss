// Opens the bind test window on the entering player: "name" (a text field, one value) and "rows" (a list of three
// text fields, an array), both watched.
#include "nw_inc_nui"

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    json jRow = NuiListTemplateCell(NuiTextEdit(JsonString(""), NuiBind("rows"), 32, FALSE), 0.0, TRUE);
    json jCol = JsonArray();
    jCol = JsonArrayInsert(jCol, NuiRow(JsonArrayInsert(JsonArray(), NuiId(NuiTextEdit(JsonString(""), NuiBind("name"), 32, FALSE), "name_edit"))));
    jCol = JsonArrayInsert(jCol, NuiId(NuiList(JsonArrayInsert(JsonArray(), jRow), NuiBind("count"), 30.0), "rows_list"));
    json jWin = NuiWindow(NuiCol(jCol), JsonString("Bind test"), NuiRect(100.0, 100.0, 300.0, 250.0), JsonBool(FALSE), JsonBool(FALSE), JsonBool(TRUE), JsonBool(FALSE), JsonBool(TRUE));
    int nToken = NuiCreate(oPC, jWin, "bindtest", "bn_event");
    json jRows = JsonArray();
    jRows = JsonArrayInsert(jRows, JsonString("a"));
    jRows = JsonArrayInsert(jRows, JsonString("b"));
    jRows = JsonArrayInsert(jRows, JsonString("c"));
    NuiSetBind(oPC, nToken, "name", JsonString("start"));
    NuiSetBind(oPC, nToken, "rows", jRows);
    NuiSetBind(oPC, nToken, "count", JsonInt(3));
    NuiSetBindWatch(oPC, nToken, "name", TRUE);
    NuiSetBindWatch(oPC, nToken, "rows", TRUE);
    WriteTimestampedLogEntry("BINDTEST|open|token=" + IntToString(nToken));
}

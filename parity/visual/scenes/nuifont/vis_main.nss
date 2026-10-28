#include "visual_inc"
#include "nw_inc_nui"
// The width and height of NUI label text: long left-aligned lines on the window background, then narrow fixed-width
// cells with and without padding, as persistent worlds size them for the original client.
json Line(string s, float w, float fPadding)
{
    json j = NuiLabel(JsonString(s), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_MIDDLE));
    j = NuiWidth(NuiHeight(j, 26.0), w);
    j = NuiMargin(j, 0.0);
    if (fPadding >= 0.0) j = NuiPadding(j, fPadding);
    return j;
}
json Row(json a, json b = JSON_NULL, json c = JSON_NULL)
{
    json r = JsonArrayInsert(JsonArray(), a);
    if (b != JSON_NULL) r = JsonArrayInsert(r, b);
    if (c != JSON_NULL) r = JsonArrayInsert(r, c);
    r = JsonArrayInsert(r, NuiSpacer());
    return NuiRow(r);
}
// A filled box over an element's whole rectangle, so its position and size show in the shot.
json Box(json j, float w, float h, int r, int g, int b)
{
    json jRect = NuiDrawListRect(JsonBool(TRUE), NuiColor(r, g, b), JsonBool(TRUE), JsonFloat(1.0), NuiRect(0.0, 0.0, w, h), NUI_DRAW_LIST_ITEM_ORDER_BEFORE);
    return NuiDrawList(NuiHeight(NuiWidth(j, w), h), JsonBool(FALSE), JsonArrayInsert(JsonArray(), jRect));
}
json Cell(int n, int bZeroMargin)
{
    json j = Box(NuiSpacer(), 60.0, 20.0, n == 0 ? 255 : 0, n == 1 ? 255 : 0, n == 2 ? 255 : 0);
    return bZeroMargin ? NuiMargin(j, 0.0) : j;
}
json Text(string s, float fPadding)
{
    json j = Box(NuiLabel(JsonString(s), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_TOP)), 110.0, 26.0, 0, 0, 160);
    j = NuiMargin(j, 0.0);
    if (fPadding >= 0.0) j = NuiPadding(j, fPadding);
    return j;
}
// Spacing: boxes in rows with default and zero margins, boxes straight in the column, and label text against its box.
void Spacing(object oPC)
{
    json jCol = JsonArray();
    jCol = JsonArrayInsert(jCol, Row(Cell(0, FALSE), Cell(1, FALSE), Cell(2, FALSE)));
    jCol = JsonArrayInsert(jCol, Row(Cell(0, TRUE), Cell(1, TRUE), Cell(2, TRUE)));
    jCol = JsonArrayInsert(jCol, Cell(0, TRUE));
    jCol = JsonArrayInsert(jCol, Cell(1, TRUE));
    jCol = JsonArrayInsert(jCol, Cell(2, FALSE));
    jCol = JsonArrayInsert(jCol, Row(Text("Hdgy default", -1.0), Text("Hdgy zero", 0.0), Text("Hdgy five", 5.0)));
    jCol = JsonArrayInsert(jCol, NuiSpacer());
    json jWin = NuiWindow(NuiCol(jCol), JsonString("Spacing"), NuiBind("geometry"), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE));
    int nToken = NuiCreate(oPC, jWin, "nuispacing", "nt_event");
    NuiSetBind(oPC, nToken, "geometry", NuiRect(700.0, 100.0, 400.0, 300.0));
}
json Cells(int bZeroMargin)
{
    return JsonArrayInsert(JsonArrayInsert(JsonArray(), Cell(0, bZeroMargin)), Cell(1, bZeroMargin));
}
// Margins: explicit widget margins, rows and columns with zero margins, nested columns, a group and a button, and a
// padded label whose text runs past its width.
void Margins(object oPC)
{
    json jCol = JsonArray();
    jCol = JsonArrayInsert(jCol, Row(NuiMargin(Cell(0, TRUE), 6.0), NuiMargin(Cell(1, TRUE), 6.0), Cell(2, TRUE)));
    jCol = JsonArrayInsert(jCol, NuiMargin(Row(Cell(0, TRUE), Cell(1, TRUE), Cell(2, TRUE)), 0.0));
    jCol = JsonArrayInsert(jCol, Row(NuiCol(Cells(TRUE)), NuiMargin(NuiCol(Cells(TRUE)), 0.0), Cell(2, TRUE)));
    jCol = JsonArrayInsert(jCol, NuiCol(Cells(TRUE)));
    json jButton = NuiDrawList(NuiHeight(NuiWidth(NuiButton(JsonString("B")), 60.0), 20.0), JsonBool(FALSE), JsonArrayInsert(JsonArray(),
        NuiDrawListRect(JsonBool(TRUE), NuiColor(255, 0, 255), JsonBool(FALSE), JsonFloat(1.0), NuiRect(0.0, 0.0, 60.0, 20.0))));
    json jGroup = NuiHeight(NuiWidth(NuiGroup(Cell(2, TRUE), TRUE, NUI_SCROLLBARS_NONE), 90.0), 40.0);
    json jClip = Box(NuiLabel(JsonString("Fortitude: Fortitude:"), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_TOP)), 75.0, 26.0, 0, 0, 160);
    jCol = JsonArrayInsert(jCol, Row(jButton, jGroup, NuiPadding(NuiMargin(jClip, 0.0), 5.0)));
    jCol = JsonArrayInsert(jCol, NuiSpacer());
    json jWin = NuiWindow(NuiCol(jCol), JsonString("Margins"), NuiBind("geometry"), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE));
    int nToken = NuiCreate(oPC, jWin, "nuimargins", "nt_event");
    NuiSetBind(oPC, nToken, "geometry", NuiRect(100.0, 380.0, 400.0, 300.0));
}
// Default margins of each widget kind: a 60 x 20 widget between two zero-margin boxes.
json Between(json jWidget)
{
    return Row(Cell(0, TRUE), NuiHeight(NuiWidth(jWidget, 60.0), 20.0), Cell(2, TRUE));
}
void Widgets(object oPC)
{
    json jCol = JsonArray();
    jCol = JsonArrayInsert(jCol, Between(NuiLabel(JsonString("L"), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_TOP))));
    jCol = JsonArrayInsert(jCol, Between(NuiText(JsonString("T"), FALSE, NUI_SCROLLBARS_NONE)));
    jCol = JsonArrayInsert(jCol, Between(NuiImage(JsonString("gui_mp_examineu"), JsonInt(NUI_ASPECT_STRETCH), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_TOP))));
    jCol = JsonArrayInsert(jCol, Between(NuiProgress(JsonFloat(0.5))));
    jCol = JsonArrayInsert(jCol, Between(NuiCheck(JsonString("C"), JsonBool(FALSE))));
    jCol = JsonArrayInsert(jCol, Between(NuiTextEdit(JsonString(""), JsonString("E"), 10, FALSE)));
    jCol = JsonArrayInsert(jCol, Between(NuiCombo(JsonArrayInsert(JsonArray(), NuiComboEntry("A", 0)), JsonInt(0))));
    jCol = JsonArrayInsert(jCol, Between(NuiSpacer()));
    jCol = JsonArrayInsert(jCol, NuiSpacer());
    json jWin = NuiWindow(NuiCol(jCol), JsonString("Widgets"), NuiBind("geometry"), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE));
    int nToken = NuiCreate(oPC, jWin, "nuiwidgets", "nt_event");
    NuiSetBind(oPC, nToken, "geometry", NuiRect(700.0, 410.0, 400.0, 300.0));
}
void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    json jCol = JsonArray();
    jCol = JsonArrayInsert(jCol, Row(Line("Attacks/Round: Fortitude: Attack Damage:", 520.0, 0.0)));
    jCol = JsonArrayInsert(jCol, Row(Line("The quick brown fox jumps over the lazy dog 0123456789", 520.0, 0.0)));
    jCol = JsonArrayInsert(jCol, Row(Line("WWWWWWWWWW iiiiiiiiii MMMMMMMMMM llllllllll", 520.0, 0.0)));
    jCol = JsonArrayInsert(jCol, Row(Line("Attacks/Round: ", 115.0, 5.0), Line("Fortitude: ", 75.0, 5.0), Line("Attack Damage:", 115.0, -1.0)));
    jCol = JsonArrayInsert(jCol, Row(Line("Attacks/Round: ", 115.0, 0.0), Line("Fortitude: ", 75.0, 0.0), Line("Attack Damage:", 115.0, 0.0)));
    jCol = JsonArrayInsert(jCol, NuiSpacer());
    json jWin = NuiWindow(NuiCol(jCol), JsonString("Font"), NuiBind("geometry"), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE));
    int nToken = NuiCreate(oPC, jWin, "nuifont", "nt_event");
    NuiSetBind(oPC, nToken, "geometry", NuiRect(100.0, 100.0, 560.0, 240.0));
    Spacing(oPC);
    Margins(oPC);
    Widgets(oPC);
    DelayCommand(4.0, VShot("labels"));
    DelayCommand(6.0, VDone());
}

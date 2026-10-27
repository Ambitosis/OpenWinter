#include "visual_inc"
#include "nw_inc_nui"
// The array index the original client reports with a bind it changed: a text field and a check box bound to single
// values, and a list whose rows hold a check box and a text field bound to arrays, all watched. The clicks and typing
// use the real pointer (run with --real-mouse), the typing is posted; nb_event logs each watch with NuiGetEventArrayIndex and the bind's value.
void Log(string s) { WriteTimestampedLogEntry("VISUAL|"+s); }
string At(float x, float y) { return FloatToString(x/1280.0,0,4)+","+FloatToString(y/720.0,0,4); }
void Up(string at) { Log("mouse|"+at+"|up"); }
void Down(string at) { Log("mouse|"+at+"|down"); }
// Rests the pointer first: the window reads a press only where the pointer already hovers.
void Click(float x, float y) { Log("pointer|"+At(x,y)); DelayCommand(0.5,Down(At(x,y))); DelayCommand(0.7,Up(At(x,y))); }
void Type(string s) { Log("text|"+s); }

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    json jCells = JsonArray();
    jCells = JsonArrayInsert(jCells, NuiListTemplateCell(NuiId(NuiCheck(JsonString("Row"), NuiBind("flags")), "row_check"), 100.0, FALSE));
    jCells = JsonArrayInsert(jCells, NuiListTemplateCell(NuiId(NuiTextEdit(JsonString(""), NuiBind("rows"), 32, FALSE), "row_edit"), 0.0, TRUE));
    json jCol = JsonArray();
    jCol = JsonArrayInsert(jCol, NuiHeight(NuiId(NuiTextEdit(JsonString(""), NuiBind("name"), 32, FALSE), "name_edit"), 32.0));
    jCol = JsonArrayInsert(jCol, NuiHeight(NuiId(NuiCheck(JsonString("Single"), NuiBind("flag")), "single_check"), 32.0));
    jCol = JsonArrayInsert(jCol, NuiId(NuiList(jCells, NuiBind("count"), 32.0), "rows_list"));
    json jWin = NuiWindow(NuiCol(jCol), JsonString("Bind index"), NuiRect(400.0, 150.0, 400.0, 360.0), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(FALSE), JsonBool(TRUE));
    int nToken = NuiCreate(oPC, jWin, "bindindex", "nb_event");
    json jRows = JsonArrayInsert(JsonArrayInsert(JsonArrayInsert(JsonArray(), JsonString("a")), JsonString("b")), JsonString("c"));
    json jFlags = JsonArrayInsert(JsonArrayInsert(JsonArrayInsert(JsonArray(), JsonBool(FALSE)), JsonBool(FALSE)), JsonBool(FALSE));
    NuiSetBind(oPC, nToken, "name", JsonString("n"));
    NuiSetBind(oPC, nToken, "flag", JsonBool(FALSE));
    NuiSetBind(oPC, nToken, "rows", jRows);
    NuiSetBind(oPC, nToken, "flags", jFlags);
    NuiSetBind(oPC, nToken, "count", JsonInt(3));
    NuiSetBindWatch(oPC, nToken, "name", TRUE);
    NuiSetBindWatch(oPC, nToken, "flag", TRUE);
    NuiSetBindWatch(oPC, nToken, "rows", TRUE);
    NuiSetBindWatch(oPC, nToken, "flags", TRUE);
    DelayCommand(3.0, VShot("window"));
    // The single check box, then the check box in the second and third rows.
    DelayCommand(6.0, Click(418.0, 244.0));
    DelayCommand(8.0, Click(424.0, 322.0));
    DelayCommand(10.0, Click(424.0, 358.0));
    // The single text field, then the text field in the second row: click and type.
    DelayCommand(12.0, Click(600.0, 208.0));
    DelayCommand(13.0, Type("x"));
    DelayCommand(15.0, Click(640.0, 322.0));
    DelayCommand(16.0, Type("y"));
    DelayCommand(18.0, VShot("after"));
    DelayCommand(21.0, VDone());
}

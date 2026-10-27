// Logs every OnPlayerGuiEvent: type, integer, object (tag, type, whether it is the player), vector. On a disabled
// panel's attempt to open it opens a small NUI window naming the panel and the object.
#include "nw_inc_nui"

void main()
{
    object oPC = GetLastGuiEventPlayer();
    int nType = GetLastGuiEventType();
    int nInt = GetLastGuiEventInteger();
    object oObj = GetLastGuiEventObject();
    vector v = GetLastGuiEventVector();
    string sObj = !GetIsObjectValid(oObj) ? "INVALID" : oObj == oPC ? "PC" : GetTag(oObj) + "/" + IntToString(GetObjectType(oObj));
    string sLine = "GUIEV|" + IntToString(nType) + "|" + IntToString(nInt) + "|" + sObj + "|"
        + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2) + "|pc=" + IntToString(GetIsPC(oPC));
    WriteTimestampedLogEntry(sLine);
    if (nType != 1 && nType != 2) SendMessageToPC(oPC, sLine);
    if (nType == GUIEVENT_DISABLED_PANEL_ATTEMPT_OPEN)
    {
        json jCol = JsonArrayInsert(JsonArray(), NuiLabel(JsonString("Panel " + IntToString(nInt) + " on " + sObj), JsonInt(NUI_HALIGN_CENTER), JsonInt(NUI_VALIGN_MIDDLE)));
        json jWin = NuiWindow(NuiCol(jCol), JsonString("Replacement"), NuiRect(200.0, 120.0, 320.0, 120.0), JsonBool(FALSE), JsonBool(FALSE), JsonBool(TRUE), JsonBool(FALSE), JsonBool(TRUE));
        NuiCreate(oPC, jWin, "bg_repl" + IntToString(nInt));
    }
}

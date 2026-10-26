// NWNX_Dialog outside a conversation (nwserver needs a player to talk to).
#include "parity_inc"
#include "nwnx_dialog"

void main()
{
    PLogI("node.type", NWNX_Dialog_GetCurrentNodeType());
    PLogI("script.type", NWNX_Dialog_GetCurrentScriptType());
    PLogI("node.id", NWNX_Dialog_GetCurrentNodeID());
    PLogI("node.index", NWNX_Dialog_GetCurrentNodeIndex());
    PLog("node.text", NWNX_Dialog_GetCurrentNodeText());
    NWNX_Dialog_SetCurrentNodeText("ignored");
    PLog("node.text.after", NWNX_Dialog_GetCurrentNodeText());
    object oC = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre());
    NWNX_Dialog_End(oC);
    PLogI("end.ok", GetIsObjectValid(oC));
    PDone();
}

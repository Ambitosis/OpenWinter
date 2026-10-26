#include "nwnx_chat"
void main()
{
    object oMod = GetModule();
    string sMsg = NWNX_Chat_GetMessage();
    object oSender = NWNX_Chat_GetSender();
    string s = IntToString(NWNX_Chat_GetChannel()) + ":" + sMsg + ":" + GetTag(oSender) + ":" + GetTag(NWNX_Chat_GetTarget()) + ":" + GetTag(OBJECT_SELF)
        + ":" + IntToString(oSender == OBJECT_INVALID) + IntToString(OBJECT_SELF == oMod);
    SetLocalString(oMod, "CHAT", GetLocalString(oMod, "CHAT") + "[" + s + "]");
    if (sMsg == "skip me") NWNX_Chat_SkipMessage();
}

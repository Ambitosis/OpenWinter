// Panel commands said in chat: "dis N [TAG]" and "en N [TAG]" (SetGuiPanelDisabled, TAG the target: an object's
// tag, or "pc"), "pop N" (PopUpGUIPanel), "exam TAG" (ActionExamine), "flip N" (disabled then enabled at once) and
// "flipd N" (disabled, then enabled by DelayCommand 0.0).
object Target(object oPC, string sTag)
{
    if (sTag == "") return OBJECT_INVALID;
    if (sTag == "pc") return oPC;
    if (sTag == "item") return GetItemPossessedBy(oPC, "BG_ITEM");
    return GetObjectByTag(sTag);
}

void main()
{
    object oPC = GetPCChatSpeaker();
    string sLine = GetPCChatMessage();
    int nSpace = FindSubString(sLine, " ");
    if (nSpace < 0) return;
    string sCmd = GetStringLeft(sLine, nSpace);
    string sRest = GetSubString(sLine, nSpace + 1, 100);
    int nSpace2 = FindSubString(sRest, " ");
    int nPanel = StringToInt(nSpace2 < 0 ? sRest : GetStringLeft(sRest, nSpace2));
    object oTarget = Target(oPC, nSpace2 < 0 ? "" : GetSubString(sRest, nSpace2 + 1, 100));
    if (sCmd == "dis") SetGuiPanelDisabled(oPC, nPanel, TRUE, oTarget);
    else if (sCmd == "en") SetGuiPanelDisabled(oPC, nPanel, FALSE, oTarget);
    else if (sCmd == "pop") PopUpGUIPanel(oPC, nPanel);
    else if (sCmd == "exam") AssignCommand(oPC, ActionExamine(Target(oPC, sRest)));
    else if (sCmd == "flip") { SetGuiPanelDisabled(oPC, nPanel, TRUE); SetGuiPanelDisabled(oPC, nPanel, FALSE); }
    else if (sCmd == "flipd") { SetGuiPanelDisabled(oPC, nPanel, TRUE); DelayCommand(0.0, SetGuiPanelDisabled(oPC, nPanel, FALSE)); }
    else return;
    WriteTimestampedLogEntry("GUIEV|cmd|" + sLine);
    SetPCChatMessage("");
}

// convobusy: every reading is "VISUAL|note|convobusy|<case>|<what>|pcTalking=..|npcTalking=..|pcCombat=..|npcCombat=..|pcAction=..|npcAction=.."
object Npc() { return GetObjectByTag("probe_npc"); }
void Note(string sWhat)
{
    object pc = GetFirstPC(); object n = Npc();
    WriteTimestampedLogEntry("VISUAL|note|convobusy|" + GetLocalString(GetModule(), "case") + "|" + sWhat
        + "|pcTalking=" + IntToString(IsInConversation(pc)) + "|npcTalking=" + IntToString(IsInConversation(n))
        + "|pcCombat=" + IntToString(GetIsInCombat(pc)) + "|npcCombat=" + IntToString(GetIsInCombat(n))
        + "|pcAction=" + IntToString(GetCurrentAction(pc)) + "|npcAction=" + IntToString(GetCurrentAction(n)));
}

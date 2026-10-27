// The state every reading carries: the case, who is talking, cutscene mode and the cutscene aborts so far.
void CcNote(string phase) {
 object pc=GetFirstPC(); object n=GetObjectByTag("probe_npc");
 WriteTimestampedLogEntry("VISUAL|note|cutconvo|"+GetLocalString(GetModule(),"case")+"|"+phase+"|pcTalking="+IntToString(IsInConversation(pc))
  +"|npcTalking="+IntToString(IsInConversation(n))+"|cutscene="+IntToString(GetCutsceneMode(pc))+"|aborts="+IntToString(GetLocalInt(GetModule(),"aborts")));
}

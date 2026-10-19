void main() { object pc=GetFirstPC(); object n=GetObjectByTag("probe_npc");
 WriteTimestampedLogEntry("VISUAL|note|convoend|"+GetLocalString(GetModule(),"case")+"|stopen|pcTalking="+IntToString(IsInConversation(pc))+"|dist="+FloatToString(GetDistanceBetween(pc,n),0,2)); }

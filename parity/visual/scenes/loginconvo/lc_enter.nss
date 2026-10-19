// OnClientEnter queues a conversation with the entering player, then samples the loading window.
void Poll(int i) {
 object pc=GetFirstPC(); object n=GetObjectByTag("probe_npc");
 WriteTimestampedLogEntry("VISUAL|note|loginconvo|poll"+IntToString(i)+"|pcArea="+IntToString(GetIsObjectValid(GetArea(pc)))+"|npcTalking="+IntToString(IsInConversation(n))+"|pcTalking="+IntToString(IsInConversation(pc))+"|npcAction="+IntToString(GetCurrentAction(n)));
 if(i<40)DelayCommand(0.25,Poll(i+1));
}
void main() {
 object pc=GetEnteringObject(); object n=GetObjectByTag("probe_npc");
 WriteTimestampedLogEntry("VISUAL|note|loginconvo|cliententer|pcArea="+IntToString(GetIsObjectValid(GetArea(pc)))+"|npc="+IntToString(GetIsObjectValid(n)));
 AssignCommand(n,ActionStartConversation(pc,"lc_talk",FALSE,FALSE));
 Poll(0);
}

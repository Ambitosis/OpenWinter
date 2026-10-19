// A conversation node's action script opening the store, as merchant dialogs do.
void main() {
 object pc=GetPCSpeaker();
 WriteTimestampedLogEntry("VISUAL|note|convoend|"+GetLocalString(GetModule(),"case")+"|openstore_script|speaker="+IntToString(GetIsObjectValid(pc))+"|pcTalking="+IntToString(IsInConversation(GetFirstPC())));
 OpenStore(GetObjectByTag("probe_store"),GetIsObjectValid(pc)?pc:GetFirstPC());
}

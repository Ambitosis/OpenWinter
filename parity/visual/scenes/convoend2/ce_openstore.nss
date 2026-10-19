// A conversation node's action script opening the store, as merchant dialogs do.
void main() {
 WriteTimestampedLogEntry("VISUAL|note|convoend|"+GetLocalString(GetModule(),"case")+"|openstore_script|pcTalking="+IntToString(IsInConversation(GetPCSpeaker())));
 OpenStore(GetObjectByTag("probe_store"),GetPCSpeaker());
}

// Opens the store half a second after the line that asked for it is shown.
void Open(object pc) { WriteTimestampedLogEntry("VISUAL|note|convoend|"+GetLocalString(GetModule(),"case")+"|delayed_open|pcTalking="+IntToString(IsInConversation(pc))); OpenStore(GetObjectByTag("probe_store"),pc); }
void main() { object pc=GetPCSpeaker(); DelayCommand(0.5,Open(pc)); }

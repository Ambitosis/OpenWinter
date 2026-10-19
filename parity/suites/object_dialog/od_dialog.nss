#include "parity_inc"
void main(){string k=GetTag(OBJECT_SELF)+"."+GetLocalString(GetModule(),"phase");PLogI(k+".event",GetCurrentlyRunningEvent(FALSE));PLog(k+".speaker",GetTag(GetLastSpeaker()));PLogI(k+".pc",GetIsPC(GetPCSpeaker()));SetLocalInt(OBJECT_SELF,"count",GetLocalInt(OBJECT_SELF,"count")+1);}

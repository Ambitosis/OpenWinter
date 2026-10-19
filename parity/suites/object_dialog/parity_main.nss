#include "parity_inc"
void Signal(){SetLocalString(GetModule(),"phase","signal");SignalEvent(GetObjectByTag("PLC"),EventConversation());SignalEvent(GetObjectByTag("DOOR"),EventConversation());}
void Talk(){SetLocalString(GetModule(),"phase","npc_action");object c=GetObjectByTag("CRIT");AssignCommand(c,ActionStartConversation(GetObjectByTag("PLC"),"",FALSE,FALSE));}
void Finish(){PLogI("plc.count",GetLocalInt(GetObjectByTag("PLC"),"count"));PLogI("door.count",GetLocalInt(GetObjectByTag("DOOR"),"count"));PDone();}
void main(){PLog("plc.loaded",GetEventScript(GetObjectByTag("PLC"),EVENT_SCRIPT_PLACEABLE_ON_DIALOGUE));PLog("door.loaded",GetEventScript(GetObjectByTag("DOOR"),EVENT_SCRIPT_DOOR_ON_DIALOGUE));DelayCommand(.5,Signal());DelayCommand(1.0,Talk());DelayCommand(5.0,Finish());}

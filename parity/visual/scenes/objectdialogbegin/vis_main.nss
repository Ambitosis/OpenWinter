#include "visual_inc"
void Start(object p){AssignCommand(GetFirstPC(),ActionStartConversation(p,"od_line",FALSE,FALSE));}
void Finish(){WriteTimestampedLogEntry("VISUAL|note|finish|pc="+IntToString(GetIsPC(GetFirstPC()))+"|conversation="+IntToString(IsInConversation(GetFirstPC())));VDone();}
void main(){object pc=GetFirstPC();object p=CreateObject(OBJECT_TYPE_PLACEABLE,"plc_chair",Location(GetArea(pc),Vector(22.0,20.0,0.0),0.0),FALSE,"PLC");SetEventScript(p,EVENT_SCRIPT_PLACEABLE_ON_DIALOGUE,"od_begin");DelayCommand(1.0,Start(p));DelayCommand(5.0,Finish());}

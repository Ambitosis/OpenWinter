#include "visual_inc"
void main(){WriteTimestampedLogEntry("VISUAL|note|"+GetLocalString(OBJECT_SELF,"hp_case")+".impact|"+IntToString(GetLocalInt(GetModule(),"hp_clock"))+".spell."+IntToString(GetSpellId()));}

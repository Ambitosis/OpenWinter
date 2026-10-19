#include "visual_inc"
void Setup(object pc) {object o=VPlaceable(pc,"plc_chest1",3.0,0.0);SetTag(o,"RD_CHEST");SetLocked(o,FALSE);WriteTimestampedLogEntry("VISUAL|note|portrait="+GetPortraitResRef(o)+"|door="+GetPortraitResRef(GetObjectByTag("RD_DOOR")));}
void Hover(float t,string point,string name) { DelayCommand(t,WriteTimestampedLogEntry("VISUAL|hover|"+point));DelayCommand(t+1.0,VShot(name)); }
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(1.0,Setup(pc));DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));DelayCommand(3.0,VShot("a_world"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|hover|0.754,0.63"));DelayCommand(4.3,WriteTimestampedLogEntry("VISUAL|mouse|0.754,0.63|down|right"));
 Hover(5.0,"0.832,0.63","b_use");Hover(7.0,"0.676,0.63","c_lock");Hover(9.0,"0.699,0.728","d_bash");Hover(11.0,"0.809,0.728","e_trap");
 DelayCommand(13.0,WriteTimestampedLogEntry("VISUAL|key|99|tap"));DelayCommand(14.0,VShot("f_trapmenu"));
 DelayCommand(15.0,WriteTimestampedLogEntry("VISUAL|key|101|tap"));DelayCommand(15.5,WriteTimestampedLogEntry("VISUAL|key|101|tap"));DelayCommand(16.0,WriteTimestampedLogEntry("VISUAL|mouse|0.754,0.63|up|right"));
 DelayCommand(18.0,WriteTimestampedLogEntry("VISUAL|hover|0.50,0.40"));DelayCommand(18.3,WriteTimestampedLogEntry("VISUAL|mouse|0.50,0.40|down|right"));DelayCommand(20.0,VShot("g_door"));DelayCommand(21.0,WriteTimestampedLogEntry("VISUAL|mouse|0.50,0.40|up|right"));
 DelayCommand(23.0,VDone());}

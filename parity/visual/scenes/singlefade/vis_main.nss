#include "visual_inc"
void Apply(object pc){
 WriteTimestampedLogEntry("VISUAL|note|models|"+Get2DAString("visualeffects","Imp_Root_M_Node",42)+"|"+Get2DAString("visualeffects","Imp_Root_M_Node",36));
 VFxAt(pc,42,-3.0,2.0);VFxAt(pc,36,3.0,2.0);
}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);VHidePC(pc);
 DelayCommand(.5,VCamera(pc,90.0,12.0,65.0));
 DelayCommand(1.0,VShot("baseline"));
 DelayCommand(2.0,Apply(pc));
 DelayCommand(2.5,VShot("age050"));
 DelayCommand(3.0,VShot("age100"));
 DelayCommand(3.7,VShot("age170"));
 DelayCommand(4.3,VShot("age230"));
 DelayCommand(4.9,VShot("age290"));
 DelayCommand(5.5,VShot("age350"));
 DelayCommand(6.5,VShot("age450"));
 DelayCommand(7.5,VShot("age550"));
 DelayCommand(9.0,VShot("age700"));
 DelayCommand(10.5,VShot("age850"));
 DelayCommand(11.0,VDone());
}

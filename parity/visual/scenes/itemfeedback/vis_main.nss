#include "visual_inc"
void Grant(object pc){object i=CreateItemOnObject("nw_it_mpotion001",pc,2,"fixture_potion");SetLocalObject(pc,"potion",i);}
void Use(object pc){object i=GetLocalObject(pc,"potion");AssignCommand(pc,ActionUseItemOnObject(i,GetFirstItemProperty(i),pc));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 DelayCommand(2.0,Grant(pc));DelayCommand(3.0,VShot("grant"));
 DelayCommand(5.0,Use(pc));DelayCommand(8.5,VShot("first_used"));
 DelayCommand(12.0,Use(pc));DelayCommand(15.5,VShot("last_used"));DelayCommand(20.0,VDone());}

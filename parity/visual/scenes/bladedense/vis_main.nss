#include "visual_inc"
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);VHidePC(pc);
 DelayCommand(1.0,VCamera(pc,270.0,9.0,75.0));
 DelayCommand(2.0,ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,EffectAreaOfEffect(AOE_PER_WALLBLADE),GetLocation(pc),3.0));
 DelayCommand(2.05,VShot("blade_00"));
 DelayCommand(2.10,VShot("blade_01"));
 DelayCommand(2.15,VShot("blade_02"));
 DelayCommand(2.20,VShot("blade_03"));
 DelayCommand(2.25,VShot("blade_04"));
 DelayCommand(2.30,VShot("blade_05"));
 DelayCommand(2.35,VShot("blade_06"));
 DelayCommand(2.40,VShot("blade_07"));
 DelayCommand(2.45,VShot("blade_08"));
 DelayCommand(2.50,VShot("blade_09"));
 DelayCommand(2.55,VShot("blade_10"));
 DelayCommand(2.60,VShot("blade_11"));
 DelayCommand(2.65,VShot("blade_12"));
 DelayCommand(2.70,VShot("blade_13"));
 DelayCommand(2.75,VShot("blade_14"));
 DelayCommand(2.80,VShot("blade_15"));
 DelayCommand(2.85,VShot("blade_16"));
 DelayCommand(2.90,VShot("blade_17"));
 DelayCommand(2.95,VShot("blade_18"));
 DelayCommand(3.00,VShot("blade_19"));
 DelayCommand(3.05,VShot("blade_20"));
 DelayCommand(3.10,VShot("blade_21"));
 DelayCommand(3.15,VShot("blade_22"));
 DelayCommand(3.20,VShot("blade_23"));
 DelayCommand(3.25,VShot("blade_24"));
 DelayCommand(3.30,VShot("blade_25"));
 DelayCommand(3.35,VShot("blade_26"));
 DelayCommand(3.40,VShot("blade_27"));
 DelayCommand(6.5,VShot("removed"));
 DelayCommand(7.0,ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,EffectAreaOfEffect(AOE_PER_WALLBLADE),Location(GetArea(pc),GetPosition(pc),0.0),3.0));
 DelayCommand(7.5,VShot("turned_050"));DelayCommand(8.0,VShot("turned_100"));DelayCommand(8.5,VShot("turned_150"));
 DelayCommand(11.0,VShot("turned_removed"));DelayCommand(11.3,VDone());}

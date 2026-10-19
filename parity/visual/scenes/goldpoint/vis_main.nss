#include "visual_inc"
void Report(object pc,string phase) {
 vector p=GetPosition(pc);int piles=0;object o=GetFirstObjectInArea(GetArea(pc));
 while(GetIsObjectValid(o)){
  if(GetObjectType(o)==OBJECT_TYPE_ITEM && GetBaseItemType(o)==BASE_ITEM_GOLD){
   piles++;vector v=GetPosition(o);
   WriteTimestampedLogEntry("VISUAL|note|pile|"+phase+"|count="+IntToString(GetItemStackSize(o))+"|x="+FloatToString(v.x)+"|y="+FloatToString(v.y)+"|pcdistance="+FloatToString(GetDistanceBetween(pc,o))+"|facing="+FloatToString(GetFacing(o)));
  }
  o=GetNextObjectInArea(GetArea(pc));
 }
 WriteTimestampedLogEntry("VISUAL|note|gold|"+phase+"|balance="+IntToString(GetGold(pc))+"|piles="+IntToString(piles)+"|action="+IntToString(GetCurrentAction(pc))+"|x="+FloatToString(p.x)+"|y="+FloatToString(p.y)+"|facing="+FloatToString(GetFacing(pc)));
}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 GiveGoldToCreature(pc,123);
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(3.5,VShot("inventory"));
 DelayCommand(3.7,WriteTimestampedLogEntry("VISUAL|hover|0.261,0.619"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|mouse|0.261,0.619|down"));
 DelayCommand(4.2,WriteTimestampedLogEntry("VISUAL|mouse|0.261,0.619|up"));
 DelayCommand(5.7,WriteTimestampedLogEntry("VISUAL|hover|0.5,0.522"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|mouse|0.5,0.522|down"));
 DelayCommand(6.2,WriteTimestampedLogEntry("VISUAL|mouse|0.5,0.522|up"));
 DelayCommand(7.0,VShot("amount"));
 DelayCommand(7.7,WriteTimestampedLogEntry("VISUAL|hover|0.45,0.608"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|mouse|0.45,0.608|down"));
 DelayCommand(8.2,WriteTimestampedLogEntry("VISUAL|mouse|0.45,0.608|up"));
 DelayCommand(10.0,Report(pc,"accepted"));
 string point=FindSubString(GetName(GetModule()),"near")>=0?"0.53,0.63":"0.77,0.56";
 DelayCommand(11.0,WriteTimestampedLogEntry("VISUAL|mouse|"+point+"|down"));
 DelayCommand(11.1,WriteTimestampedLogEntry("VISUAL|mouse|"+point+"|up"));
 if(FindSubString(GetName(GetModule()),"cancel")>=0)DelayCommand(11.5,AssignCommand(pc,ClearAllActions(TRUE)));
 DelayCommand(10.8,Report(pc,"10.8"));
 DelayCommand(10.9,Report(pc,"10.9"));
 DelayCommand(11.0,Report(pc,"11.0"));
 DelayCommand(11.1,Report(pc,"11.1"));
 DelayCommand(11.2,Report(pc,"11.2"));
 DelayCommand(11.3,Report(pc,"11.3"));
 DelayCommand(11.4,Report(pc,"11.4"));
 DelayCommand(11.5,Report(pc,"11.5"));
 DelayCommand(11.6,Report(pc,"11.6"));
 DelayCommand(11.7,Report(pc,"11.7"));
 DelayCommand(11.8,Report(pc,"11.8"));
 DelayCommand(11.9,Report(pc,"11.9"));
 DelayCommand(12.0,Report(pc,"12.0"));
 DelayCommand(12.1,Report(pc,"12.1"));
 DelayCommand(12.2,Report(pc,"12.2"));
 DelayCommand(12.3,Report(pc,"12.3"));
 DelayCommand(12.4,Report(pc,"12.4"));
 DelayCommand(12.5,Report(pc,"12.5"));
 DelayCommand(12.6,Report(pc,"12.6"));
 DelayCommand(12.7,Report(pc,"12.7"));
 DelayCommand(12.8,Report(pc,"12.8"));
 DelayCommand(12.9,Report(pc,"12.9"));
 DelayCommand(13.0,Report(pc,"13.0"));
 DelayCommand(13.1,Report(pc,"13.1"));
 DelayCommand(13.2,Report(pc,"13.2"));
 DelayCommand(13.3,Report(pc,"13.3"));
 DelayCommand(13.4,Report(pc,"13.4"));
 DelayCommand(13.5,Report(pc,"13.5"));
 DelayCommand(13.6,Report(pc,"13.6"));
 DelayCommand(13.7,Report(pc,"13.7"));
 DelayCommand(13.8,Report(pc,"13.8"));
 DelayCommand(13.9,Report(pc,"13.9"));
 DelayCommand(14.0,Report(pc,"14.0"));
 DelayCommand(14.1,Report(pc,"14.1"));
 DelayCommand(14.2,Report(pc,"14.2"));
 DelayCommand(14.3,Report(pc,"14.3"));
 DelayCommand(14.4,Report(pc,"14.4"));
 DelayCommand(14.5,Report(pc,"14.5"));
 DelayCommand(14.6,Report(pc,"14.6"));
 DelayCommand(14.7,Report(pc,"14.7"));
 DelayCommand(15.0,VShot("after"));
 DelayCommand(16.0,Report(pc,"final"));
 DelayCommand(17.0,VDone());
}

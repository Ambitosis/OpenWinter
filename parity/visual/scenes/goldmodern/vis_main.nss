// Modern client layout companion to golddrop; the balance/pile observations are shared.
#include "visual_inc"
void Report(object pc,string phase) {
 int piles=0; object o=GetFirstObjectInArea(GetArea(pc));
 while(GetIsObjectValid(o)) { if(GetObjectType(o)==OBJECT_TYPE_ITEM && GetBaseItemType(o)==BASE_ITEM_GOLD) piles++; o=GetNextObjectInArea(GetArea(pc)); }
 WriteTimestampedLogEntry("VISUAL|note|gold|"+phase+"|balance="+IntToString(GetGold(pc))+"|groundPiles="+IntToString(piles));
}
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 GiveGoldToCreature(pc,123);
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|click|0.224,0.846"));
 DelayCommand(7.0,VShot("a_select_all"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|click|0.67,0.575"));
 DelayCommand(10.0,VShot("b_accepted")); DelayCommand(10.0,Report(pc,"accepted"));
 DelayCommand(11.0,WriteTimestampedLogEntry("VISUAL|click|0.14,0.278"));
 DelayCommand(14.0,VShot("c_ground")); DelayCommand(14.0,Report(pc,"ground"));
 DelayCommand(16.0,VDone());
}

#include "visual_inc"
void Inspect(object actor) {
 WriteTimestampedLogEntry("VISUAL|fixture|druid|"+IntToString(GetLevelByClass(CLASS_TYPE_DRUID,actor)));
 WriteTimestampedLogEntry("VISUAL|fixture|palemaster|"+IntToString(GetLevelByClass(CLASS_TYPE_PALEMASTER,actor)));
 WriteTimestampedLogEntry("VISUAL|fixture|fighter|"+IntToString(GetLevelByClass(CLASS_TYPE_FIGHTER,actor)));
 WriteTimestampedLogEntry("VISUAL|fixture|wildshape|"+IntToString(GetHasFeat(FEAT_WILD_SHAPE,actor)));
 WriteTimestampedLogEntry("VISUAL|fixture|bardsong|"+IntToString(GetHasFeat(FEAT_BARD_SONGS,actor)));
 WriteTimestampedLogEntry("VISUAL|fixture|martial|"+IntToString(GetHasFeat(FEAT_WEAPON_PROFICIENCY_MARTIAL,actor)));
 object item=GetFirstItemInInventory(actor); while(GetIsObjectValid(item)) { WriteTimestampedLogEntry("VISUAL|fixture|inventory|"+GetResRef(item)); item=GetNextItemInInventory(actor); }
 object weapon=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,actor);
 WriteTimestampedLogEntry("VISUAL|fixture|weapon|"+GetResRef(weapon)+"|"+IntToString(GetBaseItemType(weapon)));
 VShot("profile");
}
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetPlotFlag(pc,TRUE); SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
 object actor=CreateObject(OBJECT_TYPE_CREATURE,"vis_profile",Location(GetArea(pc),Vector(20.0,17.0,0.0),270.0));
 SetPlotFlag(actor,TRUE); SetCommandable(FALSE,actor);
 DelayCommand(1.0,VCamera(pc,90.0,9.0,65.0));
 DelayCommand(2.0,Inspect(actor)); DelayCommand(8.0,VShot("profile_late")); DelayCommand(12.0,VDone());
}

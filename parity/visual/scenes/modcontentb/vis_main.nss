#include "visual_inc"
void main()
{
 object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(OBJECT_SELF,"started"))return;
 SetLocalInt(OBJECT_SELF,"started",1);
 WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
 VBegin(pc);
 object marker=VSpawn(pc,"nw_chicken",3.0,0.0,0.0);
 SetCreatureAppearanceType(marker,386);
 SetCommandable(FALSE,marker);
 DelayCommand(.5,VCamera(pc,90.0,12.0,55.0));
 DelayCommand(2.0,VShot("blue_destination"));
 DelayCommand(4.0,VDone());
}

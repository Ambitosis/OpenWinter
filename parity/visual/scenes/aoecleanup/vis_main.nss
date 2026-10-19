#include "visual_inc"
void RemoveCloud(location at)
{
    object cloud=GetNearestObjectToLocation(OBJECT_TYPE_AREA_OF_EFFECT,at);
    WriteTimestampedLogEntry("VISUAL|note|cloud.before."+IntToString(GetIsObjectValid(cloud)));
    DestroyObject(cloud);
}
void CheckCloud(location at)
{
    WriteTimestampedLogEntry("VISUAL|note|cloud.after."+IntToString(GetIsObjectValid(GetNearestObjectToLocation(OBJECT_TYPE_AREA_OF_EFFECT,at))));
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);
    location at=Location(GetArea(pc),Vector(20.0,20.0,0.0),0.0);
    DelayCommand(1.0,ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,EffectAreaOfEffect(AOE_PER_FOGACID),at,60.0));
    DelayCommand(3.0,VShot("a_active"));
    DelayCommand(5.0,RemoveCloud(at));
    DelayCommand(5.5,CheckCloud(at));
    DelayCommand(6.0,VShot("b_ending"));
    DelayCommand(10.0,VShot("c_after"));
    DelayCommand(15.0,VShot("d_late"));
    DelayCommand(17.0,VDone());
}

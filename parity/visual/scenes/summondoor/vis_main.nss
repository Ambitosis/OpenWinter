// A player clicks an open transition door with her own summon standing in her way (docs/INTER_AREA_MOVEMENT.md):
// does she walk round it, does it step aside, or is she stopped? The Prelude's Graduation Chamber doorway
// M0Q1C_M0Q1B, the summon (a dire badger) halfway between her and the door on her line to it. 1: a real click on the
// open doorway (the original needs --real-mouse); 2: the same by ActionMoveToObject(door) from a script; 3: a click
// while the summon walks towards her (busy, on a collision course).
#include "visual_inc"
#include "sd_inc"

object DoorA() { return GetObjectByTag("M0Q1C_M0Q1B"); }
object DoorB() { return GetObjectByTag("M0Q1B_M0Q1C"); }
void Tick() { SetLocalInt(GetModule(),"SD_TICK",GetLocalInt(GetModule(),"SD_TICK")+1); DelayCommand(0.1,Tick()); }
object Summon() { return GetAssociate(ASSOCIATE_TYPE_SUMMONED,GetFirstPC()); }
void Reading(string phase) {
    object p=GetFirstPC();
    SDNote(phase+"|pc|"+Where(p)+"|facing="+FloatToString(GetFacing(p),0,1)+"|summon|"+Where(Summon()));
}
void Readings(string phase, float from, float to) { float t; for(t=from;t<=to;t+=0.2) DelayCommand(t,Reading(phase)); }
// Out from the door on the room side, on its use-point line (0.37 m off its axis), facing it.
location Out(float d, float facing) {
    object door=DoorA(); float g=GetFacing(door); vector v=GetPosition(door);
    return Location(GetArea(door),v+Vector(0.37*sin(g)-d*cos(g),-0.37*cos(g)-d*sin(g),0.0),facing);
}
void Place() {
    object p=GetFirstPC(); object s=Summon(); float g=GetFacing(DoorA());
    AssignCommand(p,ClearAllActions(TRUE)); AssignCommand(p,JumpToLocation(Out(5.0,g)));
    AssignCommand(s,ClearAllActions(TRUE)); AssignCommand(s,JumpToLocation(Out(2.5,g+180.0)));
    DelayCommand(0.5,VCamera(p,g,9.0,50.0));
    SDNote("place|door|"+Vec(GetPosition(DoorA()))+"|"+FloatToString(g,0,1));
}
void Back() {
    object p=GetFirstPC();
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,JumpToObject(DoorA()));
}
void Move() {
    object p=GetFirstPC();
    SDNote("move|to|door");
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,ActionMoveToObject(DoorA(),TRUE));
}
void Open() {
    SetLocked(DoorB(),FALSE);
    AssignCommand(DoorA(),ActionOpenDoor(DoorA()));
    AssignCommand(DoorB(),ActionOpenDoor(DoorB()));
}
void main() {
    object p=GetEnteringObject();
    if(!GetIsPC(p) || GetLocalInt(GetModule(),"SD_STARTED")) return;
    SetLocalInt(GetModule(),"SD_STARTED",1);
    SetPlotFlag(p,TRUE);
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Tick();
    DelayCommand(1.0,Open());
    DelayCommand(1.5,AssignCommand(p,ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,EffectSummonCreature("nw_s_badgerdire"),GetLocation(p),600.0)));
    // 1. A click on the open doorway.
    DelayCommand(5.0,Place());
    DelayCommand(7.5,VShot("a_placed"));
    DelayCommand(8.5,WriteTimestampedLogEntry("VISUAL|mouse|0.505,0.270|down"));
    DelayCommand(8.6,WriteTimestampedLogEntry("VISUAL|mouse|0.505,0.270|up"));
    Readings("click",8.0,16.0);
    DelayCommand(10.0,VShot("b_click"));
    // 2. The same by script, back in the first room (the summon comes along through the door, so place again).
    DelayCommand(18.0,Back());
    DelayCommand(21.0,Place());
    DelayCommand(24.0,Move());
    Readings("move",23.5,31.0);
    DelayCommand(25.5,VShot("c_move"));
    // 3. The summon busy: walking towards (and past) her when she clicks.
    DelayCommand(34.0,Back());
    DelayCommand(37.0,Place());
    DelayCommand(40.0,AssignCommand(Summon(),ActionMoveToLocation(Out(8.0,0.0))));
    DelayCommand(40.2,WriteTimestampedLogEntry("VISUAL|mouse|0.505,0.270|down"));
    DelayCommand(40.3,WriteTimestampedLogEntry("VISUAL|mouse|0.505,0.270|up"));
    Readings("busy",39.5,48.0);
    DelayCommand(41.0,VShot("d_busy"));
    DelayCommand(49.0,VDone());
}

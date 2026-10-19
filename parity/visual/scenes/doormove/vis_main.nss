// doormove: a scripted move whose target IS a transition (docs/INTER_AREA_MOVEMENT.md). The Graduation Chamber's door
// M0Q1C_M0Q1B leads to the Training Halls (M0Q1B_M0Q1C); the scene adds a 2 m transition trigger DM_TRANS at (20,37)
// leading there too. Phases: the player's ActionMoveToObject to the open door, an NPC's, the player's and an NPC's
// ActionMoveToLocation onto the trigger, the player's move to the closed door, and her move to the open door with a
// 3 m range. Everything is read from notes.txt.
#include "visual_inc"
#include "dm_inc"

object DoorA() { return GetObjectByTag("M0Q1C_M0Q1B"); }
object DoorB() { return GetObjectByTag("M0Q1B_M0Q1C"); }
object Npc() { return GetObjectByTag("DM_NPC"); }
void Tick() { SetLocalInt(GetModule(),"DM_TICK",GetLocalInt(GetModule(),"DM_TICK")+1); DelayCommand(0.1,Tick()); }

void Read(string phase, object o) {
    DMNote(phase+"|"+(GetIsPC(o)?"pc":GetTag(o))+"|"+Where(o)+"|doorA="+IntToString(GetIsOpen(DoorA()))+"|doorB="+IntToString(GetIsOpen(DoorB())));
}
void ReadPC(string phase) { Read(phase,GetFirstPC()); }
void ReadNpc(string phase) { Read(phase,Npc()); }
void Readings(string phase, float from, float to, int npc) {
    float t;
    for(t=from;t<=to;t+=0.25) { if(npc) DelayCommand(t,ReadNpc(phase)); else DelayCommand(t,ReadPC(phase)); }
}
void Open() {
    SetLocked(DoorA(),FALSE); SetLocked(DoorB(),FALSE);
    AssignCommand(DoorA(),ActionOpenDoor(DoorA()));
    AssignCommand(DoorB(),ActionOpenDoor(DoorB()));
}
void Close() { AssignCommand(DoorA(),ActionCloseDoor(DoorA())); }
void Home() {
    object p=GetFirstPC();
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,JumpToLocation(Location(GetArea(DoorA()),Vector(12.0,35.0,0.0),180.0)));
    DMNote("home");
}
void PcToDoor(float range) {
    object p=GetFirstPC();
    DMNote("pc.movetoobject|range="+FloatToString(range,0,1)+"|door="+Vec(GetPosition(DoorA()))+"|open="+IntToString(GetIsOpen(DoorA())));
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,ActionMoveToObject(DoorA(),FALSE,range));
}
void PcToTrigger() {
    object p=GetFirstPC();
    DMNote("pc.movetolocation|trigger="+Vec(GetPosition(GetObjectByTag("DM_TRANS"))));
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,ActionMoveToLocation(Location(GetArea(DoorA()),Vector(20.0,37.0,0.0),0.0)));
}
void NewNpc(float x, float y) {
    object n=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(DoorA()),Vector(x,y,0.0),180.0),FALSE,"DM_NPC");
    ChangeToStandardFaction(n,STANDARD_FACTION_COMMONER);
    SetPlotFlag(n,TRUE);
    DMNote("npc.created|"+Where(n));
}
void NpcToDoor() { DMNote("npc.movetoobject"); AssignCommand(Npc(),ActionMoveToObject(DoorA())); }
void NpcToTrigger() { DMNote("npc.movetolocation"); AssignCommand(Npc(),ActionMoveToLocation(Location(GetArea(DoorA()),Vector(20.0,37.0,0.0),0.0))); }
void NoNpc() { object n=Npc(); DMNote("npc.end|"+Where(n)); SetPlotFlag(n,FALSE); DestroyObject(n); }

void main() {
    object p=GetEnteringObject();
    DMNote("enter|"+GetTag(OBJECT_SELF)+"|"+GetTag(p)+"|pc="+IntToString(GetIsPC(p))+"|"+Vec(GetPosition(p)));
    if(!GetIsPC(p) || GetLocalInt(GetModule(),"DM_STARTED")) return;
    SetLocalInt(GetModule(),"DM_STARTED",1);
    SetPlotFlag(p,TRUE);
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Tick();
    DelayCommand(0.5,Open());
    // 1. The player's ActionMoveToObject to the open door.
    DelayCommand(3.0,PcToDoor(1.0)); Readings("p1",3.0,13.0,FALSE);
    DelayCommand(14.0,Home());
    // 2. An NPC's ActionMoveToObject to the open door.
    DelayCommand(17.0,NewNpc(12.0,37.0)); DelayCommand(18.0,NpcToDoor()); Readings("p2",18.0,28.0,TRUE);
    DelayCommand(29.0,NoNpc());
    // 3. The player's ActionMoveToLocation onto the transition trigger.
    DelayCommand(31.0,PcToTrigger()); Readings("p3",31.0,43.0,FALSE);
    DelayCommand(44.0,Home());
    // 4. An NPC's ActionMoveToLocation onto the trigger.
    DelayCommand(46.0,NewNpc(15.0,37.0)); DelayCommand(47.0,NpcToTrigger()); Readings("p4",47.0,57.0,TRUE);
    DelayCommand(58.0,NoNpc());
    // 5. The player's ActionMoveToObject to the closed door.
    DelayCommand(58.0,Close()); DelayCommand(60.0,PcToDoor(1.0)); Readings("p5",60.0,70.0,FALSE);
    DelayCommand(71.0,Home());
    // 6. The open door again, with a 3 m range.
    DelayCommand(72.0,Open()); DelayCommand(74.0,PcToDoor(3.0)); Readings("p6",74.0,84.0,FALSE);
    DelayCommand(85.0,VShot("end"));
    DelayCommand(88.0,VDone());
}

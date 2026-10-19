// doormove2 (after doormove): is the scene's transition trigger DM_TRANS (a 2 m square at (20,37) leading to the
// Training Halls) taken when the player walks into it by keyboard (posted W), and by scripted moves that pass
// through it or target it: ActionMoveToLocation to a point beyond it, ActionMoveToObject(trigger) for the player and
// for an NPC. Everything is read from notes.txt (docs/INTER_AREA_MOVEMENT.md).
#include "visual_inc"
#include "dm_inc"

object Trig() { return GetObjectByTag("DM_TRANS"); }
object Npc() { return GetObjectByTag("DM_NPC"); }
object Here() { return GetArea(Trig()); }
void Tick() { SetLocalInt(GetModule(),"DM_TICK",GetLocalInt(GetModule(),"DM_TICK")+1); DelayCommand(0.1,Tick()); }
void Key(string state) { WriteTimestampedLogEntry("VISUAL|key|87|"+state); }

void Read(string phase, object o) { DMNote(phase+"|"+(GetIsPC(o)?"pc":GetTag(o))+"|"+Where(o)); }
void ReadPC(string phase) { Read(phase,GetFirstPC()); }
void ReadNpc(string phase) { Read(phase,Npc()); }
void Readings(string phase, float from, float to, int npc) {
    float t;
    for(t=from;t<=to;t+=0.25) { if(npc) DelayCommand(t,ReadNpc(phase)); else DelayCommand(t,ReadPC(phase)); }
}
void Place(float x, float y, float facing) {
    object p=GetFirstPC();
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,JumpToLocation(Location(Here(),Vector(x,y,0.0),facing)));
    DMNote("place|"+FloatToString(x,0,1)+","+FloatToString(y,0,1)+"|trigger="+Vec(GetPosition(Trig()))+"|type="+IntToString(GetObjectType(Trig())));
}
void Face() { AssignCommand(GetFirstPC(),SetCameraFacing(0.0,10.0,50.0,CAMERA_TRANSITION_TYPE_SNAP)); }
void PcPast() { DMNote("pc.movetolocation.past"); AssignCommand(GetFirstPC(),ActionMoveToLocation(Location(Here(),Vector(24.0,37.0,0.0),0.0))); }
void PcToTrigger() { DMNote("pc.movetoobject.trigger"); AssignCommand(GetFirstPC(),ActionMoveToObject(Trig())); }
void NewNpc() {
    object n=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(Here(),Vector(15.0,37.0,0.0),0.0),FALSE,"DM_NPC");
    ChangeToStandardFaction(n,STANDARD_FACTION_COMMONER);
    SetPlotFlag(n,TRUE);
}
void NpcToTrigger() { DMNote("npc.movetoobject.trigger"); AssignCommand(Npc(),ActionMoveToObject(Trig())); }

void main() {
    object p=GetEnteringObject();
    DMNote("enter|"+GetTag(OBJECT_SELF)+"|"+GetTag(p)+"|pc="+IntToString(GetIsPC(p))+"|"+Vec(GetPosition(p)));
    if(!GetIsPC(p) || GetLocalInt(GetModule(),"DM_STARTED")) return;
    SetLocalInt(GetModule(),"DM_STARTED",1);
    SetPlotFlag(p,TRUE);
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Tick();
    // 1. W held from 4 m west of the trigger, facing it.
    DelayCommand(1.0,Place(16.0,37.0,0.0)); DelayCommand(1.5,Face());
    DelayCommand(3.0,Key("down")); DelayCommand(6.0,Key("up")); Readings("k1",2.5,10.0,FALSE);
    // 2. ActionMoveToLocation to a point beyond the trigger.
    DelayCommand(12.0,Place(16.0,37.0,0.0)); DelayCommand(13.0,PcPast()); Readings("p2",13.0,22.0,FALSE);
    // 3. The player's ActionMoveToObject(trigger).
    DelayCommand(24.0,Place(16.0,37.0,0.0)); DelayCommand(25.0,PcToTrigger()); Readings("p3",25.0,34.0,FALSE);
    // 4. An NPC's ActionMoveToObject(trigger).
    DelayCommand(36.0,Place(12.0,35.0,180.0)); DelayCommand(36.5,NewNpc()); DelayCommand(37.5,NpcToTrigger()); Readings("p4",37.5,46.0,TRUE);
    DelayCommand(48.0,VShot("end"));
    DelayCommand(50.0,VDone());
}

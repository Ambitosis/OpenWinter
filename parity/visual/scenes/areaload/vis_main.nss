// How long a player is out of every area when it changes area (GetArea invalid between leaving and appearing): the
// Prelude's Graduation Chamber and Training Halls (small interiors) and Chapter 1's 16x16 city exterior map_m1q5a,
// each way twice, by JumpToObject / JumpToLocation. A tenth-second clock; readings in notes.txt
// (docs/INTER_AREA_MOVEMENT.md, area loading).
#include "visual_inc"

void Tick() { SetLocalInt(GetModule(),"AL_TICK",GetLocalInt(GetModule(),"AL_TICK")+1); DelayCommand(0.1,Tick()); }
string Clock() { return FloatToString(IntToFloat(GetLocalInt(GetModule(),"AL_TICK"))/10.0,0,1); }
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|t="+Clock()+"|"+s); }
object AreaByResRef(string r) {
    object a=GetFirstArea();
    while(GetIsObjectValid(a)) { if(GetStringLowerCase(GetResRef(a))==r) return a; a=GetNextArea(); }
    return OBJECT_INVALID;
}
// Where the player is, every tenth of a second while it is between areas and just after.
void Watch(string label, int n) {
    object p=GetFirstPC(); object a=GetArea(p);
    string where = GetIsObjectValid(a) ? GetResRef(a) : "none";
    if(GetLocalString(GetModule(),"AL_WHERE")!=where) { Note(label+"|"+where); SetLocalString(GetModule(),"AL_WHERE",where); }
    if(n>0) DelayCommand(0.1,Watch(label,n-1));
}
location Spot(string area) {
    object a=AreaByResRef(area);
    object o=GetFirstObjectInArea(a);
    while(GetIsObjectValid(o)) { if(GetObjectType(o)==OBJECT_TYPE_WAYPOINT) return GetLocation(o); o=GetNextObjectInArea(a); }
    return Location(a,Vector(20.0,20.0,0.0),0.0);
}
void Go(string label, string area) {
    object p=GetFirstPC();
    Note(label+"|go|"+area);
    AssignCommand(p,ClearAllActions(TRUE));
    if(area=="m0q1b") AssignCommand(p,JumpToObject(GetObjectByTag("M0Q1B_M0Q1C")));
    else if(area=="m0q1c") AssignCommand(p,JumpToObject(GetObjectByTag("M0Q1C_M0Q1B")));
    else AssignCommand(p,JumpToLocation(Spot(area)));
    Watch(label,60);
}
void main() {
    object p=GetEnteringObject();
    if(!GetIsPC(p) || GetLocalInt(GetModule(),"AL_STARTED")) return;
    SetLocalInt(GetModule(),"AL_STARTED",1);
    SetPlotFlag(p,TRUE);
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN);
    Tick();
    DelayCommand(4.0,Go("small1","m0q1b"));
    DelayCommand(10.0,Go("small2","m0q1c"));
    DelayCommand(16.0,Go("small3","m0q1b"));
    DelayCommand(22.0,Go("small4","m0q1c"));
    DelayCommand(28.0,Go("large1","map_m1q5a"));
    DelayCommand(35.0,Go("large2","m0q1c"));
    DelayCommand(41.0,Go("large3","map_m1q5a"));
    DelayCommand(47.0,VShot("large_exterior"));
    DelayCommand(48.0,Go("large4","m0q1c"));
    DelayCommand(55.0,VDone());
}

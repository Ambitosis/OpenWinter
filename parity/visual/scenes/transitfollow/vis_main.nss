// A player crossing between two Prelude areas (the Graduation Chamber and the Training Halls, joined by the
// doorway M0Q1C_M0Q1B / M0Q1B_M0Q1C) by keyboard (posted W keys) and by script, followed by an attacker and by
// each kind of associate. Everything is read from notes.txt (docs/INTER_AREA_MOVEMENT.md).
#include "visual_inc"
#include "tf_inc"

object DoorA() { return GetObjectByTag("M0Q1C_M0Q1B"); }
object DoorB() { return GetObjectByTag("M0Q1B_M0Q1C"); }
object Here() { object p=GetFirstPC(); return GetArea(p)==GetArea(DoorA()) ? DoorA() : DoorB(); }
object There() { object p=GetFirstPC(); return GetArea(p)==GetArea(DoorA()) ? DoorB() : DoorA(); }
void Key(string state) { WriteTimestampedLogEntry("VISUAL|key|87|"+state); }
void Tick() { SetLocalInt(GetModule(),"TF_TICK",GetLocalInt(GetModule(),"TF_TICK")+1); DelayCommand(0.1,Tick()); }

void Assoc(string label, object o) {
    object p=GetFirstPC();
    string d = GetArea(o)==GetArea(p) ? FloatToString(GetDistanceBetween(o,p),0,2) : "-";
    TFNote(label+"|"+GetTag(o)+"|"+Where(o)+"|dist="+d+"|master="+IntToString(GetMaster(o)==p)+"|type="+IntToString(GetAssociateType(o)));
}
void Everyone(string phase) {
    object p=GetFirstPC();
    TFNote(phase+"|pc|"+Where(p)+"|facing="+FloatToString(GetFacing(p),0,1)+"|doorA="+IntToString(GetIsOpen(DoorA()))+"|doorB="+IntToString(GetIsOpen(DoorB())));
    object a=GetObjectByTag("TF_ATTACKER");
    if(GetIsObjectValid(a)) TFNote(phase+"|attacker|"+Where(a)+"|target="+IntToString(GetAttackTarget(a)==p));
    int i;
    for(i=1;i<=5;i++) { object o=GetAssociate(i,p); if(GetIsObjectValid(o)) Assoc(phase+"|assoc"+IntToString(i),o); }
    object h=GetHenchman(p,1); if(GetIsObjectValid(h)) Assoc(phase+"|hench",h);
}
void Readings(string phase, float from, float to, float step) {
    float t; for(t=from;t<=to;t+=step) DelayCommand(t,Everyone(phase));
}

// Phase 1: walk into the closed doorway.
void Start() {
    object p=GetFirstPC();
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,JumpToLocation(Location(GetArea(DoorA()),Vector(9.0,35.0,0.0),180.0)));
}
// Phase 2: the doorway open, an attacker on the player.
void OpenUp() {
    object p=GetFirstPC();
    SetLocked(DoorB(),FALSE);
    AssignCommand(DoorA(),ActionOpenDoor(DoorA()));
    AssignCommand(DoorB(),ActionOpenDoor(DoorB()));
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,JumpToLocation(Location(GetArea(DoorA()),Vector(9.0,35.0,0.0),180.0)));
}
void Attacker() {
    object p=GetFirstPC();
    object a=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(p),Vector(12.0,35.0,0.0),180.0),FALSE,"TF_ATTACKER");
    SetPlotFlag(a,TRUE);
    SetIsTemporaryEnemy(p,a);
    AssignCommand(a,ActionAttack(p));
    TFNote("attacker.created|"+Where(a)+"|enemy="+IntToString(GetIsEnemy(p,a)));
}
void NoAttacker() { object a=GetObjectByTag("TF_ATTACKER"); SetPlotFlag(a,FALSE); DestroyObject(a); }
// Phase 3: every kind of associate, then walk into the doorway of the area the player is in.
void Associates() {
    object p=GetFirstPC();
    location l=GetLocation(p);
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,EffectSummonCreature("nw_s_badgerdire"),l,600.0));
    SummonFamiliar(p);
    SummonAnimalCompanion(p);
    object h=CreateObject(OBJECT_TYPE_CREATURE,"nw_hen_bod_01",l,FALSE,"TF_HENCH");
    AddHenchman(p,h);
    // Neutral, 3 m further from the door than the player: OpenWinter does not yet make a scripted EffectDominated an
    // associate, and the goblin would otherwise fight the party or stand in the doorway (added after the native runs,
    // where it was created on the player's spot and hostile; the original dominated it either way).
    vector away=VectorNormalize(GetPosition(p)-GetPosition(Here()));
    object g=CreateObject(OBJECT_TYPE_CREATURE,"nw_goblina",Location(GetArea(p),GetPosition(p)+away*3.0,0.0),FALSE,"TF_DOMINATED");
    ChangeToStandardFaction(g,STANDARD_FACTION_COMMONER);
    AssignCommand(p,ApplyEffectToObject(DURATION_TYPE_TEMPORARY,SupernaturalEffect(EffectDominated()),g,600.0));
}
void Tags() {
    object p=GetFirstPC();
    SetTag(GetAssociate(ASSOCIATE_TYPE_SUMMONED,p),"TF_SUMMON");
    SetTag(GetAssociate(ASSOCIATE_TYPE_FAMILIAR,p),"TF_FAMILIAR");
    SetTag(GetAssociate(ASSOCIATE_TYPE_ANIMALCOMPANION,p),"TF_COMPANION");
}
void Face() {
    object p=GetFirstPC(); object d=Here();
    AssignCommand(p,ClearAllActions(TRUE));
    // Four metres out from the door on the room side, on its use point line (0.37 m off its axis, clear of the open
    // leaf), facing it. GetFacing on a door is its bearing plus 90 degrees.
    float g=GetFacing(d); vector v=GetPosition(d);
    vector o=Vector(0.37*sin(g)-4.0*cos(g),-0.37*cos(g)-4.0*sin(g),0.0);
    AssignCommand(p,JumpToLocation(Location(GetArea(d),v+o,g)));
    TFNote("face|"+GetTag(d)+"|"+Vec(v)+"|"+FloatToString(g,0,1));
}
// The summon and the henchman sent away from the player (does a far associate still come?).
void Spread() {
    object p=GetFirstPC(); object a=GetArea(p);
    object s=GetAssociate(ASSOCIATE_TYPE_SUMMONED,p); object h=GetHenchman(p,1);
    AssignCommand(s,ClearAllActions(TRUE)); AssignCommand(s,JumpToLocation(Location(a,Vector(18.0,35.0,0.0),0.0)));
    AssignCommand(h,ClearAllActions(TRUE)); AssignCommand(h,JumpToLocation(Location(a,Vector(28.0,36.0,0.0),0.0)));
    TFNote("spread");
}
// Phase 4: a scripted jump to the other area.
void ScriptJump() {
    object p=GetFirstPC(); object d=There();
    TFNote("jump|to|"+GetTag(d));
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,JumpToObject(d));
}
// Phase 5: a scripted move to an object in the other area.
void ScriptMove() {
    object p=GetFirstPC(); object d=There();
    TFNote("move|to|"+GetTag(d));
    AssignCommand(p,ClearAllActions(TRUE));
    AssignCommand(p,ActionMoveToObject(d,FALSE));
}

void main() {
    object o=GetEnteringObject();
    TFNote("enter|"+GetTag(OBJECT_SELF)+"|"+GetTag(o)+"|pc="+IntToString(GetIsPC(o))+"|"+Vec(GetPosition(o)));
    if(!GetIsPC(o) || GetLocalInt(GetModule(),"TF_STARTED")) return;
    SetLocalInt(GetModule(),"TF_STARTED",1);
    object p=o;
    SetPlotFlag(p,TRUE);
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN); SetCameraLimits(p,1.0,89.0,1.0,40.0);
    Tick();
    DelayCommand(1.0,Start());
    // 1. Closed doorway, W held 3 s.
    DelayCommand(3.0,Key("down")); DelayCommand(6.0,Key("up")); Readings("closed",2.5,7.0,0.25);
    // 2. Open doorway with an attacker on the player, W held 4 s.
    DelayCommand(8.0,OpenUp()); DelayCommand(9.0,Attacker());
    DelayCommand(13.0,Key("down")); DelayCommand(17.0,Key("up")); Readings("open",12.5,30.0,0.25);
    DelayCommand(25.0,VShot("attacker_followed"));
    DelayCommand(31.0,NoAttacker());
    // 3. Associates, then W held 2 s into the doorway of the area the player is in (4 s in the native runs).
    DelayCommand(32.0,Associates()); DelayCommand(34.0,Tags());
    DelayCommand(36.0,Face()); Readings("assoc",35.0,45.0,0.25);
    DelayCommand(37.5,Key("down")); DelayCommand(39.5,Key("up")); DelayCommand(45.0,VShot("associates_followed"));
    // 4. The summon 12 m and the henchman 22 m away, then the doorway again.
    DelayCommand(46.0,Spread()); DelayCommand(46.5,Face()); Readings("far",45.5,56.0,0.25);
    DelayCommand(48.0,Key("down")); DelayCommand(51.0,Key("up"));
    // 5. Scripted jump to the other area.
    DelayCommand(58.0,ScriptJump()); Readings("jump",57.5,63.0,0.5);
    // 6. Scripted ActionMoveToObject to the other area's door.
    DelayCommand(65.0,ScriptMove()); Readings("move",64.5,95.0,0.5);
    DelayCommand(97.0,VDone());
}

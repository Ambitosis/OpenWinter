#include "parity_inc"
#include "parity_time"
void Done(string tag){PLogF(tag+".queueDone",PSince(),0.25);}
void Actor(string tag,float x,float y){object c=CreateObject(OBJECT_TYPE_CREATURE,"db_actor",Location(GetObjectByTag("PARITY_AREA"),Vector(x,y-5.0,0.0),90.0),FALSE,tag+"_ACTOR");SetAILevel(c,AI_LEVEL_NORMAL);PLogI(tag+".valid",GetIsObjectValid(c));PLogF(tag+".startY",GetPosition(c).y,0.01);AssignCommand(c,ActionMoveToLocation(Location(GetArea(c),Vector(x,y+5.0,0.0),90.0),FALSE));AssignCommand(c,ActionDoCommand(Done(tag)));AssignCommand(c,ActionWait(2.0));}
void Start(){PLogI("OPEN.isOpen",GetIsOpen(GetObjectByTag("OPEN")));Actor("QUIET",10.0,10.0);Actor("OPEN",30.0,10.0);Actor("RECOVER",10.0,30.0);Actor("LOCKED",30.0,30.0);}
void State(string tag){object c=GetObjectByTag(tag+"_ACTOR");PLogI(tag+".blocks",GetLocalInt(c,"blocks"));PLogI(tag+".action",GetCurrentAction(c));PLogF(tag+".endX",GetPosition(c).x,0.15);PLogF(tag+".endY",GetPosition(c).y,0.15);PLogI(tag+".open",GetIsOpen(GetObjectByTag(tag)));}
void End(){State("QUIET");State("OPEN");State("RECOVER");State("LOCKED");PDone();}
void main(){PStart();object d=GetObjectByTag("OPEN");AssignCommand(d,ActionOpenDoor(d));DelayCommand(2.0,Start());DelayCommand(16.0,End());}

#include "parity_inc"
#include "parity_time"
void main(){object me=OBJECT_SELF;string k=GetTag(me);int n=GetLocalInt(me,"blocks")+1;SetLocalInt(me,"blocks",n);PLog(k+".block."+IntToString(n)+".door",GetTag(GetBlockingDoor()));PLogI(k+".block."+IntToString(n)+".action",GetCurrentAction(me));PLogF(k+".block."+IntToString(n)+".t",PSince(),0.2);PLogF(k+".block."+IntToString(n)+".y",GetPosition(me).y,0.15);if(k=="RECOVER_ACTOR"&&n==1){ActionOpenDoor(GetBlockingDoor());ActionMoveToLocation(Location(GetArea(me),Vector(10.0,35.0,0.0),0.0),FALSE);}}

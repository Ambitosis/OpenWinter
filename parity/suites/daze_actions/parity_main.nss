#include "parity_inc"
void Sample(object c,string k){PLogF(k+".x",GetPosition(c).x,0.5);PLogI(k+".action",GetCurrentAction(c));}
object Actor(float y,int daze){object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(0.0,y));int i;for(i=5000;i<=5012;i++)SetEventScript(c,i,"");SetAILevel(c,AI_LEVEL_NORMAL);if(daze)ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectDazed(),c,8.0);return c;}
void Move(object c,string key,int run){AssignCommand(c,ActionMoveToLocation(Location(GetArea(c),GetPosition(c)+Vector(30.0,0.0,0.0),0.0),run));DelayCommand(1.0,Sample(c,key));}
void main(){object a=Actor(0.0,FALSE);object b=Actor(5.0,TRUE);object c=Actor(10.0,TRUE);DelayCommand(1.0,Move(a,"run",TRUE));DelayCommand(1.0,Move(b,"dazeRun",TRUE));DelayCommand(1.0,Move(c,"dazeWalk",FALSE));DelayCommand(4.0,PDone());}

#include "parity_inc"
void Read(object c, object t, string phase)
{
 string k=GetTag(c)+"."+phase;
 PLogI(k+".action",GetCurrentAction(c));
 if(phase!="wait")PLogI(k+".target",GetAttackTarget(c)==t);
 if(phase=="late")PLogI(k+".attacked",GetLocalInt(t,"attacks")>0);
 PLogI(k+".done",GetLocalInt(c,"done"));
}
void Start(object c, object t, int mode, int door)
{
 PLogI(GetTag(c)+".valid",GetIsObjectValid(t));
 PLogI(GetTag(c)+".open",GetIsOpen(t));
 PLogI(GetTag(c)+".plot",GetPlotFlag(t));
 AssignCommand(c,PLogI(GetTag(c)+".possible",door?GetIsDoorActionPossible(t,DOOR_ACTION_BASH):GetIsPlaceableObjectActionPossible(t,PLACEABLE_ACTION_BASH)));
 AssignCommand(c,ActionWait(2.0));
 if(mode==1) AssignCommand(c,ActionAttack(t));
 else if(door) AssignCommand(c,DoDoorAction(t,DOOR_ACTION_BASH));
 else AssignCommand(c,DoPlaceableObjectAction(t,PLACEABLE_ACTION_BASH));
 AssignCommand(c,ActionDoCommand(SetLocalInt(c,"done",1)));
 DelayCommand(.3,Read(c,t,"wait"));
 DelayCommand(3.2,Read(c,t,"active"));
 DelayCommand(5.2,Read(c,t,"late"));
}
void main()
{
 int n;
 for(n=0;n<12;n++)
 {
  object t=GetObjectByTag("BD"+IntToString(n));
  location l=GetLocation(t);vector v=GetPosition(t);v.y+=2.2;
  object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(t),v,270.0));
  SetTag(c,"case"+IntToString(n));SetAILevel(c,AI_LEVEL_NORMAL);
  int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");
  ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);
  int mode=n%6;
  if(mode==2)SetPlotFlag(t,TRUE);
  if(mode==3&&n<6){SetLocked(t,FALSE);AssignCommand(t,ActionOpenDoor(t));}
  if(mode==4)SetCommandable(FALSE,c);
  if(mode==5)SetPlotFlag(t,TRUE);
  DelayCommand(3.0,Start(c,t,mode==5?1:mode,n<6));
 }
 DelayCommand(9.0,PDone());
}

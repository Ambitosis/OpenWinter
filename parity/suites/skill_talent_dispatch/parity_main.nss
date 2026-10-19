#include "parity_inc"
// Deliberately report outcomes, not the unrelated duration of each existing skill action.
void Read(object c,object t,string phase)
{
 string k=GetTag(c)+"."+phase;int skill=GetLocalInt(c,"skill");
 PLogI(k+".action",GetCurrentAction(c));PLogI(k+".done",GetLocalInt(c,"done"));
 if(skill==SKILL_OPEN_LOCK)PLogI(k+".locked",GetLocked(t));
 if(skill==SKILL_HEAL){PLogI(k+".healed",GetCurrentHitPoints(t)>1);PLogI(k+".kit",GetIsObjectValid(GetLocalObject(c,"kit")));}
 if(skill==SKILL_HIDE||skill==SKILL_MOVE_SILENTLY)PLogI(k+".stealth",GetActionMode(c,ACTION_MODE_STEALTH));
 if(skill==SKILL_SEARCH||skill==SKILL_SPOT||skill==SKILL_LISTEN)PLogI(k+".detect",GetActionMode(c,ACTION_MODE_DETECT));
 if(skill==SKILL_PICK_POCKET)PLogI(k+".stolen",GetGold(c)>0);
}
void Repeat(object c,object t,int skill)
{
 AssignCommand(c,ActionUseTalentOnObject(TalentSkill(skill),t));
}
void Start(object c,object t,int skill,int route)
{
 string k=GetTag(c);talent tal=TalentSkill(skill);SetLocalInt(c,"skill",skill);
 PLogI(k+".valid",GetIsTalentValid(tal));PLogI(k+".rank",GetSkillRank(skill,c,TRUE));
 PLogI(k+".target",GetIsObjectValid(t));
 AssignCommand(c,ActionWait(1.0));
 if(route==0)AssignCommand(c,ActionUseSkill(skill,t,0,GetLocalObject(c,"kit")));
 if(route==1)AssignCommand(c,ActionUseTalentOnObject(tal,t));
 if(route==3)AssignCommand(c,ActionUseSkill(skill,t));
 if(route==2)AssignCommand(c,ActionUseTalentAtLocation(tal,GetLocation(t)));
 AssignCommand(c,ActionDoCommand(SetLocalInt(c,"done",1)));
 DelayCommand(.2,Read(c,t,"waiting"));
 if(route==1&&(skill==SKILL_HIDE||skill==SKILL_MOVE_SILENTLY||skill==SKILL_SEARCH)){DelayCommand(4.0,Read(c,t,"first"));DelayCommand(7.0,Repeat(c,t,skill));}
 DelayCommand(12.0,Read(c,t,"result"));
}
void main()
{
 int n;
 for(n=0;n<34;n++)
 {
  // Taunt/AnimalEmpathy lack successful direct positives; their unresolved diagnostics are excluded.
  if((n>=12&&n<=14)||(n>=28&&n<=30))continue;
  int group=n>=19?5+(n-19)/3:n==15?1:n>=16?2:n/3;int skill=group==0?SKILL_OPEN_LOCK:group==1?SKILL_HEAL:group==2?SKILL_HIDE:group==3?SKILL_SEARCH:group==4?SKILL_TAUNT:group==5?SKILL_MOVE_SILENTLY:group==6?SKILL_SPOT:group==7?SKILL_LISTEN:group==8?SKILL_ANIMAL_EMPATHY:SKILL_PICK_POCKET;
  location l=PCentre(IntToFloat(n%5)*14.0-8.0,IntToFloat(n/5)*12.0-8.0);
  object c=CreateObject(OBJECT_TYPE_CREATURE,"st_actor",l);
  SetTag(c,"case"+IntToString(n));SetAILevel(c,AI_LEVEL_NORMAL);ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);
  int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");
  ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSkillIncrease(skill,50),c);
  vector v=GetPosition(c);v.x+=1.5;object t=c;
  if(group==0){t=CreateObject(OBJECT_TYPE_PLACEABLE,"plc_chest1",Location(GetArea(c),v,0.0));SetLocked(t,TRUE);SetLockLockable(t,TRUE);SetLockUnlockDC(t,0);SetLockKeyRequired(t,FALSE);}
  if(group==1||group==4||group==9){t=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(c),v,0.0));for(e=5000;e<=5012;e++)SetEventScript(t,e,"");ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);SetCurrentHitPoints(t,1);}
  if(group==4||group==9)ChangeToStandardFaction(t,STANDARD_FACTION_HOSTILE);
  if(group==9)GiveGoldToCreature(t,100);
  if(group==8){t=CreateObject(OBJECT_TYPE_CREATURE,"nw_dog",Location(GetArea(c),v,0.0));for(e=5000;e<=5012;e++)SetEventScript(t,e,"");ChangeToStandardFaction(t,STANDARD_FACTION_COMMONER);}
  if(group==4||group==8){SetIsTemporaryEnemy(t,c);SetIsTemporaryEnemy(c,t);}
  if(group==1)SetLocalObject(c,"kit",CreateItemOnObject("nw_it_medkit001",c));
  int route=n>=19?(n-19)%3:n%3;
  if(n==15)route=3;
  if(n==16){route=1;SetCommandable(FALSE,c);}
  if(n==17){route=1;skill=999;}
  if(n==18){route=1;t=OBJECT_INVALID;}
  DelayCommand(5.0,Start(c,t,skill,route));
 }
 DelayCommand(18.0,PDone());
}

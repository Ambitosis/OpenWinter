#include "parity_inc"
#include "parity_time"
void Clear(object c){PLogF(GetTag(c)+".clear",PNow()-GetLocalFloat(c,"start"),.2);AssignCommand(c,ClearAllActions());}
void Finished(object c,int n){PLogF(GetTag(c)+".done"+IntToString(n),PNow()-GetLocalFloat(c,"start"),.2);}
void Poll(object c,int previous,int n)
{
 int uses=GetFeatRemainingUses(FEAT_TURN_UNDEAD,c);
 if(uses!=previous){int change=GetLocalInt(c,"changes")+1;SetLocalInt(c,"changes",change);string k=GetTag(c)+".spent"+IntToString(change);PLogF(k,PNow()-GetLocalFloat(c,"start"),.2);PLogI(k+".uses",uses);if(GetTag(c)=="committed"&&change==1)DelayCommand(.1,Clear(c));}
 if(n<300)DelayCommand(.05,Poll(c,uses,n+1));
}
void Start(object c)
{
 SetLocalFloat(c,"start",PNow());PLogI(GetTag(c)+".initial",GetFeatRemainingUses(FEAT_TURN_UNDEAD,c));
 AssignCommand(c,ActionUseFeat(FEAT_TURN_UNDEAD,c));AssignCommand(c,ActionDoCommand(Finished(c,1)));
 AssignCommand(c,ActionUseFeat(FEAT_TURN_UNDEAD,c));AssignCommand(c,ActionDoCommand(Finished(c,2)));
 Poll(c,GetFeatRemainingUses(FEAT_TURN_UNDEAD,c),0);
 if(GetTag(c)=="early")DelayCommand(.5,Clear(c));
}
void Report(object c){string k=GetTag(c);PLogI(k+".finaluses",GetFeatRemainingUses(FEAT_TURN_UNDEAD,c));PLogI(k+".impacts",GetLocalInt(c,"impacts"));PLogI(k+".action",GetCurrentAction(c));}
void Case(string name,float x,int haste)
{
 object c=CreateObject(OBJECT_TYPE_CREATURE,"tq_cleric",PCentre(x,0.0));SetTag(c,name);SetAILevel(c,AI_LEVEL_NORMAL);SetPlotFlag(c,TRUE);
 int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");
 if(haste)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);
 PLogI(name+".level",GetLevelByClass(CLASS_TYPE_CLERIC,c));PLogI(name+".owns",GetHasFeat(FEAT_TURN_UNDEAD,c,TRUE));
 DelayCommand(3.0,Start(c));DelayCommand(19.0,Report(c));
}
void main(){Case("normal",-25.0,FALSE);Case("haste",-10.0,TRUE);Case("early",10.0,FALSE);Case("committed",25.0,FALSE);DelayCommand(20.0,PDone());}

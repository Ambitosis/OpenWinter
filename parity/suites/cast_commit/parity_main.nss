#include "parity_inc"
#include "parity_time"
void Die(object o){SetPlotFlag(o,FALSE);ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(FALSE,FALSE),o);}
void After(object o){SetLocalInt(o,"after",1);}
void Cancel(object o,int combat)
{
 SetLocalFloat(o,"clearTime",PNow()-GetLocalFloat(o,"start"));
 AssignCommand(o,ClearAllActions(combat));
}
void Start(object o,int cheat)
{
 SetLocalFloat(o,"start",PNow());
 AssignCommand(o,ActionCastSpellAtObject(SPELL_MAGE_ARMOR,o,METAMAGIC_NONE,cheat));
 AssignCommand(o,ActionDoCommand(After(o)));
}
void Read(object o)
{
 string tag=GetTag(o);
 PLogI(tag+".valid",GetIsObjectValid(o));
 PLogI(tag+".dead",GetIsDead(o));
 PLogI(tag+".impacts",GetLocalInt(o,"impacts"));
 PLogI(tag+".after",GetLocalInt(o,"after"));
 PLogI(tag+".ready",GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,1,0));
 if(GetLocalInt(o,"impacts"))PLogF(tag+".impactTime",GetLocalFloat(o,"impactTime"),0.15);
 PLogF(tag+".clearTime",GetLocalFloat(o,"clearTime"),0.10);
}
void Prepare(object o)
{
 AssignCommand(o,ClearAllActions(TRUE));
 int l;int s;
 for(l=0;l<10;l++)for(s=0;s<GetMemorizedSpellCountByLevel(o,CLASS_TYPE_WIZARD,l);s++)SetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,l,s,FALSE);
 SetMemorizedSpell(o,CLASS_TYPE_WIZARD,1,0,SPELL_MAGE_ARMOR,TRUE,METAMAGIC_NONE,FALSE);
 PLogI(GetTag(o)+".class",GetLevelByClass(CLASS_TYPE_WIZARD,o));
 PLogI(GetTag(o)+".prepared",GetMemorizedSpellId(o,CLASS_TYPE_WIZARD,1,0));
 PLogI(GetTag(o)+".preparedReady",GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,1,0));
}
void Case(int index,float clear,int combat,int cheat)
{
 object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_drowmage001",PCentre(IntToFloat(index%6)*5.0,IntToFloat(index/6)*5.0));
 SetTag(o,IntToString(index));SetPlotFlag(o,TRUE);SetAILevel(o,AI_LEVEL_NORMAL);
 SetEventScript(o,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 object item=GetFirstItemInInventory(o);
 while(GetIsObjectValid(item)){object next=GetNextItemInInventory(o);DestroyObject(item);item=next;}
 DelayCommand(0.3,Prepare(o));
 float start=1.0+IntToFloat(index)*0.5;
 DelayCommand(start,Start(o,cheat));
 if(clear>=0.0)DelayCommand(start+clear,Cancel(o,combat));
 if(index==22||index==23)DelayCommand(start+1.80,Die(o));
 if(index==24)DelayCommand(start+1.0,Die(o));
 DelayCommand(start+5.0,Read(o));
}
void Run()
{
 int kind;for(kind=0;kind<2;kind++)
 {
 int b=kind*11;
 Case(b,-1.0,TRUE,kind);
 Case(b+1,0.25,TRUE,kind);Case(b+2,0.75,TRUE,kind);Case(b+3,1.25,TRUE,kind);
 Case(b+4,1.60,TRUE,kind);Case(b+5,1.75,TRUE,kind);Case(b+6,1.90,TRUE,kind);
 Case(b+7,2.15,TRUE,kind);Case(b+8,2.5,TRUE,kind);
 Case(b+9,1.75,FALSE,kind);Case(b+10,0.75,FALSE,kind);
 }
 Case(22,-1.0,TRUE,FALSE); Case(23,1.60,TRUE,FALSE); Case(24,-1.0,TRUE,FALSE);
 DelayCommand(19.0,PDone());
}
void main(){DelayCommand(0.2,Run());}

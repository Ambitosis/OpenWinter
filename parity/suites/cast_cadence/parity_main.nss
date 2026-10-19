#include "pcombat_inc"
#include "parity_time"
void Finished(object o){
PLogF(GetTag(o)+".done",PNow()-GetLocalFloat(o,"start"),0.2);
}

void Begin(object o,int meta){
string k=GetTag(o);
SetLocalFloat(o,"start",PNow());
PLogI(k+".pc",GetIsPC(o));
PLogI(k+".combatStart",GetIsInCombat(o));
PLogI(k+".attacksBefore",GetLocalInt(o,"attacks")>0);
int i;
for(i=0;
i<3;
i++)AssignCommand(o,ActionCastSpellAtObject(0,o,meta,TRUE));
AssignCommand(o,ActionDoCommand(Finished(o)));
}

void Report(object o){
PLogI(GetTag(o)+".impacts",GetLocalInt(o,"impacts"));
PLogI(GetTag(o)+".combatEnd",GetIsInCombat(o));
}

void Fight(object e,object c){
AssignCommand(e,ActionAttack(c));
}

void Case(int n){
float x=5.0+IntToFloat(n%4)*18.0;
float y=10.0+IntToFloat(n/4)*40.0;
object c=PSpawn("nw_drowmage020",x,y);
SetTag(c,IntToString(n));
SetPlotFlag(c,TRUE);
SetEventScript(c,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"cad_attacked");
int mode=n%4;
int haste=mode==1||mode==3;
int quick=mode==2||mode==3;
int meta=METAMAGIC_NONE;
if(quick)meta=METAMAGIC_QUICKEN;
PLogI(IntToString(n)+".hasteRequested",haste);
if(haste)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);
if(n>=4){
object e=PSpawn("nw_bandit001",x+1.5,y);
SetPlotFlag(e,TRUE);
SetIsTemporaryEnemy(e,c,FALSE,60.0);
SetIsTemporaryEnemy(c,e,FALSE,60.0);
DelayCommand(3.0,Fight(e,c));
}
float t=8.0+IntToFloat(n)*0.1;
DelayCommand(t,Begin(c,meta));
DelayCommand(t+22.0,Report(c));
}

void main(){
int i;
for(i=0;
i<8;
i++)Case(i);
DelayCommand(32.0,PDone());
}


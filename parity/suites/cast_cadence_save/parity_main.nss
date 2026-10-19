#include "pcombat_inc"
#include "parity_time"
void After(object c,int n){PLogF(GetTag(c)+".after"+IntToString(n),PNow()-GetLocalFloat(c,"start"),0.2);}
void Cast(object c,int meta,int n){AssignCommand(c,ActionCastSpellAtObject(102,c,meta,FALSE));AssignCommand(c,ActionDoCommand(After(c,n)));}
void Start(object c,int meta){SetLocalFloat(c,"start",PNow());PLogI(GetTag(c)+".level",GetLevelByClass(CLASS_TYPE_WIZARD,c));PLogI(GetTag(c)+".prepared",GetMemorizedSpellId(c,CLASS_TYPE_WIZARD,1,0));Cast(c,meta,0);}
void Report(object c){PLogI(GetTag(c)+".levelAfter",GetLevelByClass(CLASS_TYPE_WIZARD,c));PLogI(GetTag(c)+".impacts",GetLocalInt(c,"impacts"));PLogI(GetTag(c)+".normalReady1",GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,1,1));PLogI(GetTag(c)+".quickReady1",GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,5,1));}
void main(){if(GetLocalInt(GetModule(),"SETUP"))return;SetLocalInt(GetModule(),"SETUP",1);int i;for(i=0;i<3;i++){object c=PSpawn("cad_wiz",10.0+IntToFloat(i)*10.0,10.0);SetTag(c,IntToString(i));int meta=METAMAGIC_NONE;if(i==2)meta=METAMAGIC_QUICKEN;if(i==1)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);float delay=2.0;if(i>0)delay=3.0;DelayCommand(delay,Start(c,meta));DelayCommand(5.5,Cast(c,meta,1));DelayCommand(13.0,Report(c));}DelayCommand(5.3,PLog("__save__","1"));DelayCommand(15.0,PDone());}

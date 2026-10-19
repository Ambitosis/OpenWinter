#include "pcombat_inc"
#include "parity_time"
void Change(object o,int on){effect e=GetFirstEffect(o);while(GetIsEffectValid(e)){if(GetEffectType(e)==EFFECT_TYPE_HASTE)RemoveEffect(o,e);e=GetNextEffect(o);}if(on)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),o);PLogF(GetTag(o)+".change",PNow()-GetLocalFloat(o,"start"),0.15);}
void Finish(object o){PLogF(GetTag(o)+".finish",PNow()-GetLocalFloat(o,"start"),0.15);}
void Begin(object o,int on,float change){SetLocalFloat(o,"start",PNow());AssignCommand(o,ActionCastSpellAtObject(0,o,METAMAGIC_NONE,FALSE));AssignCommand(o,ActionDoCommand(Finish(o)));DelayCommand(change,Change(o,on));}
void Case(int n,int on,float change){object o=PSpawn("cad_wiz",5.0+IntToFloat(n%4)*18.0,5.0+IntToFloat(n/4)*18.0);SetTag(o,IntToString(n));SetPlotFlag(o,TRUE);if(!on)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),o);DelayCommand(3.0+IntToFloat(n)*0.2,Begin(o,on,change));}
void main(){Case(0,TRUE,0.5);Case(1,TRUE,1.0);Case(2,TRUE,1.6);Case(3,TRUE,1.85);Case(4,TRUE,2.2);Case(5,FALSE,0.25);Case(6,FALSE,0.6);Case(7,FALSE,0.9);Case(8,FALSE,1.1);Case(9,FALSE,1.4);DelayCommand(14.0,PDone());}

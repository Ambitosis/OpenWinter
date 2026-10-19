#include "parity_fx"
void Read(string key) {
 effect e=GetFirstEffect(OBJECT_SELF);while(GetIsEffectValid(e)){if(GetEffectTag(e)=="aop_probe")PLog(key+".applied",PFxShort(e));e=GetNextEffect(OBJECT_SELF);}
}
void main(){
 string key="actual"+IntToString(GetSpellId())+".meta"+IntToString(GetMetaMagicFeat());effect e=TagEffect(EffectAreaOfEffect(37,"aop_none","aop_none","aop_none"),"aop_probe");
 PLog(key+".created",PFxShort(e));PLogI(key+".level",GetCasterLevel(OBJECT_SELF));PLogI(key+".dc",GetSpellSaveDC());PLogI(key+".meta",GetMetaMagicFeat());
 ApplyEffectToObject(DURATION_TYPE_TEMPORARY,e,OBJECT_SELF,30.0);DelayCommand(1.0,Read(key));
}

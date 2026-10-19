#include "parity_fx"
void main(){effect e=EffectAreaOfEffect(GetLocalInt(OBJECT_SELF,"row"),"aop_none","aop_none","aop_none");PLog(GetLocalString(OBJECT_SELF,"key")+".creatureCtor",PFxShort(e));}

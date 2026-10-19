#include "parity_inc"
#include "parity_time"
void main(){object c=OBJECT_SELF;string k=GetTag(c);int n=GetLocalInt(c,"impacts")+1;SetLocalInt(c,"impacts",n);PLogF(k+".impact"+IntToString(n),PNow()-GetLocalFloat(c,"start"),0.15);PLogI(k+".spell"+IntToString(n),GetSpellId());PLogI(k+".stackAtImpact"+IntToString(n),GetItemStackSize(GetSpellCastItem()));ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectHeal(1),GetSpellTargetObject());}

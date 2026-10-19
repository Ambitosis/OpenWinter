#include "parity_inc"
void main(){string k=GetLocalString(OBJECT_SELF,"key");PLogI(k+".aoeSpell",GetSpellId());PLogI(k+".aoeLevel",GetCasterLevel(OBJECT_SELF));PLogI(k+".aoeDC",GetSpellSaveDC());}

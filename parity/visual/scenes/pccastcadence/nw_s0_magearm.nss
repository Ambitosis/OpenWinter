#include "cadclock_inc"
void main(){object c=OBJECT_SELF;int n=GetLocalInt(c,"cad_impacts");SetLocalInt(c,"cad_impacts",n+1);Event(c,"impact"+IntToString(n));Note(GetLocalString(c,"cad_key")+"."+GetLocalString(c,"cad_phase")+".meta"+IntToString(n),IntToString(GetMetaMagicFeat()));}

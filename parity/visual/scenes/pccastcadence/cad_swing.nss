#include "cadclock_inc"
void main(){object c=GetLastAttacker();if(GetLocalInt(c,"cad_swings")==0)Event(c,"firstSwing");SetLocalInt(c,"cad_swings",GetLocalInt(c,"cad_swings")+1);}

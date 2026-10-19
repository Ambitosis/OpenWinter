// Threatener OnEndCombatRound: when its rounds end.
#include "parity_inc"
#include "parity_time"
void main(){object c=OBJECT_SELF;SetLocalString(c,"eor",GetLocalString(c,"eor")+FloatToString(PSince(),0,2)+" ");}

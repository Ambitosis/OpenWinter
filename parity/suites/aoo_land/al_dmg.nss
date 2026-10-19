// Caster OnDamaged: when a blow landed.
#include "parity_inc"
#include "parity_time"
void main(){object c=OBJECT_SELF;SetLocalString(c,"dmg",GetLocalString(c,"dmg")+FloatToString(PSince(),0,2)+" ");}

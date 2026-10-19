// Caster OnPhysicalAttacked: who attacked it and when; the attacker's order is then cleared (its combat state and
// round stay), so every attack read is an attack of opportunity, not a follow-up.
#include "parity_inc"
#include "parity_time"
void main()
{
    object c=OBJECT_SELF;object a=GetLastAttacker();string k=GetTag(a);
    SetLocalString(c,"atk",GetLocalString(c,"atk")+k+"@"+FloatToString(PSince(),0,2)+" ");
    SetLocalInt(c,k,GetLocalInt(c,k)+1);
    AssignCommand(a,ClearAllActions());
}

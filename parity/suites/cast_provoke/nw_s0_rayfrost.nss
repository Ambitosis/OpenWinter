// Ray of Frost replaced by an observer: logs when it lands (on the caster) and makes the target a harmful
// spell's target (combat state, hostility) without hurting it.
#include "parity_inc"
#include "parity_time"
void main()
{
    object c=OBJECT_SELF;object t=GetSpellTargetObject();
    SetLocalString(c,"casts",GetLocalString(c,"casts")+"c@"+FloatToString(PSince(),0,2)+" ");
    SignalEvent(t,EventSpellCastAt(c,SPELL_RAY_OF_FROST,TRUE));
}

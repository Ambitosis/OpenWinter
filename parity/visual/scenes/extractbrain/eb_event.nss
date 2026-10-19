#include "eb_inc"
void main()
{
    object caster=GetLastSpellCaster();
    int valid=GetLastSpell()==716 && caster==GetLocalObject(OBJECT_SELF,"eb_caster");
    if(valid) SetLocalInt(OBJECT_SELF,"eb_events",GetLocalInt(OBJECT_SELF,"eb_events")+1);
    EBNote("event_target="+GetTag(OBJECT_SELF)+"|spell="+IntToString(GetLastSpell())
        +"|caster="+GetTag(caster)+"|valid="+IntToString(valid));
}

#include "cr_inc"
void main()
{
    object a=GetLastSpellCaster();object t=OBJECT_SELF;
    PLogI(GetTag(a)+".stock_event_spell",GetLastSpell());
    PLogI(GetTag(a)+".stock_event_caster",a==GetLocalObject(t,"caster"));
    if(GetLastSpell()==601 && a==GetLocalObject(t,"caster"))SetLocalInt(t,"spells",GetLocalInt(t,"spells")+1);
    State(a,t,"spell_event");Samples(a,t,"impact");
}

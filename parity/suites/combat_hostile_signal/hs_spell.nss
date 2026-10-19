#include "hs_inc"
void main()
{
    object a=GetLastSpellCaster();object t=OBJECT_SELF;
    string k=GetTag(GetLocalObject(t,"caster"));
    PLogI(k+".callback_caster",a==GetLocalObject(t,"caster"));
    PLogI(k+".callback_spell",GetLastSpell());PLogI(k+".callback_harmful",GetLastSpellHarmful());
    SetLocalInt(t,"spells",GetLocalInt(t,"spells")+1);
    HState(GetLocalObject(t,"caster"),t,"spell_callback");
}

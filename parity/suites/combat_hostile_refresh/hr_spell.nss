#include "hr_inc"
void main()
{
    object t=OBJECT_SELF;object a=GetLocalObject(t,"caster");
    int count=GetLocalInt(t,"spells")+1;SetLocalInt(t,"spells",count);
    string stage="spell"+IntToString(count);string k=GetTag(a)+"."+stage;
    PLogI(k+".caster",GetLastSpellCaster()==a);PLogI(k+".spell",GetLastSpell());
    PLogI(k+".harmful",GetLastSpellHarmful());HState(a,t,stage);
}

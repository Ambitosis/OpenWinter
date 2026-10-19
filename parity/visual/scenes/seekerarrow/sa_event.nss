#include "sa_inc"
void main()
{
    object a=GetLastSpellCaster();
    int valid=GetLastSpell()==601 && a==GetLocalObject(OBJECT_SELF,"caster");
    if(valid)SetLocalInt(OBJECT_SELF,"events",GetLocalInt(OBJECT_SELF,"events")+1);
    SANote("event_target="+GetTag(OBJECT_SELF)+"|caster="+GetTag(a)+"|spell="+IntToString(GetLastSpell())+"|valid="+IntToString(valid));
}

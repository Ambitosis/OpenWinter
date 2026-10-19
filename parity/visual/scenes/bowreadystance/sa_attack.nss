#include "sa_inc"
void main()
{
    object a=GetLastAttacker();
    int valid=a==GetLocalObject(OBJECT_SELF,"caster");
    if(valid)SetLocalInt(OBJECT_SELF,"attacks",GetLocalInt(OBJECT_SELF,"attacks")+1);
    SANote("attack_target="+GetTag(OBJECT_SELF)+"|caster="+GetTag(a)+"|valid="+IntToString(valid));
}

#include "cr_inc"
void main()
{
    object a=GetLastAttacker();object t=OBJECT_SELF;
    if(a!=GetLocalObject(t,"caster"))return;
    SetLocalInt(t,"attacks",GetLocalInt(t,"attacks")+1);
    State(a,t,"attack_event");
    if(GetTag(a)=="bow" && GetLocalInt(t,"attacks")==1)
    {
        State(a,t,"before_stop");AssignCommand(a,ClearAllActions(TRUE));
        DelayCommand(.05,State(a,t,"after_stop"));Samples(a,t,"stop");
    }
}

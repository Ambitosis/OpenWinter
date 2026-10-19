#include "parity_inc"
#include "parity_time"
void State(object a,object t,string stage)
{
    string k=GetTag(a)+"."+stage;
    PLogF(k+".time",GetLocalInt(a,"started")?PNow()-GetLocalFloat(a,"start"):-1.0,0.10);
    PLogI(k+".a_combat",GetIsInCombat(a));PLogI(k+".t_combat",GetIsInCombat(t));
    PLogI(k+".a_action",GetCurrentAction(a));PLogI(k+".t_action",GetCurrentAction(t));
    PLogI(k+".attacks",GetLocalInt(t,"attacks"));PLogI(k+".spells",GetLocalInt(t,"spells"));
    PLogF(k+".distance",GetDistanceBetween(a,t),0.01);
}
void Samples(object a,object t,string phase)
{
    DelayCommand(.25,State(a,t,phase+"025"));DelayCommand(.75,State(a,t,phase+"075"));
    DelayCommand(1.5,State(a,t,phase+"150"));DelayCommand(3.0,State(a,t,phase+"300"));
    DelayCommand(6.0,State(a,t,phase+"600"));DelayCommand(7.0,State(a,t,phase+"700"));
}

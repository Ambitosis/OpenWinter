#include "dy_inc"
#include "visual_inc"
// The real pointer rests on the death panel's Respawn button (dth_deathopts, 1280x720), then a posted click:
// the original takes a posted click where the real pointer is, without needing focus.
void Click()
{
    WriteTimestampedLogEntry("VISUAL|mouse|0.5,0.542|up");
    DelayCommand(0.6,WriteTimestampedLogEntry("VISUAL|click|0.5,0.542|0.15"));
}
void Done(){SetLocalInt(GetModule(),"stop",1);VDone();}
void Missed(object pc){if(GetLocalInt(GetModule(),"respawns")>0)return;DyNote(pc,"respawn_click_missed");DelayCommand(1.0,Done());}
// OnPlayerDeath: the stock nw_o0_death (death panel after 2.5 s), then Respawn is clicked.
void main()
{
    object pc=GetLastPlayerDied();object m=GetModule();int n=GetLocalInt(m,"deaths")+1;SetLocalInt(m,"deaths",n);
    DyNote(pc,"ev_death_"+IntToString(n));
    if(n>1)return;
    ExecuteScript("nw_o0_death",OBJECT_SELF);
    DelayCommand(0.1,DyNote(pc,"death_0.1"));
    DelayCommand(4.0,VShot("a_death_panel"));
    DelayCommand(5.0,Click());
    DelayCommand(12.0,Missed(pc));
}

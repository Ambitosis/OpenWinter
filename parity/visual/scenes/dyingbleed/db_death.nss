#include "dy_inc"
#include "visual_inc"
// A first click on the panel title brings the window forward; the pointer then rests on Respawn before the press.
void Mouse(float y,string s){WriteTimestampedLogEntry("VISUAL|mouse|0.5,"+FloatToString(y,0,3)+"|"+s);}
void Click()
{
    Mouse(0.214,"up");DelayCommand(0.3,Mouse(0.214,"down"));DelayCommand(0.5,Mouse(0.214,"up"));
    DelayCommand(1.5,Mouse(0.542,"up"));DelayCommand(2.0,Mouse(0.542,"down"));DelayCommand(2.2,Mouse(0.542,"up"));
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

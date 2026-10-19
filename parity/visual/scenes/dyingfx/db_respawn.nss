#include "dy_inc"
#include "visual_inc"
void Done(){SetLocalInt(GetModule(),"stop",1);VDone();}
// OnPlayerRespawn: the stock nw_o0_respawn (to the NW_DEATH_TEMPLE waypoint, with its penalty).
void main()
{
    object pc=GetLastRespawnButtonPresser();object m=GetModule();int n=GetLocalInt(m,"respawns")+1;SetLocalInt(m,"respawns",n);
    DyNote(pc,"ev_respawn_"+IntToString(n));
    if(n>1)return;
    ExecuteScript("nw_o0_respawn",OBJECT_SELF);
    DyNote(pc,"after_stock_respawn");
    DelayCommand(0.5,DyNote(pc,"respawn_0.5"));
    DelayCommand(1.5,VShot("b_respawned"));
    DelayCommand(3.0,DyNote(pc,"respawn_3"));
    DelayCommand(5.3,VShot("c_penalty_text"));
    DelayCommand(5.5,DyNote(pc,"respawn_5.5"));
    DelayCommand(7.0,Done());
}

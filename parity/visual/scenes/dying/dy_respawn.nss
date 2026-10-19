#include "dy_inc"
#include "visual_inc"
void Kill(object pc){DyNote(pc,"before_effectdeath");ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),pc);DyNote(pc,"after_effectdeath");}
// OnPlayerRespawn: records the press; the first press runs the stock nw_o0_respawn.
void main()
{
    object pc=GetLastRespawnButtonPresser();object m=GetModule();int n=GetLocalInt(m,"respawns")+1;SetLocalInt(m,"respawns",n);
    DyNote(pc,"ev_respawn_"+IntToString(n));
    if(n>1)return;
    ExecuteScript("nw_o0_respawn",OBJECT_SELF);
    DyNote(pc,"after_stock_respawn");
    DelayCommand(0.5,DyNote(pc,"respawn_0.5"));
    DelayCommand(1.5,VShot("e_respawned"));
    DelayCommand(5.3,VShot("f_penalty_text"));
    DelayCommand(5.5,DyNote(pc,"respawn_5.5"));
    DelayCommand(8.0,Kill(pc));
}

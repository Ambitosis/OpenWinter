#include "dy_inc"
#include "visual_inc"
// The real pointer rests on the death panel's Respawn button (dth_deathopts, 1280x720), then a posted click:
// the original takes a posted click where the real pointer is (a real press alone did not reach the panel).
void Click()
{
    WriteTimestampedLogEntry("VISUAL|mouse|0.5,0.542|up");
    DelayCommand(0.6,WriteTimestampedLogEntry("VISUAL|click|0.5,0.542|0.15"));
}
void Raise(object pc){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectResurrection(),pc);DyNote(pc,"raised");}
// If the Respawn click missed: raise and go on with the second death.
void Missed(object pc){if(GetLocalInt(GetModule(),"respawns")>0)return;DyNote(pc,"respawn_click_missed");ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectResurrection(),pc);
    DelayCommand(2.0,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),pc));}
void LivingPanel(object pc){PopUpDeathGUIPanel(pc,TRUE,TRUE,0,"");DyNote(pc,"living_panel");}
// OnPlayerDeath. First death: the stock nw_o0_death (death panel after 2.5 s), then Respawn is clicked.
// Second: a custom PopUpDeathGUIPanel, a raise, then the panel shown to the living player and Respawn clicked.
void main()
{
    object pc=GetLastPlayerDied();object m=GetModule();int n=GetLocalInt(m,"deaths")+1;SetLocalInt(m,"deaths",n);
    DyNote(pc,"ev_death_"+IntToString(n));
    if(n==1)
    {
        ExecuteScript("nw_o0_death",OBJECT_SELF);
        DelayCommand(1.5,VShot("d0_dead_body"));
        DelayCommand(4.0,VShot("d_death_panel"));
        DelayCommand(6.0,Click());
        DelayCommand(9.0,Missed(pc));
        return;
    }
    DelayCommand(1.0,PopUpDeathGUIPanel(pc,FALSE,TRUE,0,"Custom parity message: Respawn hidden, Wait For Help requested."));
    DelayCommand(3.0,VShot("g_custom_panel"));
    DelayCommand(5.0,Raise(pc));
    DelayCommand(6.5,VShot("h_raised"));
    DelayCommand(9.0,LivingPanel(pc));
    DelayCommand(10.5,VShot("i_living_panel"));
    DelayCommand(11.5,Click());
    DelayCommand(13.0,VShot("j_living_after"));
    DelayCommand(14.0,ExecuteScript("dy_rests",pc));
}

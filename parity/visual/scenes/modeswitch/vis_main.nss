#include "visual_inc"
void Switch(object p, int mode) {
    SetActionMode(p,mode,TRUE);
    WriteTimestampedLogEntry("VISUAL|note|switch="+IntToString(mode)
        +" power="+IntToString(GetActionMode(p,3))+" expertise="+IntToString(GetActionMode(p,8))
        +" defensive="+IntToString(GetActionMode(p,10)));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_POWER_ATTACK),p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_EXPERTISE),p);
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0,VCamera(p,90.0,9.0,60.0));
    DelayCommand(3.0,Switch(p,10)); DelayCommand(3.5,VShot("defensive"));
    DelayCommand(6.0,Switch(p,3)); DelayCommand(6.5,VShot("power_cancels_defensive"));
    DelayCommand(9.0,Switch(p,10)); DelayCommand(9.5,VShot("defensive_cancels_power"));
    DelayCommand(12.0,Switch(p,8)); DelayCommand(12.5,VShot("expertise_cancels_defensive"));
    DelayCommand(15.0,Switch(p,10));
    DelayCommand(15.1,Switch(p,3));
    DelayCommand(15.2,Switch(p,8));
    DelayCommand(15.6,VShot("burst_early"));
    DelayCommand(16.2,VShot("burst_later"));
    DelayCommand(17.5,VShot("burst_expired"));
    DelayCommand(20.0,VDone());
}

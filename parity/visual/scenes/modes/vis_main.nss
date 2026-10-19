#include "visual_inc"
void Toggle(object p, int mode, int on) {
    SetActionMode(p,mode,on);
    WriteTimestampedLogEntry("VISUAL|note|mode="+IntToString(mode)+" requested="+IntToString(on)+" actual="+IntToString(GetActionMode(p,mode)));
}
void main() {
    object p=GetEnteringObject(); if(!GetIsPC(p)) return;
    DelayCommand(1.5,WriteTimestampedLogEntry("VISUAL|note|initial keen="+IntToString(GetHasFeat(FEAT_KEEN_SENSE,p))+" detect="+IntToString(GetActionMode(p,0))+" legacy="+IntToString(GetDetectMode(p))));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_POWER_ATTACK),p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_IMPROVED_POWER_ATTACK),p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_EXPERTISE),p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_IMPROVED_EXPERTISE),p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_FLURRY_OF_BLOWS),p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_RAPID_SHOT),p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_DIRTY_FIGHTING),p);
    // Rapid Shot requires a ranged weapon in the original; other trials use the original equipment.
    object bow=CreateItemOnObject("nw_wbwln001",p);
    DelayCommand(44.0,AssignCommand(p,ActionEquipItem(bow,INVENTORY_SLOT_RIGHTHAND)));
    DelayCommand(49.5,AssignCommand(p,ActionUnequipItem(bow)));
    SetCameraMode(p,CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0,VCamera(p,90.0,9.0,60.0));
    int i;
    for(i=0;i<=11;i++) {
        float t=3.0+IntToFloat(i)*6.0;
        DelayCommand(t,Toggle(p,i,TRUE));
        DelayCommand(t+0.5,VShot("mode_"+IntToString(i)+"_on"));
        DelayCommand(t+3.0,Toggle(p,i,FALSE));
        DelayCommand(t+3.5,VShot("mode_"+IntToString(i)+"_off"));
    }
    DelayCommand(76.0,Toggle(p,10,TRUE));
    DelayCommand(77.0,VShot("lifetime_1"));
    DelayCommand(78.0,VShot("lifetime_2"));
    DelayCommand(79.0,VShot("lifetime_3"));
    DelayCommand(81.0,VShot("lifetime_5"));
    DelayCommand(81.5,SetActionMode(p,ACTION_MODE_POWER_ATTACK,TRUE));
    DelayCommand(82.0,SetActionMode(p,ACTION_MODE_EXPERTISE,TRUE));
    DelayCommand(82.5,VShot("exclusive_switch"));
    DelayCommand(84.0,WriteTimestampedLogEntry("VISUAL|key|84|tap"));
    DelayCommand(84.5,VShot("detect_key_once"));
    DelayCommand(87.0,WriteTimestampedLogEntry("VISUAL|key|84|tap"));
    DelayCommand(87.5,VShot("detect_key_twice"));
    DelayCommand(90.0,VDone());
}

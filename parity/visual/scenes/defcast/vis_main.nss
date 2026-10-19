#include "visual_inc"
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Result(object p, object e, string label) {
    Note(label + " mode=" + IntToString(GetActionMode(p, ACTION_MODE_DEFENSIVE_CAST))
        + " defensive=" + IntToString(GetDefensiveCastingMode(p))
        + " slots=" + IntToString(GetHasSpell(SPELL_MAGIC_MISSILE, p))
        + " impacts=" + IntToString(GetLocalInt(p, "impacts"))
        + " distance=" + FloatToString(GetDistanceBetween(p,e),0,2)
        + " attacks=" + IntToString(GetLocalInt(p,"attacks"))
        + " action=" + IntToString(GetCurrentAction(p)));
}
void Cast(object p, object e, string label, int mode) {
    AssignCommand(p, ClearAllActions(TRUE));
    SetActionMode(p, ACTION_MODE_DEFENSIVE_CAST, mode);
    Note(label + " start mode=" + IntToString(GetActionMode(p, ACTION_MODE_DEFENSIVE_CAST))
        + " conc=" + IntToString(GetSkillRank(SKILL_CONCENTRATION, p))
        + " slots=" + IntToString(GetHasSpell(SPELL_MAGIC_MISSILE, p)));
    if(label != "baseline.direct") AssignCommand(p, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, e, METAMAGIC_NONE, FALSE));
    DelayCommand(0.8, Result(p,e,label + ".conjure"));
    DelayCommand(5.5, Result(p,e,label + ".result"));
}
void Alone(object p) {
    AssignCommand(p,ClearAllActions(TRUE));
    DestroyObject(GetLocalObject(p,"enemy"));
    DestroyObject(GetLocalObject(p,"dummy"));
    SetMemorizedSpell(p,CLASS_TYPE_WIZARD,1,0,SPELL_MAGIC_MISSILE,TRUE);
    effect ef=GetFirstEffect(p);
    while(GetIsEffectValid(ef)) {
        if(GetEffectType(ef)==EFFECT_TYPE_SKILL_INCREASE) RemoveEffect(p,ef);
        ef=GetNextEffect(p);
    }
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSkillDecrease(SKILL_CONCENTRATION,50),p);
    SetLocalInt(p,"impacts",0);
    SetLocalInt(p,"attacks",0);
    DelayCommand(0.5,Cast(p,p,"on.low.alone",TRUE));
}
void Feedback(object p) {
    SetActionMode(p,ACTION_MODE_DEFENSIVE_CAST,FALSE);
    DelayCommand(0.5,SetActionMode(p,ACTION_MODE_DEFENSIVE_CAST,TRUE));
    DelayCommand(1.0,VShot("mode_feedback"));
    DelayCommand(2.0,WriteTimestampedLogEntry("VISUAL|key|84|tap"));
}
void DetectResult(object p) {
    Note("detect.key mode="+IntToString(GetActionMode(p,ACTION_MODE_DETECT))
        +" defensive="+IntToString(GetActionMode(p,ACTION_MODE_DEFENSIVE_CAST)));
    VShot("detect_independent");
}
void Trial(object p, string label, int mode, int bonus, float distance, int fight=FALSE) {
    AssignCommand(p, ClearAllActions(TRUE));
    DestroyObject(GetLocalObject(p,"enemy"));
    DestroyObject(GetLocalObject(p,"dummy"));
    vector v=GetPosition(p);
    object e=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(p),Vector(v.x+distance,v.y,v.z),180.0));
    int i; for(i=EVENT_SCRIPT_CREATURE_ON_HEARTBEAT;i<=EVENT_SCRIPT_CREATURE_ON_BLOCKED_BY_DOOR;i++) SetEventScript(e,i,"");
    AssignCommand(e,ClearAllActions(TRUE));
    SetAILevel(e,AI_LEVEL_NORMAL);
    SetPlotFlag(e,TRUE);
    ChangeToStandardFaction(e,STANDARD_FACTION_HOSTILE);
    SetLocalObject(p,"enemy",e);
    if(fight == 1) {
        object d=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(p),Vector(v.x+distance-1.0,v.y+1.0,v.z),180.0));
        for(i=EVENT_SCRIPT_CREATURE_ON_HEARTBEAT;i<=EVENT_SCRIPT_CREATURE_ON_BLOCKED_BY_DOOR;i++) SetEventScript(d,i,"");
        AssignCommand(d,ClearAllActions(TRUE));
        SetPlotFlag(d,TRUE);
        ChangeToStandardFaction(d,STANDARD_FACTION_DEFENDER);
        SetLocalObject(p,"dummy",d);
        AssignCommand(e,ActionAttack(d));
    }
    if(fight == 2) AssignCommand(e,ActionAttack(p));
    SetLocalInt(p,"attacks",0);
    SetMemorizedSpell(p, CLASS_TYPE_WIZARD, 1, 0, SPELL_MAGIC_MISSILE, TRUE);
    effect ef = GetFirstEffect(p);
    while (GetIsEffectValid(ef)) {
        if (GetEffectType(ef) == EFFECT_TYPE_SKILL_INCREASE || GetEffectType(ef) == EFFECT_TYPE_SKILL_DECREASE) RemoveEffect(p, ef);
        ef = GetNextEffect(p);
    }
    if (bonus > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_CONCENTRATION, bonus), p);
    if (bonus < 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillDecrease(SKILL_CONCENTRATION, -bonus), p);
    SetLocalInt(p, "impacts", 0);
    DelayCommand(0.5, Cast(p,e,label,mode));
}
void main() {
    object p = GetEnteringObject(); if (!GetIsPC(p)) return;
    DelayCommand(1.5,Note("character race="+IntToString(GetRacialType(p))+" combatCasting="+IntToString(GetHasFeat(FEAT_COMBAT_CASTING,p))+" keen="+IntToString(GetHasFeat(FEAT_KEEN_SENSE,p))));
    SetImmortal(p, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_BLUDGEONING,100),p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_PIERCING,100),p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDamageImmunityIncrease(DAMAGE_TYPE_SLASHING,100),p);
    SetCameraMode(p, CAMERA_MODE_TOP_DOWN);
    SetEventScript(p,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"dc_attacked");
    DelayCommand(1.0, VCamera(p,90.0,9.0,60.0));
    DelayCommand(3.0, Trial(p,"off.low.idle",FALSE,-50,1.5));
    DelayCommand(11.0, Trial(p,"on.low.idle",TRUE,-50,1.5));
    DelayCommand(19.0, Trial(p,"on.high.idle",TRUE,50,1.5));
    DelayCommand(27.0, Trial(p,"on.low.far",TRUE,-50,10.0));
    DelayCommand(35.0, Trial(p,"off.low.fight",FALSE,-50,1.5,TRUE));
    DelayCommand(41.5,VShot("normal_cast"));
    DelayCommand(43.0, Trial(p,"on.low.fight",TRUE,-50,1.5,TRUE));
    DelayCommand(49.5,VShot("defensive_failure"));
    DelayCommand(51.0, Trial(p,"on.high.fight",TRUE,50,1.5,TRUE));
    DelayCommand(57.5,VShot("defensive_success"));
    DelayCommand(59.0,Trial(p,"off.low.direct",FALSE,-50,1.5,2));
    DelayCommand(65.5,VShot("direct_normal"));
    DelayCommand(67.0,Trial(p,"on.high.direct",TRUE,50,1.5,2));
    DelayCommand(73.5,VShot("direct_defensive"));
    DelayCommand(75.0,Trial(p,"baseline.direct",FALSE,50,1.5,2));
    DelayCommand(83.0,Alone(p));
    DelayCommand(90.0,Feedback(p));
    DelayCommand(94.0,DetectResult(p));
    DelayCommand(96.0,VDone());
}

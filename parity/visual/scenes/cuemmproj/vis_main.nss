const int SPELL=107;
const int LEVEL=1;
const int SLOT=1;
const string CUE="spr_magicmissile";

#include "visual_inc"
void Note(string text){WriteTimestampedLogEntry("VISUAL|note|"+text);}
void Positive(object pc,string phase){Note("positive."+phase+"."+CUE);AssignCommand(pc,PlaySound(CUE));}
void Snapshot(object pc,string phase){
    object target=GetLocalObject(GetModule(),"cue_target");
    string k="state."+phase+".";
    Note(k+"pc."+IntToString(GetIsPC(pc))+".name."+GetName(pc)+".wizard."+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc))+".gender."+IntToString(GetGender(pc))+".soundset."+IntToString(GetSoundset(pc)));
    Note(k+"slot."+IntToString(GetMemorizedSpellId(pc,CLASS_TYPE_WIZARD,LEVEL,SLOT))+".ready."+IntToString(GetMemorizedSpellReady(pc,CLASS_TYPE_WIZARD,LEVEL,SLOT))+".known."+IntToString(GetIsInKnownSpellList(pc,CLASS_TYPE_WIZARD,SPELL)));
    Note(k+"target."+IntToString(GetIsObjectValid(target))+".seen."+IntToString(GetObjectSeen(target,pc))+".enemy."+IntToString(GetIsEnemy(target,pc))+".plot."+IntToString(GetPlotFlag(target))+".difficulty."+IntToString(GetGameDifficulty())+".hp."+IntToString(GetCurrentHitPoints(target)));
}
void Finished(){Note("action.finished");SetLocalInt(GetModule(),"cue_finished",1);}
void Cast(object pc){
    object target=GetLocalObject(GetModule(),"cue_target");Snapshot(pc,"before");
    int ready=GetIsPC(pc)&&GetName(pc)=="Aluvian Darkstar"&&GetLevelByClass(CLASS_TYPE_WIZARD,pc)==1&&GetGender(pc)==1&&GetSoundset(pc)==227&&GetIsObjectValid(target)&&!GetPlotFlag(target)&&GetObjectSeen(target,pc)&&GetIsEnemy(target,pc)&&GetIsEnemy(pc,target)&&GetMemorizedSpellId(pc,CLASS_TYPE_WIZARD,LEVEL,SLOT)==SPELL&&GetMemorizedSpellReady(pc,CLASS_TYPE_WIZARD,LEVEL,SLOT)&&GetIsInKnownSpellList(pc,CLASS_TYPE_WIZARD,SPELL)&&!GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,pc))&&!GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_LEFTHAND,pc));
    Note("ready."+IntToString(ready));if(!ready){Note("INVALID_CAST_CONTEXT");VDone();return;}
    Note("start."+IntToString(SPELL));AssignCommand(pc,ActionCastSpellAtObject(SPELL,target,METAMAGIC_NONE,FALSE));AssignCommand(pc,ActionDoCommand(Finished()));
}
void Check(object pc){
    Snapshot(pc,"after");int hooks=GetLocalInt(GetModule(),"cue_hooks");int hits=GetLocalInt(GetModule(),"cue_hits");int events=GetLocalInt(GetModule(),"cue_events");int finished=GetLocalInt(GetModule(),"cue_finished");
    Note("execution.hooks."+IntToString(hooks)+".hits."+IntToString(hits)+".events."+IntToString(events)+".finished."+IntToString(finished));
    if(hooks!=1||hits<1||events!=1||!finished||GetMemorizedSpellReady(pc,CLASS_TYPE_WIZARD,LEVEL,SLOT))Note("INVALID_EXECUTION");
}
void Cleanup(){object target=GetLocalObject(GetModule(),"cue_target");SetIsDestroyable(TRUE,FALSE,FALSE,target);DestroyObject(target);Note("cleanup.target");}
void Done(){Note("cue.completed");VDone();}
void main(){
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);int i;for(i=5000;i<=5012;i++)SetEventScript(pc,i,"");SetPlotFlag(pc,TRUE);
    SetMemorizedSpell(pc,CLASS_TYPE_WIZARD,LEVEL,SLOT,SPELL,TRUE);
    object target=VSpawn(pc,"nw_bandit001",0.0,6.0,270.0);for(i=5000;i<=5012;i++)SetEventScript(target,i,"");AssignCommand(target,ClearAllActions(TRUE));SetPlotFlag(target,FALSE);SetCommandable(FALSE,target);
    ChangeToStandardFaction(target,STANDARD_FACTION_HOSTILE);SetIsTemporaryEnemy(pc,target);SetIsTemporaryEnemy(target,pc);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectTemporaryHitpoints(10000),target);
    SetEventScript(target,5002,"cue_event");SetEventScript(target,5004,"cue_damage");SetLocalObject(GetModule(),"cue_target",target);SetLocalString(GetModule(),"X2_S_UD_SPELLSCRIPT","cue_hook");
    DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));DelayCommand(2.0,Positive(pc,"before"));DelayCommand(10.0,Cast(pc));
    DelayCommand(10.75,VShot("conjure"));DelayCommand(12.5,VShot("release"));DelayCommand(16.0,Check(pc));DelayCommand(17.0,VShot("after"));
    DelayCommand(18.0,Cleanup());DelayCommand(20.0,Positive(pc,"after"));DelayCommand(26.0,Done());
}

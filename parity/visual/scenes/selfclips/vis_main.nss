#include "visual_inc"
void Note(string key,string value){WriteTimestampedLogEntry("VISUAL|note|"+key+"|"+value);}
void Reset(object pc){
    ReplaceObjectAnimation(pc,"pause1","");
    ReplaceObjectAnimation(pc,"conjure1","");
    ReplaceObjectAnimation(pc,"castself","");
    ReplaceObjectAnimation(pc,"castselflp","");
    effect e=GetFirstEffect(pc);while(GetIsEffectValid(e)){RemoveEffect(pc,e);e=GetFirstEffect(pc);}
}
void After(object pc,string label){Note(label+".spent",IntToString(!GetMemorizedSpellReady(pc,CLASS_TYPE_WIZARD,1,0)));}
void Cast(object pc,string label,string clip){
    Reset(pc);SetLocalString(pc,"sc_case",label);
    if(clip!="")ReplaceObjectAnimation(pc,clip,"sitcross");
    SetMemorizedSpell(pc,CLASS_TYPE_WIZARD,1,0,SPELL_MAGE_ARMOR,TRUE);
    Note(label+".slot",IntToString(GetMemorizedSpellId(pc,CLASS_TYPE_WIZARD,1,0)));
    Note(label+".ready",IntToString(GetMemorizedSpellReady(pc,CLASS_TYPE_WIZARD,1,0)));
    Note(label+".replacement",clip);
    AssignCommand(pc,ActionCastSpellAtObject(SPELL_MAGE_ARMOR,pc,METAMAGIC_NONE,FALSE));
    AssignCommand(pc,ActionDoCommand(After(pc,label)));
    DelayCommand(.7,VShot(label+"_070"));DelayCommand(1.7,VShot(label+"_170"));
    DelayCommand(2.1,VShot(label+"_210"));DelayCommand(2.5,VShot(label+"_250"));
    DelayCommand(2.9,VShot(label+"_290"));DelayCommand(3.5,VShot(label+"_350"));
}
void Positive(object pc){
    Reset(pc);ReplaceObjectAnimation(pc,"pause1","sitcross");
    AssignCommand(pc,ActionPlayAnimation(ANIMATION_LOOPING_PAUSE,1.0,2.0));
    DelayCommand(1.0,VShot("replacement_positive"));
}
void main(){
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"sc_started"))return;
    SetLocalInt(GetModule(),"sc_started",TRUE);SetLocalString(GetModule(),"X2_S_UD_SPELLSCRIPT","sc_hook");
    Note("identity",GetName(pc)+".pc."+IntToString(GetIsPC(pc))+".wizard."+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc))+".known."+IntToString(GetIsInKnownSpellList(pc,CLASS_TYPE_WIZARD,SPELL_MAGE_ARMOR)));
    int i;for(i=5000;i<=5012;i++)SetEventScript(pc,i,"");SetPlotFlag(pc,TRUE);
    SetAILevel(pc,AI_LEVEL_NORMAL);AssignCommand(pc,ClearAllActions(TRUE));
    DelayCommand(1.0,VCamera(pc,270.0,7.0,65.0));
    DelayCommand(3.0,Cast(pc,"baseline",""));
    DelayCommand(9.0,Cast(pc,"release","castself"));
    DelayCommand(15.0,Cast(pc,"recovery","castselflp"));
    DelayCommand(21.0,Cast(pc,"preparation","conjure1"));
    DelayCommand(27.0,Positive(pc));DelayCommand(30.0,Reset(pc));DelayCommand(32.0,VDone());
}

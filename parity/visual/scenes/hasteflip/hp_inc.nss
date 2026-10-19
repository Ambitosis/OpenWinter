#include "visual_inc"
void HNote(string key,string value){WriteTimestampedLogEntry("VISUAL|note|"+key+"|"+value);}
void HClock(int tick){SetLocalInt(GetModule(),"hp_clock",tick);}
int Hasted(object c){effect e=GetFirstEffect(c);while(GetIsEffectValid(e)){if(GetEffectType(e)==EFFECT_TYPE_HASTE)return TRUE;e=GetNextEffect(c);}return FALSE;}
void HEvent(object c,string key){HNote(GetLocalString(c,"hp_case")+"."+key,IntToString(GetLocalInt(GetModule(),"hp_clock")));}
void HSet(object c,int active){effect e=GetFirstEffect(c);while(GetIsEffectValid(e)){if(GetEffectType(e)==EFFECT_TYPE_HASTE)RemoveEffect(c,e);e=GetNextEffect(c);}if(active)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);HEvent(c,"haste_change");HNote(GetLocalString(c,"hp_case")+".haste_after",IntToString(Hasted(c)));}
void HAfter(object c){HEvent(c,"after");HNote(GetLocalString(c,"hp_case")+".spent",IntToString(!GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,1,0)));}
void HClean(object c){effect e=GetFirstEffect(c);while(GetIsEffectValid(e)){RemoveEffect(c,e);e=GetFirstEffect(c);}}
void HStart(object c,int spell,string label,int initial,float change){
 SetLocalString(c,"hp_case",label);HSet(c,initial);SetMemorizedSpell(c,CLASS_TYPE_WIZARD,1,0,spell,TRUE);
 HNote(label+".slot",IntToString(GetMemorizedSpellId(c,CLASS_TYPE_WIZARD,1,0)));HNote(label+".ready",IntToString(GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,1,0)));HNote(label+".initial_haste",IntToString(Hasted(c)));HEvent(c,"start");
 AssignCommand(c,ActionCastSpellAtObject(spell,c,METAMAGIC_NONE,FALSE));AssignCommand(c,ActionDoCommand(HAfter(c)));
 if(change>0.0)DelayCommand(change,HSet(c,!initial));
 DelayCommand(0.25,VShot(label+"_025"));DelayCommand(0.70,VShot(label+"_070"));DelayCommand(1.00,VShot(label+"_100"));DelayCommand(1.40,VShot(label+"_140"));DelayCommand(1.70,VShot(label+"_170"));DelayCommand(2.00,VShot(label+"_200"));DelayCommand(2.30,VShot(label+"_230"));DelayCommand(2.60,VShot(label+"_260"));DelayCommand(3.00,VShot(label+"_300"));DelayCommand(3.40,VShot(label+"_340"));DelayCommand(5.0,HClean(c));
}
void HMain(int spell){object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(GetModule(),"hp_started"))return;SetLocalInt(GetModule(),"hp_started",TRUE);SetLocalString(GetModule(),"X2_S_UD_SPELLSCRIPT","hp_hook");int i;for(i=0;i<1100;i++)DelayCommand(IntToFloat(i)/20.0,HClock(i));for(i=5000;i<=5012;i++)SetEventScript(pc,i,"");SetPlotFlag(pc,TRUE);SetAILevel(pc,AI_LEVEL_NORMAL);AssignCommand(pc,ClearAllActions(TRUE));HNote("identity",GetName(pc)+".pc."+IntToString(GetIsPC(pc))+".wizard."+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc))+".known."+IntToString(GetIsInKnownSpellList(pc,CLASS_TYPE_WIZARD,spell)));DelayCommand(1.0,VCamera(pc,270.0,7.0,65.0));
 DelayCommand(4.0,HStart(pc,spell,"normal",FALSE,0.0));DelayCommand(12.0,HStart(pc,spell,"fast",TRUE,0.0));DelayCommand(20.0,HStart(pc,spell,"add_conjure",FALSE,0.5));DelayCommand(28.0,HStart(pc,spell,"add_release",FALSE,1.7));DelayCommand(36.0,HStart(pc,spell,"remove_conjure",TRUE,0.5));DelayCommand(44.0,HStart(pc,spell,"remove_recover",TRUE,1.6));DelayCommand(50.0,VDone());}

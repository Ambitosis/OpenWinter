#include "visual_inc"
void HNote(string key,string value){WriteTimestampedLogEntry("VISUAL|note|"+key+"|"+value);}
void HClock(int tick){SetLocalInt(GetModule(),"hl_clock",tick);}
void HEvent(object c,string key){HNote(GetLocalString(c,"hl_case")+"."+key,IntToString(GetLocalInt(GetModule(),"hl_clock")));}
void HSet(object c,int active){effect e=GetFirstEffect(c);while(GetIsEffectValid(e)){if(GetEffectType(e)==EFFECT_TYPE_HASTE)RemoveEffect(c,e);e=GetNextEffect(c);}if(active)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);HEvent(c,"haste"+IntToString(active));}
void HAfter(object c){HEvent(c,"after");HNote(GetLocalString(c,"hl_case")+".spent",IntToString(!GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,1,0)));}
void HShot(object c,string suffix){VShot(GetLocalString(c,"hl_case")+suffix);}
void HStart(object c,string label,int initial,int late){
 SetLocalString(c,"hl_case",label);HSet(c,initial);SetMemorizedSpell(c,CLASS_TYPE_WIZARD,1,0,102,TRUE);
 HNote(label+".slot",IntToString(GetMemorizedSpellId(c,CLASS_TYPE_WIZARD,1,0)));HNote(label+".ready",IntToString(GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,1,0)));HEvent(c,"start");
 AssignCommand(c,ActionCastSpellAtObject(102,c,METAMAGIC_NONE,FALSE));AssignCommand(c,ActionDoCommand(HAfter(c)));
 if(!late){DelayCommand(0.5,HSet(c,!initial));DelayCommand(1.0,HSet(c,initial));DelayCommand(1.5,HSet(c,!initial));DelayCommand(2.0,HSet(c,initial));}
 else {DelayCommand(8.2,HSet(c,TRUE));DelayCommand(8.7,HSet(c,FALSE));}
 DelayCommand(1.25,HShot(c,"_125"));DelayCommand(2.5,HShot(c,"_250"));DelayCommand(4.6,HShot(c,"_460"));DelayCommand(6.8,HShot(c,"_680"));DelayCommand(7.3,HShot(c,"_730"));DelayCommand(7.8,HShot(c,"_780"));DelayCommand(8.1,HShot(c,"_810"));DelayCommand(8.5,HShot(c,"_850"));DelayCommand(9.1,HShot(c,"_910"));DelayCommand(9.7,HShot(c,"_970"));DelayCommand(10.5,HShot(c,"_1050"));
}

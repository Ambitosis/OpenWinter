#include "visual_inc"
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|"+s); }
void Quiet(object o) { int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,""); SetCommandable(FALSE,o); SetPlotFlag(o,TRUE); }
void Clean(object pc) {
 AssignCommand(pc,ClearAllActions(TRUE));
 effect e=GetFirstEffect(pc);while(GetIsEffectValid(e)){RemoveEffect(pc,e);e=GetFirstEffect(pc);}
 object o=GetFirstObjectInArea(GetArea(pc));
 while(GetIsObjectValid(o)) {object next=GetNextObjectInArea(GetArea(pc));
  if(o!=pc && (GetObjectType(o)==OBJECT_TYPE_CREATURE || GetObjectType(o)==OBJECT_TYPE_AREA_OF_EFFECT)) {AssignCommand(o,SetIsDestroyable(TRUE,FALSE,FALSE));DestroyObject(o);}
  o=next;
 }
}
void Prepare(object pc,int gender) {
 Clean(pc);SetGender(pc,gender);
 object target=VSpawn(pc,"nw_bandit001",0.0,6.0,270.0);Quiet(target);
 ChangeToStandardFaction(target,STANDARD_FACTION_HOSTILE);
 SetLocalObject(GetModule(),"chant_target",target);
}
void Finished(int spell) {Note("finished."+IntToString(spell));}
void Cast(object pc,int spell,int hostile,int ground) {
 object target=pc;if(hostile)target=GetLocalObject(GetModule(),"chant_target");
 Note("start."+IntToString(spell));
 Note("selector."+IntToString(spell)+".gender."+IntToString(GetGender(pc))+".soundset."+IntToString(GetSoundset(pc))+".target."+IntToString(GetIsObjectValid(target)));
 if(ground)AssignCommand(pc,ActionCastSpellAtLocation(spell,GetLocation(target),METAMAGIC_NONE,TRUE));
 else AssignCommand(pc,ActionCastSpellAtObject(spell,target,METAMAGIC_NONE,TRUE));
 AssignCommand(pc,ActionDoCommand(Finished(spell)));
}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);SetPlotFlag(pc,TRUE);
 SetLocalString(GetModule(),"X2_S_UD_SPELLSCRIPT","chant_hook");
 DelayCommand(1.0,VCamera(pc,90.0,9.0,65.0));
 DelayCommand(3.0,Prepare(pc,0));
 DelayCommand(4.0,Cast(pc,0,1,1));
 DelayCommand(4.75,VShot("chant_0"));
 DelayCommand(9.0,Prepare(pc,0));
 DelayCommand(10.0,Cast(pc,1,0,0));
 DelayCommand(10.75,VShot("chant_1"));
 DelayCommand(15.0,Prepare(pc,0));
 DelayCommand(16.0,Cast(pc,2,0,0));
 DelayCommand(16.75,VShot("chant_2"));
 DelayCommand(21.0,Prepare(pc,0));
 DelayCommand(22.0,Cast(pc,5,1,1));
 DelayCommand(22.75,VShot("chant_5"));
 DelayCommand(27.0,Prepare(pc,0));
 DelayCommand(28.0,Cast(pc,11,1,1));
 DelayCommand(28.75,VShot("chant_11"));
 DelayCommand(33.0,Prepare(pc,0));
 DelayCommand(34.0,Cast(pc,18,1,1));
 DelayCommand(34.75,VShot("chant_18"));
 DelayCommand(39.0,Prepare(pc,0));
 DelayCommand(40.0,Cast(pc,20,0,0));
 DelayCommand(40.75,VShot("chant_20"));
 DelayCommand(45.0,Prepare(pc,0));
 DelayCommand(46.0,Cast(pc,32,0,0));
 DelayCommand(46.75,VShot("chant_32"));
 DelayCommand(51.0,Prepare(pc,0));
 DelayCommand(52.0,Cast(pc,44,1,0));
 DelayCommand(52.75,VShot("chant_44"));
 DelayCommand(57.0,Prepare(pc,0));
 DelayCommand(58.0,Cast(pc,55,1,0));
 DelayCommand(58.75,VShot("chant_55"));
 DelayCommand(65.0,Clean(pc));
 DelayCommand(67.0,VDone());
}

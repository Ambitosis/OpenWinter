#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void State(object pc,string phase){
 object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);
 Note(phase+".pc."+IntToString(GetIsPC(pc))+".wizard."+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc))+".type."+IntToString(GetFamiliarCreatureType(pc))+".valid."+IntToString(GetIsObjectValid(f))+".master."+IntToString(GetMaster(f)==pc)+".pcposs."+IntToString(GetIsPossessedFamiliar(pc))+".famposs."+IntToString(GetIsPossessedFamiliar(f))+".fampc."+IntToString(GetIsPC(f))+".firstPCmaster."+IntToString(GetFirstPC()==pc));
 if(GetIsObjectValid(f))Note(phase+".familiar."+GetResRef(f)+".name."+GetName(f)+".player."+GetPCPlayerName(f));
}
void Ready(object pc){
 object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);State(pc,"ready");
 if(!GetIsObjectValid(f)||GetMaster(f)!=pc||GetLevelByClass(CLASS_TYPE_WIZARD,pc)!=1||GetName(pc)!="000 Familiar Possession"){Note("INVALID_CONTEXT");VDone();return;}
 SetPlotFlag(f,TRUE);AssignCommand(f,ClearAllActions());
 AssignCommand(f,JumpToLocation(Location(GetArea(pc),GetPosition(pc)+Vector(3.0,0.0,0.0),180.0)));
}
void Release(object pc,int context){
 object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);
 if(context==0)UnpossessFamiliar(pc);
 else if(context==1)AssignCommand(pc,UnpossessFamiliar(pc));
 else UnpossessFamiliar(f);
}
void KillFamiliar(object pc){object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);SetPlotFlag(f,FALSE);SetImmortal(f,FALSE);ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(FALSE,FALSE),f);}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 Note("character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
 SetPlotFlag(pc,TRUE);SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
 DelayCommand(1.0,SummonFamiliar(pc));DelayCommand(4.0,Ready(pc));
 DelayCommand(5.0,VCamera(pc,90.0,8.0,60.0));DelayCommand(6.0,VShot("a_ready"));
 DelayCommand(8.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(9.0,VShot("b_self_root"));
 DelayCommand(10.0,WriteTimestampedLogEntry("VISUAL|key|100|tap"));
 DelayCommand(11.0,VShot("c_special"));
 DelayCommand(12.0,WriteTimestampedLogEntry("VISUAL|key|97|tap"));
 DelayCommand(13.0,State(pc,"possessed"));DelayCommand(13.1,VShot("d_possessed"));
 DelayCommand(14.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));DelayCommand(14.5,VShot("d_familiar_root"));
 DelayCommand(15.0,Release(pc,0));DelayCommand(16.0,State(pc,"release_master_direct"));
 DelayCommand(17.0,Release(pc,1));DelayCommand(18.0,State(pc,"release_master_self"));
 DelayCommand(19.0,Release(pc,2));DelayCommand(20.0,State(pc,"release_familiar_direct"));
 DelayCommand(21.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(22.0,WriteTimestampedLogEntry("VISUAL|key|100|tap"));
 DelayCommand(23.0,WriteTimestampedLogEntry("VISUAL|key|97|tap"));
 DelayCommand(24.0,State(pc,"possessed_again"));
 DelayCommand(25.0,KillFamiliar(pc));
 DelayCommand(26.0,State(pc,"killed"));DelayCommand(26.1,VShot("e_killed"));
 DelayCommand(27.0,VDone());
}

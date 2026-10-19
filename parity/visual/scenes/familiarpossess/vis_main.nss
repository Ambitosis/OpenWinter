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
 SetCampaignInt("familiar_probe","familiar",29,f);SetPlotFlag(f,TRUE);AssignCommand(f,ClearAllActions());

}
void Release(object pc,int context){
 object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);
 if(context==0)UnpossessFamiliar(pc);
 else if(context==1)AssignCommand(pc,UnpossessFamiliar(pc));
 else UnpossessFamiliar(f);
}
void KillFamiliar(object pc){object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);SetPlotFlag(f,FALSE);SetImmortal(f,FALSE);ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(FALSE,FALSE),f);}
void Campaign(object pc){object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);Note("campaign.master."+IntToString(GetCampaignInt("familiar_probe","master",pc))+".familiar."+IntToString(GetCampaignInt("familiar_probe","master",f))+".familiarown."+IntToString(GetCampaignInt("familiar_probe","familiar",f)));Note("campaign.inactive_global."+IntToString(GetCampaignInt("familiar_probe","familiar",pc))+".global."+IntToString(GetCampaignInt("familiar_probe","familiar")));ExportAllCharacters();}
void PositionState(object pc,string phase){object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc); vector a=GetPosition(pc);vector b=GetPosition(f);Note("position."+phase+".master."+FloatToString(a.x)+","+FloatToString(a.y)+".familiar."+FloatToString(b.x)+","+FloatToString(b.y));}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 Note("character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));
 SetCampaignInt("familiar_probe","master",73,pc);SetPlotFlag(pc,TRUE);SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
 DelayCommand(1.0,SummonFamiliar(pc));DelayCommand(4.0,Ready(pc));
 DelayCommand(5.0,WriteTimestampedLogEntry("VISUAL|key|13|tap"));
 DelayCommand(5.25,WriteTimestampedLogEntry("VISUAL|text|ACCESS_READY"));
 DelayCommand(6.0,WriteTimestampedLogEntry("VISUAL|key|13|tap"));
 DelayCommand(6.3,VShot("a_ready"));
 DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));
 DelayCommand(7.6,WriteTimestampedLogEntry("VISUAL|key|100|tap"));
 DelayCommand(8.2,WriteTimestampedLogEntry("VISUAL|key|97|tap"));
 DelayCommand(9.0,State(pc,"possessed"));DelayCommand(13.1,VShot("d_possessed"));
 DelayCommand(13.2,PositionState(pc,"before"));
 DelayCommand(14.0,WriteTimestampedLogEntry("VISUAL|key|87|down"));
 DelayCommand(14.8,WriteTimestampedLogEntry("VISUAL|key|87|up"));
 DelayCommand(15.2,PositionState(pc,"after"));DelayCommand(15.3,VShot("e_moved"));
 DelayCommand(16.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(17.0,VShot("f_inventory"));
 DelayCommand(18.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 DelayCommand(19.0,WriteTimestampedLogEntry("VISUAL|key|67|tap"));DelayCommand(20.0,VShot("g_character"));
 DelayCommand(21.0,WriteTimestampedLogEntry("VISUAL|key|67|tap"));
 DelayCommand(22.0,Campaign(pc));
 DelayCommand(23.0,WriteTimestampedLogEntry("VISUAL|key|96|tap"));DelayCommand(24.0,VShot("h_familiar_root"));
 DelayCommand(25.0,WriteTimestampedLogEntry("VISUAL|key|98|tap"));
 DelayCommand(26.0,State(pc,"ui_release"));DelayCommand(26.1,VShot("i_returned"));
 DelayCommand(27.0,VDone());
}

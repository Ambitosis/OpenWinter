#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Key(int k){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|tap");}
void Text(string s){WriteTimestampedLogEntry("VISUAL|text|"+s);}
void State(object pc,string s){object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);Note(s+"|masterpc="+IntToString(GetIsPC(pc))+"|familiarpc="+IntToString(GetIsPC(f))+"|possessed="+IntToString(GetIsPossessedFamiliar(f))+"|masterss="+IntToString(GetSoundset(pc))+"|familiarss="+IntToString(GetSoundset(f)));}
void Ready(object pc){object f=GetAssociate(ASSOCIATE_TYPE_FAMILIAR,pc);if(!GetIsObjectValid(f)||GetMaster(f)!=pc||GetName(pc)!="000 Familiar Possession"){Note("INVALID_CONTEXT");VDone();return;}SetLocalObject(GetModule(),"master",pc);SetLocalObject(GetModule(),"familiar",f);SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_CHAT,"fc_chat");SetPlotFlag(f,TRUE);AssignCommand(f,ClearAllActions());State(pc,"ready");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;Note("character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));SetPlotFlag(pc,TRUE);DelayCommand(1.0,SummonFamiliar(pc));DelayCommand(4.0,Ready(pc));
DelayCommand(5.0,Key(13));DelayCommand(5.25,Text("MASTER_BEFORE"));DelayCommand(6.0,Key(13));
DelayCommand(7.0,Key(96));DelayCommand(7.6,Key(100));DelayCommand(8.2,Key(97));DelayCommand(9.0,State(pc,"possessed"));
DelayCommand(10.0,Key(13));DelayCommand(10.25,Text("FAMILIAR_CHAT"));DelayCommand(11.0,Key(13));DelayCommand(11.8,VShot("a_possessed_chat"));
DelayCommand(12.2,Key(86));DelayCommand(12.6,VShot("b_quickchat_root"));DelayCommand(13.0,Key(83));DelayCommand(13.4,Key(83));DelayCommand(14.2,VShot("c_quickchat_selected"));
DelayCommand(15.0,Key(27));DelayCommand(16.0,Key(96));DelayCommand(16.6,Key(98));DelayCommand(17.4,State(pc,"returned"));
DelayCommand(18.0,Key(13));DelayCommand(18.25,Text("MASTER_AFTER"));DelayCommand(19.0,Key(13));DelayCommand(20.0,VShot("d_returned_chat"));DelayCommand(21.0,VDone());}

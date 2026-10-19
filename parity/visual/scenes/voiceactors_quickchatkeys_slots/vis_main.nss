#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Key(int k){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|tap");}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");AssignCommand(o,ClearAllActions(TRUE));}
void Gate(object pc,object hench){int ready=GetIsPC(pc)&&GetName(pc)=="Aluvian Darkstar"&&GetSoundset(pc)==5100&&GetMaster(hench)==pc&&GetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_GUIEVENT)=="vis_gui"&&GetEventScript(hench,EVENT_SCRIPT_CREATURE_ON_DIALOGUE)=="vis_order";Note("quickchat.ready."+IntToString(ready)+".pc."+IntToString(GetIsPC(pc))+".name."+GetName(pc)+".soundset."+IntToString(GetSoundset(pc))+".master."+IntToString(GetMaster(hench)==pc));if(!ready){Note("INVALID_PC_CONTEXT");VDone();}}
void Trial(int i,int slot){SetLocalInt(GetModule(),"quickchat_trial",i);Note("quickchat.start."+IntToString(i)+".slot."+IntToString(slot));}
void CloseIfOpen(){if(GetLocalInt(GetModule(),"quickchat_keys_open"))Key(27);}
void End(int i){Note("quickchat.end."+IntToString(i));}
void Positive(object pc,int slot){SetLocalInt(GetModule(),"quickchat_trial",-1);Note("positive.slot."+IntToString(slot));PlayVoiceChat(slot,pc);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;Quiet(pc);SetPlotFlag(pc,TRUE);SetSoundset(pc,5100);SetLocalInt(GetModule(),"quickchat_trial",-1);SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_GUIEVENT,"vis_gui");
object hench=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(22.0,15.0,0.0),180.0));Quiet(hench);SetSoundset(hench,5099);AddHenchman(pc,hench);SetEventScript(hench,EVENT_SCRIPT_CREATURE_ON_DIALOGUE,"vis_order");SetAssociateListenPatterns(hench);
DelayCommand(1.0,VCamera(pc,90.0,7.0,65.0));DelayCommand(4.0,Gate(pc,hench));DelayCommand(4.5,VShot("ready"));
DelayCommand(6.0,Trial(0,28));DelayCommand(6.05,Key(86));DelayCommand(6.35,Key(68));DelayCommand(6.65,Key(83));DelayCommand(7.5,End(0));DelayCommand(8.0,CloseIfOpen());
DelayCommand(9.0,Trial(1,28));DelayCommand(9.05,Key(86));DelayCommand(9.35,Key(68));DelayCommand(9.65,Key(70));DelayCommand(10.5,End(1));DelayCommand(11.0,CloseIfOpen());
DelayCommand(12.0,Trial(2,28));DelayCommand(12.05,Key(86));DelayCommand(12.35,Key(68));DelayCommand(12.65,Key(84));DelayCommand(13.5,End(2));DelayCommand(14.0,CloseIfOpen());
DelayCommand(15.0,Trial(3,28));DelayCommand(15.05,Key(86));DelayCommand(15.35,Key(68));DelayCommand(15.65,Key(82));DelayCommand(16.5,End(3));DelayCommand(17.0,CloseIfOpen());
DelayCommand(19.0,Trial(4,-1));DelayCommand(19.05,Key(86));DelayCommand(19.7,Key(86));DelayCommand(20.5,End(4));
DelayCommand(22.0,Trial(5,-1));DelayCommand(22.05,Key(86));DelayCommand(22.7,Key(81));DelayCommand(23.5,End(5));DelayCommand(24.0,CloseIfOpen());
DelayCommand(26.0,Trial(6,-1));DelayCommand(26.05,Key(86));DelayCommand(26.7,Key(27));DelayCommand(27.5,End(6));
DelayCommand(29.0,Positive(pc,28));DelayCommand(31.0,Note("quickchat.keys.completed"));DelayCommand(32.0,VDone());}

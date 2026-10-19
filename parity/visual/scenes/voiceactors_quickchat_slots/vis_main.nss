#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Key(int k){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(k)+"|tap");}
void Quiet(object o){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");AssignCommand(o,ClearAllActions(TRUE));}
void Gate(object pc,object hench){int ready=GetIsPC(pc)&&GetName(pc)=="Aluvian Darkstar"&&GetSoundset(pc)==5100&&GetMaster(hench)==pc&&GetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_GUIEVENT)=="vis_gui"&&GetEventScript(hench,EVENT_SCRIPT_CREATURE_ON_DIALOGUE)=="vis_order";Note("quickchat.ready."+IntToString(ready)+".pc."+IntToString(GetIsPC(pc))+".name."+GetName(pc)+".soundset."+IntToString(GetSoundset(pc))+".master."+IntToString(GetMaster(hench)==pc));if(!ready){Note("INVALID_PC_CONTEXT");VDone();}}
void Trial(int i,int slot){SetLocalInt(GetModule(),"quickchat_trial",i);Note("quickchat.start."+IntToString(i)+".slot."+IntToString(slot));}
void CloseIfOpen(){int type=GetLocalInt(GetModule(),"quickchat_last_gui");if(type==18||type==19)Key(86);}
void End(int i){Note("quickchat.end."+IntToString(i));}
void Positive(object pc,int slot){SetLocalInt(GetModule(),"quickchat_trial",-1);Note("positive.slot."+IntToString(slot));PlayVoiceChat(slot,pc);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;Quiet(pc);SetPlotFlag(pc,TRUE);SetSoundset(pc,5100);SetLocalInt(GetModule(),"quickchat_trial",-1);SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_GUIEVENT,"vis_gui");
object hench=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(22.0,15.0,0.0),180.0));Quiet(hench);SetSoundset(hench,5099);AddHenchman(pc,hench);SetEventScript(hench,EVENT_SCRIPT_CREATURE_ON_DIALOGUE,"vis_order");SetAssociateListenPatterns(hench);
DelayCommand(1.0,VCamera(pc,90.0,7.0,65.0));DelayCommand(4.0,Gate(pc,hench));DelayCommand(4.5,VShot("ready"));
DelayCommand(6.0,Trial(0,0));DelayCommand(6.05,Key(86));DelayCommand(6.35,Key(87));DelayCommand(6.65,Key(69));DelayCommand(7.5,End(0));
DelayCommand(8.0,Trial(1,1));DelayCommand(8.05,Key(86));DelayCommand(8.35,Key(87));DelayCommand(8.65,Key(82));DelayCommand(9.5,End(1));
DelayCommand(10.0,Trial(2,4));DelayCommand(10.05,Key(86));DelayCommand(10.35,Key(87));DelayCommand(10.65,Key(68));DelayCommand(11.5,End(2));
DelayCommand(12.0,Trial(3,5));DelayCommand(12.05,Key(86));DelayCommand(12.35,Key(87));DelayCommand(12.65,Key(87));DelayCommand(13.5,End(3));
DelayCommand(14.0,Trial(4,6));DelayCommand(14.05,Key(86));DelayCommand(14.35,Key(87));DelayCommand(14.65,Key(65));DelayCommand(15.5,End(4));
DelayCommand(16.0,Trial(5,7));DelayCommand(16.05,Key(86));DelayCommand(16.35,Key(87));DelayCommand(16.65,Key(83));DelayCommand(17.5,End(5));
DelayCommand(18.0,Trial(6,8));DelayCommand(18.05,Key(86));DelayCommand(18.35,Key(87));DelayCommand(18.65,Key(84));DelayCommand(19.5,End(6));
DelayCommand(20.0,Trial(7,9));DelayCommand(20.05,Key(86));DelayCommand(20.35,Key(87));DelayCommand(20.65,Key(70));DelayCommand(21.5,End(7));
DelayCommand(22.0,Trial(8,10));DelayCommand(22.05,Key(86));DelayCommand(22.35,Key(87));DelayCommand(22.65,Key(88));DelayCommand(23.5,End(8));
DelayCommand(24.0,Trial(9,22));DelayCommand(24.05,Key(86));DelayCommand(24.35,Key(69));DelayCommand(24.65,Key(69));DelayCommand(25.5,End(9));
DelayCommand(26.0,Trial(10,23));DelayCommand(26.05,Key(86));DelayCommand(26.35,Key(69));DelayCommand(26.65,Key(87));DelayCommand(27.5,End(10));
DelayCommand(28.0,Trial(11,24));DelayCommand(28.05,Key(86));DelayCommand(28.35,Key(69));DelayCommand(28.65,Key(68));DelayCommand(29.5,End(11));
DelayCommand(30.0,Trial(12,25));DelayCommand(30.05,Key(86));DelayCommand(30.35,Key(69));DelayCommand(30.65,Key(83));DelayCommand(31.5,End(12));
DelayCommand(32.0,Trial(13,26));DelayCommand(32.05,Key(86));DelayCommand(32.35,Key(68));DelayCommand(32.65,Key(87));DelayCommand(33.5,End(13));
DelayCommand(34.0,Trial(14,27));DelayCommand(34.05,Key(86));DelayCommand(34.35,Key(68));DelayCommand(34.65,Key(69));DelayCommand(35.5,End(14));
DelayCommand(36.0,Trial(15,28));DelayCommand(36.05,Key(86));DelayCommand(36.35,Key(68));DelayCommand(36.65,Key(83));DelayCommand(37.5,End(15));
DelayCommand(38.0,Trial(16,29));DelayCommand(38.05,Key(86));DelayCommand(38.35,Key(68));DelayCommand(38.65,Key(67));DelayCommand(39.5,End(16));
DelayCommand(40.0,Trial(17,30));DelayCommand(40.05,Key(86));DelayCommand(40.35,Key(68));DelayCommand(40.65,Key(88));DelayCommand(41.5,End(17));
DelayCommand(42.0,Trial(18,31));DelayCommand(42.05,Key(86));DelayCommand(42.35,Key(68));DelayCommand(42.65,Key(65));DelayCommand(43.5,End(18));
DelayCommand(44.0,Trial(19,34));DelayCommand(44.05,Key(86));DelayCommand(44.35,Key(83));DelayCommand(44.65,Key(83));DelayCommand(45.5,End(19));
DelayCommand(46.0,Trial(20,35));DelayCommand(46.05,Key(86));DelayCommand(46.35,Key(83));DelayCommand(46.65,Key(68));DelayCommand(47.5,End(20));
DelayCommand(48.0,Trial(21,36));DelayCommand(48.05,Key(86));DelayCommand(48.35,Key(83));DelayCommand(48.65,Key(87));DelayCommand(49.5,End(21));
DelayCommand(50.0,Trial(22,37));DelayCommand(50.05,Key(86));DelayCommand(50.35,Key(83));DelayCommand(50.65,Key(69));DelayCommand(51.5,End(22));
DelayCommand(52.0,Trial(23,38));DelayCommand(52.05,Key(86));DelayCommand(52.35,Key(83));DelayCommand(52.65,Key(67));DelayCommand(53.5,End(23));
DelayCommand(54.0,Trial(24,39));DelayCommand(54.05,Key(86));DelayCommand(54.35,Key(83));DelayCommand(54.65,Key(88));DelayCommand(55.5,End(24));
DelayCommand(56.0,Trial(25,40));DelayCommand(56.05,Key(86));DelayCommand(56.35,Key(83));DelayCommand(56.65,Key(65));DelayCommand(57.5,End(25));
DelayCommand(58.0,Trial(26,41));DelayCommand(58.05,Key(86));DelayCommand(58.35,Key(88));DelayCommand(58.65,Key(88));DelayCommand(59.5,End(26));
DelayCommand(60.0,Trial(27,42));DelayCommand(60.05,Key(86));DelayCommand(60.35,Key(88));DelayCommand(60.65,Key(87));DelayCommand(61.5,End(27));
DelayCommand(62.0,Trial(28,43));DelayCommand(62.05,Key(86));DelayCommand(62.35,Key(88));DelayCommand(62.65,Key(67));DelayCommand(63.5,End(28));
DelayCommand(64.0,Trial(29,44));DelayCommand(64.05,Key(86));DelayCommand(64.35,Key(88));DelayCommand(64.65,Key(68));DelayCommand(65.5,End(29));
DelayCommand(66.0,Trial(30,45));DelayCommand(66.05,Key(86));DelayCommand(66.35,Key(88));DelayCommand(66.65,Key(83));DelayCommand(67.5,End(30));
DelayCommand(68.0,Trial(31,46));DelayCommand(68.05,Key(86));DelayCommand(68.35,Key(88));DelayCommand(68.65,Key(65));DelayCommand(69.5,End(31));
DelayCommand(70.0,Trial(32,47));DelayCommand(70.05,Key(86));DelayCommand(70.35,Key(88));DelayCommand(70.65,Key(90));DelayCommand(71.5,End(32));
DelayCommand(72.0,Trial(33,48));DelayCommand(72.05,Key(86));DelayCommand(72.35,Key(88));DelayCommand(72.65,Key(69));DelayCommand(73.5,End(33));
DelayCommand(74.0,SetLocalInt(GetModule(),"quickchat_trial",-2));DelayCommand(75.0,Key(86));DelayCommand(76.0,VShot("menu_root"));DelayCommand(77.0,Key(87));DelayCommand(78.0,VShot("menu_combat"));DelayCommand(79.0,Key(27));DelayCommand(80.0,VShot("menu_escape"));DelayCommand(81.0,CloseIfOpen());
int slot;for(slot=0;slot<49;slot++)DelayCommand(84.0+IntToFloat(slot)*0.3,Positive(pc,slot));DelayCommand(100.0,Note("quickchat.completed.34"));DelayCommand(101.0,VDone());}

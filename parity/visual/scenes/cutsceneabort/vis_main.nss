#include "visual_inc"
void KeyEscape(){WriteTimestampedLogEntry("VISUAL|key|27|tap");}
void State(string phase){object pc=GetFirstPC();WriteTimestampedLogEntry("VISUAL|note|state|phase="+phase+"|pc="+IntToString(GetIsPC(pc))+"|cutscene="+IntToString(GetCutsceneMode(pc))+"|plot="+IntToString(GetPlotFlag(pc))+"|aborts="+IntToString(GetLocalInt(GetModule(),"aborts"))+"|outsideCanceller="+IntToString(GetIsObjectValid(GetLastPCToCancelCutscene())));}
void Start(int left){SetCutsceneMode(GetFirstPC(),TRUE,left);State("started");}
void Stop(){SetCutsceneMode(GetFirstPC(),FALSE);State("script_stop");}
void ExitHandler(){SetLocalInt(GetModule(),"exit",1);Start(TRUE);}
void main(){object pc=GetFirstPC();SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_CANCEL_CUTSCENE,"ca_abort");SetPlotFlag(pc,FALSE);State("before");DelayCommand(1.0,Start(FALSE));DelayCommand(2.0,KeyEscape());DelayCommand(2.7,State("after_first"));DelayCommand(3.5,KeyEscape());DelayCommand(4.2,State("after_repeat"));DelayCommand(5.0,Stop());DelayCommand(6.0,ExitHandler());DelayCommand(7.0,KeyEscape());DelayCommand(8.0,State("after_handler_exit"));DelayCommand(9.0,VDone());}

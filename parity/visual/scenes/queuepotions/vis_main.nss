#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Key(){WriteTimestampedLogEntry("VISUAL|key|118|tap");}
void Mouse(string xy,string button){WriteTimestampedLogEntry("VISUAL|"+button+"|"+xy);}
void Read(object pc,string label){object p=GetLocalObject(pc,"pot");Note(label+".stack."+IntToString(GetIsObjectValid(p)?GetItemStackSize(p):0)+".action."+IntToString(GetCurrentAction(pc)));}
void Ready(object pc){object p=GetFirstItemInInventory(pc);while(GetIsObjectValid(p)&&(GetBaseItemType(p)!=49||GetItemPropertySubType(GetFirstItemProperty(p))!=66))p=GetNextItemInInventory(pc);SetLocalObject(pc,"pot",p);SetItemStackSize(p,9);Note("character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));Note("ready.base."+IntToString(GetBaseItemType(p))+".property."+IntToString(GetItemPropertySubType(GetFirstItemProperty(p)))+".stack."+IntToString(GetItemStackSize(p)));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetPlotFlag(pc,TRUE);
 DelayCommand(.5,Ready(pc));DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));DelayCommand(2.0,VShot("before"));
 DelayCommand(3.0,Key());DelayCommand(3.15,Key());DelayCommand(3.3,Key());DelayCommand(3.7,VShot("keys_queued"));DelayCommand(10.8,Read(pc,"keys_finished"));
 DelayCommand(12.0,Mouse("0.527,0.975","click"));DelayCommand(12.2,Mouse("0.527,0.975","click"));DelayCommand(12.4,Mouse("0.527,0.975","click"));DelayCommand(12.7,VShot("mouse_queued"));
 DelayCommand(13.1,Mouse("0.052,0.027","click"));DelayCommand(13.5,VShot("left_cancel_middle"));DelayCommand(20.0,Read(pc,"left_finished"));
 DelayCommand(22.0,Key());DelayCommand(22.2,Key());DelayCommand(22.4,Key());DelayCommand(22.7,VShot("right_before"));DelayCommand(23.1,Mouse("0.052,0.027","rightclick"));DelayCommand(23.5,VShot("right_cancel_middle"));
 DelayCommand(31.0,Read(pc,"right_finished"));DelayCommand(32.0,VShot("done"));DelayCommand(33.0,VDone());}

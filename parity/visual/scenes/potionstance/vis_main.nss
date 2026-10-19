#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void State(object pc,string label){Note(label+".combat."+IntToString(GetIsInCombat(pc))+".action."+IntToString(GetCurrentAction(pc))+".stack."+IntToString(GetItemStackSize(GetLocalObject(pc,"pot"))));}
void Trial(object pc,string clip){
 string old=GetLocalString(pc,"oldclip");if(old!="")ReplaceObjectAnimation(pc,old,"");
 if(clip!="baseline")ReplaceObjectAnimation(pc,clip,"sitcross");
 SetLocalString(pc,"oldclip",clip=="baseline"?"":clip);
 Note("start."+clip);object p=GetLocalObject(pc,"pot");AssignCommand(pc,ActionUseItemOnObject(p,GetFirstItemProperty(p),pc));
 DelayCommand(.5,State(pc,clip+".prep"));DelayCommand(.5,VShot(clip+"_prep"));
 DelayCommand(1.5,State(pc,clip+".drink"));DelayCommand(1.5,VShot(clip+"_drink"));
 DelayCommand(3.0,State(pc,clip+".end"));DelayCommand(3.0,VShot(clip+"_end"));
}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);
 object p=GetFirstItemInInventory(pc);while(GetIsObjectValid(p)&&(GetBaseItemType(p)!=49||GetItemPropertySubType(GetFirstItemProperty(p))!=66))p=GetNextItemInInventory(pc);
 SetItemStackSize(p,8);SetLocalObject(pc,"pot",p);Note("ready.pc."+IntToString(GetIsPC(pc))+".base."+IntToString(GetBaseItemType(p))+".stack."+IntToString(GetItemStackSize(p)));
 DelayCommand(1.0,VCamera(pc,90.0,4.5,65.0));
 DelayCommand(3.0,Trial(pc,"baseline"));DelayCommand(8.0,Trial(pc,"nwreadyr"));DelayCommand(13.0,Trial(pc,"1hreadyr"));DelayCommand(18.0,Trial(pc,"cpause1"));DelayCommand(23.0,Trial(pc,"pause2"));DelayCommand(28.0,Trial(pc,"pause1"));DelayCommand(32.0,VDone());}

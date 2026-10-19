#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Read(object pc,int mode){object p=GetLocalObject(pc,"pot");Note("mode."+IntToString(mode)+".remaining."+IntToString(GetItemStackSize(p)));}
void Activate(object pc,int mode){object p=GetLocalObject(pc,"pot");itemproperty ip=GetFirstItemProperty(p);while(GetIsItemPropertyValid(ip)){if(GetItemPropertyType(ip)!=ITEM_PROPERTY_CAST_SPELL)RemoveItemProperty(p,ip);ip=GetNextItemProperty(p);}SetItemStackSize(p,5);
 if(mode==1)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByClass(CLASS_TYPE_CLERIC),p);
 if(mode==2)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByRace(RACIAL_TYPE_HALFORC),p);
 if(mode==3)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByAlign(IP_CONST_ALIGNMENTGROUP_EVIL),p);
 Note("mode."+IntToString(mode)+".before."+IntToString(GetItemStackSize(p)));WriteTimestampedLogEntry("VISUAL|key|118|tap");DelayCommand(3.5,Read(pc,mode));}
void Ready(object pc){object p=GetFirstItemInInventory(pc);while(GetIsObjectValid(p)&&(GetBaseItemType(p)!=49||GetItemPropertySubType(GetFirstItemProperty(p))!=66))p=GetNextItemInInventory(pc);SetLocalObject(pc,"pot",p);Note("ready.pc."+IntToString(GetIsPC(pc))+".wizard."+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc))+".race."+IntToString(GetRacialType(pc))+".good."+IntToString(GetGoodEvilValue(pc))+".valid."+IntToString(GetIsObjectValid(p)));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetPlotFlag(pc,TRUE);DelayCommand(.5,Ready(pc));DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));DelayCommand(3.0,Activate(pc,0));DelayCommand(8.0,Activate(pc,1));DelayCommand(13.0,Activate(pc,2));DelayCommand(18.0,Activate(pc,3));DelayCommand(23.0,Activate(pc,4));DelayCommand(28.0,VDone());}

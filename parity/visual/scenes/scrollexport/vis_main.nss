#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Read(object pc,int mode){object p=GetLocalObject(pc,"pot");Note("mode."+IntToString(mode)+".remaining."+IntToString(GetItemStackSize(p)));}
void Activate(object pc,int mode){object p=GetLocalObject(pc,"pot");itemproperty ip=GetFirstItemProperty(p);while(GetIsItemPropertyValid(ip)){if(GetItemPropertyType(ip)!=ITEM_PROPERTY_CAST_SPELL){RemoveItemProperty(p,ip);ip=GetFirstItemProperty(p);}else ip=GetNextItemProperty(p);}SetItemStackSize(p,5);
 if(mode==1)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByClass(CLASS_TYPE_CLERIC),p);
 if(mode==2)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByRace(RACIAL_TYPE_HALFORC),p);
 if(mode==3)AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyLimitUseByAlign(IP_CONST_ALIGNMENTGROUP_EVIL),p);
 Note("mode."+IntToString(mode)+".before."+IntToString(GetItemStackSize(p)));WriteTimestampedLogEntry("VISUAL|key|118|tap");DelayCommand(3.5,Read(pc,mode));}
void Ready(object pc){Note("character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));object p=GetFirstItemInInventory(pc);while(GetIsObjectValid(p)&&(GetBaseItemType(p)!=75||GetItemPropertySubType(GetFirstItemProperty(p))!=348))p=GetNextItemInInventory(pc);SetLocalObject(pc,"pot",p);Note("ready.name."+GetName(pc)+".pc."+IntToString(GetIsPC(pc))+".wizard."+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc))+".race."+IntToString(GetRacialType(pc))+".good."+IntToString(GetGoodEvilValue(pc))+".umd."+IntToString(GetSkillRank(SKILL_USE_MAGIC_DEVICE,pc))+".valid."+IntToString(GetIsObjectValid(p))+".base."+IntToString(GetBaseItemType(p))+".property."+IntToString(GetItemPropertySubType(GetFirstItemProperty(p))));}
void Mouse(int down){WriteTimestampedLogEntry("VISUAL|mouse|0.967,0.072|"+(down?"down":"up"));}
void Script(object pc){object p=GetLocalObject(pc,"pot");Note("script.before."+IntToString(GetItemStackSize(p)));AssignCommand(pc,ActionUseItemOnObject(p,GetFirstItemProperty(p),pc));DelayCommand(3.5,Read(pc,9));}
void Export(object pc){ExportSingleCharacter(pc);Note("exported."+GetName(pc));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;DelayCommand(.5,Ready(pc));DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));DelayCommand(2.0,VShot("ready"));DelayCommand(3.0,Export(pc));DelayCommand(4.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));DelayCommand(5.0,VShot("inventory"));DelayCommand(7.0,VDone());}

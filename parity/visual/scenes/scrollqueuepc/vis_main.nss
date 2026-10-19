#include "visual_inc"
float Now(){return IntToFloat((GetTimeHour()*(FloatToInt(HoursToSeconds(1))/60)+GetTimeMinute())*60+GetTimeSecond())+IntToFloat(GetTimeMillisecond())/1000.0;}
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void DoneUse(object c,int n){Note(GetLocalString(c,"case")+".done."+IntToString(n)+".time."+FloatToString(Now()-GetLocalFloat(c,"start"),0,3)+".stack."+IntToString(GetItemStackSize(GetLocalObject(c,"pot"))));}
void Use(object c,int n){object p=GetLocalObject(c,"pot");AssignCommand(c,ActionUseItemOnObject(p,GetFirstItemProperty(p),c));AssignCommand(c,ActionDoCommand(DoneUse(c,n)));}
void Poll(object c,int previous,int n){int stack=GetItemStackSize(GetLocalObject(c,"pot"));if(stack!=previous)Note(GetLocalString(c,"case")+".stack."+IntToString(stack)+".time."+FloatToString(Now()-GetLocalFloat(c,"start"),0,3));if(n<360)DelayCommand(0.05,Poll(c,stack,n+1));}
void Begin(object pc){object p=CreateItemOnObject("nw_it_sparscr104",pc,3);SetLocalObject(pc,"pot",p);SetLocalString(pc,"case","scroll");SetLocalFloat(pc,"start",Now());Note("start.pc."+IntToString(GetIsPC(pc))+".class."+IntToString(GetLevelByClass(CLASS_TYPE_WIZARD,pc))+".base."+IntToString(GetBaseItemType(p))+".stack."+IntToString(GetItemStackSize(p)));Use(pc,1);Use(pc,2);Use(pc,3);Poll(pc,3,0);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);DelayCommand(1.0,VCamera(pc,90.0,4.5,65.0));DelayCommand(3.0,Begin(pc));
DelayCommand(2.8,VShot("before"));
DelayCommand(3.2,VShot("prep0"));
DelayCommand(3.8,VShot("prep1"));
DelayCommand(4.5,VShot("prep2"));
DelayCommand(5.1,VShot("release_start"));
DelayCommand(5.55,VShot("impact"));
DelayCommand(6.3,VShot("recovery"));
DelayCommand(6.8,VShot("waiting"));
DelayCommand(9.2,VShot("second_prep"));
DelayCommand(11.6,VShot("second_impact"));
DelayCommand(15.2,VShot("third_prep"));
DelayCommand(18.7,VShot("finished"));
DelayCommand(21.0,VDone());}

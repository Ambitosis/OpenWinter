#include "visual_inc"
float Now(){return IntToFloat((GetTimeHour()*(FloatToInt(HoursToSeconds(1))/60)+GetTimeMinute())*60+GetTimeSecond())+IntToFloat(GetTimeMillisecond())/1000.0;}
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void DoneUse(object c,int n){Note(GetLocalString(c,"case")+".done."+IntToString(n)+".time."+FloatToString(Now()-GetLocalFloat(c,"start"),0,3)+".stack."+IntToString(GetItemStackSize(GetLocalObject(c,"pot"))));}
void Use(object c,int n){object p=GetLocalObject(c,"pot");AssignCommand(c,ActionUseItemOnObject(p,GetFirstItemProperty(p),c));AssignCommand(c,ActionDoCommand(DoneUse(c,n)));}
void Poll(object c,int previous,int n){int stack=GetItemStackSize(GetLocalObject(c,"pot"));if(stack!=previous)Note(GetLocalString(c,"case")+".stack."+IntToString(stack)+".time."+FloatToString(Now()-GetLocalFloat(c,"start"),0,3));if(n<360)DelayCommand(0.05,Poll(c,stack,n+1));}
void Begin(object pc,int n,string clip){object p=CreateItemOnObject("nw_it_sparscr104",pc,3);SetLocalObject(pc,"pot",p);SetLocalString(pc,"case","trial"+IntToString(n));SetLocalFloat(pc,"start",Now());ReplaceObjectAnimation(pc,"castarea","castarea");ReplaceObjectAnimation(pc,"castout","castout");ReplaceObjectAnimation(pc,"castup","castup");ReplaceObjectAnimation(pc,"castpoint","castpoint");ReplaceObjectAnimation(pc,clip,"sitcross");Note("start."+IntToString(n)+".clip."+clip);Use(pc,n);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);DelayCommand(1.0,VCamera(pc,90.0,4.5,65.0));
DelayCommand(3.0,Begin(pc,1,"castarea"));
DelayCommand(5.1,VShot("trial1_2.1"));
DelayCommand(5.6,VShot("trial1_2.6"));
DelayCommand(6.1,VShot("trial1_3.1"));
DelayCommand(9.0,Begin(pc,2,"castout"));
DelayCommand(11.1,VShot("trial2_2.1"));
DelayCommand(11.6,VShot("trial2_2.6"));
DelayCommand(12.1,VShot("trial2_3.1"));
DelayCommand(15.0,Begin(pc,3,"castup"));
DelayCommand(17.1,VShot("trial3_2.1"));
DelayCommand(17.6,VShot("trial3_2.6"));
DelayCommand(18.1,VShot("trial3_3.1"));
DelayCommand(21.0,Begin(pc,4,"castpoint"));
DelayCommand(23.1,VShot("trial4_2.1"));
DelayCommand(23.6,VShot("trial4_2.6"));
DelayCommand(24.1,VShot("trial4_3.1"));
DelayCommand(27.0,VDone());}

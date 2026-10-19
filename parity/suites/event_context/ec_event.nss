#include "ec_inc"
void main()
{
 string key=GetTag(OBJECT_SELF)+"."+IntToString(GetCurrentlyRunningEvent(FALSE));
 Snapshot(key);
 int eventType=GetCurrentlyRunningEvent(FALSE);
 if(eventType==3001 || eventType==4001 || eventType==5011 || eventType==9013 || eventType==10011)
  PLogI(key+".user",GetUserDefinedEventNumber());
 SetLocalString(GetModule(),"key",key);
 ExecuteScript("ec_nested",OBJECT_SELF);
 Snapshot(key+".after");
 DelayCommand(0.1,Snapshot(key+".delay"));
 AssignCommand(OBJECT_SELF,Snapshot(key+".assign"));
 SetLocalInt(OBJECT_SELF,"count",GetLocalInt(OBJECT_SELF,"count")+1);
}

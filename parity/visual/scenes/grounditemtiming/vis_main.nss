// One stationary pickup and drop, with public ownership/action timing and full recovery frames.
#include "visual_inc"
#include "git_time"
void Poll(object pc, int n)
{
    object item = GetLocalObject(pc,"item");
    Note("sample="+IntToString(n)+"|owned="+IntToString(GetItemPossessor(item)==pc)
        +"|action="+IntToString(GetCurrentAction(pc)));
    if(n<54) DelayCommand(0.05,Poll(pc,n+1));
}
void Finished() { Note("queue_finished"); }
void Go(object pc, int drop)
{
    Note("request");
    object item = GetLocalObject(pc,"item");
    if(drop) AssignCommand(pc,ActionPutDownItem(item));
    else AssignCommand(pc,ActionPickUpItem(item));
    AssignCommand(pc,ActionDoCommand(Finished()));
    Poll(pc,0);
}
void Burst(object pc, int drop)
{
    SetLocalString(GetModule(),"case",drop ? "drop" : "pickup");
    SetLocalFloat(GetModule(),"start",Now());
    WriteTimestampedLogEntry("VISUAL|burst|"+(drop ? "drop" : "pickup")+"|3.0|10");
    DelayCommand(0.3,Go(pc,drop));
}
void Setup(object pc)
{
    object item = CreateObject(OBJECT_TYPE_ITEM,"nw_wswls001",
        Location(GetArea(pc),Vector(20.0,15.5,0.0),0.0),FALSE,"TIMING_ITEM");
    SetLocalObject(pc,"item",item);
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_ACQUIRE_ITEM,"git_acq");
    SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_LOSE_ITEM,"git_lost");
}
void main()
{
    object pc = GetEnteringObject();
    if(!GetIsPC(pc)) return;
    DelayCommand(0.5,Setup(pc));
    DelayCommand(1.0,VCamera(pc,0.0,5.0,65.0));
    DelayCommand(3.0,Burst(pc,FALSE));
    DelayCommand(7.0,Burst(pc,TRUE));
    DelayCommand(11.0,VDone());
}

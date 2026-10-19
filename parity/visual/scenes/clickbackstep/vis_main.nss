// Actual posted ground clicks. Screen offsets are probes, not claimed metre/angle thresholds.
// Final world displacement and heading determine each accepted sample's distance/angle.
#include "visual_inc"
float Now()
{
    int minutes = FloatToInt(HoursToSeconds(1)) / 60;
    return IntToFloat((GetTimeHour()*minutes+GetTimeMinute())*60+GetTimeSecond())
        +IntToFloat(GetTimeMillisecond())/1000.0;
}
void Note(object pc,string stage)
{
    vector p=GetPosition(pc);
    WriteTimestampedLogEntry("VISUAL|note|"+GetLocalString(pc,"case")+"|"+stage
        +"|t="+FloatToString(Now()-GetLocalFloat(pc,"start"),0,3)
        +"|x="+FloatToString(p.x,0,4)+"|y="+FloatToString(p.y,0,4)
        +"|facing="+FloatToString(GetFacing(pc),0,3)
        +"|action="+IntToString(GetCurrentAction(pc)));
}
void Poll(object pc,int n)
{
    Note(pc,"sample"+IntToString(n));
    if(n<40) DelayCommand(0.05,Poll(pc,n+1));
    else
    {
        vector displacement=GetPosition(pc)-Vector(20.0,15.0,0.0);
        float distance=VectorMagnitude(displacement);
        // Misses/self-clicks have no useful displacement. A wildly different heading
        // means the screen point missed the intended open ground strip.
        int valid=GetLocalInt(pc,"start_valid") && distance>0.05 && distance<4.0 && displacement.x<0.0
            && fabs(displacement.y)<0.35;
        WriteTimestampedLogEntry("VISUAL|note|"+GetLocalString(pc,"case")
            +"|valid="+IntToString(valid)+"|distance="+FloatToString(distance,0,4)
            +"|heading="+FloatToString(VectorToAngle(displacement),0,3)
            +"|initial_facing="+FloatToString(GetLocalFloat(pc,"facing"),0,3));
    }
}
void Reset(object pc,string label,float facing)
{
    SetLocalString(pc,"case",label);
    SetLocalFloat(pc,"facing",facing);
    VMovePC(pc,20.0,15.0,0.0,facing);
    VCamera(pc,90.0,8.0,60.0);
}
void Click(object pc,string point)
{
    SetLocalFloat(pc,"start",Now());
    SetLocalInt(pc,"start_valid",VectorMagnitude(GetPosition(pc)-Vector(20.0,15.0,0.0))<0.01);
    Note(pc,"before");
    WriteTimestampedLogEntry("VISUAL|click|"+point);
    Poll(pc,0);
}
void Trial(object pc,float t,string label,float facing,string point)
{
    DelayCommand(t,Reset(pc,label,facing));
    DelayCommand(t+1.0,Click(pc,point));
    DelayCommand(t+1.2,VShot(label+"_early"));
    DelayCommand(t+1.6,VShot(label+"_moving"));
    DelayCommand(t+3.2,VShot(label+"_after"));
}
void main()
{
    object pc=GetEnteringObject();
    if(!GetIsPC(pc))return;
    SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc,1.0,89.0,1.0,40.0);
    Trial(pc,2.0,"distance_a",0.0,"0.46,0.66");
    Trial(pc,6.0,"distance_b",0.0,"0.43,0.66");
    Trial(pc,10.0,"distance_c",0.0,"0.40,0.66");
    Trial(pc,14.0,"distance_d",0.0,"0.35,0.66");
    Trial(pc,18.0,"angle_30",30.0,"0.43,0.66");
    Trial(pc,22.0,"angle_60",60.0,"0.43,0.66");
    Trial(pc,26.0,"angle_90",90.0,"0.43,0.66");
    Trial(pc,30.0,"forward_control",180.0,"0.43,0.66");
    DelayCommand(35.0,VDone());
}

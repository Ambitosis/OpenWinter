#include "visual_inc"
void Mark(string label) { WriteTimestampedLogEntry("VISUAL|note|door_audio|"+label); }
void Open(object door) { Mark("Opened");AssignCommand(door,ActionOpenDoor(door)); }
void Close(object door) { Mark("Closed");AssignCommand(door,ActionCloseDoor(door)); }
void Locked(object door,object pc) { Mark("Locked");SetLocked(door,TRUE);AssignCommand(pc,ActionOpenDoor(door)); }
void Destroy(object door) { Mark("Destroyed");ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),door); }
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 object door=GetObjectByTag("audio_door");SetLocked(door,FALSE);SetPlotFlag(door,FALSE);
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(4.0,Open(door));DelayCommand(7.0,VShot("a_open"));
 DelayCommand(10.0,Close(door));DelayCommand(13.0,VShot("b_closed"));
 DelayCommand(16.0,Locked(door,pc));DelayCommand(19.0,VShot("c_locked"));
 DelayCommand(22.0,Destroy(door));DelayCommand(25.0,VShot("d_destroyed"));
 DelayCommand(28.0,VDone());}

#include "visual_inc"
void Setup(object pc){WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));object o=VPlaceable(pc,"plc_chest1",3.0,0.0);SetTag(o,"RC_CHEST");SetLocked(o,FALSE);SetLockLockable(o,FALSE);}
void Lock(){SetLocked(GetObjectByTag("RC_CHEST"),TRUE);}
void Trap(object pc){object o=GetObjectByTag("RC_CHEST");CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE,o);SetTrapDetectedBy(o,pc,TRUE);}
void Key(object pc){object o=GetObjectByTag("RC_CHEST");SetTrapDisabled(o);SetLockKeyTag(o,"RC_KEY");SetLockKeyRequired(o,TRUE);object key=CreateItemOnObject("nw_it_mpotion001",pc);SetTag(key,"RC_KEY");}
void Table(object pc){object chest=GetObjectByTag("RC_CHEST");vector v=GetPosition(chest);DestroyObject(chest);object table=CreateObject(OBJECT_TYPE_PLACEABLE,"plc_table",Location(GetArea(pc),v,0.0));SetUseableFlag(table,TRUE);SetTag(table,"RC_TABLE");}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;DelayCommand(.5,Setup(pc));DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));DelayCommand(8.0,Lock());DelayCommand(12.0,Trap(pc));DelayCommand(16.0,Key(pc));DelayCommand(27.0,Table(pc));}

#include "visual_inc"
void OpenTileMenu(){WriteTimestampedLogEntry("VISUAL|mouse|0.70,0.50|down|right");}
void CloseTileMenu(){WriteTimestampedLogEntry("VISUAL|mouse|0.70,0.50|up|right");}
void main(){
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 SetEventScript(GetModule(),EVENT_SCRIPT_MODULE_ON_PLAYER_TILE_ACTION,"ta_event");
 WriteTimestampedLogEntry("VISUAL|note|fixture|act1="+Get2DAString("surfacemat","Act1_Strref",3)+"|act8="+Get2DAString("surfacemat","Act8_Strref",3)+"|surface="+IntToString(GetSurfaceMaterial(GetLocation(pc))));
 DelayCommand(1.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(5.0,OpenTileMenu());
 DelayCommand(6.0,VShot("root"));
 DelayCommand(7.0,WriteTimestampedLogEntry("VISUAL|key|99|tap"));
 DelayCommand(8.0,VShot("submenu"));
 DelayCommand(8.5,AssignCommand(pc,ActionMoveToLocation(Location(GetArea(pc),Vector(35.0,15.0,0.0),0.0),FALSE)));
 DelayCommand(9.0,WriteTimestampedLogEntry("VISUAL|key|104|tap"));
 DelayCommand(10.0,CloseTileMenu());
 DelayCommand(11.0,VShot("after_north"));
 DelayCommand(12.0,OpenTileMenu());
 DelayCommand(13.0,WriteTimestampedLogEntry("VISUAL|key|99|tap"));
 DelayCommand(14.0,WriteTimestampedLogEntry("VISUAL|key|105|tap"));
 DelayCommand(15.0,CloseTileMenu());
 DelayCommand(16.0,VShot("after_east"));
 DelayCommand(17.0,OpenTileMenu());
 DelayCommand(18.0,WriteTimestampedLogEntry("VISUAL|key|99|tap"));
 DelayCommand(19.0,WriteTimestampedLogEntry("VISUAL|key|99|tap"));
 DelayCommand(20.0,CloseTileMenu());
 DelayCommand(21.0,VShot("after_northwest"));
 DelayCommand(24.0,VDone());
}

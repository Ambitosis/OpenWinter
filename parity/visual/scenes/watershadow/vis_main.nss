#include "visual_inc"
// Does a blended water surface cast a shadow onto the bed beneath it? (#204 moved blended world
// materials to a depth prepass.) Each tile's lower half is water; a statue stands at the shore for reference.
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetCameraMode(pc,CAMERA_MODE_TOP_DOWN);SetCameraLimits(pc,1.0,89.0,1.0,40.0);
 CreateObject(OBJECT_TYPE_PLACEABLE,"plc_statue1",Location(GetArea(pc),Vector(18.0,15.0,0.0),0.0));
 DelayCommand(2.0,VCamera(pc,270.0,14.0,40.0));DelayCommand(5.0,VShot("a_water_low"));
 DelayCommand(6.0,VCamera(pc,270.0,18.0,75.0));DelayCommand(9.0,VShot("b_water_high"));
 DelayCommand(11.0,VDone());
}

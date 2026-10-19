#include "visual_inc"
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));}

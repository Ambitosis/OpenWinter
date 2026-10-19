#include "visual_inc"
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;DelayCommand(1.0,VCamera(pc,90.0,6.0,65.0));}

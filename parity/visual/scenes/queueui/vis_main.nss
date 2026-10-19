// Neutral input-enabled room for --capture-action-queue in either UI.
#include "visual_inc"
void main()
{
    object pc=GetEnteringObject();
    if(!GetIsPC(pc))return;
    SetPlotFlag(pc,TRUE);
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|"+GetName(pc)+"|pc=1");
    DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));
}

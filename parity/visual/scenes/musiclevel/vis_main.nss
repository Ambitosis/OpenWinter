// Music level only: the area day track for twelve seconds (OpenWinter level check against musicbattle).
#include "visual_inc"
void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    WriteTimestampedLogEntry("VISUAL|note|musiclevel|phase|enter");
    DelayCommand(12.0, VDone());
}

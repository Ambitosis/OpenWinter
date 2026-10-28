#include "visual_inc"
// Which triggers light up and change the pointer when hovered: a transition to a door (east), a generic trigger
// (west), a generic trigger with a cursor set (north), a transition with a 2 m highlight height (south) and a
// transition linked to a missing tag (far east), then all of them with Tab held. Seen from above.
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Hover(float t, string sPoint, string sShot)
{
    // The shot waits on the wall clock so the posted pointer has arrived.
    DelayCommand(t, Log("hover|" + sPoint));
    DelayCommand(t, Log("pointer|" + sPoint));
    DelayCommand(t, Log("wall|1.5|shot|" + sShot));
}
void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetPlotFlag(oPC, TRUE);
    DelayCommand(1.0, VCamera(oPC, 90.0, 18.0, 1.0));
    Hover(3.0, "0.03,0.5", "none");
    Hover(6.0, "0.72,0.5", "east");
    Hover(9.0, "0.28,0.5", "west");
    Hover(12.0, "0.5,0.2", "north");
    Hover(15.0, "0.5,0.8", "south");
    Hover(18.0, "0.93,0.5", "unlinked");
    // Held Tab lights up everything that can be highlighted.
    DelayCommand(21.0, Log("hover|0.03,0.5"));
    DelayCommand(21.0, Log("key|9|down"));
    DelayCommand(21.0, Log("wall|1.5|shot|tab"));
    DelayCommand(24.0, Log("key|9|up"));
    DelayCommand(25.0, VDone());
}

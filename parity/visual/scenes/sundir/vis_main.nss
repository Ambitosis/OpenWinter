// The area's sun and moon direction through the day: the ground drawn through the scene's readout shader
// (ow_sundir.shd), which writes lightAreaDiffuseDirection (in world space and as given), the light colours and the
// hour as flat colours (python tools/visual/sundir_readout.py decodes them). First two views at the start time to
// check the world-space conversion, then one view after each SetTime. The variants change the area's flags and
// day/night cycle.
#include "visual_inc"

// Hours (and the half hour, in game minutes of two per hour) visited in turn.
int Hour(int i)
{
    switch (i)
    {
        case 0: return 0;  case 1: return 3;  case 2: return 5;  case 3: return 6;  case 4: return 6;
        case 5: return 7;  case 6: return 9;  case 7: return 12; case 8: return 12; case 9: return 15;
        case 10: return 17; case 11: return 18; case 12: return 18; case 13: return 19; case 14: return 21;
        case 15: return 23;
    }
    return -1;
}

int Half(int i)
{
    return i == 4 || i == 8 || i == 12;
}

void Step(object oPC, int i)
{
    int h = Hour(i);
    if (h < 0)
    {
        DelayCommand(1.0, VDone());
        return;
    }
    SetTime(h, Half(i) ? 1 : 0, 0, 0);
    string sName = "t" + (h < 10 ? "0" : "") + IntToString(h) + (Half(i) ? "30" : "00");
    DelayCommand(4.0, WriteTimestampedLogEntry("VISUAL|note|" + sName + "|" + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute())
        + "|day " + IntToString(GetIsDay()) + " night " + IntToString(GetIsNight()) + " dawn " + IntToString(GetIsDawn()) + " dusk " + IntToString(GetIsDusk())));
    DelayCommand(4.0, VShot(sName));
    DelayCommand(6.0, Step(oPC, i + 1));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    VBegin(oPC);
    VHidePC(oPC);
    SetAreaDefaultGrassDisabled(GetArea(oPC), TRUE);
    DelayCommand(3.0, VCamera(oPC, 90.0, 8.0, 1.0));
    DelayCommand(5.0, VShot("start_top"));
    DelayCommand(6.0, VCamera(oPC, 0.0, 8.0, 30.0));
    DelayCommand(8.0, VShot("start_east"));
    DelayCommand(9.0, VCamera(oPC, 90.0, 8.0, 1.0));
    DelayCommand(11.0, Step(oPC, 0));
}

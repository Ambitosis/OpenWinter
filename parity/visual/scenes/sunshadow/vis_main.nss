// Which way the sun's and moon's shadows fall through the day: an obelisk on open ground (player hidden), seen from
// straight above (screen up is north) after each SetTime, and once from the south.
#include "visual_inc"

int Hour(int i)
{
    switch (i)
    {
        case 0: return 0;  case 1: return 6;  case 2: return 9;  case 3: return 12;
        case 4: return 15; case 5: return 18; case 6: return 21;
    }
    return -1;
}

int Half(int i)
{
    return i == 1 || i == 5;
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
    VPlaceable(oPC, "x0_obelisk", 0.0, 0.0);
    DelayCommand(3.0, VCamera(oPC, 90.0, 18.0, 1.0));
    DelayCommand(5.0, VShot("start_top"));
    DelayCommand(6.0, VCamera(oPC, 90.0, 16.0, 60.0));
    DelayCommand(8.0, VShot("start_south"));
    DelayCommand(9.0, VCamera(oPC, 90.0, 18.0, 1.0));
    DelayCommand(11.0, Step(oPC, 0));
}

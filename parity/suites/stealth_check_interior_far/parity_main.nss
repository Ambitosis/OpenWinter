// Hearing through walls at a distance in tiles (expected:
// in interior and underground areas a creature behind an obstruction is heard only along the tileset's tile
// connectivity, at most 4 tile steps away). Three 8 x 8 areas of tile 121 (tde01's walled corner: each tile's floor
// is its lower-left corner, walled off from the next tile's), ARE Flags 0 (exterior), 1 (interior) and 2
// (underground). An observer (bandit, PerceptionRange row 9, whose ranges.2da row is overridden here to 90 m sight
// and hearing) stands in the corner of tile (0, 0); plain creatures (not sneaking, merchant faction) stand in the
// corners of tiles at 1-7 tile steps (|dx| + |dy|). Read 14 s in: heard and seen per target, line of sight.
#include "pstealth_inc"

void Setup(int a)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    object o = PsMake("sn_obs", oArea, 3.0, 3.0, STANDARD_FACTION_COMMONER);
    SetLocalObject(GetModule(), "O" + IntToString(a), o);
    string s = "1,0 2,0 3,0 4,0 5,0 6,0 7,0 0,1 1,1 2,1 3,1 4,1 5,1 2,2 3,2 2,3 4,4 0,4 0,5 1,4 ";
    int n = 0;
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string p = GetStringLeft(s, sp);
        s = GetSubString(s, sp + 1, 1000);
        int c = FindSubString(p, ",");
        int i = StringToInt(GetStringLeft(p, c)), j = StringToInt(GetSubString(p, c + 1, 9));
        object t = PsMake("sn_tgt", oArea, 10.0 * IntToFloat(i) + 3.0, 10.0 * IntToFloat(j) + 3.0, STANDARD_FACTION_MERCHANT);
        SetLocalObject(o, "T" + IntToString(n), t);
        SetLocalString(o, "K" + IntToString(n), IntToString(i) + "_" + IntToString(j));
        n++;
    }
    SetLocalInt(o, "N", n);
}

void Read(int a)
{
    object o = GetLocalObject(GetModule(), "O" + IntToString(a));
    string k = "flags" + IntToString(a);
    int i;
    for (i = 0; i < GetLocalInt(o, "N"); i++)
    {
        object t = GetLocalObject(o, "T" + IntToString(i));
        string kk = k + ".t" + GetLocalString(o, "K" + IntToString(i));
        PLogI(kk + ".heard", GetObjectHeard(t, o));
        PLogI(kk + ".seen", GetObjectSeen(t, o));
        PLogI(kk + ".los", LineOfSightObject(o, t));
    }
}

void main()
{
    PStart();
    int a;
    for (a = 0; a < 3; a++)
    {
        Setup(a);
        DelayCommand(14.0, Read(a));
    }
    DelayCommand(15.0, PDone());
}

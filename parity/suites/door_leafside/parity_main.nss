// Which way a door's leaf swings: openers standing at four spots around doors facing 0, 90 and 180
// degrees (local to the door: the closed use points' sides, -y and +y, at x 0.4 and -0.3), a door opened by itself,
// openers walking up from the south and the north, and a linked door. The leaf is found by jumping a probe creature
// onto each side's open leaf (local -0.75, -1.0 for "open 1" and -0.75, +1.0 for "open 2"): the one standing on the
// leaf is moved off it.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }

// A point in a door model's own frame: the closed leaf runs along x (-1.05 to 1.05), the use points are at
// y -0.9 and +0.9, "open 1" puts the leaf at x -1.07 to -0.44, y -1.97 to 0, "open 2" at y 0 to 1.97. A door placed
// with bearing b reads GetFacing b + 90 on nwserver.
location Local(object d, float lx, float ly, float fFacing = 0.0)
{
    vector p = GetPosition(d);
    float f = GetFacing(d) - 90.0;
    return Location(Area(), Vector(p.x + lx * cos(f) - ly * sin(f), p.y + lx * sin(f) + ly * cos(f), 0.0), fFacing);
}

object Actor(string sTag, location l)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", l, FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetPlotFlag(o, TRUE);
    return o;
}

string g_doors = "D0A D0B D0C D0D D90A D90B D90C D90D D180A D180B D180C D180D DSELF DFAR DFARN DCLOSE LK1 LK2 ";

void Open(string sDoor, float lx, float ly)
{
    object d = GetObjectByTag(sDoor);
    object o = Actor("O_" + sDoor, Local(d, lx, ly));
    AssignCommand(o, ActionOpenDoor(d));
}

void Start()
{
    string s = "0 90 180 ";
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string f = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100);
        Open("D" + f + "A", 0.4, -1.0);
        Open("D" + f + "B", 0.4, 1.0);
        Open("D" + f + "C", -0.3, -1.0);
        Open("D" + f + "D", -0.3, 1.0);
    }
    object d = GetObjectByTag("DSELF");
    AssignCommand(d, ActionOpenDoor(d));
    Open("DFAR", 0.0, -6.0);
    Open("DFARN", 0.0, 6.0);
    Open("LK1", 0.4, -1.0);
}

void Clear()
{
    string s = g_doors;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string t = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100);
        object o = GetObjectByTag("O_" + t);
        if (GetIsObjectValid(o))
        {
            vector v = GetPosition(o), dp = GetPosition(GetObjectByTag(t));
            PLogF(t + ".opener.dx", v.x - dp.x, 0.15);
            PLogF(t + ".opener.dy", v.y - dp.y, 0.15);
            AssignCommand(o, ClearAllActions());
            AssignCommand(o, JumpToLocation(Location(Area(), Vector(dp.x + 4.0, dp.y + 4.0, 0.0), 0.0)));
        }
    }
}

void Probe()
{
    string s = g_doors;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string t = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100);
        object d = GetObjectByTag(t);
        PLogI(t + ".open", GetIsOpen(d));
        Actor("P1_" + t, Local(d, -0.75, -1.0));
        Actor("P2_" + t, Local(d, -0.75, 1.0));
    }
}

void Read()
{
    string s = g_doors;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string t = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100);
        object d = GetObjectByTag(t);
        float m1 = GetDistanceBetweenLocations(GetLocation(GetObjectByTag("P1_" + t)), Local(d, -0.75, -1.0));
        float m2 = GetDistanceBetweenLocations(GetLocation(GetObjectByTag("P2_" + t)), Local(d, -0.75, 1.0));
        PLogF(t + ".facing", GetFacing(d), 0.5);
        PLog(t + ".side", m1 > 0.2 && m2 <= 0.2 ? "1" : m2 > 0.2 && m1 <= 0.2 ? "2" : "?" + FloatToString(m1, 0, 2) + "/" + FloatToString(m2, 0, 2));
    }
}

void main()
{
    DelayCommand(1.0, Start());
    DelayCommand(12.0, Clear());
    DelayCommand(13.0, Probe());
    DelayCommand(15.0, Read());
    DelayCommand(16.0, PDone());
}

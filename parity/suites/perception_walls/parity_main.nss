// Perception and line of sight through tile walls (tile 121, a walled corner: each tile's floor is its
// lower-left corner, walled off from the next tile's).
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float f = 0.0) { return Location(Area(), Vector(x, y, 0.0), f); }

object Make(string sRes, string sTag, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, sTag);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetLocalFloat(o, "BORN", PSince());
    return o;
}

void Check()
{
    object a = GetObjectByTag("OBS_WALL"), b = GetObjectByTag("BEYOND"), c = GetObjectByTag("SAMEROOM");
    PLogI("los.wall", LineOfSightObject(a, b));
    PLogI("los.room", LineOfSightObject(a, c));
    PLogI("los.vector.wall", LineOfSightVector(GetPosition(a) + Vector(0.0, 0.0, 1.0), GetPosition(b) + Vector(0.0, 0.0, 1.0)));
    PLogI("seen.wall", GetObjectSeen(b, a));
    PLogI("heard.wall", GetObjectHeard(b, a));
    PLogI("seen.room", GetObjectSeen(c, a));
    PLogF("pos.beyond.x", GetPosition(b).x, 0.05);
    PLogF("pos.beyond.y", GetPosition(b).y, 0.05);
}

void Setup()
{
    Make("p_obs9", "OBS_WALL", 3.0, 3.0);
    Make("p_tgt", "BEYOND", 13.0, 3.0);
    Make("p_tgt", "SAMEROOM", 5.0, 4.5);
}

// Line of sight between points of the floor in neighbouring tiles, at several heights.
void Vectors()
{
    float z;
    for (z = 0.3; z < 4.0; z += 1.2)
    {
        string h = FloatToString(z, 0, 1);
        PLogI("losv.east." + h, LineOfSightVector(Vector(3.0, 3.0, z), Vector(13.0, 3.0, z)));
        PLogI("losv.north." + h, LineOfSightVector(Vector(3.0, 3.0, z), Vector(3.0, 13.0, z)));
        PLogI("losv.diag." + h, LineOfSightVector(Vector(3.0, 3.0, z), Vector(13.0, 13.0, z)));
        PLogI("losv.inroom." + h, LineOfSightVector(Vector(1.0, 1.0, z), Vector(6.0, 5.0, z)));
        PLogI("losv.far." + h, LineOfSightVector(Vector(3.0, 3.0, z), Vector(33.0, 23.0, z)));
    }
}

void Stop()
{
    SetLocalInt(GetModule(), "PARITY_STOP", 1);
    PDone();
}

void main()
{
    PStart();
    DelayCommand(1.0, Setup());
    DelayCommand(7.0, Check());
    DelayCommand(7.0, Vectors());
    DelayCommand(8.0, Stop());
}

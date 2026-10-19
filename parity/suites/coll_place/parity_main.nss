// Personal space when creatures are placed: CreateObject, jumps and module creatures onto taken spots (the ring
// search for a free spot, how far apart creatures stay), and how near the area's edges a creature may stand.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float z = 0.0, float f = 0.0) { return Location(Area(), Vector(x, y, z), f); }

object Mk(string sRes, float x, float y, string sTag = "PL")
{
    return CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, sTag);
}

string Off(object o, float x, float y)
{
    vector p = GetPosition(o);
    return FloatToString(p.x - x, 0, 2) + " " + FloatToString(p.y - y, 0, 2);
}

void Clear()
{
    int n = 0;
    object o = GetObjectByTag("PL", n);
    while (GetIsObjectValid(o)) { DestroyObject(o); o = GetObjectByTag("PL", ++n); }
}

// A goblin created at an offset from another: where it ends up, relative to where it was asked to go.
void Pairs()
{
    int i, j;
    for (i = 0; i < 7; i++)
        for (j = 0; j < 7; j++)
        {
            float ox = 3.0 + 5.0 * IntToFloat(i), oy = 3.0 + 5.0 * IntToFloat(j);
            float dx = -0.75 + 0.25 * IntToFloat(i), dy = -0.75 + 0.25 * IntToFloat(j);
            Mk("p_gob", ox, oy);
            PLog("pair " + FloatToString(dx, 0, 2) + " " + FloatToString(dy, 0, 2), Off(Mk("p_gob", ox + dx, oy + dy), ox + dx, oy + dy));
        }
}

// Tiny creatures (fairies, 0.1 m personal space) created one after another on one spot show the search order.
void Fill()
{
    int i;
    for (i = 0; i < 40; i++)
    {
        object o = Mk("p_fairy", 20.0, 20.0);
        vector p = GetPosition(o);
        PLog("fill" + IntToString(100 + i), FloatToString((p.x - 20.0) * 10.0, 0, 1) + " " + FloatToString((p.y - 20.0) * 10.0, 0, 1));
    }
}

void Report(object o, float x, float y, string k) { PLog(k, Off(o, x, y)); PLogI(k + ".action", GetCurrentAction(o)); }

void Sizes()
{
    // Same spot: goblins, three goblins, a goblin on an ogre, two humans.
    Mk("p_gob", 4.0, 4.0); PLog("same.goblin", Off(Mk("p_gob", 4.0, 4.0), 4.0, 4.0));
    Mk("p_gob", 10.0, 4.0); PLog("three.b", Off(Mk("p_gob", 10.0, 4.0), 10.0, 4.0)); PLog("three.c", Off(Mk("p_gob", 10.0, 4.0), 10.0, 4.0));
    // (Not exactly on the ogre: points exactly personal space away round differently in the two engines.)
    Mk("p_ogre", 16.0, 4.0); PLog("ogre.goblin", Off(Mk("p_gob", 16.1, 4.0), 16.1, 4.0));
    Mk("p_gob", 22.0, 4.0); PLog("goblin.ogre", Off(Mk("p_ogre", 22.0, 4.0), 22.0, 4.0));
    Mk("p_human", 28.0, 4.0); PLog("same.human", Off(Mk("p_human", 28.0, 4.0), 28.0, 4.0));
    Mk("p_fairy", 34.0, 4.0); PLog("ogre.fairy", Off(Mk("p_ogre", 34.0, 4.0), 34.0, 4.0));
    // Jumps onto taken spots.
    Mk("p_gob", 4.0, 14.0); object j = Mk("p_gob", 4.0, 17.0);
    AssignCommand(j, ActionJumpToLocation(L(4.0, 14.0)));
    DelayCommand(1.0, Report(j, 4.0, 14.0, "jump.action"));
    Mk("p_gob", 10.0, 14.0); object j2 = Mk("p_gob", 10.0, 17.0);
    AssignCommand(j2, JumpToLocation(L(10.0, 14.0)));
    DelayCommand(1.0, Report(j2, 10.0, 14.0, "jump.instant"));
    Mk("p_ogre", 16.0, 14.0); object j3 = Mk("p_gob", 16.0, 17.0);
    AssignCommand(j3, ActionJumpToLocation(L(16.1, 14.0)));
    DelayCommand(1.0, Report(j3, 16.1, 14.0, "jump.ogre"));
    Mk("p_gob", 22.0, 14.0); object j4 = Mk("p_gob", 22.0, 17.0);
    AssignCommand(j4, ActionJumpToLocation(L(22.5, 14.0)));
    DelayCommand(1.0, Report(j4, 22.5, 14.0, "jump.near"));
    object t5 = Mk("p_gob", 28.0, 14.0); object j5 = Mk("p_gob", 28.0, 17.0);
    AssignCommand(j5, ActionJumpToObject(t5));
    DelayCommand(1.0, Report(j5, 28.0, 14.0, "jump.object"));
    // A creature jumping onto its own spot stays put.
    object j6 = Mk("p_gob", 34.0, 14.0);
    AssignCommand(j6, JumpToLocation(L(34.0, 14.0)));
    DelayCommand(1.0, Report(j6, 34.0, 14.0, "jump.self"));
    // Height: the ground under the requested point.
    object z1 = CreateObject(OBJECT_TYPE_CREATURE, "p_gob", L(4.0, 24.0, 3.0), FALSE, "PL");
    PLogF("z.above", GetPosition(z1).z, 0.01); PLog("z.above.xy", Off(z1, 4.0, 24.0));
    object z2 = CreateObject(OBJECT_TYPE_CREATURE, "p_gob", L(10.0, 24.0, -3.0), FALSE, "PL");
    PLogF("z.below", GetPosition(z2).z, 0.01); PLog("z.below.xy", Off(z2, 10.0, 24.0));
    // Module creatures placed on one spot.
    PLog("git.a", Off(GetObjectByTag("GIT_A"), 36.0, 36.0));
    PLog("git.b", Off(GetObjectByTag("GIT_B"), 36.0, 36.0));
}

void One(string sRes, float x, float y, string k)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, "EDGE");
    vector p = GetPosition(o);
    PLog(k + " " + sRes + " " + FloatToString(x, 0, 2) + " " + FloatToString(y, 0, 2),
        VectorMagnitude(p - Vector(x, y, 0.0)) > 0.001 ? FloatToString(p.x - x, 0, 2) + " " + FloatToString(p.y - y, 0, 2) : "ok");
    DestroyObject(o);
}

float t = 6.0;
void Line(string k, float x0, float y0, float dx, float dy, int n)
{
    int i;
    for (i = 0; i < n; i++)
    {
        float x = x0 + dx * IntToFloat(i), y = y0 + dy * IntToFloat(i);
        DelayCommand(t, One("p_fairy", x, y, k)); t += 0.1;
        DelayCommand(t, One("p_gob", x, y, k)); t += 0.1;
        DelayCommand(t, One("p_ogre", x, y, k)); t += 0.1;
    }
}

void main()
{
    PStart();
    DelayCommand(0.5, Pairs());
    DelayCommand(1.0, Clear());
    DelayCommand(1.5, Fill());
    DelayCommand(2.0, Clear());
    DelayCommand(2.5, Sizes());
    DelayCommand(5.0, Clear());
    // How near the area's edges a creature may be created.
    // (Points exactly PERSPACE from the west edge round differently: start a little off it.)
    Line("west", 0.02, 20.0, 0.05, 0.0, 10);
    Line("east", 39.98, 22.0, -0.05, 0.0, 10);
    Line("south", 20.0, 0.0, 0.0, 0.05, 4);
    Line("north", 22.0, 40.0, 0.0, -0.05, 4);
    DelayCommand(t + 0.5, PDone());
}

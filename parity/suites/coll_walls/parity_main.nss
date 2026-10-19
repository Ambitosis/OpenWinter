// Creatures created where they don't fit (in or near walls of the walled corner tile, tde01 121): where the
// original puts them instead, for three sizes (PERSPACE 0.1, 0.3 and 0.4) and two facings.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }

void One(string sRes, float x, float y, string k, float f)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(Area(), Vector(x, y, 0.0), f), FALSE, "W");
    vector p = GetPosition(o);
    PLog(k + " " + sRes + " " + FloatToString(x, 0, 2) + " " + FloatToString(y, 0, 2),
        VectorMagnitude(p - Vector(x, y, 0.0)) > 0.001 ? FloatToString(p.x - x, 0, 2) + " " + FloatToString(p.y - y, 0, 2) + " z=" + FloatToString(p.z, 0, 2) : "ok");
    DestroyObject(o);
}

float t = 0.5;
void Line(string k, float x0, float y0, float dx, float dy, int n, float f = 0.0)
{
    int i;
    for (i = 0; i < n; i++)
    {
        float x = x0 + dx * IntToFloat(i), y = y0 + dy * IntToFloat(i);
        DelayCommand(t, One("p_fairy", x, y, k, f)); t += 0.25;
        DelayCommand(t, One("p_gob", x, y, k, f)); t += 0.25;
        DelayCommand(t, One("p_ogre", x, y, k, f)); t += 0.25;
    }
}

void Grid()
{
    float x, y;
    for (x = 0.5; x < 10.0; x += 1.0)
        for (y = 0.5; y < 10.0; y += 1.0)
        {
            DelayCommand(t, One("p_gob", x, y, "grid", 0.0)); t += 0.25;
        }
}

void main()
{
    PStart();
    // A wall to the east (x 7.85), to the north (y 7.95), to the west (a pillar to x 2.1), to the south (y 2.1).
    Line("east", 7.02, 3.0, 0.1, 0.0, 9);
    Line("east90", 7.02, 3.0, 0.1, 0.0, 9, 90.0);
    Line("north", 3.5, 6.62, 0.0, 0.1, 14);
    Line("west", 2.02, 6.5, 0.1, 0.0, 10);
    Line("south", 6.0, 2.02, 0.0, 0.1, 7);
    Grid();
    DelayCommand(t, One("p_gob", 7.5, 36.0, "spot", 0.0)); t += 0.25;
    DelayCommand(t, One("p_ogre", 7.5, 36.0, "spot", 0.0)); t += 0.25;
    DelayCommand(t + 0.5, PDone());
}

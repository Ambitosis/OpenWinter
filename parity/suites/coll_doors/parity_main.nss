// Open doors: the leaf swung open blocks the way like a wall (creatures walk round it, also to close the door from
// the side it swung to), the doorway is clear, and a creature created on a leaf or a table is moved off it.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x, y, 0.0), 0.0); }

object Mk(string k, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "p_gob", L(x, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Pos(string k, float fTol)
{
    object o = GetObjectByTag(k);
    vector p = GetPosition(o);
    PLogF(k + ".x", p.x, fTol);
    PLogF(k + ".y", p.y, fTol);
    PLogI(k + ".action", GetCurrentAction(o));
}

void Arrived(string k) { PLogF(k + ".arrived", PSince(), 0.25); }

void Go(string k, float x, float y)
{
    object o = GetObjectByTag(k);
    AssignCommand(o, ActionMoveToLocation(L(x, y), FALSE));
    AssignCommand(o, ActionDoCommand(Arrived(k)));
}

// How far a walker strays sideways from its straight line, on each side.
void Track(string k, float x0)
{
    float d = GetPosition(GetObjectByTag(k)).x - x0;
    object m = GetModule();
    if (d > GetLocalFloat(m, "E_" + k)) SetLocalFloat(m, "E_" + k, d);
    if (d < GetLocalFloat(m, "W_" + k)) SetLocalFloat(m, "W_" + k, d);
}

void Tracks(string k, float x0)
{
    float t;
    for (t = 0.0; t < 8.0; t += 0.1) DelayCommand(t, Track(k, x0));
}

void Strayed(string k)
{
    PLogF(k + ".east", GetLocalFloat(GetModule(), "E_" + k), 0.15);
    PLogF(k + ".west", GetLocalFloat(GetModule(), "W_" + k), 0.15);
}

void Open()
{
    // Opened from the south: the leaves swing north. D3 opens itself.
    AssignCommand(GetObjectByTag("o1"), ActionOpenDoor(GetObjectByTag("D1")));
    AssignCommand(GetObjectByTag("o2"), ActionOpenDoor(GetObjectByTag("D2")));
    AssignCommand(GetObjectByTag("D3"), ActionOpenDoor(GetObjectByTag("D3")));
}

void Walk()
{
    PLogI("D1.open", GetIsOpen(GetObjectByTag("D1")));
    PLogI("D2.open", GetIsOpen(GetObjectByTag("D2")));
    PLogI("D3.open", GetIsOpen(GetObjectByTag("D3")));
    // Out of the openers' way first.
    AssignCommand(GetObjectByTag("o1"), ActionMoveToLocation(L(13.0, 14.0), TRUE));
    AssignCommand(GetObjectByTag("o2"), ActionMoveToLocation(L(33.0, 14.0), TRUE));
}

void Walk2()
{
    Go("w1", 9.2, 25.0); Tracks("w1", 9.2);          // north through D1's doorway, its leaf in the way
    Go("w2", 9.2, 28.0); Tracks("w2", 9.2);          // south past D3's leaf
    Go("w3", 11.0, 25.0); Tracks("w3", 11.0);        // north through the doorway, clear of the leaf
    object c = GetObjectByTag("c2");
    AssignCommand(c, ActionCloseDoor(GetObjectByTag("D2")));   // close D2 from the side its leaf swung to
    AssignCommand(c, ActionDoCommand(Arrived("c2"))); Tracks("c2", 28.6);
    // Created on a leaf, on a table.
    object g = CreateObject(OBJECT_TYPE_CREATURE, "p_gob", L(29.2, 21.0), FALSE, "inleaf");
    PLogF("inleaf.x", GetPosition(g).x, 0.01); PLogF("inleaf.y", GetPosition(g).y, 0.01);
    object h = CreateObject(OBJECT_TYPE_CREATURE, "p_gob", L(20.0, 8.0), FALSE, "ontable");
    PLogF("ontable.x", GetPosition(h).x, 0.01); PLogF("ontable.y", GetPosition(h).y, 0.01);
    object e = CreateObject(OBJECT_TYPE_CREATURE, "p_gob", L(20.5, 7.5), FALSE, "neartable");
    PLogF("neartable.x", GetPosition(e).x, 0.01); PLogF("neartable.y", GetPosition(e).y, 0.01);
}

void Report()
{
    Pos("w1", 0.15); Strayed("w1");
    Pos("w2", 0.15); Strayed("w2");
    Pos("w3", 0.15); Strayed("w3");
    Pos("c2", 0.2); Strayed("c2");
    PLogI("D2.open.end", GetIsOpen(GetObjectByTag("D2")));
}

void Setup()
{
    Mk("o1", 10.0, 17.5); Mk("o2", 30.0, 17.5);
    Mk("w1", 9.2, 15.0); Mk("w2", 9.2, 36.0); Mk("w3", 11.0, 15.0); Mk("c2", 28.6, 23.5);
}

void main()
{
    PStart();
    DelayCommand(0.5, Setup());
    DelayCommand(5.5, Open());
    DelayCommand(8.0, Walk());
    DelayCommand(11.0, Walk2());
    DelayCommand(21.0, Report());
    DelayCommand(21.5, PDone());
}

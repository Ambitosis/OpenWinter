// Creatures walking into creatures (parity for PersonalSpace): idle ones are pushed out of the way at once,
// busy or moving ones stop the walker (OnBlocked), and paths go round busy creatures within 10 m. Only creatures
// the walker perceives count, so everything is created five seconds before anyone moves.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x, y, 0.0), 0.0); }

object Mk(string sRes, string k, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

object Quiet(string k, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "p_quiet", L(x, y), FALSE, k);
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

void Arrived(string k) { PLogF(k + ".arrived", PSince(), 0.2); }

void Go(string k, float x, float y, int bRun = FALSE)
{
    object o = GetObjectByTag(k);
    AssignCommand(o, ActionMoveToLocation(L(x, y), bRun));
    AssignCommand(o, ActionDoCommand(Arrived(k)));
}

void Busy(string k) { AssignCommand(GetObjectByTag(k), ActionWait(60.0)); }

// The furthest a walker strays from its straight line (detours round busy creatures).
void Track(string k, float y0)
{
    float d = GetPosition(GetObjectByTag(k)).y - y0;
    object m = GetModule();
    if (fabs(d) > fabs(GetLocalFloat(m, "DEV_" + k))) SetLocalFloat(m, "DEV_" + k, d);
}

void Tracks(string k, float y0)
{
    float t;
    for (t = 0.0; t < 9.0; t += 0.1) DelayCommand(t, Track(k, y0));
}

void Deviation(string k) { PLogF(k + ".deviation", GetLocalFloat(GetModule(), "DEV_" + k), 0.12); }

void Start()
{
    Go("collinear", 28.0, 2.0);                       // pushed ahead again and again
    Go("lateral3", 28.0, 5.0);                        // pushed sideways
    Go("lateral6", 28.0, 8.0);
    Go("run", 34.0, 11.0, TRUE);
    // Head on: one or both are stopped (which, and whether the other then pushes it on, varies in the original).
    AssignCommand(GetObjectByTag("headA"), ActionMoveToLocation(L(28.0, 14.0)));
    AssignCommand(GetObjectByTag("headB"), ActionMoveToLocation(L(12.0, 14.0)));
    Go("onto", 20.0, 17.0);                           // walking onto an idle creature's spot
    Go("ogre", 28.0, 20.0);                           // an ogre pushes a goblin 1.6 m
    Go("small", 28.0, 23.0);                          // a goblin pushes an ogre
    Go("round", 28.0, 26.0); Tracks("round", 26.0);   // round a busy creature
    Go("wall", 28.0, 30.0);                           // through a row of idle goblins 1 m apart
    Go("gap", 28.0, 35.0);                            // between two goblins 2 m apart
    Go("aside", 28.0, 38.0);                          // straight on blocked: aside
}

void Start2()
{
    Go("north", 3.0, 30.0); Go("north2", 7.0, 30.0);  // heading north, idle creature right and left
    Go("edge", 37.0, 39.4);                           // can't push it off the area: sideways
    Go("chase", 37.0, 2.0, TRUE); Go("chased", 37.0, 2.0);  // catching up a walker: blocked
    Go("late", 37.0, 6.0); DelayCommand(1.0, Busy("lateb"));  // a creature that gets busy on the way: blocked
    Go("far", 38.0, 9.0);                             // busy 11 m away: not planned round, blocked
}

void Setup()
{
    Mk("p_gob", "collinear", 14.0, 2.0); Mk("p_gob", "collinearb", 20.0, 2.0);
    Mk("p_gob", "lateral3", 14.0, 5.0); Mk("p_gob", "lateral3b", 20.0, 5.3);
    Mk("p_gob", "lateral6", 14.0, 8.0); Mk("p_gob", "lateral6b", 20.0, 8.6);
    Mk("p_gob", "run", 10.0, 11.0); Mk("p_gob", "runb", 22.0, 11.0);
    Quiet("headA", 12.0, 14.0); Quiet("headB", 28.0, 14.0);
    Mk("p_gob", "onto", 14.0, 17.0); Mk("p_gob", "ontob", 20.0, 17.0);
    Mk("p_ogre", "ogre", 13.0, 20.0); Mk("p_gob", "ogreb", 20.0, 20.0);
    Mk("p_gob", "small", 14.0, 23.0); Mk("p_ogre", "smallb", 20.0, 23.0);
    Mk("p_gob", "round", 14.0, 26.0); Mk("p_gob", "roundb", 20.0, 26.0);
    float y;
    for (y = 28.0; y <= 32.01; y += 1.0) Mk("p_gob", "wall" + FloatToString(y, 0, 0), 20.0, y);
    Mk("p_gob", "wall", 14.0, 30.0);
    Mk("p_gob", "gapb", 20.0, 34.0); Mk("p_gob", "gapc", 20.0, 36.0); Mk("p_gob", "gap", 14.0, 35.0);
    Mk("p_gob", "aside", 14.0, 38.0); Mk("p_gob", "asideb", 20.0, 38.0); Mk("p_gob", "asidec", 20.9, 38.0);
}

void Setup2()
{
    Mk("p_gob", "north", 3.0, 22.0); Mk("p_gob", "northb", 3.5, 26.0);
    Mk("p_gob", "north2", 7.0, 22.0); Mk("p_gob", "north2b", 6.5, 26.0);
    Mk("p_gob", "edge", 37.0, 33.0); Mk("p_gob", "edgeb", 37.0, 39.2);
    Mk("p_gob", "chase", 25.0, 2.0); Mk("p_gob", "chased", 29.0, 2.0);
    Mk("p_gob", "late", 25.0, 6.0); Mk("p_gob", "lateb", 31.0, 6.0);
    Mk("p_gob", "far", 24.0, 9.0); Mk("p_gob", "farb", 35.0, 9.0);
}

void Report()
{
    Pos("collinear", 0.01); Pos("collinearb", 0.01);
    PLogI("head.apart", GetDistanceBetween(GetObjectByTag("headA"), GetObjectByTag("headB")) > 0.78);
    PLogI("head.stopped", GetCurrentAction(GetObjectByTag("headA")) == ACTION_INVALID && GetCurrentAction(GetObjectByTag("headB")) == ACTION_INVALID);
    Pos("lateral3b", 0.01); Pos("lateral6b", 0.01);
    Pos("run", 0.01); Pos("runb", 0.01);
    Pos("onto", 0.01); Pos("ontob", 0.01);
    Pos("ogre", 0.01); Pos("ogreb", 0.01);
    Pos("small", 0.01); Pos("smallb", 0.01);
    Pos("round", 0.01); Pos("roundb", 0.01); Deviation("round");
    Pos("wall28", 0.01); Pos("wall29", 0.01); Pos("wall30", 0.01); Pos("wall31", 0.01); Pos("wall32", 0.01);
    Pos("gapb", 0.01); Pos("gapc", 0.01);
    Pos("asideb", 0.01); Pos("asidec", 0.01);
}

void Report2()
{
    Pos("northb", 0.01); Pos("north2b", 0.01);
    Pos("edge", 0.01); Pos("edgeb", 0.01);
    Pos("chase", 0.4); Pos("chased", 0.01);
    Pos("late", 0.15); Pos("lateb", 0.01);
    Pos("far", 0.15); Pos("farb", 0.01);
}

// Crowds: six walkers converging on one point, four running in single file, a walker down a corridor lined with
// busy creatures with idle ones in it. (Who blocks whom when they converge varies from run to run in the original,
// so those creatures don't report OnBlocked.)
void Clear()
{
    object o = GetFirstObjectInArea(Area());
    while (GetIsObjectValid(o))
    {
        if (GetObjectType(o) == OBJECT_TYPE_CREATURE) DestroyObject(o);
        o = GetNextObjectInArea(Area());
    }
}


void Setup3()
{
    int i;
    for (i = 0; i < 6; i++)
    {
        float a = IntToFloat(i) * 60.0;
        Quiet("conv" + IntToString(i), 30.0 + 6.0 * cos(a), 10.0 + 6.0 * sin(a));
    }
    for (i = 0; i < 4; i++) Quiet("file" + IntToString(i), 8.0 - IntToFloat(i), 36.0);
    float x;
    for (x = 10.0; x <= 30.01; x += 1.0) { Mk("p_gob", "rowa", x, 24.0); Mk("p_gob", "rowb", x, 26.4); }
    Mk("p_gob", "idle1", 14.0, 25.2); Mk("p_gob", "idle2", 17.0, 25.2); Mk("p_gob", "idle3", 20.0, 25.2);
    Quiet("corridor", 8.0, 25.2);
}

void Rows()
{
    int n = 0;
    object o = GetObjectByTag("rowa", n);
    while (GetIsObjectValid(o)) { AssignCommand(o, ActionWait(60.0)); o = GetObjectByTag("rowa", ++n); }
    n = 0; o = GetObjectByTag("rowb", n);
    while (GetIsObjectValid(o)) { AssignCommand(o, ActionWait(60.0)); o = GetObjectByTag("rowb", ++n); }
}

void Start3()
{
    int i;
    for (i = 0; i < 6; i++) AssignCommand(GetObjectByTag("conv" + IntToString(i)), ActionMoveToLocation(L(30.0, 10.0)));
    for (i = 0; i < 4; i++) AssignCommand(GetObjectByTag("file" + IntToString(i)), ActionMoveToLocation(L(30.0, 36.0), TRUE));
    AssignCommand(GetObjectByTag("corridor"), ActionMoveToLocation(L(32.0, 25.2)));
}

void Report3()
{
    // Converging: they end round the point, personal space apart.
    float fNear = 99.0;
    int i, j;
    for (i = 0; i < 6; i++)
        for (j = i + 1; j < 6; j++)
        {
            float e = GetDistanceBetween(GetObjectByTag("conv" + IntToString(i)), GetObjectByTag("conv" + IntToString(j)));
            if (e < fNear) fNear = e;
        }
    PLogF("conv.closest", fNear, 0.1);
    // Single file: the leader gets there; the others are stopped behind it, about personal space apart.
    PLogI("file0.there", GetDistanceBetweenLocations(GetLocation(GetObjectByTag("file0")), L(30.0, 36.0)) < 1.0);
    // (How far back each one stops varies in the original.)
    for (i = 1; i < 4; i++)
        PLogI("file" + IntToString(i) + ".behind", GetDistanceBetween(GetObjectByTag("file" + IntToString(i)), GetObjectByTag("file" + IntToString(i - 1))) > 0.78);
    // The corridor: the walker pushes the idle creatures it can; where it can't (busy creatures all round), it
    // sometimes walks through and sometimes is stopped in the original, so only the push is compared.
    PLogI("idle1.pushed", GetPosition(GetObjectByTag("idle1")).x > 14.5);
}

void main()
{
    PStart();
    DelayCommand(0.5, Setup());
    DelayCommand(6.0, Start());
    DelayCommand(16.0, Report());
    DelayCommand(16.5, Setup2());
    DelayCommand(21.5, Busy("farb"));
    DelayCommand(22.0, Start2());
    DelayCommand(30.0, Report2());
    DelayCommand(30.6, Clear());
    DelayCommand(31.0, Setup3());
    DelayCommand(35.5, Rows());
    DelayCommand(36.5, Start3());
    DelayCommand(52.0, Report3());
    DelayCommand(52.5, PDone());
}

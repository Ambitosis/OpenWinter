// Pavel's room (Prelude m0q1d) entered the way a player does: the area loads with no player in it, its nine
// m1q0ddeadgob goblins run their spawn script (queued SetIsDestroyable(FALSE), then their own death) there, and the
// player arrives later. Are the corpses beside Pavel there when he does?
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }

void Report(string when)
{
    int n = 0;
    object o = GetObjectByTag("M1Q0DDeadGoblin", n);
    while (GetIsObjectValid(o))
    {
        vector v = GetPosition(o);
        Note(when + " goblin " + FloatToString(v.x, 0, 1) + "," + FloatToString(v.y, 0, 1) + " dead " + IntToString(GetIsDead(o))
            + " hp " + IntToString(GetCurrentHitPoints(o)) + " destroyable " + IntToString(GetIsDestroyable(o)));
        o = GetObjectByTag("M1Q0DDeadGoblin", ++n);
    }
    Note(when + " goblins " + IntToString(n));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc) || GetLocalInt(OBJECT_SELF, "begun")) return;
    SetLocalInt(OBJECT_SELF, "begun", 1);
    VBegin(pc);
    Report("start");
    object room = GetArea(GetObjectByTag("M1Q0DPavel"));
    DelayCommand(8.0, AssignCommand(pc, JumpToLocation(Location(room, Vector(44.0, 101.0, 0.0), 90.0))));
    DelayCommand(16.0, VCamera(pc, 75.0, 16.0, 45.0));
    DelayCommand(18.0, Report("arrived"));
    DelayCommand(18.0, VShot("arrive"));
    DelayCommand(30.0, Report("later"));
    DelayCommand(30.0, VShot("later"));
    DelayCommand(33.0, VDone());
}

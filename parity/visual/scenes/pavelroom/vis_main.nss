// Pavel's room after the attack on the academy (Prelude m0q1d) as the module places it: Pavel and two of the nine
// m1q0ddeadgob goblins, whose spawn script queues SetIsDestroyable(FALSE) and then their own death. Do their corpses
// stay? Notes list every dead goblin's state; the shots look at the two beside Pavel.
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
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    Report("enter");
    DelayCommand(1.0, VCamera(pc, 75.0, 16.0, 45.0));
    DelayCommand(3.0, Report("3s"));
    DelayCommand(3.0, VShot("room3"));
    DelayCommand(14.0, Report("14s"));
    DelayCommand(14.0, VShot("room14"));
    DelayCommand(30.0, Report("30s"));
    DelayCommand(30.0, VShot("room30"));
    DelayCommand(33.0, VDone());
}

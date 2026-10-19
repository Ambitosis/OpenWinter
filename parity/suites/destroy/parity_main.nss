// DestroyObject: when objects become invalid, delays, what still runs, and events on the way out.
#include "parity_inc"
#include "parity_time"

object Make(string sTag)
{
    return CreateObject(OBJECT_TYPE_PLACEABLE, "p_plc", PCentre(IntToFloat(GetLocalInt(GetModule(), "N") % 8) - 4.0, 3.0), FALSE, sTag);
}

void Valid(string k, object o)
{
    PLogI(k, GetIsObjectValid(o));
}

void ValidByTag(string k, string sTag)
{
    PLogI(k, GetIsObjectValid(GetObjectByTag(sTag)));
}

void Mark(string s)
{
    PLog("event", s);
}

void SelfDestruct()
{
    DestroyObject(OBJECT_SELF);
    PLog("event", "self.after.destroy valid=" + IntToString(GetIsObjectValid(OBJECT_SELF)) + " tag=" + GetTag(OBJECT_SELF));
    DelayCommand(0.0, Mark("self.delay0"));
}

void Immediate()
{
    object a = Make("D_NOW");
    DestroyObject(a);
    PLogI("now.same.script", GetIsObjectValid(a));
    PLogI("now.same.script.bytag", GetIsObjectValid(GetObjectByTag("D_NOW")));
    PLog("now.same.script.tag", GetTag(a));
    AssignCommand(a, Mark("now.assign.after.destroy"));
    DelayCommand(0.0, Valid("now.delay0", a));
    DelayCommand(0.1, Valid("now.next", a));

    object b = Make("D_DELAY");
    DestroyObject(b, 1.0);
    DelayCommand(0.9, Valid("delay1.at0.9", b));
    DelayCommand(1.1, Valid("delay1.at1.1", b));
    // A second call: the earlier one wins? Or the last?
    object b2 = Make("D_TWICE");
    DestroyObject(b2, 2.0);
    DestroyObject(b2, 0.5);
    DelayCommand(0.7, Valid("twice.at0.7", b2));
    DelayCommand(2.2, Valid("twice.at2.2", b2));

    object c = Make("D_SELF");
    AssignCommand(c, SelfDestruct());
    DelayCommand(0.1, Valid("self.next", c));

    // Plot objects
    object p = Make("D_PLOT");
    SetPlotFlag(p, TRUE);
    DestroyObject(p);
    DelayCommand(0.1, Valid("plot.next", p));

    // Creatures: OnExit runs on the way out, OnDeath does not.
    object cr = CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(0.0, -3.0), FALSE, "D_CRIT");
    DelayCommand(0.3, DestroyObject(cr));
    DelayCommand(0.5, Valid("crit.next", cr));
    object nd = CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(2.0, -3.0), FALSE, "D_UNDESTROYABLE");
    AssignCommand(nd, SetIsDestroyable(FALSE));
    DelayCommand(0.3, DestroyObject(nd));
    DelayCommand(0.5, Valid("undestroyable.next", nd));
    object im = CreateObject(OBJECT_TYPE_CREATURE, "p_crit", PCentre(4.0, -3.0), FALSE, "D_IMMORTAL");
    SetImmortal(im, TRUE);
    SetPlotFlag(im, TRUE);
    DelayCommand(0.3, DestroyObject(im));
    DelayCommand(0.5, Valid("plotcrit.next", im));

    // Items in an inventory
    object it = CreateItemOnObject("nw_wswdg001", cr);
    DestroyObject(it);
    PLogI("item.same.script", GetIsObjectValid(it));
    DelayCommand(0.1, Valid("item.next", it));
}

// A clock jump runs a pending destruction at once, like other delayed events.
void Jump()
{
    object j = Make("D_JUMP");
    DestroyObject(j, 30.0);
    SetTime(GetTimeHour() + 1, GetTimeMinute(), GetTimeSecond(), GetTimeMillisecond());
    PLogI("jump.same.script", GetIsObjectValid(j));
    DelayCommand(0.1, Valid("jump.next", j));
    DelayCommand(0.5, PDone());
}

void main()
{
    PStart();
    DelayCommand(1.0, Immediate());
    DelayCommand(5.0, Jump());
}

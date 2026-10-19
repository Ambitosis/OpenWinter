// DelayCommand timing and order, AssignCommand and ExecuteScript semantics, and what happens to
// commands whose owner is gone.
#include "parity_inc"
#include "parity_time"

void Mark(string s)
{
    PLog("order", s + " self=" + GetTag(OBJECT_SELF));
}

void Fired(string k, float fWanted)
{
    PLogF("delay." + k, PSince() - GetLocalFloat(GetModule(), "T_" + k), 0.045);
}

void Timed(string k, float fDelay)
{
    SetLocalFloat(GetModule(), "T_" + k, PSince());
    DelayCommand(fDelay, Fired(k, fDelay));
}

void Chain(string k, int n, int bAssign)
{
    if (n == 0)
    {
        PLogF("chain." + k, PSince() - GetLocalFloat(GetModule(), "C_" + k), 0.05);
        return;
    }
    if (bAssign) AssignCommand(GetModule(), Chain(k, n - 1, bAssign));
    else DelayCommand(0.0, Chain(k, n - 1, bAssign));
}

void Precision()
{
    Timed("0.01", 0.01);
    Timed("0.05", 0.05);
    Timed("0.1", 0.1);
    Timed("0.25", 0.25);
    Timed("0.5", 0.5);
    Timed("1.0", 1.0);
    Timed("2.5", 2.5);
    Timed("6.0", 6.0);
    // A chain of commands queued with no delay: same frame or one frame each?
    SetLocalFloat(GetModule(), "C_delay", PSince());
    Chain("delay", 30, FALSE);
    SetLocalFloat(GetModule(), "C_assign", PSince());
    Chain("assign", 30, TRUE);
}

void Order()
{
    DelayCommand(0.3, Mark("d0.3a"));
    DelayCommand(0.3, Mark("d0.3b"));
    DelayCommand(0.2, Mark("d0.2"));
    DelayCommand(0.0, Mark("d0"));
    DelayCommand(-1.0, Mark("dneg"));
    DelayCommand(0.6, Mark("d0.6"));
    DelayCommand(0.1, DelayCommand(0.25, Mark("nested.0.1+0.25")));
    DelayCommand(0.45, Mark("d0.45"));
    // Delayed commands due at the same time run last queued first.
    int i;
    for (i = 1; i <= 4; i++) DelayCommand(0.7, Mark("tie" + IntToString(i)));
    DelayCommand(0.65, Mark("d0.65"));
    DelayCommand(0.7, Mark("tie5"));

    // ExecuteScript runs at once, with OBJECT_SELF the target.
    object p = CreateObject(OBJECT_TYPE_PLACEABLE, "p_plc", PCentre(2.0, 0.0), FALSE, "PLC_A");
    SetScriptParam("p", "x1");
    ExecuteScript("dl_exec", p);
    ExecuteScript("dl_exec");
    Mark("after.exec");

    // AssignCommand: runs later, as the target. Nothing runs for an invalid target.
    AssignCommand(p, Mark("assign.plc"));
    AssignCommand(GetArea(p), Mark("assign.area"));
    AssignCommand(OBJECT_INVALID, Mark("assign.invalid"));
    // Commands owned by an object that is destroyed before they run are dropped.
    object q = CreateObject(OBJECT_TYPE_PLACEABLE, "p_plc", PCentre(-2.0, 0.0), FALSE, "PLC_B");
    AssignCommand(q, DelayCommand(0.5, Mark("owner.destroyed")));
    DestroyObject(q, 0.2);
    object r = CreateObject(OBJECT_TYPE_PLACEABLE, "p_plc", PCentre(-2.0, 2.0), FALSE, "PLC_C");
    AssignCommand(r, Mark("assign.then.destroyed"));
    DestroyObject(r);
    Mark("order.end");
}

void main()
{
    PStart();
    DelayCommand(1.0, Order());
    DelayCommand(3.0, Precision());
    DelayCommand(10.0, PDone());
}

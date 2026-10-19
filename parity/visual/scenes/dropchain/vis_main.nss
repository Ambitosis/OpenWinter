// Chained put-downs and pick-ups. Each trial queues
// several ActionPutDownItem / ActionPickUpItem at once (as dragging several items out of the inventory does), and
// every 0.05 s the scene notes the player's current action and which of the trial's items lie on the ground, on the
// public game clock. Frame bursts (paused around posted keys, which a burst would delay) show whether she stays bent. Trials: three put-downs; three
// put-downs with W held from 2.2 s; a put-down cancelled by W then two more; three pick-ups; put-down, pick-up,
// put-down; a put-down issued 0.2 s after the previous one landed; one more alone.
#include "visual_inc"

float Now()
{
    int minutes = FloatToInt(HoursToSeconds(1)) / 60;
    return IntToFloat((GetTimeHour() * minutes + GetTimeMinute()) * 60 + GetTimeSecond())
        + IntToFloat(GetTimeMillisecond()) / 1000.0;
}

void Note(string detail)
{
    object m = GetModule();
    WriteTimestampedLogEntry("VISUAL|note|" + GetLocalString(m, "case") + "|t="
        + FloatToString(Now() - GetLocalFloat(m, "start"), 0, 3) + "|" + detail);
}

void Key(int nKey, string sState)
{
    Note("key=" + IntToString(nKey) + "_" + sState);
    WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nKey) + "|" + sState);
}

void Burst(string name, float seconds)
{
    WriteTimestampedLogEntry("VISUAL|burst|" + name + "|" + FloatToString(seconds, 0, 1) + "|10");
}

string State(object pc)
{
    string s = "";
    int i;
    for (i = 0; i < 3; i++)
    {
        object o = GetLocalObject(pc, "i" + IntToString(i));
        if (!GetIsObjectValid(o)) s += "-";
        else if (GetItemPossessor(o) == pc) s += "P";
        else if (!GetIsObjectValid(GetItemPossessor(o))) s += "G";
        else s += "?";
    }
    return s;
}

void Poll(object pc, string sCase, int n, int nMax)
{
    if (GetLocalString(GetModule(), "case") != sCase) return;
    vector v = GetPosition(pc);
    Note("n=" + IntToString(n) + "|action=" + IntToString(GetCurrentAction(pc)) + "|items=" + State(pc)
        + "|pos=" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2));
    if (n < nMax) DelayCommand(0.05, Poll(pc, sCase, n + 1, nMax));
}

void Finished(string what) { Note("queue_done=" + what); }

void Begin(string sCase, float seconds)
{
    object pc = GetFirstPC();
    SetLocalString(GetModule(), "case", sCase);
    SetLocalFloat(GetModule(), "start", Now());
    Note("start");
    Poll(pc, sCase, 0, FloatToInt(seconds / 0.05));
}

object Pack(object pc, int i)
{
    object o = CreateItemOnObject("nw_wswdg001", pc);
    SetLocalObject(pc, "i" + IntToString(i), o);
    return o;
}

object Ground(object pc, int i, float dx)
{
    vector v = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_ITEM, "nw_wswdg001", Location(GetArea(pc), Vector(v.x + dx, v.y + 0.4, v.z), 0.0));
    SetLocalObject(pc, "i" + IntToString(i), o);
    return o;
}

void PutDown(object pc, object o, string what)
{
    AssignCommand(pc, ActionPutDownItem(o));
    AssignCommand(pc, ActionDoCommand(Finished(what)));
}

void PickUp(object pc, object o, string what)
{
    AssignCommand(pc, ActionPickUpItem(o));
    AssignCommand(pc, ActionDoCommand(Finished(what)));
}

void Home()
{
    object pc = GetFirstPC();
    SetLocalString(GetModule(), "case", "");
    AssignCommand(pc, ClearAllActions(TRUE));
    int i;
    for (i = 0; i < 3; i++)
    {
        object o = GetLocalObject(pc, "i" + IntToString(i));
        if (GetIsObjectValid(o)) DestroyObject(o);
        DeleteLocalObject(pc, "i" + IntToString(i));
    }
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(20.0, 20.0, 0.0), 90.0)));
}

void Chain3()
{
    object pc = GetFirstPC();
    object a = Pack(pc, 0); object b = Pack(pc, 1); object c = Pack(pc, 2);
    Begin("chain3", 6.5);
    Burst("chain3", 5.0);
    PutDown(pc, a, "a"); PutDown(pc, b, "b"); PutDown(pc, c, "c");
    DelayCommand(5.0, Burst("chain3b", 2.0));
}

void ChainMove()
{
    object pc = GetFirstPC();
    object a = Pack(pc, 0); object b = Pack(pc, 1); object c = Pack(pc, 2);
    Begin("chainmove", 4.5);
    Burst("chainmove", 2.0);
    PutDown(pc, a, "a"); PutDown(pc, b, "b"); PutDown(pc, c, "c");
    DelayCommand(2.2, Key(87, "down"));
    DelayCommand(2.7, Key(87, "up"));
    DelayCommand(2.8, Burst("chainmoveb", 1.7));
}

void ChainAfterCancel2()
{
    object pc = GetFirstPC();
    PutDown(pc, GetLocalObject(pc, "i1"), "b");
    PutDown(pc, GetLocalObject(pc, "i2"), "c");
    Note("queued_b_c");
}

void CancelThenChain()
{
    object pc = GetFirstPC();
    object a = Pack(pc, 0); Pack(pc, 1); Pack(pc, 2);
    Begin("cancelchain", 5.0);
    Burst("cancelchain", 0.5);
    PutDown(pc, a, "a");
    DelayCommand(1.15, Burst("cancelchainb", 3.8));
    DelayCommand(0.6, Key(87, "down"));
    DelayCommand(1.1, Key(87, "up"));
    DelayCommand(1.3, ChainAfterCancel2());
}

void Pick3()
{
    object pc = GetFirstPC();
    object a = Ground(pc, 0, -0.3); object b = Ground(pc, 1, 0.0); object c = Ground(pc, 2, 0.3);
    Begin("pick3", 6.0);
    Burst("pick3", 5.0);
    PickUp(pc, a, "a"); PickUp(pc, b, "b"); PickUp(pc, c, "c");
    DelayCommand(5.0, Burst("pick3b", 1.0));
}

void Mixed()
{
    object pc = GetFirstPC();
    object a = Pack(pc, 0); object b = Ground(pc, 1, 0.0); object c = Pack(pc, 2);
    Begin("mixed", 6.0);
    Burst("mixed", 5.0);
    PutDown(pc, a, "a"); PickUp(pc, b, "b"); PutDown(pc, c, "c");
    DelayCommand(5.0, Burst("mixedb", 1.0));
}

void GapSecond()
{
    object pc = GetFirstPC();
    Note("issue_b");
    PutDown(pc, GetLocalObject(pc, "i1"), "b");
}

void Gap()
{
    object pc = GetFirstPC();
    object a = Pack(pc, 0); Pack(pc, 1);
    Begin("gap", 4.5);
    Burst("gap", 4.5);
    PutDown(pc, a, "a");
    DelayCommand(1.7, GapSecond());
}

void Alone()
{
    object pc = GetFirstPC();
    object a = Pack(pc, 0);
    Begin("alone", 2.5);
    PutDown(pc, a, "a");
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    SetPlotFlag(pc, TRUE);
    AssignCommand(pc, ClearAllActions(TRUE));
    DelayCommand(1.0, VCamera(pc, 0.0, 5.0, 78.0));
    DelayCommand(3.0, Chain3());
    DelayCommand(10.5, Home());
    DelayCommand(12.0, ChainMove());
    DelayCommand(17.0, Home());
    DelayCommand(18.5, CancelThenChain());
    DelayCommand(24.0, Home());
    DelayCommand(25.5, Pick3());
    DelayCommand(32.0, Home());
    DelayCommand(33.5, Mixed());
    DelayCommand(40.0, Home());
    DelayCommand(41.5, Gap());
    DelayCommand(46.5, Home());
    DelayCommand(48.0, Alone());
    DelayCommand(51.0, VDone());
}

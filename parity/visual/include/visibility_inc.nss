// Visibility observations use public NWScript state; these are not network observations.
#include "visual_inc"

void CVKey(string state)
{
    WriteTimestampedLogEntry("VISUAL|key|9|" + state);
}

void CVQuiet(object actor)
{
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(actor, i, "");
    SetCommandable(TRUE, actor);
    AssignCommand(actor, ClearAllActions(TRUE));
    SetPlotFlag(actor, TRUE);
    SetAILevel(actor, AI_LEVEL_NORMAL);
    SetEventScript(actor, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "cv_hb");
}

void CVReport(object pc, object actor, string name, vector expected)
{
    vector p = GetPosition(pc);
    vector a = GetPosition(actor);
    int valid = GetIsObjectValid(actor) && GetArea(actor) == GetArea(pc)
        && VectorMagnitude(p - expected) < 0.05;
    WriteTimestampedLogEntry("VISUAL|note|" + name
        + "|valid=" + IntToString(valid)
        + "|pc=" + FloatToString(p.x,0,3) + "," + FloatToString(p.y,0,3)
        + "|actor=" + FloatToString(a.x,0,3) + "," + FloatToString(a.y,0,3)
        + "|distance=" + FloatToString(GetDistanceBetween(pc,actor),0,4)
        + "|exists=" + IntToString(GetIsObjectValid(actor))
        + "|seen=" + IntToString(GetObjectSeen(actor,pc))
        + "|heard=" + IntToString(GetObjectHeard(actor,pc))
        + "|los=" + IntToString(LineOfSightObject(pc,actor))
        + "|heartbeat=" + IntToString(GetLocalInt(actor,"cv_heartbeat"))
        + "|action=" + IntToString(GetCurrentAction(actor)));
    VShot(name);
}

// Six seconds permits at least one usual perception update before the settled frame.
// Early and settled images distinguish immediate presentation from delayed perception.
void CVTrial(object pc, object actor, float t, string name, vector p, float zoom)
{
    DelayCommand(t, CVKey("up"));
    DelayCommand(t, VMovePC(pc,p.x,p.y,p.z,90.0));
    DelayCommand(t + 0.2, VCamera(pc,90.0,zoom,5.0));
    DelayCommand(t + 1.0, CVReport(pc,actor,name + "_early",p));
    DelayCommand(t + 6.0, CVReport(pc,actor,name + "_settled",p));
    DelayCommand(t + 6.8, CVKey("down"));
    DelayCommand(t + 8.0, CVReport(pc,actor,name + "_tab",p));
}

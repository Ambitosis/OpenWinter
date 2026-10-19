// The action queue: order, ActionWait, ActionDoCommand, what GetCurrentAction reports, ClearAllActions,
// SetCommandable, and actions given to busy, dead and non-creature objects.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x, y, 0.0), 0.0); }

object Actor(string k, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "p_actor", L(x, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

// Logs a step: its name, OBJECT_SELF's current action and the time since the group started.
void Step(string k, string s)
{
    PLog(k + ".order", s + " action=" + IntToString(GetCurrentAction()));
    PLogF(k + "." + s + ".time", PSince() - GetLocalFloat(GetModule(), "T_" + k), 0.06);
}

void Start(string k)
{
    SetLocalFloat(GetModule(), "T_" + k, PSince());
}

void Action(object o, string k, string s)
{
    PLogI(k + "." + s, GetCurrentAction(o));
}

void Basic()
{
    object a = Actor("basic", 4.0, 4.0);
    Start("basic");
    AssignCommand(a, ActionDoCommand(Step("basic", "first")));
    AssignCommand(a, ActionWait(1.0));
    AssignCommand(a, ActionDoCommand(Step("basic", "afterwait")));
    AssignCommand(a, ActionMoveToLocation(L(6.0, 4.0), TRUE));
    AssignCommand(a, ActionDoCommand(Step("basic", "aftermove")));
    AssignCommand(a, ActionWait(0.0));
    AssignCommand(a, ActionDoCommand(Step("basic", "afterwait0")));
    AssignCommand(a, ActionWait(-1.0));
    AssignCommand(a, ActionDoCommand(Step("basic", "afterwaitneg")));
    PLog("basic.order", "script.end action=" + IntToString(GetCurrentAction(a)));
    DelayCommand(0.5, Action(a, "basic", "during.wait"));
    DelayCommand(1.3, Action(a, "basic", "during.move"));
    DelayCommand(3.0, Action(a, "basic", "idle"));
    // AssignCommand runs even while the creature is busy; it doesn't wait for the queue.
    DelayCommand(0.3, AssignCommand(a, Step("basic", "assign.while.busy")));
}

void Chain(string k, int n)
{
    if (n == 0) { Step(k, "end"); return; }
    ActionDoCommand(Chain(k, n - 1));
}

void Chains()
{
    // Ten instant actions in a row: one per frame, or all at once?
    object a = Actor("chain", 4.0, 7.0);
    Start("chain");
    int i;
    for (i = 0; i < 10; i++) AssignCommand(a, ActionDoCommand(PLog("chain.order", "n" + IntToString(i))));
    AssignCommand(a, ActionDoCommand(Step("chain", "ten")));
    // An action that queues the next action from inside itself.
    object b = Actor("nested", 4.0, 10.0);
    Start("nested");
    AssignCommand(b, Chain("nested", 10));
}

void Clear()
{
    // ClearAllActions stops a move where it is and drops the rest of the queue.
    object a = Actor("clear", 4.0, 13.0);
    AssignCommand(a, ActionMoveToLocation(L(30.0, 13.0), TRUE));
    AssignCommand(a, ActionDoCommand(PLog("clear.order", "should.not.run")));
    DelayCommand(1.0, AssignCommand(a, ClearAllActions()));
    DelayCommand(1.0, Action(a, "clear", "same.frame"));
    DelayCommand(1.1, Action(a, "clear", "after"));
    DelayCommand(1.5, PLogF("clear.x", GetPosition(a).x, 0.15));
    DelayCommand(2.5, PLogF("clear.x.later", GetPosition(a).x, 0.15));
    // Cleared from inside an action: the action itself and everything after it.
    object b = Actor("clearself", 4.0, 16.0);
    AssignCommand(b, ActionDoCommand(ClearAllActions()));
    AssignCommand(b, ActionDoCommand(PLog("clearself.order", "after.clear")));
    AssignCommand(b, ActionWait(1.0));
    DelayCommand(0.5, Action(b, "clearself", "later"));
}

void Commandable()
{
    object a = Actor("cmd", 4.0, 19.0);
    AssignCommand(a, ActionWait(1.0));
    AssignCommand(a, ActionDoCommand(PLog("cmd.order", "queued.before")));
    SetCommandable(FALSE, a);
    PLogI("cmd.get", GetCommandable(a));
    AssignCommand(a, ActionDoCommand(PLog("cmd.order", "queued.while.off")));
    AssignCommand(a, PLog("cmd.order", "assign.while.off"));
    DelayCommand(0.3, AssignCommand(a, ClearAllActions()));
    DelayCommand(0.4, Action(a, "cmd", "after.clear"));
    DelayCommand(1.5, SetCommandable(TRUE, a));
    DelayCommand(1.6, AssignCommand(a, ActionDoCommand(PLog("cmd.order", "queued.after.on"))));
    // Not commandable from inside the queue: the rest of the queue still runs.
    object b = Actor("cmd2", 4.0, 22.0);
    AssignCommand(b, ActionDoCommand(SetCommandable(FALSE)));
    AssignCommand(b, ActionWait(0.5));
    AssignCommand(b, ActionDoCommand(PLog("cmd2.order", "after.off")));
    AssignCommand(b, ActionDoCommand(SetCommandable(TRUE)));
    DelayCommand(0.2, AssignCommand(b, ActionDoCommand(PLog("cmd2.order", "added.while.off"))));
    DelayCommand(1.5, PLogI("cmd2.get", GetCommandable(b)));
}

void Dead()
{
    object a = Actor("dead", 4.0, 25.0);
    AssignCommand(a, ActionWait(3.0));
    AssignCommand(a, ActionDoCommand(PLog("dead.order", "queued.before.death")));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), a);
    DelayCommand(0.2, Action(a, "dead", "after.death"));
    DelayCommand(0.3, AssignCommand(a, ActionDoCommand(PLog("dead.order", "queued.after.death"))));
    DelayCommand(0.3, AssignCommand(a, PLog("dead.order", "assign.to.dead")));
    DelayCommand(0.3, AssignCommand(a, ActionMoveToLocation(L(10.0, 25.0))));
    DelayCommand(1.0, Action(a, "dead", "later"));
    DelayCommand(1.0, PLogF("dead.x", GetPosition(a).x, 0.01));
}

void Others()
{
    // Placeables, the area and the module have action queues too?
    object p = CreateObject(OBJECT_TYPE_PLACEABLE, "p_plc", L(4.0, 30.0), FALSE, "PLC");
    Start("plc");
    AssignCommand(p, ActionWait(0.5));
    AssignCommand(p, ActionDoCommand(Step("plc", "after.wait")));
    DelayCommand(0.2, Action(p, "plc", "during.wait"));
    Start("module");
    AssignCommand(GetModule(), ActionWait(0.5));
    AssignCommand(GetModule(), ActionDoCommand(Step("module", "after.wait")));
    DelayCommand(0.2, Action(GetModule(), "module", "during.wait"));
    Start("area");
    AssignCommand(Area(), ActionDoCommand(Step("area", "docommand")));
}

void main()
{
    PStart();
    DelayCommand(1.0, Basic());
    DelayCommand(1.0, Chains());
    DelayCommand(1.0, Clear());
    DelayCommand(1.0, Commandable());
    DelayCommand(1.0, Dead());
    DelayCommand(1.0, Others());
    DelayCommand(6.0, PDone());
}

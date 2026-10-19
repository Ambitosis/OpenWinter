// Doors and placeables: ActionOpenDoor / ActionCloseDoor (walking there, timing, events), locked doors
// (failing, keys, lockpicking), doors and chests opened by script, and SetLocked / GetLocked.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float f = 0.0) { return Location(Area(), Vector(x, y, 0.0), f); }

object Actor(string k, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "p_actor", L(x, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Start(object oDoor)
{
    SetLocalFloat(GetModule(), "T_" + GetTag(oDoor), PSince());
}

void Done(string k, object oDoor)
{
    PLog(k + ".done", "open=" + IntToString(GetIsOpen(oDoor)) + " locked=" + IntToString(GetLocked(oDoor)));
    PLogF(k + ".done.time", PSince() - GetLocalFloat(GetModule(), "T_" + GetTag(oDoor)), 0.12);
}

void Where(object o, object oTarget, string k)
{
    vector p = GetPosition(o) - GetPosition(oTarget);
    PLogF(k + ".dx", p.x, 0.05);
    PLogF(k + ".dy", p.y, 0.05);
    PLogF(k + ".facing", GetFacing(o), 5.0);
}

void State(object o, object oDoor, string k)
{
    PLog(k, "open=" + IntToString(GetIsOpen(oDoor)) + " locked=" + IntToString(GetLocked(oDoor)) + " action=" + IntToString(GetCurrentAction(o)));
    PLogF(k + ".dist", GetDistanceBetween(o, oDoor), 0.15);
}

void Open(string sDoor, float fFrom)
{
    object d = GetObjectByTag(sDoor);
    vector v = GetPosition(d);
    object o = Actor(sDoor + "_ACTOR", v.x, v.y - fFrom);
    Start(d);
    AssignCommand(o, ActionOpenDoor(d));
    AssignCommand(o, ActionDoCommand(Done(sDoor + ".actor", d)));
    DelayCommand(0.5, State(o, d, sDoor + ".at0.5"));
    DelayCommand(4.0, State(o, d, sDoor + ".at4"));
    DelayCommand(4.0, Where(o, d, sDoor + ".stand"));
}

void Close(object c, object closer)
{
    Start(c);
    AssignCommand(closer, ActionCloseDoor(c));
    AssignCommand(closer, ActionDoCommand(Done("D_CLOSE.actor", c)));
}

void Locks()
{
    object c = GetObjectByTag("D_CLOSE");
    // Lock an open door, then lock it again (no change), then unlock: events only on a change.
    SetLocked(c, TRUE);
    PLogI("D_CLOSE.locked.same.script", GetLocked(c));
    SetLocked(c, TRUE);
    DelayCommand(0.3, SetLocked(c, FALSE));
    DelayCommand(0.6, SetLocked(c, TRUE));
}

void Doors()
{
    PLogI("plain.initial.open", GetIsOpen(GetObjectByTag("D_PLAIN")));
    PLogI("locked.initial.locked", GetLocked(GetObjectByTag("D_LOCKED")));
    Open("D_PLAIN", 4.0);
    Open("D_LOCKED", 4.0);
    Open("D_EASY", 4.0);
    Open("D_KEY", 4.0);
    // A door opening itself: no creature involved.
    object s = GetObjectByTag("D_SELF");
    Start(s);
    AssignCommand(s, ActionOpenDoor(s));
    AssignCommand(s, ActionDoCommand(Done("D_SELF.self", s)));
    DelayCommand(0.5, State(s, s, "D_SELF.at0.5"));
    // Locked doors still open by script on themselves?
    object p = GetObjectByTag("D_PLOT");
    SetLocked(p, TRUE);
    PLogI("plot.setlocked.same.script", GetLocked(p));
    Start(p);
    AssignCommand(p, ActionOpenDoor(p));
    DelayCommand(0.5, State(p, p, "D_PLOT.at0.5"));
    // SetLocked / GetLocked on an open door, and from another object's script.
    object c = GetObjectByTag("D_CLOSE");
    Start(c);
    AssignCommand(c, ActionOpenDoor(c));
    DelayCommand(1.0, State(c, c, "D_CLOSE.opened"));
    DelayCommand(1.5, Locks());
    DelayCommand(2.4, State(c, c, "D_CLOSE.locked.while.open"));
    // (From the north: from the south the original paths around the open door's leaf, which OpenWinter's
    // walkmesh doesn't have yet.)
    object closer = Actor("D_CLOSE_ACTOR", 30.0, 30.0);
    DelayCommand(3.0, Close(c, closer));
    DelayCommand(7.0, State(closer, c, "D_CLOSE.closed"));
    DelayCommand(7.0, Where(closer, c, "D_CLOSE.stand"));
}

void Chair(float fFacing, float fRange, float y)
{
    string k = "CHAIR" + FloatToString(fFacing, 0, 0) + ".r" + FloatToString(fRange, 0, 0);
    object chair = CreateObject(OBJECT_TYPE_PLACEABLE, "p_chair", L(28.0, y, fFacing), FALSE, k);
    object w = Actor(k + "_ACTOR", 23.0, y);
    AssignCommand(w, ActionMoveToObject(chair, FALSE, fRange));
    DelayCommand(4.0, Where(w, chair, k + ".stand"));
}

void Chests()
{
    object ch = CreateObject(OBJECT_TYPE_PLACEABLE, "p_chest", L(10.0, 36.0), FALSE, "CHEST");
    object o = Actor("CHEST_ACTOR", 6.0, 36.0);
    Start(ch);
    AssignCommand(o, ActionOpenDoor(ch));
    AssignCommand(o, ActionDoCommand(Done("CHEST.actor", ch)));
    DelayCommand(3.0, State(o, ch, "CHEST.opened"));
    DelayCommand(3.0, Where(o, ch, "CHEST.stand"));
    DelayCommand(3.5, AssignCommand(o, ActionCloseDoor(ch)));
    DelayCommand(5.0, State(o, ch, "CHEST.closed"));
    object ch2 = CreateObject(OBJECT_TYPE_PLACEABLE, "p_chest", L(20.0, 36.0), FALSE, "CHEST2");
    object u = Actor("CHEST2_ACTOR", 16.0, 36.0);
    Start(ch2);
    AssignCommand(u, ActionInteractObject(ch2));
    AssignCommand(u, ActionDoCommand(Done("CHEST2.actor", ch2)));
    DelayCommand(3.0, State(u, ch2, "CHEST2.used"));
    DelayCommand(3.0, Where(u, ch2, "CHEST2.stand"));
    // A placed chest (the GIT bearing) and a chair, approached at their use points.
    object g = GetObjectByTag("C_GIT");
    object v = Actor("C_GIT_ACTOR", 36.0, 32.0);
    AssignCommand(v, ActionInteractObject(g));
    DelayCommand(3.0, Where(v, g, "C_GIT.stand"));
    Chair(45.0, 1.0, 2.0);
    Chair(90.0, 0.0, 6.0);
    object ch3 = CreateObject(OBJECT_TYPE_PLACEABLE, "p_chest", L(30.0, 36.0), FALSE, "CHEST3");
    Start(ch3);
    AssignCommand(ch3, ActionOpenDoor(ch3));
    DelayCommand(1.0, State(ch3, ch3, "CHEST3.self"));
}

void Facings()
{
    // Placed doors and placeables report their bearing plus 90 degrees; created ones the location's facing.
    PLogF("facing.D_PLAIN", GetFacing(GetObjectByTag("D_PLAIN")), 0.1);
    PLogF("facing.C_GIT", GetFacing(GetObjectByTag("C_GIT")), 0.1);
    object p = CreateObject(OBJECT_TYPE_PLACEABLE, "p_chair", L(2.0, 2.0, 30.0), FALSE, "FACING_CHAIR");
    PLogF("facing.created", GetFacing(p), 0.1);
    AssignCommand(p, SetFacing(200.0));
    DelayCommand(0.1, PLogF("facing.set", GetFacing(p), 0.1));
    PLogF("facing.location", GetFacingFromLocation(GetLocation(GetObjectByTag("D_PLAIN"))), 0.1);
}

void main()
{
    PStart();
    Facings();
    DelayCommand(1.0, Doors());
    DelayCommand(1.0, Chests());
    DelayCommand(12.0, PDone());
}

// Traps going off: creatures walking into trap triggers (event order with OnEnter, which factions set a
// trap off, inactive, recurring and detected traps, the square's shape) and trapped doors and chests
// being opened or used. Runs with Prelude's faction table (suite.json): without one every NPC faction likes every
// other on nwserver and no NPC sets off another's trap trigger or trapped chest.
#include "ptrap_inc"

object Actor(string sTag, float x, float y, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PTrapL(x, y), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

object Trap(string sTag, float x, float y, int nFaction, int nType = TRAP_BASE_TYPE_MINOR_SPIKE, string sTrig = "tp_trig", float fFacing = 0.0, float fSize = 2.0)
{
    object t = CreateTrapAtLocation(nType, PTrapL(x, y, fFacing), fSize, sTag, nFaction, "tp_disarm", sTrig);
    SetEventScript(t, EVENT_SCRIPT_TRIGGER_ON_OBJECT_ENTER, "tp_enter");
    SetEventScript(t, EVENT_SCRIPT_TRIGGER_ON_OBJECT_EXIT, "tp_exit");
    return t;
}

// A lane: the trap at x=20, the walker from x=10 to x=30, setting off fDelay seconds later (lanes are staggered so
// their events log in lane order).
void Lane(string k, float y, int nTrapFaction, int nWalker, float fDelay = 0.0)
{
    Trap("T_" + k, 20.0, y, nTrapFaction);
    object v = Actor("V_" + k, 10.0, y, nWalker);
    DelayCommand(fDelay, AssignCommand(v, ActionMoveToLocation(PTrapL(30.0, y))));
}

void Read(string k)
{
    object t = GetObjectByTag("T_" + k), v = GetObjectByTag("V_" + k);
    PLogI(k + ".trapvalid", GetIsObjectValid(t));
    PLogI(k + ".trapped", GetIsTrapped(t));
    vector p = GetPosition(v);
    PLogF(k + ".walker.x", p.x, 0.3);
}

void Start()
{
    // Prelude's faction table (suite.json): Hostile and the others rate each other 0; Commoner rates Commoner,
    // Merchant and Defender 50; Merchant rates Commoner 50 and Defender 100; Defender rates Commoner and Merchant 100.
    Lane("HOSTILE_COMMONER", 6.0, STANDARD_FACTION_HOSTILE, STANDARD_FACTION_COMMONER);
    Lane("HOSTILE_HOSTILE", 10.0, STANDARD_FACTION_HOSTILE, STANDARD_FACTION_HOSTILE, 0.25);
    Lane("COMMONER_COMMONER", 14.0, STANDARD_FACTION_COMMONER, STANDARD_FACTION_COMMONER, 0.5);
    Lane("COMMONER_HOSTILE", 18.0, STANDARD_FACTION_COMMONER, STANDARD_FACTION_HOSTILE, 0.75);
    Lane("MERCHANT_COMMONER", 22.0, STANDARD_FACTION_MERCHANT, STANDARD_FACTION_COMMONER, 1.0);
    Lane("DEFENDER_HOSTILE", 26.0, STANDARD_FACTION_DEFENDER, STANDARD_FACTION_HOSTILE, 1.25);
    // The trap's faction likes the walker (100), the walker's doesn't like it (50).
    Lane("DEFENDER_COMMONER", 30.0, STANDARD_FACTION_DEFENDER, STANDARD_FACTION_COMMONER, 1.5);
    // The other way round: the trap's faction rates the walker 50, the walker's rates it 100.
    Lane("COMMONER_DEFENDER", 70.0, STANDARD_FACTION_COMMONER, STANDARD_FACTION_DEFENDER, 1.75);
    // Both 100 (control).
    Lane("MERCHANT_DEFENDER", 74.0, STANDARD_FACTION_MERCHANT, STANDARD_FACTION_DEFENDER, 2.0);

    // Inactive.
    object t = Trap("T_INACTIVE", 20.0, 34.0, STANDARD_FACTION_HOSTILE);
    SetTrapActive(t, FALSE);
    AssignCommand(Actor("V_INACTIVE", 10.0, 34.0, STANDARD_FACTION_COMMONER), ActionMoveToLocation(PTrapL(30.0, 34.0)));
}

void Start2()
{
    // Recurring: out, back and out again.
    object t = Trap("T_RECUR", 20.0, 38.0, STANDARD_FACTION_HOSTILE);
    SetTrapOneShot(t, FALSE);
    object v = Actor("V_RECUR", 10.0, 38.0, STANDARD_FACTION_COMMONER);
    AssignCommand(v, ActionMoveToLocation(PTrapL(30.0, 38.0)));
    AssignCommand(v, ActionMoveToLocation(PTrapL(10.0, 38.0)));
    AssignCommand(v, ActionMoveToLocation(PTrapL(30.0, 38.0)));
}

void Start3()
{
    // The stock script of traps.2da when OnTrapTriggered is empty (minor spike: 2d6 piercing).
    Trap("T_STOCK", 20.0, 42.0, STANDARD_FACTION_HOSTILE, TRAP_BASE_TYPE_MINOR_SPIKE, "");
    object v = Actor("V_STOCK", 10.0, 42.0, STANDARD_FACTION_COMMONER);
    SetLocalInt(v, "hp", GetCurrentHitPoints(v));
    AssignCommand(v, ActionMoveToLocation(PTrapL(30.0, 42.0)));

    // The walkers below set off 0.25 s apart, so that their events log in order.
    // A walker that knows about the trap.
    object d = Trap("T_KNOWN", 20.0, 46.0, STANDARD_FACTION_HOSTILE);
    v = Actor("V_KNOWN", 10.0, 46.0, STANDARD_FACTION_COMMONER);
    SetTrapDetectedBy(d, v, TRUE);
    DelayCommand(0.25, AssignCommand(v, ActionMoveToLocation(PTrapL(30.0, 46.0))));

    // The square's shape: created facing 45 degrees, walked past 1.2 m off its centre (inside only if turned).
    Trap("T_TURNED", 20.0, 52.0, STANDARD_FACTION_HOSTILE, TRAP_BASE_TYPE_MINOR_SPIKE, "tp_trig", 45.0);
    v = Actor("V_TURNED", 10.0, 53.2, STANDARD_FACTION_COMMONER);
    DelayCommand(0.5, AssignCommand(v, ActionMoveToLocation(PTrapL(30.0, 53.2))));
    // Its edge: 0.9 m off the centre of a 2 m square (inside) and 1.15 m (outside).
    Trap("T_EDGE_IN", 20.0, 58.0, STANDARD_FACTION_HOSTILE);
    v = Actor("V_EDGE_IN", 10.0, 58.9, STANDARD_FACTION_COMMONER);
    DelayCommand(0.75, AssignCommand(v, ActionMoveToLocation(PTrapL(30.0, 58.9))));
    Trap("T_EDGE_OUT", 20.0, 64.0, STANDARD_FACTION_HOSTILE);
    v = Actor("V_EDGE_OUT", 10.0, 65.15, STANDARD_FACTION_COMMONER);
    DelayCommand(1.0, AssignCommand(v, ActionMoveToLocation(PTrapL(30.0, 65.15))));
}

void Objects()
{
    // A trapped chest used by a commoner and by a hostile creature.
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(50.0, 8.0), FALSE, "T_CHEST");
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    SetEventScript(c, EVENT_SCRIPT_PLACEABLE_ON_OPEN, "tp_open");
    SetEventScript(c, EVENT_SCRIPT_PLACEABLE_ON_USED, "tp_used");
    AssignCommand(Actor("V_CHEST", 50.0, 4.0, STANDARD_FACTION_COMMONER), ActionInteractObject(c));

    c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(60.0, 8.0), FALSE, "T_CHESTH");
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    SetEventScript(c, EVENT_SCRIPT_PLACEABLE_ON_OPEN, "tp_open");
    SetEventScript(c, EVENT_SCRIPT_PLACEABLE_ON_USED, "tp_used");
    AssignCommand(Actor("V_CHESTH", 60.0, 4.0, STANDARD_FACTION_HOSTILE), ActionInteractObject(c));

    // A trapped lever-like placeable used through DoPlaceableObjectAction.
    c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(70.0, 8.0), FALSE, "T_CHESTU");
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    SetEventScript(c, EVENT_SCRIPT_PLACEABLE_ON_OPEN, "tp_open");
    SetEventScript(c, EVENT_SCRIPT_PLACEABLE_ON_USED, "tp_used");
    AssignCommand(Actor("V_CHESTU", 70.0, 4.0, STANDARD_FACTION_COMMONER), DoPlaceableObjectAction(c, PLACEABLE_ACTION_USE));

    // Trapped doors (placed in the area): plain, locked, and one of the hostile faction opened by a hostile creature.
    AssignCommand(Actor("V_DOOR", 50.0, 26.0, STANDARD_FACTION_COMMONER), ActionOpenDoor(GetObjectByTag("T_DOOR")));
    AssignCommand(Actor("V_DOORL", 60.0, 26.0, STANDARD_FACTION_COMMONER), ActionOpenDoor(GetObjectByTag("T_DOORL")));
    AssignCommand(Actor("V_DOORH", 70.0, 26.0, STANDARD_FACTION_HOSTILE), ActionOpenDoor(GetObjectByTag("T_DOORH")));
    AssignCommand(Actor("V_DOORC", 50.0, 46.0, STANDARD_FACTION_COMMONER), ActionOpenDoor(GetObjectByTag("T_DOORC")));
}

void ReadObjects()
{
    PLogI("chest.open", GetIsOpen(GetObjectByTag("T_CHEST")));
    PLogI("chest.trapped", GetIsTrapped(GetObjectByTag("T_CHEST")));
    PLogI("chesth.open", GetIsOpen(GetObjectByTag("T_CHESTH")));
    PLogI("chesth.trapped", GetIsTrapped(GetObjectByTag("T_CHESTH")));
    PLogI("chestu.open", GetIsOpen(GetObjectByTag("T_CHESTU")));
    PLogI("chestu.trapped", GetIsTrapped(GetObjectByTag("T_CHESTU")));
    PLogI("door.open", GetIsOpen(GetObjectByTag("T_DOOR")));
    PLogI("door.trapped", GetIsTrapped(GetObjectByTag("T_DOOR")));
    PLogI("doorl.open", GetIsOpen(GetObjectByTag("T_DOORL")));
    PLogI("doorl.trapped", GetIsTrapped(GetObjectByTag("T_DOORL")));
    PLogI("doorh.open", GetIsOpen(GetObjectByTag("T_DOORH")));
    PLogI("doorh.trapped", GetIsTrapped(GetObjectByTag("T_DOORH")));
    PLogI("doorc.open", GetIsOpen(GetObjectByTag("T_DOORC")));
    PLogI("doorc.trapped", GetIsTrapped(GetObjectByTag("T_DOORC")));
    PTrapObjEvents("chest", "T_CHEST"); PTrapObjEvents("chesth", "T_CHESTH"); PTrapObjEvents("chestu", "T_CHESTU");
    PTrapObjEvents("door", "T_DOOR"); PTrapObjEvents("doorl", "T_DOORL"); PTrapObjEvents("doorh", "T_DOORH"); PTrapObjEvents("doorc", "T_DOORC");
    object m = GetModule();
    PLogI("objects.ev.count", GetLocalInt(m, "PTEV_N"));
    DeleteLocalInt(m, "PTEV_N");
}

void ReadAll()
{
    PTrapEvents("lanes");
    Read("HOSTILE_COMMONER"); Read("HOSTILE_HOSTILE"); Read("COMMONER_COMMONER"); Read("COMMONER_HOSTILE");
    Read("MERCHANT_COMMONER"); Read("DEFENDER_HOSTILE"); Read("DEFENDER_COMMONER"); Read("INACTIVE");
    Read("COMMONER_DEFENDER"); Read("MERCHANT_DEFENDER");
}

void ReadAll2()
{
    PTrapEvents("recur");
    object t = GetObjectByTag("T_RECUR");
    PLogI("RECUR.trapvalid", GetIsObjectValid(t));
    PLogI("RECUR.trapped", GetIsTrapped(t));
}

void ReadAll3()
{
    PTrapEvents("stock");
    Read("STOCK"); Read("KNOWN"); Read("TURNED"); Read("EDGE_IN"); Read("EDGE_OUT");
    object v = GetObjectByTag("V_STOCK");
    PLogI("stock.hurt", GetCurrentHitPoints(v) < GetLocalInt(v, "hp"));
}

void main()
{
    DelayCommand(1.0, Start());
    DelayCommand(14.0, ReadAll());
    DelayCommand(13.0, Start2());
    DelayCommand(30.0, ReadAll2());
    DelayCommand(31.0, Start3());
    DelayCommand(43.0, ReadAll3());
    DelayCommand(44.0, Objects());
    DelayCommand(56.0, ReadObjects());
    DelayCommand(57.0, PDone());
}

// A single-player save game made by the original, for OpenWinter's loader of the original's saves
// (docs/SAVEGAME.md): the scene changes locals, the player's gold, experience, items, effects and position, an
// NPC's hit points, effects and action queue, a door, a lock, a chest's contents, a store, a ground item, a
// waypoint, a trigger's transition, the journal, the clock and the module database, queues a DelayCommand,
// then autosaves (DoSinglePlayerAutoSave). The notes record what the save should hold.
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }

void Save()
{
    object pc = GetFirstPC();
    object npc = GetObjectByTag("NS_NPC");
    vector v = GetPosition(pc);
    Note("pc|" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "|" + FloatToString(GetFacing(pc), 0, 1)
        + "|hp=" + IntToString(GetCurrentHitPoints(pc)) + "/" + IntToString(GetMaxHitPoints(pc))
        + "|gold=" + IntToString(GetGold(pc)) + "|xp=" + IntToString(GetXP(pc)) + "|id=" + ObjectToString(pc));
    Note("npc|hp=" + IntToString(GetCurrentHitPoints(npc)) + "/" + IntToString(GetMaxHitPoints(npc))
        + "|action=" + IntToString(GetCurrentAction(npc)) + "|id=" + ObjectToString(npc));
    Note("time|" + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute()) + "|" + IntToString(GetCalendarYear())
        + "-" + IntToString(GetCalendarMonth()) + "-" + IntToString(GetCalendarDay()));
    Note("door|open=" + IntToString(GetIsOpen(GetObjectByTag("NS_DOOR"))) + "|locked2=" + IntToString(GetLocked(GetObjectByTag("NS_DOOR2"))));
    Note("quest|" + IntToString(GetLocalInt(pc, "NW_JOURNAL_ENTRYq_ns")));
    DoSinglePlayerAutoSave();
    Note("autosave|requested");
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    object m = GetModule();
    object a = GetArea(pc);
    SetPlotFlag(pc, TRUE);

    SetLocalInt(m, "ns_mod_i", 11);
    SetLocalString(m, "ns_mod_s", "module");
    SetLocalFloat(m, "ns_mod_f", 2.5);
    SetLocalLocation(m, "ns_mod_l", Location(a, Vector(5.0, 6.0, 0.0), 45.0));
    SetLocalObject(m, "ns_mod_o", pc);
    SetLocalJson(m, "ns_mod_j", JsonParse("{\"a\":[1,2]}"));
    SetLocalInt(a, "ns_area_i", 22);
    SetLocalInt(pc, "ns_pc_i", 33);
    SetLocalString(pc, "ns_pc_s", "hero");

    GiveGoldToCreature(pc, 1234);
    GiveXPToCreature(pc, 500);
    object ring = CreateItemOnObject("nw_it_mring001", pc, 1, "NS_RING");
    SetLocalInt(ring, "ns_item_i", 44);
    CreateItemOnObject("nw_it_mpotion001", pc, 3, "NS_POT");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(2), pc, 600.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 2), pc);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSkillIncrease(SKILL_HIDE, 5), pc, 300.0);

    object npc = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(a, Vector(24.0, 24.0, 0.0), 180.0), FALSE, "NS_NPC");
    ChangeToStandardFaction(npc, STANDARD_FACTION_COMMONER);
    SetLocalInt(npc, "ns_npc_i", 55);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(3), npc);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectHaste(), npc, 900.0);
    AssignCommand(npc, ActionWait(120.0));

    object chest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", Location(a, Vector(16.0, 24.0, 0.0), 0.0), FALSE, "NS_CHEST");
    CreateItemOnObject("nw_it_gem001", chest, 2);
    SetLocalInt(chest, "ns_plc_i", 66);
    object store = CreateObject(OBJECT_TYPE_STORE, "nw_storgenral001", Location(a, Vector(10.0, 10.0, 0.0), 0.0), FALSE, "NS_STORE");
    SetStoreGold(store, 4321);
    CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", Location(a, Vector(22.0, 18.0, 0.0), 0.0), FALSE, "NS_GROUND");
    object wp = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", Location(a, Vector(30.0, 30.0, 0.0), 0.0), FALSE, "NS_WP");
    SetTransitionTarget(GetObjectByTag("NS_TRIG"), wp);

    object door = GetObjectByTag("NS_DOOR");
    SetLocalInt(door, "ns_door_i", 77);
    AssignCommand(door, ActionOpenDoor(door));
    SetLocked(GetObjectByTag("NS_DOOR2"), TRUE);

    AddJournalQuestEntry("q_ns", 2, pc);
    SetTime(15, 30, 0, 0);
    sqlquery q = SqlPrepareQueryObject(m, "CREATE TABLE IF NOT EXISTS ns (k TEXT, v INT)");
    SqlStep(q);
    q = SqlPrepareQueryObject(m, "INSERT INTO ns VALUES ('saved', 88)");
    SqlStep(q);

    DelayCommand(90.0, SetLocalInt(GetModule(), "ns_delayed", 1));
    AssignCommand(pc, JumpToLocation(Location(a, Vector(18.0, 21.0, 0.0), 270.0)));

    DelayCommand(5.0, Save());
    DelayCommand(9.0, VShot("01_saved"));
    DelayCommand(11.0, VDone());
}

// NPCs finding traps of another faction (Prelude's faction table: Hostile traps, Commoner NPCs): one walking back and
// forth past a DC 1 trap, the summon of an NPC beside one, one standing beside a DC 0 chest trap (all with +50
// Search). Without a faction table every faction likes every other on nwserver and none of them found anything.
// Who else learns of a trap an NPC finds: its master 5.8 m from the trap's edge (MASTER), a master 15 m away
// (MASTER2), an NPC of the same faction 15 m away (FAR).
#include "ptrap_inc"
object Actor(string sTag, string sRes, location l)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, l, FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetAILevel(o, AI_LEVEL_NORMAL);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SEARCH, 50), o);
    return o;
}
void Summon(location l)
{
    ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("tq_rogue", VFX_NONE, 0.0, 0), l, 60.0);
}
void Read()
{
    object t = GetObjectByTag("T_WALK"), w = GetObjectByTag("A_WALK");
    PLogI("walk.seen", GetTrapDetectedBy(t, w));
    object m = GetObjectByTag("A_MASTER");
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, m);
    PLogI("summon.valid", GetIsObjectValid(s));
    object t2 = GetObjectByTag("T_SUM");
    PLogI("summon.seen", GetTrapDetectedBy(t2, s));
    PLogI("master.seen", GetTrapDetectedBy(t2, m));
    PLogI("far.seen", GetTrapDetectedBy(t2, GetObjectByTag("A_FAR")));
    object m2 = GetObjectByTag("A_MASTER2");
    object s2 = GetAssociate(ASSOCIATE_TYPE_SUMMONED, m2);
    object t3 = GetObjectByTag("T_SUM2");
    PLogI("summon2.valid", GetIsObjectValid(s2));
    PLogI("summon2.seen", GetTrapDetectedBy(t3, s2));
    PLogI("master2.seen", GetTrapDetectedBy(t3, m2));
    PLogI("near.seen", GetTrapDetectedBy(GetObjectByTag("T_NEAR"), GetObjectByTag("A_NEAR")));
}
void main()
{
    object t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(20.0, 20.0), 2.0, "T_WALK", STANDARD_FACTION_HOSTILE);
    SetTrapDetectDC(t, 1);
    object w = Actor("A_WALK", "tq_rogue", PTrapL(16.0, 23.0));
    int i;
    for (i = 0; i < 4; i++) { AssignCommand(w, ActionMoveToLocation(PTrapL(24.0, 23.0))); AssignCommand(w, ActionMoveToLocation(PTrapL(16.0, 23.0))); }
    object t2 = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(40.0, 20.0), 2.0, "T_SUM", STANDARD_FACTION_HOSTILE);
    SetTrapDetectDC(t2, 1);
    object m = Actor("A_MASTER", "tq_rogue", PTrapL(44.0, 26.0));
    AssignCommand(m, Summon(PTrapL(40.0, 22.0)));
    Actor("A_FAR", "tq_rogue", PTrapL(40.0, 35.0));
    object t3 = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(10.0, 50.0), 2.0, "T_SUM2", STANDARD_FACTION_HOSTILE);
    SetTrapDetectDC(t3, 1);
    object m2 = Actor("A_MASTER2", "tq_rogue", PTrapL(25.0, 55.0));
    AssignCommand(m2, Summon(PTrapL(10.0, 52.0)));
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(30.0, 40.0), FALSE, "T_NEAR");
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, STANDARD_FACTION_HOSTILE);
    SetTrapDetectDC(c, 0);
    Actor("A_NEAR", "tq_rogue", PTrapL(30.0, 41.0));
    DelayCommand(35.0, Read());
    DelayCommand(36.0, PDone());
}

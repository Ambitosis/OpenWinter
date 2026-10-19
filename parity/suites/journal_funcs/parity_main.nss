#include "parity_inc"
// journal_funcs: the journal functions without a player (docs/JOURNAL.md). module.jrl comes from suite.json:
// q_a (150 XP, entries 1, 2, 3 = end), Q_Mixed (25 XP, entries 10, 20 = end), q_noxp (no XP, entry 1).
// What GetJournalQuestExperience reads, and what AddJournalQuestEntry / RemoveJournalQuestEntry leave in the
// "NW_JOURNAL_ENTRY<tag>" local of creatures, placeables and the module.

int J(object o, string sTag) { return GetLocalInt(o, "NW_JOURNAL_ENTRY" + sTag); }

void Locals(string sKey, object o)
{
    PLog(sKey, IntToString(J(o, "q_a")) + "," + IntToString(J(o, "Q_A")) + "," + IntToString(J(o, "Q_Mixed")) + ","
        + IntToString(J(o, "q_mixed")) + "," + IntToString(J(o, "q_none")) + "," + IntToString(J(o, "q_noxp")));
}

void main()
{
    PLogI("xp.q_a", GetJournalQuestExperience("q_a"));
    PLogI("xp.Q_A", GetJournalQuestExperience("Q_A"));
    PLogI("xp.Q_Mixed", GetJournalQuestExperience("Q_Mixed"));
    PLogI("xp.q_mixed", GetJournalQuestExperience("q_mixed"));
    PLogI("xp.q_noxp", GetJournalQuestExperience("q_noxp"));
    PLogI("xp.none", GetJournalQuestExperience("q_none"));
    PLogI("xp.empty", GetJournalQuestExperience(""));

    object oNpc = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", PCentre());
    object oNpc2 = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", PCentre(2.0));
    object oPlc = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-2.0));
    object oMod = GetModule();

    AddJournalQuestEntry("q_a", 2, oNpc, FALSE);
    Locals("npc.add2", oNpc);
    AddJournalQuestEntry("q_a", 1, oNpc, FALSE);
    Locals("npc.add1", oNpc);
    AddJournalQuestEntry("q_a", 1, oNpc, FALSE, FALSE, TRUE);
    Locals("npc.add1override", oNpc);
    AddJournalQuestEntry("q_a", 9, oNpc, FALSE);
    Locals("npc.add9", oNpc);
    AddJournalQuestEntry("Q_A", 3, oNpc, FALSE);
    Locals("npc.addUpper3", oNpc);
    AddJournalQuestEntry("q_mixed", 10, oNpc, FALSE);
    Locals("npc.addLowerMixed", oNpc);
    AddJournalQuestEntry("q_none", 1, oNpc, FALSE);
    Locals("npc.addNone", oNpc);
    AddJournalQuestEntry("q_noxp", 1, oNpc);
    Locals("npc.addDefaultParty", oNpc);
    Locals("npc2.after", oNpc2);
    AddJournalQuestEntry("q_a", 2, oNpc2, FALSE, TRUE);
    Locals("npc2.allPlayers", oNpc2);

    AddJournalQuestEntry("q_a", 2, oPlc, FALSE);
    Locals("plc.add2", oPlc);
    AddJournalQuestEntry("q_a", 2, oMod, FALSE);
    Locals("mod.add2", oMod);

    SetLocalInt(oNpc2, "NW_JOURNAL_ENTRYq_a", 7);
    AddJournalQuestEntry("q_a", 3, oNpc2, FALSE);
    Locals("npc2.presetThenAdd3", oNpc2);

    RemoveJournalQuestEntry("q_a", oNpc, FALSE);
    Locals("npc.remove", oNpc);
    RemoveJournalQuestEntry("Q_MIXED", oNpc, FALSE);
    Locals("npc.removeUpperMixed", oNpc);
    PDone();
}

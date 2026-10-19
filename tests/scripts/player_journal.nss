// Player package: journal entries.
void main()
{
    object m = GetModule();
    object pc = GetLocalObject(m, "pc");
    string q = "Q_SS_STACYRATS";
    AddJournalQuestEntry(q, 1, pc);
    SetLocalInt(m, "j1", GetLocalInt(pc, "NW_JOURNAL_ENTRY" + q));
    AddJournalQuestEntry(q, 3, pc);
    SetLocalInt(m, "j3", GetLocalInt(pc, "NW_JOURNAL_ENTRY" + q));
    AddJournalQuestEntry(q, 2, pc);
    SetLocalInt(m, "j_nolower", GetLocalInt(pc, "NW_JOURNAL_ENTRY" + q));
    AddJournalQuestEntry(q, 2, pc, TRUE, FALSE, TRUE);
    SetLocalInt(m, "j_override", GetLocalInt(pc, "NW_JOURNAL_ENTRY" + q));
    AddJournalQuestEntry(q, 77, pc, TRUE, FALSE, TRUE);
    SetLocalInt(m, "j_badstate", GetLocalInt(pc, "NW_JOURNAL_ENTRY" + q));
    SetLocalInt(m, "xp", GetJournalQuestExperience(q));
    RemoveJournalQuestEntry(q, pc);
    SetLocalInt(m, "j_removed", GetLocalInt(pc, "NW_JOURNAL_ENTRY" + q));
}

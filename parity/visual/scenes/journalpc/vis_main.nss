#include "visual_inc"
// journalpc: what the original does when a player's journal changes (docs/JOURNAL.md). Each step logs the
// player's NW_JOURNAL_ENTRY local and experience before/after, chat.txt has the lines it printed, and shots
// show the screen 0.3 s and 2 s after some updates (the journal button, any floating text).
// 1-7: AddJournalQuestEntry / RemoveJournalQuestEntry by script (new, next, end, repeat, lower, unknown entry,
// unknown quest, override); 8-9: conversation lines that set a quest (start, end); 10: an update aimed at the
// henchman with bAllPartyMembers; 11: GetJournalQuestExperience; 12: ExportSingleCharacter (the .bic is read
// afterwards for journal fields).

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|jp|" + s); }

string State(object o)
{
    return "a=" + IntToString(GetLocalInt(o, "NW_JOURNAL_ENTRYq_a")) + "|b=" + IntToString(GetLocalInt(o, "NW_JOURNAL_ENTRYq_b"))
        + "|c=" + IntToString(GetLocalInt(o, "NW_JOURNAL_ENTRYq_c")) + "|d=" + IntToString(GetLocalInt(o, "NW_JOURNAL_ENTRYq_d"))
        + "|none=" + IntToString(GetLocalInt(o, "NW_JOURNAL_ENTRYq_none")) + "|xp=" + IntToString(GetXP(o));
}

void Mark(string sStep)
{
    object pc = GetFirstPC();
    SendMessageToPC(pc, "JP " + sStep);
    Log("before|" + sStep + "|" + State(pc));
}

void After(string sStep) { Log("after|" + sStep + "|" + State(GetFirstPC())); }

void Add(string sStep, string sTag, int nState, int bOverride, int bShots)
{
    object pc = GetFirstPC();
    Mark(sStep);
    AddJournalQuestEntry(sTag, nState, pc, TRUE, FALSE, bOverride);
    After(sStep);
    if (bShots)
    {
        DelayCommand(0.3, VShot(sStep + "_03"));
        DelayCommand(0.8, VShot(sStep + "_08"));
        DelayCommand(1.3, VShot(sStep + "_13"));
        DelayCommand(2.0, VShot(sStep + "_20"));
        DelayCommand(3.0, VShot(sStep + "_30"));
        DelayCommand(4.5, VShot(sStep + "_45"));
    }
}

void Remove(string sStep, string sTag)
{
    Mark(sStep);
    RemoveJournalQuestEntry(sTag, GetFirstPC());
    After(sStep);
}

void Talk(string sStep, string sDlg)
{
    object pc = GetFirstPC();
    Mark(sStep);
    object n = GetObjectByTag("JP_NPC");
    AssignCommand(n, ActionStartConversation(pc, sDlg, FALSE, FALSE));
    DelayCommand(1.0, After(sStep));
    DelayCommand(3.0, AssignCommand(pc, ClearAllActions(TRUE)));
    DelayCommand(3.5, After(sStep + "_ended"));
}

void Hench()
{
    object pc = GetFirstPC();
    object h = CreateObject(OBJECT_TYPE_CREATURE, "jp_hench", Location(GetArea(pc), GetPosition(pc) + Vector(2.0, 0.0, 0.0), 0.0), FALSE, "JP_HENCH");
    AddHenchman(pc, h);
    Mark("10_hench");
    AddJournalQuestEntry("q_d", 5, h, TRUE);
    After("10_hench");
    Log("hench|" + State(h));
}

void Final()
{
    object pc = GetFirstPC();
    Log("xpfn|a=" + IntToString(GetJournalQuestExperience("q_a")) + "|A=" + IntToString(GetJournalQuestExperience("Q_A"))
        + "|none=" + IntToString(GetJournalQuestExperience("q_none")));
    ExportSingleCharacter(pc);
    Log("exported|" + GetPCPlayerName(pc) + "|" + GetName(pc));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    SetPlotFlag(pc, TRUE);
    object n = CreateObject(OBJECT_TYPE_CREATURE, "nw_commale", Location(GetArea(pc), GetPosition(pc) + Vector(0.0, 2.0, 0.0), 270.0), FALSE, "JP_NPC");
    ChangeToStandardFaction(n, STANDARD_FACTION_MERCHANT);
    SetPlotFlag(n, TRUE);
    VCamera(pc, 90.0, 10.0, 55.0);
    DelayCommand(3.0, VShot("00_before"));
    DelayCommand(5.0, Add("01_new", "q_a", 1, FALSE, TRUE));
    DelayCommand(10.5, Add("02_next", "q_a", 2, FALSE, FALSE));
    DelayCommand(13.0, Add("03_repeat", "q_a", 2, FALSE, FALSE));
    DelayCommand(16.0, Add("04_lower", "q_a", 1, FALSE, FALSE));
    DelayCommand(19.0, Add("05_unknownentry", "q_a", 7, FALSE, FALSE));
    DelayCommand(22.0, Add("06_unknownquest", "q_none", 1, FALSE, FALSE));
    DelayCommand(25.0, Add("07_lowerOverride", "q_a", 1, TRUE, FALSE));
    DelayCommand(28.0, Add("08_end", "q_a", 3, FALSE, TRUE));
    DelayCommand(34.0, Remove("09_remove", "q_a"));
    DelayCommand(37.0, Add("09b_readd", "q_a", 2, FALSE, FALSE));
    DelayCommand(40.0, Talk("10_talkstart", "jp_b"));
    DelayCommand(46.0, Talk("11_talkend", "jp_c"));
    DelayCommand(52.0, Hench());
    DelayCommand(55.0, Final());
    DelayCommand(58.0, VShot("99_end"));
    DelayCommand(60.0, VDone());
}

#include "mp_inc"
// mp_journal: the journal (J) of a player on a server: which tabs it shows (the World page, jnl_page_world, is in
// the game's layouts). Run on the original: python tools/multiplayer/mp.py run mp_journal --only nwn --size 1280x720.

void In(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }

void Run()
{
    float t = 0.0;
    DelayCommand(t += 1.0, In("key|MPA|74"));
    DelayCommand(t += 2.0, MPShot("a_journal"));
    // The tabs along the top (quests, completed, notes, and a fourth place to the right of them).
    DelayCommand(t += 1.0, In("click|MPA|0.140,0.061"));
    DelayCommand(t += 2.0, MPShot("b_tab2"));
    DelayCommand(t += 1.0, In("click|MPA|0.234,0.061"));
    DelayCommand(t += 2.0, MPShot("c_tab3"));
    DelayCommand(t += 1.0, In("key|MPA|74"));
    DelayCommand(t += 2.0, MPShot("d_closed"));
    DelayCommand(t += 1.0, MPDone());
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    if (GetLocalInt(GetModule(), "jnl")) return;
    SetLocalInt(GetModule(), "jnl", 1);
    SetPlotFlag(pc, TRUE);
    AddJournalQuestEntry("q_a", 1, pc, FALSE);
    DelayCommand(1.0, MPCameras(90.0, 10.0, 55.0));
    DelayCommand(5.0, Run());
}

// Shared by sched_events: one running sequence per phase (module local "PH").
void SeqAdd(string s)
{
    object m = GetModule();
    string k = "SEQ_" + GetLocalString(m, "PH");
    SetLocalString(m, k, GetLocalString(m, k) + (GetLocalString(m, k) == "" ? "" : " ") + s);
}

// OnPerception of an observer (local OBS = its short name): appends "<observer>:<perceived><kind>" to the running
// sequence of its area's current phase (kind: h heard, s seen, i inaudible, v vanished), and the time to a raw log.
#include "parity_time"
void main()
{
    object me = OBJECT_SELF;
    string o = GetLocalString(me, "OBS");
    if (o == "") return;
    object m = GetModule();
    string k = "SEQ_" + GetTag(GetArea(me)) + "_" + GetLocalString(m, "PH_" + GetTag(GetArea(me)));
    string w = GetLocalString(GetLastPerceived(), "NM");
    string e = GetLastPerceptionHeard() ? "h" : GetLastPerceptionSeen() ? "s" : GetLastPerceptionInaudible() ? "i" : "v";
    string s = o + ":" + w + e;
    SetLocalString(m, k, GetLocalString(m, k) + (GetLocalString(m, k) == "" ? "" : " ") + s);
    string ph = GetTag(GetArea(me)) + "_" + GetLocalString(m, "PH_" + GetTag(GetArea(me)));
    SetLocalString(m, "OBS_" + ph + "_" + o, GetLocalString(m, "OBS_" + ph + "_" + o) + w + e + " ");
    // Observer switches in the running sequence: all of one observer's events together gives (observers - 1).
    if (GetLocalString(m, "LASTOBS_" + ph) != o && GetLocalString(m, "LASTOBS_" + ph) != "") SetLocalInt(m, "SW_" + ph, GetLocalInt(m, "SW_" + ph) + 1);
    SetLocalString(m, "LASTOBS_" + ph, o);
    SetLocalString(m, "RAW" + k, GetLocalString(m, "RAW" + k) + " " + s + "@" + FloatToString(PSince(), 0, 3));
}

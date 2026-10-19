// Trap suites: an event log on the module (each event script appends a line, PTrapEvents writes them
// out in order) and helpers to read every property of a trap.
#include "parity_inc"

object PTrapArea() { return GetObjectByTag("PARITY_AREA"); }
location PTrapL(float x, float y, float f = 0.0) { return Location(PTrapArea(), Vector(x, y, 0.0), f); }

void PTrapEv(string s)
{
    object m = GetModule();
    // Also per object (the text up to the second ':' is "<event>:<tag>"), for readings independent of other actors' timing.
    int a = FindSubString(s, ":");
    string sTag = GetSubString(s, a + 1, GetStringLength(s));
    int b = FindSubString(sTag, ":");
    if (b >= 0) sTag = GetStringLeft(sTag, b);
    string sOld = GetLocalString(m, "PTO_" + sTag);
    SetLocalString(m, "PTO_" + sTag, sOld + (sOld == "" ? "" : ",") + GetStringLeft(s, a));
    int n = GetLocalInt(m, "PTEV_N");
    SetLocalString(m, "PTEV_" + IntToString(n), s);
    SetLocalInt(m, "PTEV_N", n + 1);
}

// Writes the events logged so far (key.ev.N) and clears the log.
void PTrapEvents(string k)
{
    object m = GetModule();
    int n = GetLocalInt(m, "PTEV_N"), i;
    PLogI(k + ".ev.count", n);
    for (i = 0; i < n; i++) { PLog(k + ".ev." + IntToString(i), GetLocalString(m, "PTEV_" + IntToString(i))); DeleteLocalString(m, "PTEV_" + IntToString(i)); }
    DeleteLocalInt(m, "PTEV_N");
}

// The events one object had, in order ("trig,enter"), as key.events.
void PTrapObjEvents(string k, string sTag)
{
    PLog(k + ".events", GetLocalString(GetModule(), "PTO_" + sTag));
}

string PTrapTag(object o) { return GetIsObjectValid(o) ? GetTag(o) : "invalid"; }

void PTrapProps(string k, object t)
{
    PLogI(k + ".valid", GetIsObjectValid(t));
    PLogI(k + ".objtype", GetObjectType(t));
    PLogI(k + ".trapped", GetIsTrapped(t));
    PLogI(k + ".base", GetTrapBaseType(t));
    PLogI(k + ".active", GetTrapActive(t));
    PLogI(k + ".detectable", GetTrapDetectable(t));
    PLogI(k + ".disarmable", GetTrapDisarmable(t));
    PLogI(k + ".oneshot", GetTrapOneShot(t));
    PLogI(k + ".recoverable", GetTrapRecoverable(t));
    PLogI(k + ".flagged", GetTrapFlagged(t));
    PLogI(k + ".detectdc", GetTrapDetectDC(t));
    PLogI(k + ".disarmdc", GetTrapDisarmDC(t));
    PLog(k + ".keytag", GetTrapKeyTag(t));
    PLog(k + ".creator", PTrapTag(GetTrapCreator(t)));
}

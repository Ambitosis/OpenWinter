// NWNX_Events suites: handlers append what they see to a module log that the suite then writes out.
// EvTags sets which event data tags the generic handler (pev_log) reads: "TAG" logs the raw text, "o:TAG"
// logs the tag of the object it names (object ids differ between engines), "x:TAG" only whether the tag is set.
// EvSkip / EvResult make pev_log skip an event or set its result.
#include "parity_inc"
#include "nwnx_events"

void EvLog(string s)
{
    object oMod = GetModule();
    SetLocalString(oMod, "EVLOG", GetLocalString(oMod, "EVLOG") + s + ";");
}

// Writes the log under sKey and clears it.
void EvFlush(string sKey)
{
    object oMod = GetModule();
    PLog(sKey, GetLocalString(oMod, "EVLOG"));
    DeleteLocalString(oMod, "EVLOG");
}

string EvObj(object o)
{
    if (o == OBJECT_INVALID) return "INVALID";
    if (o == GetModule()) return "MOD";
    if (GetIsObjectValid(o) && o == GetArea(o)) return "AREA";
    string t = GetTag(o);
    return t == "" ? "untagged" : t;
}

void EvTags(string sEvent, string sTags) { SetLocalString(GetModule(), "EVTAGS:" + sEvent, sTags); }
void EvSkip(string sEvent, int bSkip = TRUE) { SetLocalInt(GetModule(), "EVSKIP:" + sEvent, bSkip); }
void EvResult(string sEvent, string sResult) { SetLocalString(GetModule(), "EVRESULT:" + sEvent, sResult); }

// Subscribes pev_log to sBase_BEFORE and sBase_AFTER with the given tags.
void EvWatch(string sBase, string sTags)
{
    NWNX_Events_SubscribeEvent(sBase + "_BEFORE", "pev_log");
    NWNX_Events_SubscribeEvent(sBase + "_AFTER", "pev_log");
    EvTags(sBase + "_BEFORE", sTags);
    EvTags(sBase + "_AFTER", sTags);
}

// The current event's data for a tag list (see above).
string EvData(string sTags)
{
    string s = "";
    while (sTags != "")
    {
        int p = FindSubString(sTags, ",");
        string t = p < 0 ? sTags : GetStringLeft(sTags, p);
        sTags = p < 0 ? "" : GetSubString(sTags, p + 1, 10000);
        string v;
        if (GetStringLeft(t, 2) == "o:")
        {
            t = GetSubString(t, 2, 1000);
            v = EvObj(StringToObject(NWNX_Events_GetEventData(t)));
        }
        else if (GetStringLeft(t, 2) == "x:")
        {
            t = GetSubString(t, 2, 1000);
            v = NWNX_Events_GetEventData(t) == "" ? "-" : "+";
        }
        else v = NWNX_Events_GetEventData(t);
        s += "," + t + "=" + v;
    }
    return s;
}

// A short name for an event: NWNX_ON_ITEM_EQUIP_BEFORE -> ITEM_EQUIP_BEFORE.
string EvShort(string sEvent)
{
    return GetStringLeft(sEvent, 8) == "NWNX_ON_" ? GetSubString(sEvent, 8, 1000) : sEvent;
}

// ai_queries helpers: a probe run as a creature (ExecuteScript) that reads the targets and event state only the
// creature itself can read. Each field keeps its own sequence of values with the time each began, so the
// order in which different fields change within a tenth of a second does not matter.
#include "parity_inc"
#include "parity_time"

string AqName(object o)
{
    if (!GetIsObjectValid(o)) return "-";
    string s = GetLocalString(o, "AQ_NAME");
    return s == "" ? "?" : s;
}

void AqField(object o, string sField, string sValue)
{
    string k = "AQF_" + sField;
    int n = GetLocalInt(o, k + "_N");
    if (n > 0 && GetLocalString(o, k + "_V" + IntToString(n - 1)) == sValue) return;
    if (n >= 16) return;
    SetLocalInt(o, k + "_N", n + 1);
    SetLocalString(o, k + "_V" + IntToString(n), sValue);
    SetLocalFloat(o, k + "_T" + IntToString(n), PSince());
}

void AqProbe()
{
    object o = OBJECT_SELF;
    int a = GetCurrentAction(o);
    AqField(o, "act", a == ACTION_INVALID ? "none" : IntToString(a));
    AqField(o, "at", AqName(GetAttackTarget(o)));
    AqField(o, "aat", AqName(GetAttemptedAttackTarget()));
    AqField(o, "ast", AqName(GetAttemptedSpellTarget()));
    AqField(o, "cb", IntToString(GetIsInCombat(o)));
    AqField(o, "la", AqName(GetLastAttacker(o)));
    AqField(o, "lh", AqName(GetLastHostileActor(o)));
    AqField(o, "lsc", AqName(GetLastSpellCaster()));
    AqField(o, "ls", IntToString(GetLastSpell()));
}

// Logs one creature's fields: "<prefix>.<field>" = the values in order, "<prefix>.<field>.<i>" = when each began.
// A field written "f:n" keeps its first n values only; fTol is the time tolerance.
void AqReport(object o, string sPrefix, string sFields, float fTol = 0.45)
{
    while (sFields != "")
    {
        int p = FindSubString(sFields, " ");
        string f = GetStringLeft(sFields, p);
        sFields = GetSubString(sFields, p + 1, 1000);
        int nMax = 99;
        int c = FindSubString(f, ":");
        if (c >= 0) { nMax = StringToInt(GetSubString(f, c + 1, 10)); f = GetStringLeft(f, c); }
        string k = "AQF_" + f;
        int n = GetLocalInt(o, k + "_N");
        if (n > nMax) n = nMax;
        string s = "";
        int i;
        for (i = 0; i < n; i++) s += (i == 0 ? "" : ",") + GetLocalString(o, k + "_V" + IntToString(i));
        PLog(sPrefix + "." + f, s);
        for (i = 1; i < n; i++) PLogF(sPrefix + "." + f + "." + IntToString(i), GetLocalFloat(o, k + "_T" + IntToString(i)), fTol);
    }
}

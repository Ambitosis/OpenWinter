// Parity lab reporting: every measurement is one log line "PARITY|key|value[|~tolerance]",
// written by both the original game (nwserver) and OpenWinter, then compared by key.

void PLog(string sKey, string sValue)
{
    WriteTimestampedLogEntry("PARITY|" + sKey + "|" + sValue);
}

void PLogI(string sKey, int nValue)
{
    PLog(sKey, IntToString(nValue));
}

// A float with three decimals; fTolerance > 0 lets the engines differ by that much.
void PLogF(string sKey, float fValue, float fTolerance = 0.0)
{
    string s = FloatToString(fValue, 0, 3);
    if (fTolerance > 0.0) s += "|~" + FloatToString(fTolerance, 0, 3);
    WriteTimestampedLogEntry("PARITY|" + sKey + "|" + s);
}

// Marks the end of the run; the harness stops both engines when it sees this.
void PDone()
{
    WriteTimestampedLogEntry("PARITY|__done__|1");
}

// The centre of the test area, where suites put things.
location PCentre(float fDx = 0.0, float fDy = 0.0)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    return Location(oArea, Vector(20.0 + fDx, 20.0 + fDy, 0.0), 0.0);
}

// A sequence "value@time value@time ...": logs the values in order (exact) as sKey and each time as sKey.<i> with a
// tolerance, so the engines may differ by a tick or two in when things happen but not in what happens.
void PLogSeq(string sKey, string sSeq, float fTolerance)
{
    string sValues = "";
    int i = 0;
    while (sSeq != "")
    {
        int p = FindSubString(sSeq, " ");
        string sTok = p < 0 ? sSeq : GetStringLeft(sSeq, p);
        sSeq = p < 0 ? "" : GetSubString(sSeq, p + 1, 100000);
        if (sTok == "") continue;
        int a = FindSubString(sTok, "@");
        string v = a < 0 ? sTok : GetStringLeft(sTok, a);
        sValues += (i == 0 ? "" : " ") + v;
        if (a >= 0) PLogF(sKey + "." + IntToString(i), StringToFloat(GetSubString(sTok, a + 1, 100)), fTolerance);
        i++;
    }
    PLog(sKey, sValues);
}

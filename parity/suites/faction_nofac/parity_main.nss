// A module without repute.fac (no "factions" in suite.json): how the standard factions regard each other.
// One creature per standard faction set with ChangeToStandardFaction, and blueprints left in their own faction
// (nw_bandit001, nw_commoner? via FactionID). Reputations, faction equality, standard faction reputations, and what
// SetStandardFactionReputation and AdjustReputation then change.
#include "pfaction_inc"

string N(int f)
{
    switch (f)
    {
        case STANDARD_FACTION_HOSTILE: return "H";
        case STANDARD_FACTION_COMMONER: return "C";
        case STANDARD_FACTION_MERCHANT: return "M";
        case STANDARD_FACTION_DEFENDER: return "D";
    }
    return "?";
}

object Spawn(string r, float x, float y, int nFaction = -1)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, r, PCentre(x, y));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    if (nFaction >= 0) ChangeToStandardFaction(o, nFaction);
    return o;
}

object Of(string s) { return GetLocalObject(GetModule(), s); }

void Table(string k, string sSet)
{
    int a, b;
    for (a = 0; a < 4; a++)
    {
        object oa = Of(sSet + N(a));
        string s = "";
        for (b = 0; b < 4; b++)
        {
            object ob = Of(sSet + N(b));
            s += N(b) + "=" + IntToString(GetReputation(oa, ob)) + "/" + IntToString(GetFactionEqual(oa, ob)) + " ";
        }
        PLog(k + "." + N(a), s);
        string t = "";
        for (b = 0; b < 4; b++) t += N(b) + "=" + IntToString(GetStandardFactionReputation(b, oa)) + " ";
        PLog(k + ".std." + N(a), t);
    }
}

void Blueprints(string k)
{
    string r1 = "nw_bandit001", r2 = "nw_commale", r3 = "nw_shopkeep", r4 = "nw_guard", r5 = "nw_hen_bod_05";
    object o1 = Of("bp1"), o2 = Of("bp2"), o3 = Of("bp3"), o4 = Of("bp4"), o5 = Of("bp5");
    PLog(k + ".bandit", IntToString(GetReputation(o1, o2)) + " " + IntToString(GetReputation(o1, o3)) + " " + IntToString(GetReputation(o1, o4)) + " " + IntToString(GetReputation(o1, o5)));
    PLog(k + ".commale", IntToString(GetReputation(o2, o1)) + " " + IntToString(GetReputation(o2, o3)) + " " + IntToString(GetReputation(o2, o4)) + " " + IntToString(GetReputation(o2, o5)));
    PLog(k + ".guard", IntToString(GetReputation(o4, o1)) + " " + IntToString(GetReputation(o4, o2)) + " " + IntToString(GetReputation(o4, o3)) + " " + IntToString(GetReputation(o4, o5)));
    PLog(k + ".equal", IntToString(GetFactionEqual(o1, o2)) + IntToString(GetFactionEqual(o1, o3)) + IntToString(GetFactionEqual(o1, o4))
        + IntToString(GetFactionEqual(o2, o3)) + IntToString(GetFactionEqual(o2, o4)) + IntToString(GetFactionEqual(o3, o4)) + IntToString(GetFactionEqual(o4, o5)));
    PLog(k + ".enemy", IntToString(GetIsEnemy(o2, o1)) + IntToString(GetIsEnemy(o4, o1)) + IntToString(GetIsEnemy(o1, o4)) + IntToString(GetIsFriend(o4, o2)));
    PLog(k + ".std.bandit", IntToString(GetStandardFactionReputation(STANDARD_FACTION_HOSTILE, o1)) + " " + IntToString(GetStandardFactionReputation(STANDARD_FACTION_DEFENDER, o1)));
    PLog(k + ".std.guard", IntToString(GetStandardFactionReputation(STANDARD_FACTION_HOSTILE, o4)) + " " + IntToString(GetStandardFactionReputation(STANDARD_FACTION_COMMONER, o4)));
}

void Members(string k)
{
    int a;
    for (a = 0; a < 4; a++)
    {
        int n = 0;
        object m = GetFirstFactionMember(Of("s" + N(a)), FALSE);
        while (GetIsObjectValid(m)) { n++; m = GetNextFactionMember(Of("s" + N(a)), FALSE); }
        PLogI(k + ".members." + N(a), n);
    }
}

// Blueprints in FactionID 0..6 (fa_f0..fa_f6): which standard-faction creature each shares a faction with, and
// how the standard factions regard it.
void FactionIds(string k)
{
    int i, a;
    for (i = 0; i < 7; i++)
    {
        object o = Of("f" + IntToString(i));
        if (!GetIsObjectValid(o)) { PLog(k + ".f" + IntToString(i), "invalid"); continue; }
        string s = "";
        for (a = 0; a < 4; a++) s += N(a) + "=" + IntToString(GetFactionEqual(o, Of("s" + N(a)))) + "/" + IntToString(GetStandardFactionReputation(a, o))
            + "/" + IntToString(GetReputation(Of("s" + N(a)), o)) + "/" + IntToString(GetReputation(o, Of("s" + N(a)))) + " ";
        s += "bod=" + IntToString(GetFactionEqual(o, Of("bp5")));
        PLog(k + ".f" + IntToString(i), s);
    }
}

void main()
{
    int a;
    object m = GetModule();
    for (a = 0; a < 4; a++)
    {
        SetLocalObject(m, "s" + N(a), Spawn("nw_bandit001", IntToFloat(a) * 3.0 - 6.0, -6.0, a));
        SetLocalObject(m, "t" + N(a), Spawn("nw_bandit001", IntToFloat(a) * 3.0 - 6.0, 6.0, a));
    }
    SetLocalObject(m, "bp1", Spawn("nw_bandit001", -6.0, 0.0));
    SetLocalObject(m, "bp2", Spawn("nw_commale", -3.0, 0.0));
    SetLocalObject(m, "bp3", Spawn("nw_shopkeep", 0.0, 0.0));
    SetLocalObject(m, "bp4", Spawn("nw_guard", 3.0, 0.0));
    SetLocalObject(m, "bp5", Spawn("nw_hen_bod_05", 6.0, 0.0));
    for (a = 0; a < 7; a++) SetLocalObject(m, "f" + IntToString(a), Spawn("fa_f" + IntToString(a), IntToFloat(a) * 3.0 - 9.0, 12.0));
    DelayCommand(0.5, FactionIds("start"));
    DelayCommand(1.5, FactionIds("setDH"));
    DelayCommand(0.5, Table("start", "s"));
    DelayCommand(0.5, Blueprints("bp"));
    DelayCommand(0.5, Members("start"));
    // Hostile made to hate Defender (as it would in the toolset's table), then Commoner made to hate Merchant.
    DelayCommand(1.0, SetStandardFactionReputation(STANDARD_FACTION_DEFENDER, 0, Of("sH")));
    DelayCommand(1.5, Table("setDH", "s"));
    DelayCommand(1.5, Table("setDH.t", "t"));
    DelayCommand(1.5, Blueprints("setDH.bp"));
    DelayCommand(2.0, AdjustReputation(Of("sM"), Of("sC"), -100));
    DelayCommand(2.5, Table("adjCM", "s"));
    DelayCommand(2.5, Blueprints("adjCM.bp"));
    DelayCommand(3.0, FrRun());
    DelayCommand(6.0, PDone());
}

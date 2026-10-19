// How GetCreatureTalentBest breaks ties and applies nCRMax, and how GetCreatureTalentRandom weights its talents,
// from many draws: the set of talents each query gives (exact) and how often each comes up (a rate with a tolerance).
// Fixture creatures (tc_/tw_/ts_/tf_) hold exactly the spells or feats under test; stock blueprints the rest.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }

object Make(string sRes, string sTag, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(Area(), Vector(x, y, 0.0), 0.0), FALSE, sTag);
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    AssignCommand(o, ClearAllActions(TRUE));
    return o;
}

int Code(talent t) { return GetIsTalentValid(t) ? GetTypeFromTalent(t) * 10000 + GetIdFromTalent(t) : -1; }

string AddSorted(string sList, int n)
{
    string sOut = "";
    int bDone = FALSE;
    while (sList != "")
    {
        int p = FindSubString(sList, ",");
        string sTok = p < 0 ? sList : GetStringLeft(sList, p);
        sList = p < 0 ? "" : GetSubString(sList, p + 1, 10000);
        int v = StringToInt(sTok);
        if (v == n) bDone = TRUE;
        if (!bDone && v > n) { sOut += (sOut == "" ? "" : ",") + IntToString(n); bDone = TRUE; }
        sOut += (sOut == "" ? "" : ",") + sTok;
    }
    if (!bDone) sOut += (sOut == "" ? "" : ",") + IntToString(n);
    return sOut;
}

void PLogRate(string sKey, int nNum, int nDen)
{
    float p = IntToFloat(nNum) / IntToFloat(nDen);
    float q = p < 0.02 ? 0.02 : (p > 0.98 ? 0.98 : p);
    float tol = 3.5 * sqrt(2.0 * q * (1.0 - q) / IntToFloat(nDen));
    if (tol < 0.02) tol = 0.02;
    PLogF(sKey, p, tol);
}

// nMode 0: best at nCR, 1: random. Logs the set of talents and, with bRates, each one's share of the n draws.
void Dist(string sTag, int nCat, int nCR, int nMode, int n, string sKey, int bRates = TRUE)
{
    object o = GetObjectByTag(sTag);
    object m = GetModule();
    string sList = "";
    int i;
    for (i = 0; i < n; i++)
    {
        int c = Code(nMode == 0 ? GetCreatureTalentBest(nCat, nCR, o) : GetCreatureTalentRandom(nCat, o));
        string k = "C_" + sKey + "_" + IntToString(c);
        if (GetLocalInt(m, k) == 0) sList = AddSorted(sList, c);
        SetLocalInt(m, k, GetLocalInt(m, k) + 1);
    }
    PLog(sKey + ".set", sList);
    if (!bRates) return;
    while (sList != "")
    {
        int p = FindSubString(sList, ",");
        string sTok = p < 0 ? sList : GetStringLeft(sList, p);
        sList = p < 0 ? "" : GetSubString(sList, p + 1, 10000);
        PLogRate(sKey + "." + sTok, GetLocalInt(m, "C_" + sKey + "_" + sTok), n);
    }
}

void Setup()
{
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    Make("nw_bandit004", "clr", 3.0, 5.0);
    Make("nw_goblinboss", "sorc", 6.0, 5.0);
    Make("nw_dwarfmerc005", "ftr", 9.0, 5.0);
    Make("nw_bandit001", "pot", 12.0, 5.0);
    Make("nw_bandit001", "pot2", 15.0, 5.0);
    Make("nw_drgred001", "drag", 30.0, 30.0);
    Make("nw_bandit005", "wiz", 18.0, 5.0);
    string s = "tf_28 tf_23 tf_2819 tf_23289 tf_92328 tf_mix tf_none tf_death tc_a tc_b tc_c tc_d tc_e tc_f tc_g tc_h "
             + "tw_a tw_b tw_c tw_d tw_e tw_f tw_g tw_h tw_i tw_j tw_k tw_l tw_m tw_n tw_o tw_p ts_a ts_b ts_c ";
    float x = 2.0, y = 10.0;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string t = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 1000);
        Make(t, t, x, y);
        x += 2.0;
        if (x > 36.0) { x = 2.0; y += 4.0; }
    }
}

void Items()
{
    object o = GetObjectByTag("pot");
    CreateItemOnObject("nw_it_mpotion003", o);   // Cure Critical Wounds
    CreateItemOnObject("nw_it_mpotion001", o);   // Cure Light Wounds
    CreateItemOnObject("nw_it_mpotion020", o);   // Cure Moderate Wounds
    object o2 = GetObjectByTag("pot2");
    CreateItemOnObject("nw_it_mpotion020", o2);
    CreateItemOnObject("nw_it_mpotion001", o2, 4);
}

// Stock creatures: feats ignore nCRMax and come up evenly; memorised copies weight Random; -1 is Random.
void P1()
{
    Dist("ftr", 22, 20, 0, 300, "ftr.best20");
    Dist("ftr", 22, 0, 0, 300, "ftr.best0");
    Dist("ftr", 22, -5, 0, 150, "ftr.bestm5");
    Dist("ftr", 22, 20, 1, 300, "ftr.rand");
}
void P2()
{
    Dist("clr", 5, 20, 1, 300, "clr5.rand");
    Dist("clr", 9, 20, 1, 300, "clr9.rand");
    Dist("clr", 9, -1, 0, 200, "clr9.bestm1");
    Dist("clr", 9, -2, 0, 50, "clr9.bestm2");
    Dist("clr", 5, -1, 0, 200, "clr5.bestm1");
}
void P3()
{
    Dist("sorc", 13, 1, 0, 200, "sorc13.best1");
    Dist("sorc", 13, 20, 0, 100, "sorc13.best20");
    Dist("sorc", 8, 20, 0, 200, "sorc8.best20");
    Dist("sorc", 13, -1, 0, 240, "sorc13.bestm1");
}
// Potions: the highest-rated one whatever nCRMax, one talent per item (a stack counts once).
void P4()
{
    Dist("pot", 17, 0, 0, 60, "pot.best0");
    Dist("pot", 17, 20, 0, 60, "pot.best20");
    Dist("pot", 17, -3, 0, 60, "pot.bestm3");
    Dist("pot", 17, -1, 0, 200, "pot.bestm1");
    Dist("pot", 17, 20, 1, 240, "pot.rand");
    Dist("pot2", 17, 20, 0, 60, "pot2.best20");
    Dist("pot2", 17, 20, 1, 240, "pot2.rand");
}
// Special abilities: each copy is a talent; Random weights by copies.
void P5()
{
    Dist("drag", 2, 20, 1, 300, "drag2.rand");
    Dist("drag", 2, 20, 0, 60, "drag2.best20");
    Dist("drag", 22, 20, 0, 200, "drag22.best20");
    Dist("drag", 22, 0, 0, 200, "drag22.best0");
    Dist("wiz", 1, 20, 1, 240, "wiz1.rand");
    Dist("wiz", 2, 20, 1, 240, "wiz2.rand");
}
// Feat fixtures: every combat feat ties; category 0 holds the uncategorised skills and feats.
void P6(string s)
{
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string t = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 1000);
        Dist(t, 22, 20, 0, 150, t + ".best20");
        Dist(t, 22, 0, 0, 150, t + ".best0");
        Dist(t, 22, 20, 1, 150, t + ".rand");
        Dist(t, 0, 20, 1, 400, t + ".cat0", FALSE);
    }
}
void P7()
{
    Dist("tf_death", 1, 20, 1, 60, "tf_death.1", FALSE);
    Dist("tf_death", 11, 20, 1, 60, "tf_death.11", FALSE);
    Dist("tf_death", 1, 0, 0, 60, "tf_death.1.best0", FALSE);
}
// Spell fixtures: order decides plain ties (the later slot), a sub-radial master turns the ties after it random.
void Probe(string t, int nCat)
{
    Dist(t, nCat, 0, 0, 60, t + "." + IntToString(nCat) + ".best0");
    Dist(t, nCat, 1, 0, 60, t + "." + IntToString(nCat) + ".best1");
    Dist(t, nCat, 3, 0, 60, t + "." + IntToString(nCat) + ".best3");
    Dist(t, nCat, 5, 0, 60, t + "." + IntToString(nCat) + ".best5");
    Dist(t, nCat, 20, 0, 90, t + "." + IntToString(nCat) + ".best20");
    Dist(t, nCat, 20, 1, 120, t + "." + IntToString(nCat) + ".rand");
}
void P8() { Probe("tw_a", 2); Probe("tw_b", 2); Probe("tw_c", 2); Probe("tw_n", 2); Probe("tw_l", 2); Probe("tw_m", 2); }
void P9() { Probe("tw_d", 13); Probe("tw_e", 13); Probe("tw_f", 13); Probe("tw_g", 13); }
void P10() { Probe("tw_h", 9); Probe("tw_i", 9); Probe("tw_j", 9); Probe("tw_k", 9); Probe("tc_a", 9); Probe("tc_b", 9); Probe("tc_c", 9); Probe("tc_d", 9); }
void P11() { Probe("tc_e", 9); Probe("tc_f", 9); Probe("tc_g", 9); Probe("tc_h", 9); Probe("ts_a", 13); Probe("ts_b", 13); }
void P12() { Probe("tw_o", 1); Probe("tw_o", 11); Probe("tw_p", 1); Probe("ts_c", 2); Probe("ts_c", 1); Probe("ts_c", 11); }

// GetHasSpell counts a special ability once however many copies; DecrementRemainingSpellUses spends one copy.
void P13()
{
    object d = GetObjectByTag("drag");
    PLogI("drag.has107", GetHasSpell(107, d));
    DecrementRemainingSpellUses(d, 107);
    PLogI("drag.has107.dec1", GetHasSpell(107, d));
    DecrementRemainingSpellUses(d, 107);
    PLogI("drag.has107.dec2", GetHasSpell(107, d));
    DecrementRemainingSpellUses(d, 107);
    PLogI("drag.has107.dec3", GetHasSpell(107, d));
    PLogI("drag.talent107.dec3", GetCreatureHasTalent(TalentSpell(107), d));
    PLogI("drag.has239", GetHasSpell(239, d));
    PLogI("drag.has41", GetHasSpell(41, d));
    object w = GetObjectByTag("wiz");
    PLogI("wiz.has10", GetHasSpell(10, w));
    PLogI("wiz.talent10", GetCreatureHasTalent(TalentSpell(10), w));
    DecrementRemainingSpellUses(w, 10);
    PLogI("wiz.has10.dec", GetHasSpell(10, w));
    object t = GetObjectByTag("tw_d");
    PLog("tw_d.has", IntToString(GetCreatureHasTalent(TalentSpell(321), t)) + IntToString(GetCreatureHasTalent(TalentSpell(138), t))
        + IntToString(GetCreatureHasTalent(TalentSpell(102), t)) + " " + IntToString(GetHasSpell(321, t)) + IntToString(GetHasSpell(138, t)));
    object l = GetObjectByTag("tw_l");
    PLog("tw_l.has", IntToString(GetCreatureHasTalent(TalentSpell(107), l)) + " " + IntToString(GetHasSpell(107, l)));
}

void main()
{
    DelayCommand(1.0, Setup());
    DelayCommand(2.0, Items());
    DelayCommand(4.0, P1());
    DelayCommand(4.3, P2());
    DelayCommand(4.6, P3());
    DelayCommand(4.9, P4());
    DelayCommand(5.2, P5());
    DelayCommand(5.5, P6("tf_28 tf_23 tf_2819 "));
    DelayCommand(5.8, P6("tf_23289 tf_92328 "));
    DelayCommand(6.1, P6("tf_mix tf_none "));
    DelayCommand(6.4, P7());
    DelayCommand(6.7, P8());
    DelayCommand(7.0, P9());
    DelayCommand(7.3, P10());
    DelayCommand(7.6, P11());
    DelayCommand(7.9, P12());
    DelayCommand(9.0, P13());
    DelayCommand(10.0, PDone());
}

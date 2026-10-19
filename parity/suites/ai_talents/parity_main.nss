// The talent functions the stock AI (x0_i0_talent, nw_i0_generic) picks its actions with:
// GetCreatureTalentBest by category and nCRMax, GetCreatureTalentRandom (which talents can come up),
// GetCreatureHasTalent, the talent constructors, and how spells, special abilities, feats, skills and
// items (potions, kits, wands, scrolls) enter the categories. Stock blueprints with their event scripts removed.
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

string TS(talent t)
{
    if (!GetIsTalentValid(t)) return "-";
    return IntToString(GetTypeFromTalent(t)) + ":" + IntToString(GetIdFromTalent(t));
}

// Inserts n into a sorted, comma-separated list of distinct numbers.
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

int Code(talent t) { return GetIsTalentValid(t) ? GetTypeFromTalent(t) * 10000 + GetIdFromTalent(t) : -1; }

// Best by nCRMax for one category: one line, the talents 24 calls give for nCRMax 0..9, 20 and 100 in order
// (several when the original picks at random among ties). nCRMax -1 is in RandomLine.
void BestLine(object o, string sTag, int nCat)
{
    string s = "";
    int i, j;
    for (i = 0; i < 12; i++)
    {
        int nCR = i == 10 ? 20 : (i == 11 ? 100 : i);
        string sSet = "";
        for (j = 0; j < 24; j++) sSet = AddSorted(sSet, Code(GetCreatureTalentBest(nCat, nCR, o)));
        s += (i == 0 ? "" : " ") + sSet;
    }
    PLog("best." + sTag + "." + IntToString(nCat), s);
}

// Which talents GetCreatureTalentRandom gives in 60 draws, GetCreatureTalentBest(.., -1) in 60 and (.., 20) in 40.
void RandomLine(object o, string sTag, int nCat)
{
    string sR = "", sB = "", sM = "";
    int i;
    for (i = 0; i < 60; i++) sR = AddSorted(sR, Code(GetCreatureTalentRandom(nCat, o)));
    for (i = 0; i < 60; i++) sM = AddSorted(sM, Code(GetCreatureTalentBest(nCat, -1, o)));
    for (i = 0; i < 40; i++) sB = AddSorted(sB, Code(GetCreatureTalentBest(nCat, 20, o)));
    PLog("rand." + sTag + "." + IntToString(nCat), sR);
    PLog("bestm1." + sTag + "." + IntToString(nCat), sM);
    PLog("best20set." + sTag + "." + IntToString(nCat), sB);
}

void Cats(string sTag, int nFrom, int nTo)
{
    object o = GetObjectByTag(sTag);
    int c;
    for (c = nFrom; c <= nTo; c++) BestLine(o, sTag, c);
    if (nFrom == 1)
    {
        PLogI("best." + sTag + ".0.valid", GetIsTalentValid(GetCreatureTalentBest(0, 20, o)));
        PLogI("best." + sTag + ".m2.valid", GetIsTalentValid(GetCreatureTalentBest(2, -2, o)) + GetIsTalentValid(GetCreatureTalentBest(22, -2, o)) + GetIsTalentValid(GetCreatureTalentBest(17, -2, o)));
    }
}

void AllCats(string sTag)
{
    DelayCommand(0.0, Cats(sTag, 1, 6));
    DelayCommand(0.05, Cats(sTag, 7, 12));
    DelayCommand(0.1, Cats(sTag, 13, 18));
    DelayCommand(0.15, Cats(sTag, 19, 23));
}

void RandomCats(string sTag, int nFrom, int nTo)
{
    object o = GetObjectByTag(sTag);
    int c;
    for (c = nFrom; c <= nTo; c++) RandomLine(o, sTag, c);
}

void AllRandom(string sTag)
{
    DelayCommand(0.0, RandomCats(sTag, 1, 6));
    DelayCommand(0.05, RandomCats(sTag, 7, 12));
    DelayCommand(0.1, RandomCats(sTag, 13, 18));
    DelayCommand(0.15, RandomCats(sTag, 19, 22));
}

void Has(string sTag)
{
    object o = GetObjectByTag(sTag);
    string s = "";
    int i;
    string sSp = "6 10 15 32 33 34 36 37 58 78 88 100 102 107 119 144 165 172 174 197 321 ";
    while (sSp != "")
    {
        int p = FindSubString(sSp, " ");
        int nSp = StringToInt(GetStringLeft(sSp, p));
        sSp = GetSubString(sSp, p + 1, 1000);
        s += IntToString(nSp) + "=" + IntToString(GetCreatureHasTalent(TalentSpell(nSp), o)) + "/" + IntToString(GetHasSpell(nSp, o)) + " ";
    }
    PLog("has.spell." + sTag, s);
    s = "";
    string sFt = "5 9 19 23 28 39 204 211 257 299 301 389 ";
    while (sFt != "")
    {
        int p = FindSubString(sFt, " ");
        int nFt = StringToInt(GetStringLeft(sFt, p));
        sFt = GetSubString(sFt, p + 1, 1000);
        s += IntToString(nFt) + "=" + IntToString(GetCreatureHasTalent(TalentFeat(nFt), o)) + "/" + IntToString(GetHasFeat(nFt, o)) + " ";
    }
    PLog("has.feat." + sTag, s);
    s = "";
    for (i = 0; i <= 27; i++) s += IntToString(GetCreatureHasTalent(TalentSkill(i), o));
    PLog("has.skill." + sTag, s);
}

void Constructors()
{
    talent t = TalentSpell(107);
    PLog("ctor.spell", TS(t) + " valid=" + IntToString(GetIsTalentValid(t)));
    PLog("ctor.feat", TS(TalentFeat(28)));
    PLog("ctor.skill", TS(TalentSkill(4)));
    PLog("ctor.spellneg", TS(TalentSpell(-1)) + " " + IntToString(GetTypeFromTalent(TalentSpell(-1))) + " " + IntToString(GetIdFromTalent(TalentSpell(-1))));
    PLog("ctor.spellbig", TS(TalentSpell(5000)));
    talent tNone;
    PLog("ctor.default", TS(tNone) + " " + IntToString(GetTypeFromTalent(tNone)) + " " + IntToString(GetIdFromTalent(tNone)) + " " + IntToString(GetIsTalentValid(tNone)));
    PLog("ctor.has.invalid", IntToString(GetCreatureHasTalent(TalentSpell(107), OBJECT_INVALID)));
    PLog("ctor.best.invalid", TS(GetCreatureTalentBest(2, 20, OBJECT_INVALID)));
    PLog("ctor.best.module", TS(GetCreatureTalentBest(2, 20, GetModule())));
}

void Items()
{
    object o = GetObjectByTag("items");
    CreateItemOnObject("nw_it_mpotion001", o);   // Cure Light Wounds
    CreateItemOnObject("nw_it_mpotion020", o);   // Cure Moderate Wounds
    CreateItemOnObject("nw_it_mpotion003", o);   // Cure Critical Wounds
    CreateItemOnObject("nw_it_mpotion005", o);
    CreateItemOnObject("nw_it_mpotion009", o);
    CreateItemOnObject("nw_it_mpotion015", o);
    CreateItemOnObject("nw_it_mpotion004", o);
    CreateItemOnObject("nw_it_medkit001", o);
    CreateItemOnObject("nw_wmgwn002", o);
    CreateItemOnObject("nw_it_sparscr107", o);
    CreateItemOnObject("nw_it_sparscr109", o);
    object oRing = CreateItemOnObject("nw_it_mring003", o);
    AssignCommand(o, ActionEquipItem(oRing, INVENTORY_SLOT_LEFTRING));
    object o2 = GetObjectByTag("potion1");
    CreateItemOnObject("nw_it_mpotion001", o2, 3);
}

// Spending: the best talent after the casts it uses are gone.
void Spend()
{
    object w = GetObjectByTag("wiz");
    DecrementRemainingSpellUses(w, 107);
    BestLine(w, "wiz.mm1", 2);
    DecrementRemainingSpellUses(w, 107);
    BestLine(w, "wiz.mm2", 2);
    PLogI("wiz.mm2.has", GetCreatureHasTalent(TalentSpell(107), w));
    object s = GetObjectByTag("sorc");
    int i;
    for (i = 0; i < 4; i++) DecrementRemainingSpellUses(s, 15);
    BestLine(s, "sorc.l4a", 2);
    BestLine(s, "sorc.l4b", 13);
    for (i = 0; i < 4; i++) DecrementRemainingSpellUses(s, 15);
    BestLine(s, "sorc.l4c", 2);
    BestLine(s, "sorc.l4d", 13);
    PLog("sorc.l4.has", IntToString(GetCreatureHasTalent(TalentSpell(15), s)) + " " + IntToString(GetHasSpell(15, s)) + " " + IntToString(GetHasSpell(88, s)));
    object c = GetObjectByTag("orc");
    object oP = GetItemPossessedBy(c, "NW_IT_MPOTION002");
    PLog("orc.potion2", GetTag(oP) + " " + IntToString(GetItemStackSize(oP)));
    DestroyObject(oP);
}

void AfterSpend()
{
    object c = GetObjectByTag("orc");
    BestLine(c, "orc.after", 17);
    BestLine(c, "orc.after", 21);
    object o2 = GetObjectByTag("potion1");
    BestLine(o2, "potion1", 17);
}

void Setup()
{
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    Make("nw_bandit005", "wiz", 5.0, 5.0);
    Make("nw_bandit004", "clr", 8.0, 5.0);
    Make("nw_goblinboss", "sorc", 11.0, 5.0);
    Make("nw_drowcler005", "dclr", 14.0, 5.0);
    Make("nw_dwarfmerc005", "ftr", 17.0, 5.0);
    Make("nw_orcboss", "orc", 20.0, 5.0);
    Make("nw_bandit001", "items", 23.0, 5.0);
    Make("nw_drgred001", "drag", 30.0, 30.0);
    Make("nw_curst004", "monk", 5.0, 12.0);
    Make("nw_mumfight", "mum", 8.0, 12.0);
    Make("nw_bandit001", "potion1", 11.0, 12.0);
    Make("nw_bugbearboss", "bug", 14.0, 12.0);
    Make("nw_elfranger005", "rng", 17.0, 12.0);
}

void Report4()
{
    Has("wiz"); Has("clr"); Has("sorc"); Has("dclr"); Has("ftr"); Has("orc"); Has("items"); Has("drag"); Has("monk"); Has("rng");
    Constructors();
}

void main()
{
    DelayCommand(1.0, Setup());
    DelayCommand(2.0, Items());
    string sAll = "wiz clr sorc dclr ftr orc items drag monk mum potion1 bug rng ";
    float fT = 3.0;
    while (sAll != "")
    {
        int p = FindSubString(sAll, " ");
        string sTag = GetStringLeft(sAll, p);
        sAll = GetSubString(sAll, p + 1, 1000);
        DelayCommand(fT, AllCats(sTag));
        fT += 0.2;
    }
    DelayCommand(5.5, Report4());
    DelayCommand(6.0, AllRandom("wiz"));
    DelayCommand(6.2, AllRandom("clr"));
    DelayCommand(6.4, AllRandom("sorc"));
    DelayCommand(6.6, AllRandom("dclr"));
    DelayCommand(6.8, AllRandom("ftr"));
    DelayCommand(7.0, AllRandom("orc"));
    DelayCommand(7.2, AllRandom("items"));
    DelayCommand(7.4, AllRandom("drag"));
    DelayCommand(7.6, AllRandom("monk"));
    DelayCommand(7.8, AllRandom("mum"));
    DelayCommand(8.0, AllRandom("bug"));
    DelayCommand(8.2, AllRandom("rng"));
    DelayCommand(9.0, Spend());
    DelayCommand(10.0, AfterSpend());
    DelayCommand(11.0, PDone());
}

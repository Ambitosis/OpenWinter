// Polymorph and abilities: the form's SPELL1-3 (special abilities), the creature's own spells and feats,
// its sound set, casting while polymorphed (form abilities, own spells, cheat casts), items used while
// polymorphed and levelling up while polymorphed.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

object Spawn(string sRes)
{
    object o = PFxSpawn(NextCell(), sRes);
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    return o;
}

string Has(object o, int nPoly)
{
    string s = "";
    int i;
    for (i = 1; i <= 3; i++)
    {
        int nSpell = StringToInt(Get2DAString("polymorph", "SPELL" + IntToString(i), nPoly));
        if (Get2DAString("polymorph", "SPELL" + IntToString(i), nPoly) != "")
            s += " s" + IntToString(nSpell) + "=" + IntToString(GetHasSpell(nSpell, o));
    }
    return s;
}

// The form's abilities, the creature's own Mage Armor and a feat, its sound set and portrait.
void Watch(string k, object o, int nPoly)
{
    PLog(k, "forms:" + Has(o, nPoly) + " own102=" + IntToString(GetHasSpell(102, o))
        + " feat=" + IntToString(GetHasFeat(FEAT_WEAPON_PROFICIENCY_WIZARD, o))
        + " sound=" + IntToString(GetSoundset(o)) + " portrait=" + IntToString(GetPortraitId(o))
        + " app=" + IntToString(GetAppearanceType(o)) + " fx=" + PFxTypes(o));
}

// Which of the form's items are equipped: a form with a weapon (EQUIPPED) and creature weapons too.
void Slots(string k, object o)
{
    string s = "";
    int n;
    for (n = 0; n < 18; n++) { object it = GetItemInSlot(n, o); if (GetIsObjectValid(it)) s += " " + IntToString(n) + "=" + GetResRef(it); }
    PLog(k, s);
}

void Items(string k, int nPoly)
{
    object o = Spawn("nw_bandit001");
    DelayCommand(0.6, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 4.0));
    DelayCommand(0.7, Slots(k + ".slots", o));
}

void Form(string k, int nPoly)
{
    object o = Spawn("pa_wiz");
    DelayCommand(0.5, Watch(k + ".before", o, nPoly));
    DelayCommand(0.6, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 4.0));
    DelayCommand(0.7, Watch(k + ".during", o, nPoly));
    DelayCommand(6.0, Watch(k + ".after", o, nPoly));
}

// Effects that came from spell nSpell.
string FromSpell(object o, int nSpell)
{
    int n = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectSpellId(e) == nSpell) n++; e = GetNextEffect(o); }
    return IntToString(n);
}

// (GetHasSpell of a special ability is left out: the original's count for the imp's three Invisibility
// entries is 1 before and after one use, OpenWinter's 3 and 2, a difference outside polymorph.)
void Result(string k, object o, int nSpell)
{
    PLog(k, "fromSpell=" + FromSpell(o, nSpell) + " has=" + (nSpell == 90 ? "-" : IntToString(GetHasSpell(nSpell, o)))
        + " own102=" + IntToString(GetHasSpell(102, o)) + " action=" + IntToString(GetCurrentAction(o))
        + " poly=" + IntToString(GetEffectType(GetFirstEffect(o)) != 0 && FindSubString(PFxTypes(o), "62") >= 0));
}

// Polymorphed (nPoly >= 0) for 20 s, the creature casts nSpell at itself (cheat or not), twice.
void Cast(string k, int nPoly, int nSpell, int bCheat, string sRes = "pa_wiz")
{
    object o = Spawn(sRes);
    if (nPoly >= 0) DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 20.0));
    DelayCommand(1.0, AssignCommand(o, ActionCastSpellAtObject(nSpell, o, METAMAGIC_ANY, bCheat, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(4.0, Result(k + ".one", o, nSpell));
    DelayCommand(4.5, AssignCommand(o, ActionCastSpellAtObject(nSpell, o, METAMAGIC_ANY, bCheat, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(8.0, Result(k + ".two", o, nSpell));
    DelayCommand(23.0, Result(k + ".late", o, nSpell));
}

// A creature's own special ability (an imp's Invisibility) while polymorphed.
void Imp(string k, int nPoly)
{
    object o = Spawn("nw_imp");
    if (nPoly >= 0) DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 20.0));
    DelayCommand(1.0, AssignCommand(o, ActionCastSpellAtObject(90, o, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(4.0, Result(k + ".one", o, 90));
    DelayCommand(23.0, Result(k + ".late", o, 90));
}

// A potion drunk while polymorphed: hit points and whether the potion is used.
void Potion(string k, int nPoly)
{
    object o = Spawn("nw_bandit001");
    object p = CreateItemOnObject("nw_it_mpotion001", o);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(5), o);
    if (nPoly >= 0) DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 20.0));
    DelayCommand(0.8, SetLocalInt(o, "hp", GetCurrentHitPoints(o)));
    DelayCommand(0.8, PLog(k + ".before", "potion=" + IntToString(GetIsObjectValid(p))));
    DelayCommand(1.0, AssignCommand(o, ActionUseItemOnObject(p, GetFirstItemProperty(p), o)));
    DelayCommand(8.0, PLog(k + ".after", "healed=" + IntToString(GetCurrentHitPoints(o) > GetLocalInt(o, "hp")) + " potion=" + IntToString(GetIsObjectValid(p))
        + " action=" + IntToString(GetCurrentAction(o))));
}

// Levelling up while polymorphed.
void LevelCase(string k, int nPoly)
{
    object o = Spawn("nw_bandit001");
    SetXP(o, 100000);
    if (nPoly >= 0) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 20.0);
    DelayCommand(0.5, PLog(k + ".up", IntToString(LevelUpHenchman(o, CLASS_TYPE_INVALID, TRUE)) + " hd=" + IntToString(GetHitDice(o))));
}

void Run()
{
    Form("drow", POLYMORPH_TYPE_MALE_DROW);
    Form("water", POLYMORPH_TYPE_HUGE_WATER_ELEMENTAL);
    Form("rakshasa", 88);
    Form("spectre", 76);
    Form("flayer", 68);
    Form("wyrm", POLYMORPH_TYPE_WYRMLING_RED);
    Form("chicken", POLYMORPH_TYPE_CHICKEN);
    Form("bear", POLYMORPH_TYPE_BROWN_BEAR);
    Items("items.rakshasa", 88);
    Items("items.frostgiant", POLYMORPH_TYPE_FROST_GIANT_MALE);
    Items("items.drider", 62);
    Items("items.firegiant", POLYMORPH_TYPE_FIRE_GIANT);
    Items("items.azer", 85);
    Items("items.medusa", 64);
    Cast("spectre799", 76, 799, FALSE);
    Cast("flayer741", 68, 741, FALSE);
    Cast("plain799", -1, 799, FALSE);
    Cast("bear102", POLYMORPH_TYPE_BROWN_BEAR, 102, FALSE);
    Cast("bear102cheat", POLYMORPH_TYPE_BROWN_BEAR, 102, TRUE);
    Cast("plain102", -1, 102, FALSE);
    Cast("spectre102", 76, 102, FALSE);
    Cast("bandit102bear", POLYMORPH_TYPE_BROWN_BEAR, 102, FALSE, "nw_bandit001");
    Cast("bandit102", -1, 102, FALSE, "nw_bandit001");
    Potion("potion.bear", POLYMORPH_TYPE_BROWN_BEAR);
    Potion("potion.plain", -1);
    Imp("imp.bear", POLYMORPH_TYPE_BROWN_BEAR);
    Imp("imp.plain", -1);
    LevelCase("level.bear", POLYMORPH_TYPE_BROWN_BEAR);
    LevelCase("level.plain", -1);
    DelayCommand(25.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}

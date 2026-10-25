// Prestige classes that advance spellcasting (classes.2da ArcSpellLvlMod; the Pale Master's is 2). A sorcerer 6,
// a bard 7 and a wizard 9 (each from a first-level blueprint, levelled with LevelUpHenchman and its own package)
// take Pale Master levels, then one more level of the base class. Logged per level from the last base level on:
// the spells known it added per spell level (which spells a package picks at the base levels is a separate matter)
// and the wizard's memorisation slots per spell level. Measured: Pale Master levels add no spells known; the next
// base level adds its own class level's cls_spkn_* row difference; spells per day follow the caster level.
#include "parity_inc"

void Up(string sKey, object o, int nBase, int nClass, int bLog, int bGain = TRUE)
{
    int l;
    string gain = "";
    for (l = 0; l < 10; l++) SetLocalInt(o, "k" + IntToString(l), GetKnownSpellCount(o, nBase, l));
    int r = LevelUpHenchman(o, nClass, TRUE, nClass == CLASS_TYPE_PALE_MASTER ? PACKAGE_INVALID : nBase);
    if (!bLog) return;
    for (l = 0; l < 10; l++) gain += " " + IntToString(GetKnownSpellCount(o, nBase, l) - GetLocalInt(o, "k" + IntToString(l)));
    PLog(sKey + ".level", IntToString(r) + " base=" + IntToString(GetLevelByClass(nBase, o)) + " pm=" + IntToString(GetLevelByClass(CLASS_TYPE_PALE_MASTER, o)));
    if (bGain) PLog(sKey + ".knowngain", gain);
    if (nBase == CLASS_TYPE_WIZARD)
    {
        string slots = "";
        for (l = 0; l < 10; l++) slots += " " + IntToString(GetMemorizedSpellCountByLevel(o, nBase, l));
        PLog(sKey + ".slots", slots);
    }
}

// Base class to nBaseLevel, then nPm Pale Master levels, then one base level.
void Run(string sKey, string sRes, int nBase, int nBaseLevel, int nPm, float fX)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, PCentre(fX, 0.0));
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    AdjustAlignment(o, ALIGNMENT_LAWFUL, 50 - GetLawChaosValue(o), FALSE);
    AdjustAlignment(o, ALIGNMENT_GOOD, 50 - GetGoodEvilValue(o), FALSE);
    SetXP(o, 1000000);
    float t = 0.2;
    for (i = 2; i <= nBaseLevel; i++) { DelayCommand(t, Up(sKey + ".base" + IntToString(i), o, nBase, nBase, i == nBaseLevel, FALSE)); t += 0.1; }
    for (i = 1; i <= nPm; i++) { DelayCommand(t, Up(sKey + ".pm" + IntToString(i), o, nBase, CLASS_TYPE_PALE_MASTER, TRUE)); t += 0.1; }
    DelayCommand(t, Up(sKey + ".after", o, nBase, nBase, TRUE));
}

void main()
{
    DelayCommand(0.5, Run("sorc", "pk_sorc", CLASS_TYPE_SORCERER, 6, 3, -4.0));
    DelayCommand(0.5, Run("bard", "pk_bard", CLASS_TYPE_BARD, 7, 3, 0.0));
    DelayCommand(0.5, Run("wiz", "pk_wiz", CLASS_TYPE_WIZARD, 9, 1, 4.0));
    DelayCommand(6.0, PDone());
}

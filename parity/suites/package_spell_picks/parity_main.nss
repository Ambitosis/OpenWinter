// Spells a class package picks on LevelUpHenchman. A sorcerer, a bard and a wizard (each from a first-level
// blueprint with no spells known) are levelled to 10 with their own package (packages.2da SpellPref2DA); after
// every level the spells known (or in the spellbook) are logged per spell level, in list order.
// Not matched (left out): the bard's 2nd-level spells. The original's bard learns Cloud of Bewilderment (569) first,
// a spell its package list (packspbar1) doesn't hold, then follows the list; OpenWinter takes only the list's.
#include "parity_inc"

void Up(string sKey, object o, int nClass)
{
    int r = LevelUpHenchman(o, nClass, TRUE, nClass);
    int l;
    for (l = 0; l < 10; l++)
    {
        int n = GetKnownSpellCount(o, nClass, l);
        if (n == 0 || nClass == CLASS_TYPE_BARD && l == 2) continue;
        string s = "";
        int i;
        for (i = 0; i < n; i++) s += " " + IntToString(GetKnownSpellId(o, nClass, l, i));
        PLog(sKey + "." + IntToString(r) + ".known" + IntToString(l), s);
    }
}

void Run(string sKey, string sRes, int nClass, float fX)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, PCentre(fX, 0.0));
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    AdjustAlignment(o, ALIGNMENT_LAWFUL, 50 - GetLawChaosValue(o), FALSE);
    AdjustAlignment(o, ALIGNMENT_GOOD, 50 - GetGoodEvilValue(o), FALSE);
    SetXP(o, 1000000);
    for (i = 2; i <= 10; i++) DelayCommand(0.1 * IntToFloat(i), Up(sKey, o, nClass));
}

void main()
{
    DelayCommand(0.5, Run("sorc", "ps_sorc", CLASS_TYPE_SORCERER, -4.0));
    DelayCommand(0.5, Run("bard", "ps_bard", CLASS_TYPE_BARD, 0.0));
    DelayCommand(0.5, Run("wiz", "ps_wiz", CLASS_TYPE_WIZARD, 4.0));
    DelayCommand(4.0, PDone());
}

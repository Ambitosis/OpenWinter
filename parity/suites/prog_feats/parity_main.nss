// cls_feat_* rows with a GrantedOnLevel but a List other than 3 (wizard: Brew Potion, List 1, level 3; Craft Wand,
// List 1, level 5; Scribe Scroll, List 3, level 1). The original's creation screen doesn't offer Brew Potion to a
// first-level wizard (menu capture c27); these levels show whether the level is a minimum for picking it and whether
// the feat comes by itself at that level. The wizard package (packftwiz1) prefers Brew Potion and Craft Wand to any
// other feat, so a general feat taken where they can be picked takes Brew Potion. MINSPELLLVL (Combat Casting: level 1
// spells) is checked before the level: a fighter's first wizard level takes Dodge instead.
// Not matched (left out): "WW" (fighter 1, wizard 2 at character 3) and "FFFWW" (fighter 4, wizard 2 at character 6)
// take Brew Potion at wizard 2, while "WFFFW" (fighter 4, wizard 2 at character 6, the first wizard level earlier)
// doesn't; OpenWinter offers it from wizard 3 (docs/CLASSIC_CHARGEN.md).
#include "parity_inc"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

void Up(object o, int nClass) { LevelUpHenchman(o, nClass, FALSE, nClass == CLASS_TYPE_WIZARD ? 10 : PACKAGE_INVALID); }

void Log(string sKey, object o)
{
    PLog(sKey, "hd=" + IntToString(GetHitDice(o)) + " wiz=" + IntToString(GetLevelByClass(CLASS_TYPE_WIZARD, o))
        + " brew=" + IntToString(GetHasFeat(FEAT_BREW_POTION, o)) + " wand=" + IntToString(GetHasFeat(FEAT_CRAFT_WAND, o))
        + " scribe=" + IntToString(GetHasFeat(FEAT_SCRIBE_SCROLL, o)) + " combatcast=" + IntToString(GetHasFeat(FEAT_COMBAT_CASTING, o))
        + " toughness=" + IntToString(GetHasFeat(FEAT_TOUGHNESS, o)) + " dodge=" + IntToString(GetHasFeat(FEAT_DODGE, o)));
}

// The bandit blueprint is a fighter 1; each case adds levels in the given classes (W wizard, F fighter).
void Case(string sKey, string sLevels)
{
    int n = NextCell();
    object o = CreateObject(OBJECT_TYPE_CREATURE, "pf_mage", PCentre(IntToFloat(n % 6) * 2.0 - 5.0, IntToFloat(n / 6) * 2.0 - 5.0));
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    SetXP(o, 100000);
    for (i = 0; i < GetStringLength(sLevels); i++)
        DelayCommand(0.3 + 0.2 * IntToFloat(i), Up(o, GetSubString(sLevels, i, 1) == "W" ? CLASS_TYPE_WIZARD : CLASS_TYPE_FIGHTER));
    DelayCommand(0.5 + 0.2 * IntToFloat(GetStringLength(sLevels)), Log(sKey, o));
}

void Run()
{
    Case("f2w1", "FW");       // character 3 (general feat) at wizard 1
    Case("w5", "WWWWW");      // character 6 (general feat) at wizard 5 (bonus feat too)
    Case("f4w1", "FFFW");     // character 5 at wizard 1
    Case("w2f2w3", "WWFFW");  // Brew Potion at character 3 (wizard 2), then character 6 (general feat) at wizard 3
    Case("w3f1w4", "WWWFW");  // Brew Potion at character 3 (wizard 2), then character 6 (general feat) at wizard 4
    Case("f5w1", "FFFFW");    // character 6 (general feat) at wizard 1
    Case("f3w3", "FFWWW");    // character 6 (general feat) at wizard 3
    Case("w1f2w2", "WFFFW");  // character 6 (general feat) at wizard 2, a wizard level already before
    Case("f2w3", "FWWW");     // wizard 3 at character 5, no general feat since character 3 (wizard 1)
    Case("f2w5", "FWWWWW");   // wizard 5 at character 7 (its bonus feat), general feats at 3 (wizard 1) and 6 (wizard 4)
    DelayCommand(4.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}

// Prestige class requirement rows (cls_pres_*.2da) that the base game's tables don't use: SAVE and ALIGN,
// next to controls (an unmet VAR row; a table of rows that are all met). Each prestige class's table is
// replaced by one row under test plus rows that are always met, and the classes lose their alignment
// restrictions. A level 8 fighter (base saves Fort 6, Ref 2, Will 2; all abilities 10, so totals equal
// the base; levelled from the bandit blueprint's fighter 1 with its own package) and a tough one (Constitution 18: Fort total 10) try one LevelUpHenchman into each class.
#include "parity_inc"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

void Level(string sKey, object o, int nClass, int nPackage);
void Up(object o) { LevelUpHenchman(o, CLASS_TYPE_FIGHTER, FALSE); }

void Try(string sKey, string sRes, int nClass, int nPackage, int nLawChaos = 50, int nGoodEvil = 50, int bEffect = FALSE)
{
    int n = NextCell();
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, PCentre(IntToFloat(n % 6) * 2.0 - 5.0, IntToFloat(n / 6) * 2.0 - 5.0));
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    // To the requested alignment from wherever the blueprint stands.
    AdjustAlignment(o, ALIGNMENT_LAWFUL, nLawChaos - GetLawChaosValue(o), FALSE);
    AdjustAlignment(o, ALIGNMENT_GOOD, nGoodEvil - GetGoodEvilValue(o), FALSE);
    SetXP(o, 100000);
    if (bEffect) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSavingThrowIncrease(SAVING_THROW_FORT, 4)), o);
    // Fighter 1 to fighter 8 (its own package), then the level under test.
    for (i = 0; i < 7; i++) DelayCommand(0.2 + 0.1 * IntToFloat(i), Up(o));
    DelayCommand(1.5, Level(sKey, o, nClass, nPackage));
}

void Level(string sKey, object o, int nClass, int nPackage)
{
    // The creature as the requirements see it (saves include abilities and effects; the level after is logged
    // without them: the package's later picks are not what this suite measures).
    string before = "fort=" + IntToString(GetFortitudeSavingThrow(o)) + " ref=" + IntToString(GetReflexSavingThrow(o)) + " will=" + IntToString(GetWillSavingThrow(o))
        + " gf=" + IntToString(GetHasFeat(FEAT_GREAT_FORTITUDE, o)) + " lc=" + IntToString(GetLawChaosValue(o)) + " ge=" + IntToString(GetGoodEvilValue(o));
    int r = LevelUpHenchman(o, nClass, FALSE, nPackage);
    PLog(sKey, before + " result=" + IntToString(r) + " hd=" + IntToString(GetHitDice(o)) + " class2=" + IntToString(GetClassByPosition(2, o)));
}

void Run()
{
    Try("control.bandit", "nw_bandit001", CLASS_TYPE_INVALID, PACKAGE_INVALID);
    Try("control.fighter", "pp_fight", CLASS_TYPE_FIGHTER, PACKAGE_INVALID);
    Try("control.rogue", "pp_fight", CLASS_TYPE_ROGUE, PACKAGE_INVALID);
    Try("control.allmet", "pp_fight", CLASS_TYPE_BLACKGUARD, 67);
    Try("control.varunmet", "pp_fight", CLASS_TYPE_ARCANE_ARCHER, 65);
    Try("save.fort99", "pp_fight", CLASS_TYPE_SHADOWDANCER, 63);
    Try("save.fort8", "pp_fight", CLASS_TYPE_HARPER, 64);
    Try("save.fort9", "pp_fight", CLASS_TYPE_PALE_MASTER, 110);
    Try("save.fort9.tough", "pp_tough", CLASS_TYPE_PALE_MASTER, 110);
    Try("save.fort9.effect", "pp_fight", CLASS_TYPE_PALE_MASTER, 110, 50, 50, TRUE);
    Try("save.ref2", "pp_fight", CLASS_TYPE_SHIFTER, 108);
    Try("save.ref3", "pp_fight", CLASS_TYPE_ASSASSIN, 66);
    Try("save.will2", "pp_fight", CLASS_TYPE_DWARVEN_DEFENDER, 89);
    Try("save.will3", "pp_fight", CLASS_TYPE_PURPLE_DRAGON_KNIGHT, 131);
    Try("save.type0.9", "pp_fight", CLASS_TYPE_DRAGON_DISCIPLE, 111);
    Try("save.type4.1", "pp_fight", CLASS_TYPE_DIVINE_CHAMPION, 109);
    Try("align.1_0.neutral", "pp_fight", CLASS_TYPE_WEAPON_MASTER, 112);
    Try("align.1_0.lawfulgood", "pp_fight", CLASS_TYPE_WEAPON_MASTER, 112, 85, 85);
    Try("align.1_0.chaoticevil", "pp_fight", CLASS_TYPE_WEAPON_MASTER, 112, 15, 15);
    DelayCommand(3.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}

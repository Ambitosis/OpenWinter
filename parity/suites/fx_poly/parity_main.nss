// Polymorph: stats, appearance, size, race, items and effects while polymorphed and after.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

string Base(object o)
{
    string s = "";
    int i;
    for (i = 0; i < PFX_STATS; i++) s += PFxStatName(i) + "=" + IntToString(PFxStat(o, i)) + " ";
    s += "race=" + IntToString(GetRacialType(o)) + " baseStr=" + IntToString(GetAbilityScore(o, ABILITY_STRENGTH, TRUE))
        + " baseCon=" + IntToString(GetAbilityScore(o, ABILITY_CONSTITUTION, TRUE)) + " portrait=" + GetPortraitResRef(o)
       ;
    return s;
}

// What is equipped (the form's creature weapons and hide; fx_polyitems looks closer).
string Slots(object o)
{
    string s = "";
    int nSlot;
    for (nSlot = 0; nSlot < 18; nSlot++)
    {
        object it = GetItemInSlot(nSlot, o);
        if (GetIsObjectValid(it)) s += " slot" + IntToString(nSlot) + "=" + GetResRef(it);
    }
    return s;
}

void Watch(string sKey, object o)
{
    PLog(sKey + ".stats", Base(o));
    PLog(sKey + ".slots", Slots(o));
    PLog(sKey + ".effects", PFxList(o));
}

void Poly(string sKey, string sResRef, int nPoly, effect eExtra)
{
    object o = PFxSpawn(NextCell(), sResRef);
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    if (GetEffectType(eExtra, TRUE) != 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, eExtra, o);
    Watch(sKey + ".before", o);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(nPoly), o, 4.0);
    Watch(sKey + ".during", o);
    DelayCommand(1.0, Watch(sKey + ".during1", o));
    DelayCommand(6.0, Watch(sKey + ".after", o));
}

void Run()
{
    Poly("bear.bandit1", "nw_bandit001", POLYMORPH_TYPE_BROWN_BEAR, EffectHeal(-1));
    Poly("bear.bandit6", "nw_bandit006", POLYMORPH_TYPE_BROWN_BEAR, EffectHeal(-1));
    Poly("bear.goblin", "nw_goblina", POLYMORPH_TYPE_BROWN_BEAR, EffectHeal(-1));
    Poly("wolf.bandit1", "nw_bandit001", POLYMORPH_TYPE_WEREWOLF, EffectHeal(-1));
    Poly("bear.strbuff", "nw_bandit001", POLYMORPH_TYPE_BROWN_BEAR, EffectAbilityIncrease(ABILITY_STRENGTH, 4));
    Poly("bear.dexbuff", "nw_bandit001", POLYMORPH_TYPE_BROWN_BEAR, EffectAbilityIncrease(ABILITY_DEXTERITY, 4));
    Poly("bear.acbuff", "nw_bandit001", POLYMORPH_TYPE_BROWN_BEAR, EffectACIncrease(3, AC_NATURAL_BONUS));
    DelayCommand(8.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}

// How close a caster comes to cast a touch spell (Light) at a creature: one caster 8 m south of one target, for
// casters and targets of different sizes (CREPERSPACE / HITDIST in appearance.2da). The distance when the Light lands.
// Then Haste (short, 8 m) from 14 m.
#include "parity_inc"

object Spawn(string r, float x, float y, float fFacing)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, r, Location(GetObjectByTag("PARITY_AREA"), Vector(x, y, 0.0), fFacing));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetPlotFlag(o, TRUE);
    return o;
}

int Landed(object t, object c, int nSpell)
{
    effect e = GetFirstEffect(t);
    while (GetIsEffectValid(e)) { if (GetEffectSpellId(e) == nSpell && GetEffectCreator(e) == c) return TRUE; e = GetNextEffect(t); }
    return FALSE;
}

void Watch(string k, object c, object t, float fT, int nSpell)
{
    if (fT > 14.0) { PLog(k, "never"); return; }
    if (Landed(t, c, nSpell)) { PLogF(k, GetDistanceBetween(c, t), 0.15); return; }
    DelayCommand(0.1, Watch(k, c, t, fT + 0.1, nSpell));
}

void Cast(string k, object c, object t, int nSpell = SPELL_LIGHT)
{
    AssignCommand(c, ActionCastSpellAtObject(nSpell, t, METAMAGIC_NONE, TRUE));
    Watch(k, c, t, 0.0, nSpell);
}

void Pair(int i, string sCaster, string sTarget)
{
    float x = 6.0 + IntToFloat(i % 5) * 15.0, y = 14.0 + IntToFloat(i / 5) * 16.0;
    object t = Spawn(sTarget, x, y, 270.0);
    object c = Spawn(sCaster, x, y - 8.0, 90.0);
    DelayCommand(5.0, Cast("reach." + sCaster + "." + sTarget, c, t));
}

void Far(int i, string sCaster, string sTarget, int nSpell, float fFrom)
{
    float x = 6.0 + IntToFloat(i) * 15.0, y = 78.0;
    object t = Spawn(sTarget, x, y, 270.0);
    object c = Spawn(sCaster, x, y - fFrom, 90.0);
    DelayCommand(5.0, Cast("reach" + IntToString(nSpell) + "." + sCaster + "." + sTarget, c, t, nSpell));
}

void main()
{
    Far(0, "nw_bandit001", "nw_bandit001", SPELL_HASTE, 14.0);
    Far(1, "nw_bandit001", "nw_gntfire", SPELL_HASTE, 14.0);
    Far(2, "nw_ogre01", "nw_rat001", SPELL_HASTE, 14.0);
    string c0 = "nw_bandit001", c1 = "nw_halfling001", c2 = "nw_ogre01", c3 = "nw_dog";
    string t0 = "nw_bandit001", t1 = "nw_rat001", t2 = "nw_gntfire", t3 = "nw_dog", t4 = "nw_halfling001";
    int i = 0, a, b;
    for (a = 0; a < 4; a++)
        for (b = 0; b < 5; b++)
        {
            string sc = a == 0 ? c0 : a == 1 ? c1 : a == 2 ? c2 : c3;
            string st = b == 0 ? t0 : b == 1 ? t1 : b == 2 ? t2 : b == 3 ? t3 : t4;
            Pair(i++, sc, st);
        }
    DelayCommand(22.0, PDone());
}

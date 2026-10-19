// GetIsInCombat of a creature whose ActionAttack waits in its queue behind another action (Prelude's faction table,
// suite.json). Each case: c (Hostile, stripped, immortal) and t (Commoner) 1.5 m apart, sampled every 0.4 s to 2.8 s.
//   c0: a cheat-cast Mage Armor on itself, then ActionAttack(t).   c1: the cast only.
//   c2: ActionWait(3.0), then ActionAttack(t).                     c3: nothing.
//   c4: the cast, then ActionMoveToObject(t).                      c5: as c0 with t Hostile too (a friend).
// Found by cast_cadence_handoffs re-run with a faction table (cases 0-3: in combat at the cast's impact).
#include "pcombat_inc"
void S(object c, string k) { PLogI(k, GetIsInCombat(c)); }
void Case(int n)
{
    float x = 5.0 + IntToFloat(n) * 10.0;
    object c = PSpawn("nw_bandit001", x, 10.0); object t = PSpawn("nw_bandit001", x + 1.5, 10.0, 180.0);
    ChangeToStandardFaction(c, STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(t, n == 5 ? STANDARD_FACTION_HOSTILE : STANDARD_FACTION_COMMONER);
    SetImmortal(c, TRUE); SetImmortal(t, TRUE);
    string k = "c" + IntToString(n);
    PLogI(k + ".enemy", GetIsEnemy(t, c));
    if (n == 0 || n == 5) { AssignCommand(c, ActionCastSpellAtObject(SPELL_MAGE_ARMOR, c, METAMAGIC_ANY, TRUE)); AssignCommand(c, ActionAttack(t)); }
    if (n == 1) AssignCommand(c, ActionCastSpellAtObject(SPELL_MAGE_ARMOR, c, METAMAGIC_ANY, TRUE));
    if (n == 2) { AssignCommand(c, ActionWait(3.0)); AssignCommand(c, ActionAttack(t)); }
    if (n == 4) { AssignCommand(c, ActionCastSpellAtObject(SPELL_MAGE_ARMOR, c, METAMAGIC_ANY, TRUE)); AssignCommand(c, ActionMoveToObject(t)); }
    float f; int i;
    for (i = 1; i <= 7; i++) { f = IntToFloat(i) * 0.4; DelayCommand(f, S(c, k + ".a" + IntToString(i))); DelayCommand(f, S(t, k + ".t" + IntToString(i))); }
}
void main() { int n; for (n = 0; n < 6; n++) DelayCommand(8.0, Case(n)); DelayCommand(14.0, PDone()); }

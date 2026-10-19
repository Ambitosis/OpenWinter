// OnEndCombatRound of creatures that cast instead of attacking (the stock AI's casters decide at their round's end):
// when the rounds fall after a self buff, a harmful spell, two spells in a row, and while being attacked.
// Casters have no other scripts; a hostile goblin stands 6 m away (plot, scripts removed) to be cast at.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }

object Mk(string sRes, float x, float y, string sTag, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(Area(), Vector(x, y, 0.0), 0.0), FALSE, sTag);
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, "cr_eor");
    SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT, "cr_spl");
    AssignCommand(o, ClearAllActions(TRUE));
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetImmortal(o, TRUE);
    return o;
}

object C(int n) { return GetObjectByTag("CR_C" + IntToString(n)); }
object T(int n) { return GetObjectByTag("CR_T" + IntToString(n)); }

void Pair(int n)
{
    float x = 6.0 + 14.0 * IntToFloat(n % 4), y = 8.0 + 16.0 * IntToFloat(n / 4);
    Mk("nw_bandit005", x, y, "CR_C" + IntToString(n), STANDARD_FACTION_DEFENDER);
    object t = Mk("nw_bandit001", x + (n >= 5 && n != 6 ? 1.5 : 6.0), y, "CR_T" + IntToString(n), STANDARD_FACTION_HOSTILE);
    SetEventScript(C(n), EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "cr_atk");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, "");
}

void Go()
{
    int n;
    for (n = 0; n < 12; n++) { SetLocalFloat(C(n), "CR_T0", PSince()); SetLocalFloat(T(n), "CR_T0", PSince()); }
    // 7-11: like 5, more of them (whether casting beside an attacker draws attacks of opportunity).
    for (n = 7; n < 12; n++)
    {
        AssignCommand(T(n), ActionAttack(C(n)));
        DelayCommand(3.0, AssignCommand(C(n), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, T(n))));
    }
    // 0: Mage Armor on self; 1: Magic Missile at the goblin; 2: Mage Armor then Magic Missile; 3: Magic Missile, then
    // another 8 s later; 4: nothing (control, out of combat); 5: the goblin attacks the caster, who casts Magic Missile
    // at it 3 s later; 6: Mage Armor, cheat-cast (no slot).
    AssignCommand(C(0), ActionCastSpellAtObject(SPELL_MAGE_ARMOR, C(0)));
    AssignCommand(C(1), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, T(1)));
    AssignCommand(C(2), ActionCastSpellAtObject(SPELL_MAGE_ARMOR, C(2)));
    AssignCommand(C(2), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, T(2)));
    AssignCommand(C(3), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, T(3)));
    DelayCommand(8.0, AssignCommand(C(3), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, T(3))));
    AssignCommand(T(5), ActionAttack(C(5)));
    DelayCommand(3.0, AssignCommand(C(5), ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, T(5))));
    AssignCommand(C(6), ActionCastSpellAtObject(SPELL_MAGE_ARMOR, C(6), METAMAGIC_ANY, TRUE));
}

void Report()
{
    int n;
    for (n = 0; n < 12; n++)
    {
        string k = "cr" + IntToString(n);
        PLogSeq(k + ".attacked", GetLocalString(C(n), "CR_ATK"), 1.0);
        PLogSeq(k + ".eor", GetLocalString(C(n), "CR_EOR"), 0.3);
        PLogSeq(k + ".spells", GetLocalString(C(n), "CR_SPL"), 0.3);
        PLogI(k + ".combat", GetIsInCombat(C(n)));
    }
    PDone();
}

void Setup() { int n; for (n = 0; n < 12; n++) Pair(n); }

void main()
{
    PStart();
    DelayCommand(1.0, Setup());
    DelayCommand(7.0, Go());
    DelayCommand(37.0, Report());
}

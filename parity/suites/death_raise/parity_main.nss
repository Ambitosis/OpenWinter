// Raising and healing the dead with the stock spell scripts and items (docs/DEATH_AND_DYING.md): Raise Dead,
// Resurrection, Heal and Cure Light Wounds cast (cheat, instant) by a 10th level cleric at a raiseable corpse, a
// healer's kit used on one, and EffectHitPointChangeWhenDying on an NPC. Event scripts are cleared.
#include "parity_inc"

object Make(string sResRef, float fX, float fY)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetObjectByTag("PARITY_AREA"), Vector(fX, fY, 0.0), 0.0));
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

object Corpse(float fX, float fY)
{
    object o = Make("nw_bandit001", fX, fY);
    SetIsDestroyable(FALSE, TRUE, FALSE, o);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), o);
    return o;
}

void State(string sKey, object o)
{
    PLog(sKey, IntToString(GetIsDead(o)) + ":" + IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o)));
}

void Cast(string sKey, int nSpell, float fX)
{
    object c = Make("nw_drowcler010", fX, 20.0);
    object d = Corpse(fX + 2.0, 20.0);
    DelayCommand(0.5, State(sKey + ".dead", d));
    DelayCommand(0.5, PLogI(sKey + ".seen", GetObjectSeen(d, c)));
    DelayCommand(0.6, AssignCommand(c, ActionCastSpellAtObject(nSpell, d, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(4.0, State(sKey + ".after", d));
}

void main()
{
    Cast("raisedead", SPELL_RAISE_DEAD, 10.0);
    Cast("resurrection", SPELL_RESURRECTION, 20.0);
    Cast("heal", SPELL_HEAL, 30.0);
    Cast("clw", SPELL_CURE_LIGHT_WOUNDS, 40.0);
    // A healer's kit on a corpse.
    object m = Make("nw_bandit001", 50.0, 20.0);
    object k = CreateItemOnObject("nw_it_medkit001", m);
    object d = Corpse(52.0, 20.0);
    DelayCommand(0.6, AssignCommand(m, ActionUseSkill(SKILL_HEAL, d, 0, k)));
    DelayCommand(4.0, State("kit.after", d));
    DelayCommand(4.0, PLogI("kit.left", GetIsObjectValid(k) ? GetItemStackSize(k) : 0));
    // The hit point change when dying on an NPC (alive, then dead).
    object n = Make("nw_bandit001", 60.0, 20.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectHitPointChangeWhenDying(1.0), n);
    int nCount = 0;
    effect e = GetFirstEffect(n);
    while (GetIsEffectValid(e)) { nCount++; e = GetNextEffect(n); }
    PLogI("npc.hpdying.effects", nCount);
    DelayCommand(8.0, PDone());
}

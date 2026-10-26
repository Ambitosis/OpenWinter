#include "visual_inc"
// bridgecombat: a fight on a stock server, for the NWN bridge's combat presentation. The player stays in control.
// From her arrival: a commoner and a defender stand by (neutral and friendly reactions); two hostile goblins attack
// her with their stock scripts; she is given lasting effects (haste, a stoneskin glow, fire immunity and resistance,
// spell resistance) and a goblin caster throws a fireball and magic missiles at her (a reflex save, spell
// resistance, immunity and resistance lines) before she is healed; a goblin chieftain attacks; she is brought to
// dying, healed, and finally killed, so the death panel and respawn can be seen. The module's death and respawn
// events run the stock scripts. Every step is announced in the chat window ("BC <step>").

void Mark(string s) { SendMessageToPC(GetFirstPC(), "BC " + s); }

object Spawn(object oPC, string sResRef, float fDx, float fDy, int nFaction)
{
    vector v = GetPosition(oPC);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(oPC), Vector(v.x + fDx, v.y + fDy, v.z), 0.0));
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

object Hostile(object oPC, string sResRef, float fDx, float fDy)
{
    object o = Spawn(oPC, sResRef, fDx, fDy, STANDARD_FACTION_HOSTILE);
    SetIsTemporaryEnemy(oPC, o);
    SetIsTemporaryEnemy(o, oPC);
    return o;
}

// Keeps a creature on the player until one of them falls.
void Engage(object o, object oPC)
{
    if (!GetIsObjectValid(o) || GetIsDead(o) || GetIsDead(oPC)) return;
    if (GetAttackTarget(o) != oPC) AssignCommand(o, ActionAttack(oPC));
    DelayCommand(3.0, Engage(o, oPC));
}

void Bystanders(object oPC)
{
    Mark("bystanders");
    object c = Spawn(oPC, "nw_bandit001", -6.0, 6.0, STANDARD_FACTION_COMMONER);
    object d = Spawn(oPC, "nw_bandit001", 6.0, 6.0, STANDARD_FACTION_DEFENDER);
    object f = Spawn(oPC, "nw_bandit001", 0.0, 8.0, STANDARD_FACTION_DEFENDER);
    SetIsTemporaryFriend(oPC, f);
    SetIsTemporaryFriend(f, oPC);
    int i; for (i = 5000; i <= 5012; i++) { SetEventScript(c, i, ""); SetEventScript(d, i, ""); SetEventScript(f, i, ""); }
    SetPlotFlag(c, TRUE); SetPlotFlag(d, TRUE); SetPlotFlag(f, TRUE);
}

void Goblins(object oPC)
{
    Mark("goblins");
    Engage(Hostile(oPC, "nw_goblina", 7.0, 2.0), oPC);
    Engage(Hostile(oPC, "nw_goblina", 7.0, -2.0), oPC);
}

void Saves(object oPC);

void Effects(object oPC)
{
    Mark("effects");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectHaste(), oPC, 40.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectVisualEffect(VFX_DUR_PROT_STONESKIN), oPC, 12.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE, 50), oPC, 20.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDamageResistance(DAMAGE_TYPE_FIRE, 5), oPC, 20.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSpellResistanceIncrease(18), oPC, 20.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDamageReduction(5, DAMAGE_POWER_PLUS_FIVE), oPC, 20.0);
    DelayCommand(1.0, Saves(oPC));
}

// Saving throws and fire damage against the immunity and resistance, as scripts make them.
void Saves(object oPC)
{
    Mark("saves");
    ReflexSave(oPC, 30, SAVING_THROW_TYPE_FIRE);
    FortitudeSave(oPC, 5);
    WillSave(oPC, 15, SAVING_THROW_TYPE_MIND_SPELLS);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(20, DAMAGE_TYPE_FIRE), oPC);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(12, DAMAGE_TYPE_SLASHING), oPC);
}

void Cast(object oCaster, int nSpell, object oPC)
{
    AssignCommand(oCaster, ClearAllActions(TRUE));
    AssignCommand(oCaster, ActionCastSpellAtObject(nSpell, oPC, METAMAGIC_ANY, TRUE, 9, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
}

void Caster(object oPC)
{
    Mark("caster");
    object g = Hostile(oPC, "nw_goblinb", 0.0, -9.0);
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(g, i, "");
    SetPlotFlag(g, TRUE);
    DelayCommand(1.0, Cast(g, SPELL_FIREBALL, oPC));
    DelayCommand(5.0, Cast(g, SPELL_MAGIC_MISSILE, oPC));
    DelayCommand(9.0, Cast(g, SPELL_MAGIC_MISSILE, oPC));
    DelayCommand(14.0, DestroyObject(g));
}

void Heal(object oPC, int nAmount)
{
    Mark("heal " + IntToString(nAmount));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectHeal(nAmount), oPC);
}

void Chief(object oPC)
{
    Mark("chief");
    Engage(Hostile(oPC, "nw_gobchiefa", -7.0, 0.0), oPC);
}

void Dying(object oPC)
{
    if (GetIsDead(oPC)) return;
    Mark("dying");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(GetCurrentHitPoints(oPC) + 3, DAMAGE_TYPE_MAGICAL), oPC);
}

void Lethal(object oPC)
{
    if (GetIsDead(oPC)) return;
    Mark("lethal");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(GetCurrentHitPoints(oPC) + 20, DAMAGE_TYPE_FIRE, DAMAGE_POWER_ENERGY), oPC);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc) || GetLocalInt(pc, "bc_started")) return;
    SetLocalInt(pc, "bc_started", 1);
    SetEventScript(GetModule(), EVENT_SCRIPT_MODULE_ON_PLAYER_DEATH, "nw_o0_death");
    SetEventScript(GetModule(), EVENT_SCRIPT_MODULE_ON_RESPAWN_BUTTON_PRESSED, "nw_o0_respawn");
    DelayCommand(2.0, Bystanders(pc));
    DelayCommand(6.0, Goblins(pc));
    DelayCommand(18.0, Effects(pc));
    DelayCommand(20.0, Caster(pc));
    DelayCommand(34.0, Heal(pc, 30));
    DelayCommand(40.0, Chief(pc));
    DelayCommand(55.0, Dying(pc));
    DelayCommand(62.0, Heal(pc, 10));
    DelayCommand(70.0, Lethal(pc));
}

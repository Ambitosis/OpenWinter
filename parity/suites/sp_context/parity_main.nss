// What spell scripts see: save DC, caster level, metamagic, class and level for cheat casts, cast
// from a memorized slot, with metamagic, at objects and at locations. The impact scripts of the
// spells used are overridden here to log instead of casting.
#include "parity_fx"

object Caster(string sResRef, string sTag, float fX, float fY)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, PCentre(fX, fY));
    SetTag(o, sTag);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Slots(string sKey, object o)
{
    PLog("slots." + sKey, IntToString(GetHasSpell(SPELL_MAGIC_MISSILE, o)) + " " + IntToString(GetHasSpell(SPELL_FIREBALL, o))
        + " " + IntToString(GetHasSpell(SPELL_HOLD_MONSTER, o)) + " " + IntToString(GetHasSpell(SPELL_BLESS, o))
        + " " + IntToString(GetHasSpell(SPELL_MAGE_ARMOR, o)));
}

void Run()
{
    object oMage = Caster("nw_drowmage010", "MAGE", -10.0, -10.0);
    object oCleric = Caster("nw_drowcler010", "CLERIC", 10.0, -10.0);
    object oBandit = Caster("nw_bandit001", "BANDIT", -10.0, 10.0);
    object oMage5 = Caster("nw_drowmage005", "MAGE5", 10.0, 10.0);
    object oSR = Caster("nw_bandit001", "SRTARGET", 0.0, 14.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSpellResistanceIncrease(20), oSR);
    SetLocalObject(GetModule(), "srtarget", oSR);
    Slots("mage.before", oMage);
    Slots("cleric.before", oCleric);

    // Cheat casts.
    SetLocalString(GetModule(), "phase", "cheat");
    AssignCommand(oMage, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oMage, METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    AssignCommand(oMage, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oMage, METAMAGIC_MAXIMIZE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    AssignCommand(oMage, ActionCastSpellAtObject(SPELL_HOLD_MONSTER, oMage, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    AssignCommand(oMage, ActionCastSpellAtLocation(SPELL_FIREBALL, PCentre(-6.0, -10.0), METAMAGIC_NONE, TRUE, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    AssignCommand(oCleric, ActionCastSpellAtObject(SPELL_BLESS, oCleric, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    AssignCommand(oCleric, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oCleric, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    AssignCommand(oBandit, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oBandit, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    AssignCommand(oBandit, ActionCastSpellAtObject(SPELL_HOLD_MONSTER, oBandit, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
    AssignCommand(oMage5, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oMage5, METAMAGIC_NONE, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));

    // From memorized slots (the class given, no cheat).
    DelayCommand(2.9, SetLocalString(GetModule(), "phase", "slot"));
    DelayCommand(3.0, AssignCommand(oMage, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oMage, METAMAGIC_NONE, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE, CLASS_TYPE_WIZARD)));
    DelayCommand(3.0, AssignCommand(oMage, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oMage, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE, CLASS_TYPE_WIZARD)));
    DelayCommand(3.0, AssignCommand(oMage, ActionCastSpellAtObject(SPELL_HOLD_MONSTER, oMage, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE, CLASS_TYPE_WIZARD)));
    DelayCommand(3.0, AssignCommand(oMage, ActionCastSpellAtLocation(SPELL_FIREBALL, PCentre(-6.0, -10.0), METAMAGIC_ANY, FALSE, PROJECTILE_PATH_TYPE_DEFAULT, TRUE, CLASS_TYPE_WIZARD)));
    DelayCommand(3.0, AssignCommand(oCleric, ActionCastSpellAtObject(SPELL_BLESS, oCleric, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE, CLASS_TYPE_CLERIC)));
    DelayCommand(3.0, AssignCommand(oMage5, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oMage5, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE, CLASS_TYPE_WIZARD)));
    // Without a class and without cheat: the creature's own spell.
    DelayCommand(4.9, SetLocalString(GetModule(), "phase", "own"));
    DelayCommand(5.0, AssignCommand(oMage, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oMage)));
    DelayCommand(5.0, AssignCommand(oMage, ActionCastSpellAtObject(SPELL_MAGE_ARMOR, oMage, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    // A spell the creature doesn't have, without cheat.
    DelayCommand(5.0, AssignCommand(oBandit, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, oBandit, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));

    DelayCommand(12.0, Slots("mage.after", oMage));
    DelayCommand(12.0, Slots("cleric.after", oCleric));
    DelayCommand(12.5, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}

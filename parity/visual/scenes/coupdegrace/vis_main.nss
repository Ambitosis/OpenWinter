#include "visual_inc"
// coupdegrace: the real Sleep spell cast by a drow mage on a bandit, the player attacking the sleeper with a
// dagger (coup de grace), then a held bandit, then the player put to sleep herself (she is a half-elf, so
// IgnoreEffectImmunity) and woken by a point of damage. GUI and chat logging are on.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|coupdegrace|" + s); }

object Npc(string sResRef, float fDx, float fDy, string sTag, int nFaction)
{
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(v.x + fDx, v.y + fDy, v.z), 270.0), FALSE, sTag);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, nFaction);
    return o;
}

int Asleep(object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_SLEEP) return TRUE;
        e = GetNextEffect(o);
    }
    return FALSE;
}

void Effects(string sKey, object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        s += IntToString(GetEffectType(e)) + "/" + IntToString(GetEffectSpellId(e)) + " ";
        e = GetNextEffect(o);
    }
    Log(sKey + "|" + s);
}

void Cast()
{
    object mage = GetObjectByTag("cdg_mage");
    object sleeper = GetObjectByTag("cdg_sleeper");
    AssignCommand(mage, ActionCastSpellAtObject(SPELL_SLEEP, sleeper, METAMAGIC_NONE, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, FALSE, CLASS_TYPE_WIZARD));
}

void Attack(string sTag)
{
    object pc = GetFirstPC();
    object t = GetObjectByTag(sTag);
    Log(sTag + ".before|hp=" + IntToString(GetCurrentHitPoints(t)) + "|asleep=" + IntToString(Asleep(t)));
    AssignCommand(pc, ActionAttack(t));
}

void After(string sTag)
{
    object t = GetObjectByTag(sTag);
    Log(sTag + ".after|hp=" + IntToString(GetCurrentHitPoints(t)) + "|dead=" + IntToString(GetIsDead(t)));
    AssignCommand(GetFirstPC(), ClearAllActions(TRUE));
}

void SleepPlayer()
{
    object pc = GetFirstPC();
    AssignCommand(GetObjectByTag("cdg_caster"), ApplyEffectToObject(DURATION_TYPE_TEMPORARY, IgnoreEffectImmunity(EffectSleep()), pc, 12.0));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    object dagger = CreateItemOnObject("nw_wswdg001", pc);
    AssignCommand(pc, ActionEquipItem(dagger, INVENTORY_SLOT_RIGHTHAND));
    object caster = Npc("nw_bandit001", 0.0, -14.0, "cdg_caster", STANDARD_FACTION_HOSTILE);
    object mage = Npc("nw_drowmage020", -4.0, 6.0, "cdg_mage", STANDARD_FACTION_MERCHANT);
    object sleeper = Npc("nw_bandit001", 1.5, 4.0, "cdg_sleeper", STANDARD_FACTION_HOSTILE);
    object held = Npc("nw_bandit001", 3.5, 4.0, "cdg_held", STANDARD_FACTION_HOSTILE);
    SetIsTemporaryEnemy(sleeper, mage);
    SetIsTemporaryEnemy(mage, sleeper);
    SetIsTemporaryFriend(pc, mage);
    SetIsTemporaryFriend(mage, pc);
    SetIsTemporaryFriend(pc, sleeper);
    SetIsTemporaryFriend(sleeper, pc);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowDecrease(SAVING_THROW_ALL, 30), sleeper);
    SetMemorizedSpell(mage, CLASS_TYPE_WIZARD, 1, 0, SPELL_SLEEP, TRUE, METAMAGIC_NONE);
    DelayCommand(1.0, VCamera(pc, 90.0, 9.0, 55.0));
    DelayCommand(3.0, Cast());
    DelayCommand(3.6, VShot("a_cast_1"));
    DelayCommand(4.6, VShot("a_cast_2"));
    DelayCommand(5.6, VShot("a_cast_3"));
    DelayCommand(7.0, VShot("a_cast_4"));
    DelayCommand(9.0, VShot("a_cast_5"));
    DelayCommand(9.1, Effects("sleeper.effects", sleeper));
    // If the spell did not take, sleep it by effect so the coup de grace can still be seen.
    DelayCommand(9.5, AssignCommand(caster, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSleep(), sleeper)));
    DelayCommand(9.5, AssignCommand(caster, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectParalyze(), held)));
    DelayCommand(11.0, DestroyObject(mage));
    DelayCommand(12.0, ClearPersonalReputation(sleeper, pc));
    DelayCommand(12.0, ClearPersonalReputation(pc, sleeper));
    DelayCommand(12.5, Attack("cdg_sleeper"));
    DelayCommand(13.2, VShot("b_cdg_1"));
    DelayCommand(13.8, VShot("b_cdg_2"));
    DelayCommand(14.6, VShot("b_cdg_3"));
    DelayCommand(16.5, VShot("b_cdg_4"));
    DelayCommand(18.0, After("cdg_sleeper"));
    DelayCommand(19.0, Attack("cdg_held"));
    DelayCommand(19.8, VShot("c_held_1"));
    DelayCommand(21.0, VShot("c_held_2"));
    DelayCommand(24.0, VShot("c_held_3"));
    DelayCommand(25.0, After("cdg_held"));
    DelayCommand(27.0, VCamera(pc, 90.0, 6.0, 60.0));
    DelayCommand(28.0, SleepPlayer());
    DelayCommand(28.3, VShot("d_pcsleep_1"));
    DelayCommand(28.8, VShot("d_pcsleep_2"));
    DelayCommand(29.5, VShot("d_pcsleep_3"));
    DelayCommand(31.0, VShot("d_pcsleep_4"));
    DelayCommand(31.1, Log("pc.asleep|" + IntToString(Asleep(pc))));
    DelayCommand(34.0, AssignCommand(caster, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), pc)));
    DelayCommand(34.3, VShot("e_pcwake_1"));
    DelayCommand(35.0, VShot("e_pcwake_2"));
    DelayCommand(36.5, VShot("e_pcwake_3"));
    DelayCommand(36.6, Log("pc.asleep.afterdamage|" + IntToString(Asleep(pc))));
    DelayCommand(40.0, VDone());
}

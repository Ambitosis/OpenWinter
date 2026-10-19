#include "visual_inc"
// conditions: how each condition looks on creatures and on the player, and what the chat window says.
// Rows of four mortal, non-plot NPCs (plot and immortal creatures refuse these effects) 5 m north of the
// player get one condition each, permanently, shot 0.5, 2 and 5 s later; then the player gets each disabling
// condition for 4 s (portrait icons, pose, chat lines); then a hostile bandit casts the real Sleep spell on
// another, the player attacks the sleeper with a dagger (coup de grace), and an NPC does the same to a
// paralysed bandit. GUI and chat logging are on, so the scene is not in cutscene mode.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|conditions|" + s); }

effect Make(int i)
{
    switch (i)
    {
        case 0: return EffectSleep(); case 1: return EffectStunned(); case 2: return EffectParalyze();
        case 3: return EffectDazed(); case 4: return EffectConfused(); case 5: return EffectFrightened();
        case 6: return EffectCharmed(); case 7: return EffectDominated(); case 8: return EffectKnockdown();
        case 9: return EffectEntangle(); case 10: return EffectPetrify(); case 11: return EffectCutsceneParalyze();
        case 12: return EffectSlow(); case 13: return EffectHaste(); case 14: return EffectBlindness();
        case 15: return EffectTurned(); case 16: return EffectDeaf(); case 17: return EffectSilence();
        case 18: return EffectPoison(POISON_GIANT_WASP_POISON); case 19: return EffectCurse(2, 2, 2, 2, 2, 2);
        case 20: return EffectNegativeLevel(1); case 21: return EffectInvisibility(INVISIBILITY_TYPE_NORMAL);
        case 22: return EffectSanctuary(30); case 23: return EffectEthereal();
    }
    return EffectDarkness();
}

string Name(int i)
{
    switch (i)
    {
        case 0: return "sleep"; case 1: return "stun"; case 2: return "paralyze"; case 3: return "daze";
        case 4: return "confuse"; case 5: return "fear"; case 6: return "charm"; case 7: return "dominate";
        case 8: return "knockdown"; case 9: return "entangle"; case 10: return "petrify"; case 11: return "cutparalyze";
        case 12: return "slow"; case 13: return "haste"; case 14: return "blind"; case 15: return "turned";
        case 16: return "deaf"; case 17: return "silence"; case 18: return "poison"; case 19: return "curse";
        case 20: return "neglevel"; case 21: return "invis"; case 22: return "sanctuary"; case 23: return "ethereal";
    }
    return "darkness";
}

// Effects are applied by a hostile "caster" NPC standing out of view, so charm and domination have a creator.
object Caster() { return GetObjectByTag("cond_caster"); }

void ApplyAs(int i, object o, float fDur)
{
    if (fDur > 0.0) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, Make(i), o, fDur);
    else ApplyEffectToObject(DURATION_TYPE_PERMANENT, Make(i), o);
}

void Afflict(int i, object o, float fDur = 0.0)
{
    AssignCommand(Caster(), ApplyAs(i, o, fDur));
}

object Npc(string sResRef, float fDx, float fDy, string sTag, int nFaction = STANDARD_FACTION_MERCHANT)
{
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(v.x + fDx, v.y + fDy, v.z), 270.0), FALSE, sTag);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    ChangeToStandardFaction(o, nFaction);
    return o;
}

void Clear(string sTag)
{
    int n = 0;
    object o = GetObjectByTag(sTag, n);
    while (GetIsObjectValid(o))
    {
        DestroyObject(o);
        o = GetObjectByTag(sTag, ++n);
    }
}

// A row of four creatures, conditions a..d from left to right.
void Row(string sPrefix, string sResRef, int a, int b, int c, int d)
{
    string tag = "cond_row";
    object o1 = Npc(sResRef, -3.75, 5.0, tag); object o2 = Npc(sResRef, -1.25, 5.0, tag);
    object o3 = Npc(sResRef, 1.25, 5.0, tag); object o4 = Npc(sResRef, 3.75, 5.0, tag);
    DelayCommand(1.0, Afflict(a, o1)); DelayCommand(1.0, Afflict(b, o2));
    DelayCommand(1.0, Afflict(c, o3)); DelayCommand(1.0, Afflict(d, o4));
    string name = sPrefix + "_" + Name(a) + "_" + Name(b) + "_" + Name(c) + "_" + Name(d);
    DelayCommand(0.9, VShot(name + "_0before"));
    DelayCommand(1.5, VShot(name + "_1at05s"));
    DelayCommand(3.0, VShot(name + "_2at2s"));
    DelayCommand(6.0, VShot(name + "_3at5s"));
    DelayCommand(8.0, Clear(tag));
}

// One condition on the player for 4 s.
void OnPlayer(string sPrefix, int i)
{
    object pc = GetFirstPC();
    Log("player|" + Name(i));
    Afflict(i, pc, 4.0);
    DelayCommand(0.6, VShot(sPrefix + "_pc_" + Name(i) + "_a"));
    DelayCommand(2.5, VShot(sPrefix + "_pc_" + Name(i) + "_b"));
}

void SleepSpell()
{
    object pc = GetFirstPC();
    object mage = Npc("nw_bandit001", -5.0, 9.0, "cond_mage", STANDARD_FACTION_HOSTILE);
    object sleeper = Npc("nw_bandit001", 2.0, 9.0, "cond_sleeper", STANDARD_FACTION_COMMONER);
    SetIsTemporaryEnemy(sleeper, mage);
    SetIsTemporaryEnemy(mage, sleeper);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowDecrease(SAVING_THROW_ALL, 30), sleeper);
    DelayCommand(1.0, AssignCommand(mage, ActionCastSpellAtObject(SPELL_SLEEP, sleeper, METAMAGIC_ANY, TRUE)));
    DelayCommand(1.8, VShot("s_sleep_cast"));
    DelayCommand(3.5, VShot("s_sleep_impact"));
    DelayCommand(6.0, VShot("s_sleep_after"));
    DelayCommand(6.1, Log("sleeper.sleep|" + IntToString(GetHasSpellEffect(SPELL_SLEEP, sleeper))));
    DelayCommand(7.0, DestroyObject(mage));
}

void CoupDeGrace()
{
    object pc = GetFirstPC();
    object sleeper = GetObjectByTag("cond_sleeper");
    ChangeToStandardFaction(sleeper, STANDARD_FACTION_HOSTILE);
    Log("cdg.before|hp=" + IntToString(GetCurrentHitPoints(sleeper)) + "|asleep=" + IntToString(GetHasSpellEffect(SPELL_SLEEP, sleeper)));
    object dagger = CreateItemOnObject("nw_wswdg001", pc);
    AssignCommand(pc, ActionEquipItem(dagger, INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(pc, ActionAttack(sleeper));
    DelayCommand(1.0, VShot("t_cdg_a"));
    DelayCommand(2.0, VShot("t_cdg_b"));
    DelayCommand(3.5, VShot("t_cdg_c"));
    DelayCommand(3.6, Log("cdg.after|hp=" + IntToString(GetCurrentHitPoints(sleeper)) + "|dead=" + IntToString(GetIsDead(sleeper))));
    DelayCommand(6.0, AssignCommand(pc, ClearAllActions(TRUE)));
}

void NpcCoupDeGrace()
{
    object killer = Npc("nw_bandit001", -2.0, 7.0, "cond_killer", STANDARD_FACTION_HOSTILE);
    object victim = Npc("nw_bandit001", -0.6, 7.0, "cond_victim", STANDARD_FACTION_COMMONER);
    Afflict(2, victim);
    DelayCommand(1.0, AssignCommand(killer, ActionAttack(victim)));
    DelayCommand(2.0, VShot("u_npc_cdg_a"));
    DelayCommand(3.5, VShot("u_npc_cdg_b"));
    DelayCommand(3.6, Log("npccdg.after|hp=" + IntToString(GetCurrentHitPoints(victim)) + "|dead=" + IntToString(GetIsDead(victim))));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    // A caster for every effect, out of view to the south.
    Npc("nw_bandit001", 0.0, -12.0, "cond_caster", STANDARD_FACTION_HOSTILE);
    DelayCommand(1.0, VCamera(pc, 90.0, 11.0, 55.0));
    float t = 3.0;
    DelayCommand(t, Row("a", "nw_bandit001", 0, 1, 2, 3)); t += 9.0;
    DelayCommand(t, Row("b", "nw_goblina", 0, 1, 2, 3)); t += 9.0;
    DelayCommand(t, Row("c", "nw_bandit001", 8, 9, 10, 11)); t += 9.0;
    DelayCommand(t, Row("d", "nw_bandit001", 4, 5, 6, 7)); t += 9.0;
    DelayCommand(t, Row("e", "nw_bandit001", 12, 13, 14, 15)); t += 9.0;
    DelayCommand(t, Row("f", "nw_bandit001", 21, 22, 23, 24)); t += 9.0;
    DelayCommand(t, VCamera(pc, 90.0, 6.0, 60.0)); t += 1.0;
    // The player: the disabling conditions, then the rest for the portrait icons and chat lines.
    // Not a negative level (it kills a first-level player); petrification last, in case it ends the game.
    string order = "0 1 2 3 8 9 11 4 5 6 7 12 13 14 15 16 17 18 19 21 22 23 24 10 ";
    int n = 0;
    while (order != "")
    {
        int sp = FindSubString(order, " ");
        string p = "p" + (n < 10 ? "0" : "") + IntToString(n);
        DelayCommand(t, OnPlayer(p, StringToInt(GetStringLeft(order, sp))));
        order = GetSubString(order, sp + 1, 200);
        t += 5.0;
        n++;
    }
    DelayCommand(t, VCamera(pc, 90.0, 12.0, 55.0)); t += 1.0;
    DelayCommand(t, SleepSpell()); t += 8.0;
    DelayCommand(t, CoupDeGrace()); t += 7.0;
    DelayCommand(t, NpcCoupDeGrace()); t += 5.0;
    DelayCommand(t, VDone());
}

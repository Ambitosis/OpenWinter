#include "visual_inc"
// killmsg: the chat lines a death prints, with the GUI and chat log on. A 1 hit point goblin (challenge rating 1)
// dies to the player's light crossbow, her dagger, Magic Missile and her EffectDamage (on a goblin made in the same
// instant, then on one she has seen for 3 s); to a henchman's sword, near and 60 m away; to a summoned badger; to a
// neutral bystander beside her and one 60 m away; and to the module's EffectDeath (no killer). Then a party member
// dies (a 1 hit point henchman to a hostile goblin's EffectDamage, the other to the module's EffectDeath), and the
// player dies to a goblin's EffectDamage and is raised by script. "KM <case>" is sent to the player before each, so
// chat.txt shows which lines each case printed; shots after each case keep the chat window's colours.
// Under the harness the player's own ActionAttack and ActionCastSpellAtObject orders did not land within their
// windows (only one dagger kill, late, in the first run), so those three cases may print nothing.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|km|" + s); }

object Spawn(string sResRef, float fDx, float fDy, string sTag, int nFaction = -1)
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(p.x + fDx, p.y + fDy, p.z), 180.0), FALSE, sTag);
    SetAILevel(o, AI_LEVEL_NORMAL);
    if (nFaction >= 0) ChangeToStandardFaction(o, nFaction);
    return o;
}

void Mark(string sCase) { SendMessageToPC(GetFirstPC(), "KM " + sCase); Log(sCase + "|xp=" + IntToString(GetXP(GetFirstPC()))); }
void After(string sCase, string sTag)
{
    object g = GetObjectByTag(sTag);
    Log(sCase + ".after|dead=" + IntToString(GetIsDead(g)) + "|valid=" + IntToString(GetIsObjectValid(g)) + "|xp=" + IntToString(GetXP(GetFirstPC())));
}
void Clear(object o) { AssignCommand(o, ClearAllActions(TRUE)); }
void Burn(object oBy, object oVictim) { AssignCommand(oBy, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(5, DAMAGE_TYPE_FIRE), oVictim)); }

// Targets for the player's own attacks lose 30 AC, so her wizard's attack roll lands within a round or two.
object Easy(object g) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACDecrease(30), g); return g; }
void Bow()
{
    object pc = GetFirstPC();
    AssignCommand(pc, ActionEquipItem(GetObjectByTag("km_bow"), INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(pc, ActionEquipItem(GetObjectByTag("km_bolts"), INVENTORY_SLOT_BOLTS));
}
void Ranged() { Mark("ranged"); object g = Easy(Spawn("km", 8.0, 0.0, "g_ranged")); AssignCommand(GetFirstPC(), ActionAttack(g)); }
void Dagger()
{
    object pc = GetFirstPC();
    Clear(pc);
    AssignCommand(pc, ActionEquipItem(GetObjectByTag("km_dagger"), INVENTORY_SLOT_RIGHTHAND));
}
void Melee() { Mark("melee"); object g = Easy(Spawn("km", 1.6, 0.0, "g_melee")); AssignCommand(GetFirstPC(), ActionAttack(g)); }
void Spell()
{
    object pc = GetFirstPC();
    Clear(pc);
    Mark("spell");
    object g = Spawn("km", 6.0, 0.0, "g_spell");
    int memorized = GetHasSpell(SPELL_MAGIC_MISSILE, pc);
    Log("spell|memorized=" + IntToString(memorized));
    AssignCommand(pc, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, g, METAMAGIC_ANY, memorized == 0));
}
void Unseen()
{
    Clear(GetFirstPC());
    Mark("effectdamage_unseen");
    Burn(GetFirstPC(), Spawn("km", 3.0, 0.0, "g_unseen"));
}
void SpawnSeen() { Spawn("km", 3.0, -1.5, "g_seen"); }
void Seen() { Mark("effectdamage_seen"); Burn(GetFirstPC(), GetObjectByTag("g_seen")); }
void Henchman() { Mark("henchman"); AssignCommand(GetObjectByTag("km_hench"), ActionAttack(Spawn("km", 2.0, 2.5, "g_hench"))); }
void HenchmanFar() { Mark("henchman_far"); Burn(GetObjectByTag("km_hench"), Spawn("km", 46.0, 44.0, "g_hfar")); }
void Place(object o, float fDx, float fDy)
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    Clear(o);
    AssignCommand(o, JumpToLocation(Location(GetArea(pc), Vector(p.x + fDx, p.y + fDy, p.z), 0.0)));
}
void Summon()
{
    Mark("summon");
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, GetFirstPC());
    object g = Spawn("km", 2.0, -2.5, "g_summon");
    Log("summon|" + GetName(s));
    Clear(s);
    AssignCommand(s, ActionAttack(g));
}
void Unsummon() { DestroyObject(GetAssociate(ASSOCIATE_TYPE_SUMMONED, GetFirstPC())); }
void Bystander() { Mark("bystander_near"); AssignCommand(GetObjectByTag("km_npc"), ActionAttack(Spawn("km", -2.0, 2.5, "g_near"))); }
void FarBystander() { Mark("bystander_far"); Burn(GetObjectByTag("km_far"), Spawn("km", 46.0, 44.0, "g_far")); }
void NoKiller()
{
    Mark("module_effectdeath");
    object g = Spawn("km", 2.5, 1.0, "g_module");
    AssignCommand(GetModule(), ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), g));
}
void KillPlayer() { Mark("enemy_kills_player"); AssignCommand(GetObjectByTag("km_enemy"), ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(100, DAMAGE_TYPE_FIRE), GetFirstPC())); }
void Raise()
{
    object pc = GetFirstPC();
    Log("player.dead=" + IntToString(GetIsDead(pc)));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectResurrection(), pc);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectHeal(100), pc);
}
void EnemyKillsHenchman() { Mark("enemy_kills_henchman"); Burn(GetObjectByTag("km_enemy"), GetObjectByTag("km_weak")); }
void ModuleKillsHenchman()
{
    Mark("module_kills_henchman");
    AssignCommand(GetModule(), ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), GetObjectByTag("km_hench")));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetMaxHenchmen(4);
    DelayCommand(1.0, VCamera(pc, 90.0, 12.0, 50.0));
    CreateItemOnObject("nw_wswdg001", pc, 1, "km_dagger");
    CreateItemOnObject("nw_wbwxl001", pc, 1, "km_bow");
    CreateItemOnObject("nw_wambo001", pc, 50, "km_bolts");
    DelayCommand(2.0, Bow());
    object h = Spawn("kmh", -2.0, 0.0, "km_hench", STANDARD_FACTION_COMMONER);
    AssignCommand(h, ActionEquipItem(CreateItemOnObject("nw_wswss001", h), INVENTORY_SLOT_RIGHTHAND));
    object w = Spawn("kmw", -2.0, -2.0, "km_weak", STANDARD_FACTION_COMMONER);
    object n = Spawn("kmh", -3.0, 4.0, "km_npc", STANDARD_FACTION_COMMONER);
    AssignCommand(n, ActionEquipItem(CreateItemOnObject("nw_wswss001", n), INVENTORY_SLOT_RIGHTHAND));
    object far = Spawn("kmh", 45.0, 45.0, "km_far", STANDARD_FACTION_COMMONER);
    object enemy = Spawn("km", -5.0, -3.0, "km_enemy", STANDARD_FACTION_MERCHANT);
    Log("start|xp=" + IntToString(GetXP(pc)) + "|hd=" + IntToString(GetHitDice(pc)));

    DelayCommand(5.0, Ranged());
    DelayCommand(13.0, After("ranged", "g_ranged"));
    DelayCommand(13.2, VShot("a_ranged"));
    DelayCommand(14.0, Dagger());
    DelayCommand(16.0, Melee());
    DelayCommand(24.0, After("melee", "g_melee"));
    DelayCommand(24.2, VShot("b_melee"));
    DelayCommand(26.0, Spell());
    DelayCommand(33.0, After("spell", "g_spell"));
    DelayCommand(33.2, VShot("c_spell"));
    DelayCommand(35.0, Unseen());
    DelayCommand(37.0, After("effectdamage_unseen", "g_unseen"));
    DelayCommand(37.2, VShot("d_effectdamage_unseen"));
    DelayCommand(38.0, SpawnSeen());
    DelayCommand(41.0, Seen());
    DelayCommand(43.0, After("effectdamage_seen", "g_seen"));
    DelayCommand(43.2, VShot("e_effectdamage_seen"));
    DelayCommand(45.0, AddHenchman(pc, h));
    DelayCommand(45.0, AddHenchman(pc, w));
    DelayCommand(46.0, Henchman());
    DelayCommand(53.0, After("henchman", "g_hench"));
    DelayCommand(53.2, VShot("f_henchman"));
    DelayCommand(54.0, Place(h, 45.0, 43.0));
    DelayCommand(56.0, HenchmanFar());
    DelayCommand(58.0, After("henchman_far", "g_hfar"));
    DelayCommand(58.2, VShot("g_henchman_far"));
    DelayCommand(59.0, Place(h, -2.0, 0.0));
    DelayCommand(60.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire"), pc, 120.0));
    DelayCommand(64.0, Summon());
    DelayCommand(71.0, After("summon", "g_summon"));
    DelayCommand(71.2, VShot("h_summon"));
    DelayCommand(72.0, Unsummon());
    DelayCommand(73.0, Bystander());
    DelayCommand(80.0, After("bystander_near", "g_near"));
    DelayCommand(80.2, VShot("i_bystander_near"));
    DelayCommand(81.0, FarBystander());
    DelayCommand(83.0, After("bystander_far", "g_far"));
    DelayCommand(83.2, VShot("j_bystander_far"));
    DelayCommand(84.0, NoKiller());
    DelayCommand(86.0, After("module_effectdeath", "g_module"));
    DelayCommand(86.2, VShot("k_module_effectdeath"));
    DelayCommand(97.0, EnemyKillsHenchman());
    DelayCommand(99.0, After("enemy_kills_henchman", "km_weak"));
    DelayCommand(99.2, VShot("m_enemy_kills_henchman"));
    DelayCommand(101.0, ModuleKillsHenchman());
    DelayCommand(103.0, After("module_kills_henchman", "km_hench"));
    DelayCommand(103.2, VShot("n_module_kills_henchman"));
    DelayCommand(105.0, KillPlayer());
    DelayCommand(107.0, VShot("o_enemy_kills_player"));
    DelayCommand(109.0, Raise());
    DelayCommand(112.0, VDone());
}

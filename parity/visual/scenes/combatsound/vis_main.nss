#include "visual_inc"
// Which weapon sounds the original plays (combat_audio.py records them as tones) and how the fighters move during
// melee rounds. A fresh bandit per phase attacks the player (unarmed, so its own blows sound different; a ring of
// armour proficiencies lets it wear chain and plate, as the original always plays the first pregenerated character):
// unarmed and with a longsword and a club, hitting and missing, against chain, plate and clothing; then the player
// attacks a badger with a longsword. Positions every quarter second.
object Foe() { return GetObjectByTag("probe_foe"); }
object Badger() { return GetObjectByTag("probe_badger"); }
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
void Phase(string p) { SetLocalString(GetModule(), "phase", p); Log("note|combatsound|phase|" + p); }
void Pos() {
 object pc = GetFirstPC(); vector a = GetPosition(pc); vector b = GetPosition(Foe()); vector c = GetPosition(Badger());
 Log("note|combatsound|pos|" + GetLocalString(GetModule(), "phase") + "|pc=" + FloatToString(a.x, 0, 2) + "," + FloatToString(a.y, 0, 2) + "," + FloatToString(GetFacing(pc), 0, 0)
  + "|foe=" + FloatToString(b.x, 0, 2) + "," + FloatToString(b.y, 0, 2) + "," + FloatToString(GetFacing(Foe()), 0, 0)
  + "|badger=" + FloatToString(c.x, 0, 2) + "," + FloatToString(c.y, 0, 2) + "|pcAction=" + IntToString(GetCurrentAction(pc)));
 DelayCommand(0.25, Pos());
}
void Strip(object o) {
 object r = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o); object l = GetItemInSlot(INVENTORY_SLOT_LEFTHAND, o);
 if (GetIsObjectValid(r)) DestroyObject(r); if (GetIsObjectValid(l)) DestroyObject(l);
}
void Wear(object o, string resref, int slot) {
 if (resref == "") return;
 object it = CreateItemOnObject(resref, o);
 AssignCommand(o, ActionEquipItem(it, slot));
}
void Report(object o, string who) {
 object ch = GetItemInSlot(INVENTORY_SLOT_CHEST, o); object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o);
 Log("note|combatsound|gear|" + GetLocalString(GetModule(), "phase") + "|" + who + "|chest=" + GetResRef(ch) + "|ac=" + IntToString(GetItemACValue(ch)) + "|weapon=" + GetResRef(w));
}
void Spawn(string weapon) {
 object pc = GetFirstPC(); vector p = GetPosition(pc);
 object foe = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), p + Vector(1.6, 0.0, 0.0), 180.0), FALSE, "probe_foe");
 ChangeToStandardFaction(foe, STANDARD_FACTION_MERCHANT); SetImmortal(foe, TRUE);
 Strip(foe);
 DelayCommand(0.3, Wear(foe, weapon, INVENTORY_SLOT_RIGHTHAND));
}
void Attack(int hits) {
 object pc = GetFirstPC(); object foe = Foe();
 ApplyEffectToObject(DURATION_TYPE_TEMPORARY, hits ? EffectAttackIncrease(20) : EffectAttackDecrease(20), foe, 14.0);
 if (!hits) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(20), pc, 14.0);
 ChangeToStandardFaction(foe, STANDARD_FACTION_HOSTILE);
 AssignCommand(foe, ActionAttack(pc));
}
void End() { object foe = Foe(); AssignCommand(foe, ClearAllActions(TRUE)); SetImmortal(foe, FALSE); SetPlotFlag(foe, FALSE); DestroyObject(foe); AssignCommand(GetFirstPC(), ClearAllActions(TRUE)); }
void FoePhase(string phase, string weapon, string armor, int hits, float t) {
 object pc = GetFirstPC();
 DelayCommand(t, Phase(phase));
 DelayCommand(t, Wear(pc, armor, INVENTORY_SLOT_CHEST));
 DelayCommand(t + 0.2, Spawn(weapon));
 DelayCommand(t + 1.5, Report(pc, "pc"));
 DelayCommand(t + 1.6, Report(Foe(), "foe"));
 DelayCommand(t + 2.0, Attack(hits));
 DelayCommand(t + 15.0, End());
}
void PcPhase(string phase, float t) {
 object pc = GetFirstPC();
 DelayCommand(t, Phase(phase));
 DelayCommand(t, Wear(pc, "nw_wswls001", INVENTORY_SLOT_RIGHTHAND));
 DelayCommand(t + 1.5, Report(pc, "pc"));
 DelayCommand(t + 2.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectAttackIncrease(20), pc, 14.0));
 DelayCommand(t + 2.0, ChangeToStandardFaction(Badger(), STANDARD_FACTION_HOSTILE));
 DelayCommand(t + 2.1, AssignCommand(pc, ActionAttack(Badger())));
 DelayCommand(t + 15.0, AssignCommand(pc, ClearAllActions(TRUE)));
 DelayCommand(t + 15.0, ChangeToStandardFaction(Badger(), STANDARD_FACTION_MERCHANT));
}
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 object a = GetArea(pc);
 object badger = CreateObject(OBJECT_TYPE_CREATURE, "nw_badger", Location(a, Vector(10.0, 20.0, 0.0), 270.0), FALSE, "probe_badger");
 ChangeToStandardFaction(badger, STANDARD_FACTION_MERCHANT); SetImmortal(badger, TRUE); SetImmortal(pc, TRUE);
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 60.0));
 DelayCommand(1.0, Strip(pc));
 object ring = CreateItemOnObject("nw_it_mring001", pc);
 AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyBonusFeat(24), ring);
 AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyBonusFeat(25), ring);
 AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyBonusFeat(26), ring);
 AssignCommand(pc, ActionEquipItem(ring, INVENTORY_SLOT_LEFTRING));
 DelayCommand(2.0, Pos());
 float t = 3.0;
 FoePhase("unarmed_hit_chain", "", "nw_aarcl004", TRUE, t); t += 16.0;
 FoePhase("unarmed_miss", "", "", FALSE, t); t += 16.0;
 FoePhase("sword_hit_chain", "nw_wswls001", "", TRUE, t); t += 16.0;
 FoePhase("sword_miss", "nw_wswls001", "", FALSE, t); t += 16.0;
 FoePhase("sword_hit_plate", "nw_wswls001", "nw_aarcl011", TRUE, t); t += 16.0;
 FoePhase("club_hit_cloth", "nw_wblcl001", "nw_cloth001", TRUE, t); t += 16.0;
 FoePhase("sword_hit_cloth", "nw_wswls001", "", TRUE, t); t += 16.0;
 PcPhase("pc_sword_hit_badger", t); t += 16.0;
 DelayCommand(t, VDone());
}

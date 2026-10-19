#include "visual_inc"
// The radial menu on each kind of the player's own associate (summon, familiar, henchman, dominated), with
// every root wedge hovered while the right button is held, plus scripted hostility readings and a player
// ActionAttack / Taunt on her own summon. Docs: docs/ASSOCIATE_COMMANDS.md.
void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Log(string s) { WriteTimestampedLogEntry("VISUAL|" + s); }
location Spot(object pc) { vector v = GetPosition(pc); return Location(GetArea(pc), Vector(v.x + 3.0, v.y, v.z), 180.0); }
void Pin(object o) { if (GetIsObjectValid(o)) { AssignCommand(o, ClearAllActions(TRUE)); AssignCommand(o, JumpToLocation(Spot(GetFirstPC()))); DelayCommand(0.5, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), o)); } }
void Rel(object pc, object a, string k) {
 Note(k + "|valid=" + IntToString(GetIsObjectValid(a)) + "|type=" + IntToString(GetAssociateType(a)) + "|master=" + IntToString(GetMaster(a) == pc)
  + "|pcEnemy=" + IntToString(GetIsEnemy(a, pc)) + "|aEnemy=" + IntToString(GetIsEnemy(pc, a)) + "|pcFriend=" + IntToString(GetIsFriend(a, pc))
  + "|pcHostile=" + IntToString(GetIsReactionTypeHostile(a, pc)) + "|pcFriendly=" + IntToString(GetIsReactionTypeFriendly(a, pc))
  + "|rep=" + IntToString(GetReputation(pc, a)) + "|repBack=" + IntToString(GetReputation(a, pc)) + "|faction=" + IntToString(GetFactionEqual(pc, a))
  + "|name=" + GetName(a) + "|resref=" + GetResRef(a) + "|conv=" + GetLocalString(a, "x") );
}
// Opens the radial on the associate at Spot with the right button held, shoots the root, then hovers each wedge.
void Ring(float t, string k) {
 float cx = 0.754, cy = 0.525;
 DelayCommand(t, Log("hover|" + FloatToString(cx, 0, 3) + "," + FloatToString(cy, 0, 3)));
 DelayCommand(t + 0.3, Log("mouse|" + FloatToString(cx, 0, 3) + "," + FloatToString(cy, 0, 3) + "|down|right"));
 DelayCommand(t + 1.3, VShot(k + "_0root"));
 string names = "N NE E SE S SW W NW ";
 float dx0 = 0.0, dx1 = 0.0562, dx2 = 0.0781;
 float dy0 = 0.1416, dy1 = 0.1014;
 float x; float y; int i;
 for (i = 0; i < 8; i++) {
  if (i == 0) { x = 0.0; y = -dy0; } else if (i == 1) { x = dx1; y = -dy1; } else if (i == 2) { x = dx2; y = 0.0; }
  else if (i == 3) { x = dx1; y = dy1; } else if (i == 4) { x = 0.0; y = dy0; } else if (i == 5) { x = -dx1; y = dy1; }
  else if (i == 6) { x = -dx2; y = 0.0; } else { x = -dx1; y = -dy1; }
  float at = t + 2.0 + IntToFloat(i) * 0.9;
  DelayCommand(at, Log("hover|" + FloatToString(cx + x, 0, 4) + "," + FloatToString(cy + y, 0, 4)));
  DelayCommand(at + 0.5, VShot(k + "_" + IntToString(i + 1) + GetSubString("N__NE_E__SE_S__SW_W__NW_", i * 3, 3)));
 }
 DelayCommand(t + 9.4, Log("hover|" + FloatToString(cx, 0, 3) + "," + FloatToString(cy, 0, 3)));
 DelayCommand(t + 9.7, Log("mouse|" + FloatToString(cx, 0, 3) + "," + FloatToString(cy, 0, 3) + "|up|right"));
}
void Summon(object pc) { AssignCommand(pc, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire", VFX_FNF_SUMMON_MONSTER_1), Spot(pc), 600.0)); }
void Familiar(object pc) { SummonFamiliar(pc); DelayCommand(1.0, Pin(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc))); }
void Hench(object pc) { object h = CreateObject(OBJECT_TYPE_CREATURE, "nw_hen_gal_01", Spot(pc)); AddHenchman(pc, h); DelayCommand(0.5, Pin(h)); }
void Dominate(object pc) { object g = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Spot(pc)); SetTag(g, "AR_GOB");
 AssignCommand(pc, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, SupernaturalEffect(EffectDominated()), g, 600.0)); DelayCommand(0.5, Pin(g)); }
void Companion(object pc) { SummonAnimalCompanion(pc); DelayCommand(1.0, Rel(pc, GetAssociate(ASSOCIATE_TYPE_ANIMALCOMPANION, pc), "companion")); }
void Gone(object a) { if (GetIsObjectValid(a)) { if (GetAssociateType(a) == ASSOCIATE_TYPE_HENCHMAN) RemoveHenchman(GetMaster(a), a); SetPlotFlag(a, FALSE); DestroyObject(a); } }
void Probe(object pc, string k) {
 object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc);
 Note(k + "|pcAction=" + IntToString(GetCurrentAction(pc)) + "|pcTarget=" + GetName(GetAttackTarget(pc)) + "|pcAttemptTarget=" + GetName(GetAttemptedAttackTarget())
  + "|combat=" + IntToString(GetIsInCombat(pc)) + "|hp=" + IntToString(GetCurrentHitPoints(s)) + "/" + IntToString(GetMaxHitPoints(s))
  + "|lastAttacker=" + GetName(GetLastAttacker(s)) + "|sTarget=" + GetName(GetAttackTarget(s)) + "|sValid=" + IntToString(GetIsObjectValid(s)) + "|inConvo=" + IntToString(IsInConversation(pc)));
}
void Attack(object pc) { object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc); AssignCommand(pc, ClearAllActions(TRUE)); AssignCommand(pc, ActionAttack(s)); }
void Taunt(object pc) { object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc); AssignCommand(pc, ClearAllActions(TRUE)); AssignCommand(pc, ActionUseSkill(SKILL_TAUNT, s)); }
void main() {
 object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
 SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
 DelayCommand(1.0, Summon(pc));
 DelayCommand(2.0, VCamera(pc, 90.0, 8.0, 60.0));
 DelayCommand(3.0, Pin(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc)));
 DelayCommand(4.0, Rel(pc, GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc), "summon"));
 DelayCommand(4.5, VShot("a_world"));
 Ring(5.0, "s");                                     // summon: 5..15
 // The default action: a held-button left click on the summon, then the player's state.
 DelayCommand(16.0, Log("hover|0.754,0.525"));
 DelayCommand(16.3, Log("mouse|0.754,0.525|down|left"));
 DelayCommand(16.5, Log("mouse|0.754,0.525|up|left"));
 DelayCommand(17.5, Probe(pc, "click1"));
 DelayCommand(19.0, Probe(pc, "click3"));
 DelayCommand(19.2, VShot("s_9click"));
 DelayCommand(19.5, Attack(pc));
 DelayCommand(20.5, Probe(pc, "attack1"));
 DelayCommand(23.0, Probe(pc, "attack3"));
 DelayCommand(23.2, VShot("s_9attack"));
 DelayCommand(23.5, Taunt(pc));
 DelayCommand(26.0, Probe(pc, "taunt"));
 DelayCommand(26.5, AssignCommand(pc, ClearAllActions(TRUE)));
 DelayCommand(27.0, Gone(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc)));
 DelayCommand(28.0, Familiar(pc));
 DelayCommand(30.0, Rel(pc, GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc), "familiar"));
 Ring(31.0, "f");                                    // familiar: 31..41
 DelayCommand(42.0, Gone(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc)));
 DelayCommand(43.0, Hench(pc));
 DelayCommand(45.0, Rel(pc, GetHenchman(pc), "henchman"));
 Ring(46.0, "h");                                    // henchman: 46..56
 DelayCommand(57.0, Gone(GetHenchman(pc)));
 DelayCommand(58.0, Dominate(pc));
 DelayCommand(60.0, Rel(pc, GetAssociate(ASSOCIATE_TYPE_DOMINATED, pc), "dominated"));
 Ring(61.0, "d");                                    // dominated: 61..71
 DelayCommand(72.0, Gone(GetAssociate(ASSOCIATE_TYPE_DOMINATED, pc)));
 DelayCommand(73.0, Companion(pc));
 DelayCommand(76.0, VShot("z_end"));
 DelayCommand(77.0, VDone());
}

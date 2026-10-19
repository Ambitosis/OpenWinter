#include "visual_inc"
// killxp3: follow-up to killxp/killxp2 (docs/KILL_XP.md). Run at two
// difficulties (visual.py --difficulty 1 and 4): the amounts must not change.
// 1. Challenge ratings just above the halves: 2.505, 2.509, 0.505, 1.505 (rounded by adding 0.49 and truncating, or
//    half down?), and 2.495, 0.5, 1, 2 as controls. Level 1.
// 2. The victim's OnDeath logs her experience: given before or after it?
// 3. Victims that are associates of a hostile NPC: its summoned creature (no experience?) and its henchman.
// 4. A dead henchman beside the victim: does it still take a share?
// 5. Single-precision scale: level 1, CR 1 (50) at scale 21 and 42; level 2, CR 2 (90) at scale 13 (exactly
//    105, 210, 117; one less in single precision).
// 6. Three classes: elf wizard 1 (favoured) / rogue 4 / fighter 2 (fighter two below the highest: 20% off), then
//    fighter 3 (one below: nothing off).
// 7. Her familiar beside her: an associate, a quarter share. (Possessing it by posted keys Num0, Num4, Num1, as
//    familiarpossess does, didn't take here, so the possessed-familiar case is not in this scene.)

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|kx3|" + s); }

object Killer(string sKey)
{
    object pc = GetFirstPC();
    if (sKey == "pc") return pc;
    return GetLocalObject(GetModule(), sKey);
}

void Snap(string sKey)
{
    object o = Killer(sKey);
    SetLocalInt(GetModule(), "xp_" + sKey, GetIsObjectValid(o) ? GetXP(o) : -1);
}

string Delta(string sKey)
{
    object o = Killer(sKey);
    int before = GetLocalInt(GetModule(), "xp_" + sKey);
    if (!GetIsObjectValid(o) || before < 0) return "";
    return "|" + sKey + "=" + IntToString(GetXP(o) - before);
}

void DoKill(object v) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(500, DAMAGE_TYPE_MAGICAL), v); }

void Report(object v, string sLabel)
{
    Log(sLabel + "|cr=" + FloatToString(GetLocalFloat(GetModule(), "cr"), 0, 4) + "|before=" + IntToString(GetLocalInt(GetModule(), "xp_pc"))
        + "|scale=" + IntToString(GetModuleXPScale()) + "|diff=" + IntToString(GetGameDifficulty()) + "|dead=" + IntToString(GetIsDead(v))
        + "|master=" + GetName(GetMaster(v)) + Delta("pc"));
    DestroyObject(v);
}

void KillObject(object v, string sLabel, int nSetXp)
{
    object pc = GetFirstPC();
    if (nSetXp >= 0) SetXP(pc, nSetXp);
    Snap("pc");
    SetLocalFloat(GetModule(), "cr", GetChallengeRating(v));
    SendMessageToPC(pc, "KX3 " + sLabel);
    DelayCommand(0.15, AssignCommand(pc, DoKill(v)));
    DelayCommand(0.75, Report(v, sLabel));
}

void Kill(string sResRef, string sLabel, int nSetXp = 0)
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    object v = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(p.x + 2.5, p.y, p.z), 180.0));
    KillObject(v, sLabel, nSetXp);
}

void Quiet(object o)
{
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
}

// A hostile NPC 6 m away that owns associates.
void Boss()
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    object b = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(pc), Vector(p.x + 6.0, p.y + 3.0, p.z), 180.0), FALSE, "KX3_BOSS");
    Quiet(b);
    ChangeToStandardFaction(b, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(b, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneImmobilize(), b);
    SetMaxHenchmen(4);
    AssignCommand(b, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire"), b, 120.0));
}

void KillSummon()
{
    object b = GetObjectByTag("KX3_BOSS");
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, b);
    Log("summon|valid=" + IntToString(GetIsObjectValid(s)) + "|name=" + GetName(s) + "|type=" + IntToString(GetAssociateType(s)));
    Quiet(s);
    KillObject(s, "assoc|summoned", 0);
}

void KillHenchOfBoss()
{
    object pc = GetFirstPC();
    object b = GetObjectByTag("KX3_BOSS");
    vector p = GetPosition(pc);
    object v = CreateObject(OBJECT_TYPE_CREATURE, "kc8", Location(GetArea(pc), Vector(p.x + 2.5, p.y, p.z), 180.0));
    Quiet(v);
    AddHenchman(b, v);
    Log("henchofboss|master=" + GetName(GetMaster(v)) + "|type=" + IntToString(GetAssociateType(v)));
    KillObject(v, "assoc|henchman", 0);
}

// The same blueprint as the summon, created plainly (a control for its challenge rating).
void KillPlainBadger()
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    object v = CreateObject(OBJECT_TYPE_CREATURE, "nw_s_badgerdire", Location(GetArea(pc), Vector(p.x + 2.5, p.y, p.z), 180.0));
    Quiet(v);
    ChangeToStandardFaction(v, STANDARD_FACTION_HOSTILE);
    KillObject(v, "assoc|plainbadger", 0);
}

void Hire()
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    object h = CreateObject(OBJECT_TYPE_CREATURE, "kxh1", Location(GetArea(pc), Vector(p.x - 2.0, p.y + 2.0, p.z), 0.0), FALSE, "KX3_H1");
    Quiet(h);
    SetLocalObject(GetModule(), "h1", h);
    AddHenchman(pc, h);
}

void KillHench()
{
    object h = GetObjectByTag("KX3_H1");
    AssignCommand(h, SetIsDestroyable(FALSE, FALSE, FALSE));
    DelayCommand(0.2, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), h));
    DelayCommand(1.0, Log("henchdead|dead=" + IntToString(GetIsDead(h)) + "|master=" + GetName(GetMaster(h)) + "|valid=" + IntToString(GetIsObjectValid(h))));
}

void Scale(int n) { SetModuleXPScale(n); Log("scale|" + IntToString(GetModuleXPScale())); }

string Classes(object o)
{
    string s = "";
    int i;
    for (i = 1; i <= 3; i++)
        if (GetClassByPosition(i, o) != CLASS_TYPE_INVALID)
            s += IntToString(GetClassByPosition(i, o)) + "x" + IntToString(GetLevelByPosition(i, o)) + " ";
    return s;
}

void Levels(int nXp, int nClass, int nCount)
{
    object pc = GetFirstPC();
    SetXP(pc, nXp);
    int i;
    for (i = 0; i < nCount; i++) LevelUpHenchman(pc, nClass, TRUE);
    Log("classes|hd=" + IntToString(GetHitDice(pc)) + "|" + Classes(pc) + "|xp=" + IntToString(GetXP(pc)));
}

void FamiliarState(string sWhen)
{
    object pc = GetFirstPC();
    object f = GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc);
    Log("familiar|" + sWhen + "|valid=" + IntToString(GetIsObjectValid(f)) + "|hd=" + IntToString(GetHitDice(f)) + "|xp=" + IntToString(GetXP(f))
        + "|possessed=" + IntToString(GetIsPossessedFamiliar(f)) + "|ispc=" + IntToString(GetIsPC(f)) + "|pchd=" + IntToString(GetHitDice(pc)) + "|pcxp=" + IntToString(GetXP(pc)));
    if (GetIsObjectValid(f)) { SetPlotFlag(f, TRUE); AssignCommand(f, ClearAllActions()); }
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    if (GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 55.0));
    Log("start|xp=" + IntToString(GetXP(pc)) + "|hd=" + IntToString(GetHitDice(pc)) + "|scale=" + IntToString(GetModuleXPScale()) + "|diff=" + IntToString(GetGameDifficulty()));

    float t = 3.0;
    int i;
    // 1. Rounding (level 1: C0 20, C1 50, C2 96, C3 144).
    for (i = 0; i < 8; i++) { DelayCommand(t, Kill("kc" + IntToString(i), "cr|kc" + IntToString(i))); t += 1.0; }
    // 2. OnDeath order (CR 5, level 1: 300).
    DelayCommand(t, Kill("kd", "ondeath")); t += 1.0;
    // 3. Associates of a hostile NPC.
    DelayCommand(t, Boss()); t += 3.0;
    DelayCommand(t, KillSummon()); t += 1.0;
    DelayCommand(t, KillPlainBadger()); t += 1.0;
    DelayCommand(t, KillHenchOfBoss()); t += 1.0;
    // 4. A dead henchman beside the victim (alone CR 5 = 300; a live henchman makes it 240).
    DelayCommand(t, Hire()); t += 0.5;
    DelayCommand(t, Kill("kc8", "hench|alive")); t += 1.0;
    DelayCommand(t, KillHench()); t += 2.0;
    DelayCommand(t, Kill("kc8", "hench|dead")); t += 1.0;
    // 5. Single-precision scale.
    DelayCommand(t, Scale(21)); t += 0.3;
    DelayCommand(t, Kill("kc6", "scale|s21|L1|C1", 0)); t += 1.0;
    DelayCommand(t, Scale(42)); t += 0.3;
    DelayCommand(t, Kill("kc6", "scale|s42|L1|C1", 0)); t += 1.0;
    DelayCommand(t, Scale(13)); t += 0.3;
    DelayCommand(t, Kill("kc7", "scale|s13|L2|C2", 1000)); t += 1.0;
    DelayCommand(t, Scale(47)); t += 0.3;
    DelayCommand(t, Kill("kc6", "scale|s47|L1|C1", 0)); t += 1.0;
    DelayCommand(t, Scale(10)); t += 0.3;
    // 6. Three classes: rogue 4 and fighter 2 on the wizard 1 (level 7: 21000), then fighter 3 (level 8: 28000).
    DelayCommand(t, Levels(21000, CLASS_TYPE_ROGUE, 4)); t += 0.5;
    DelayCommand(t, Levels(21000, CLASS_TYPE_FIGHTER, 2)); t += 0.5;
    DelayCommand(t, Kill("kc8", "multi|w1r4f2|L7", 21000)); t += 1.0;
    DelayCommand(t, Levels(28000, CLASS_TYPE_FIGHTER, 1)); t += 0.5;
    DelayCommand(t, Kill("kc8", "multi|w1r4f3|L8", 28000)); t += 1.0;
    // 7. The familiar (level 9 by experience: 36000; wizard 1, fighter 3, rogue 4: no multiclass penalty).
    DelayCommand(t, SummonFamiliar(pc)); t += 3.0;
    DelayCommand(t, FamiliarState("summoned")); t += 0.5;
    DelayCommand(t, Kill("kc8", "familiar|beside|L9", 36000)); t += 1.5;
    DelayCommand(t + 1.0, VDone());
}

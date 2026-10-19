#include "visual_inc"
// rulesfollowpc: follow-ups of the conditions, stealth and polymorph work, on the player (docs/CONDITIONS.md,
// docs/POLYMORPH.md).
//   f  factions in a module without repute.fac: how NPCs of each standard faction and the player regard each other
//   c  the player charmed by a hostile bandit K 3 m east (a second hostile bandit E 9 m north): what she does, with
//      the transitions of her weapon, Mage Armor, summons and action logged (as cond_charm_ai does for NPCs)
//   d  charmed by K standing 9 m east (E 15 m north)
//   r  her own radial's Special Abilities wedge (W)
//   s  the character sheet with a dagger carrying +1d6 fire, then as a Rakshasa (polymorph 88), and the radial again

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }
void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }
object PC() { return GetFirstPC(); }

float Now()
{
    int nMinPerHour = FloatToInt(HoursToSeconds(1)) / 60;
    return IntToFloat((GetTimeHour() * nMinPerHour + GetTimeMinute()) * 60 + GetTimeSecond()) + IntToFloat(GetTimeMillisecond()) / 1000.0;
}

object Npc(string sResRef, float fDx, float fDy, string sTag, int nFaction)
{
    vector v = GetPositionFromLocation(GetLocalLocation(GetModule(), "HOME"));
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(PC()), Vector(v.x + fDx, v.y + fDy, v.z), 180.0), FALSE, sTag);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    if (nFaction >= 0) ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
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

void Home()
{
    object pc = PC();
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(GetLocalLocation(GetModule(), "HOME")));
}

// ---------------------------------------------------------------- f: factions

void FactionRead(object o, string k)
{
    object pc = PC();
    Note("f|" + k + "|npcToPc=" + IntToString(GetReputation(o, pc)) + "|pcToNpc=" + IntToString(GetReputation(pc, o))
        + "|enemy=" + IntToString(GetIsEnemy(pc, o)) + IntToString(GetIsEnemy(o, pc)));
}

void Factions()
{
    object pc = PC();
    int f;
    for (f = 0; f < 4; f++)
    {
        object o = Npc("nw_bandit001", -6.0 + IntToFloat(f) * 2.0, -8.0, "rf_f", f);
        SetPlotFlag(o, TRUE);
        DelayCommand(0.3, FactionRead(o, "std" + IntToString(f)));
        Note("f|stdToPc" + IntToString(f) + "|" + IntToString(GetStandardFactionReputation(f, pc)));
    }
    object b = Npc("nw_commale", 4.0, -8.0, "rf_f", -1);
    SetPlotFlag(b, TRUE);
    DelayCommand(0.3, FactionRead(b, "commale"));
    object h = Npc("nw_hen_bod_05", 6.0, -8.0, "rf_f", -1);
    SetPlotFlag(h, TRUE);
    DelayCommand(0.3, FactionRead(h, "bod"));
    DelayCommand(1.5, Clear("rf_f"));
}

// ---------------------------------------------------------------- c, d: charm

void Add(string g, string s) { object m = GetModule(); SetLocalString(m, g, GetLocalString(m, g) + " " + s); }

void Watch(object c, string g, float fT0, float fEnd)
{
    float t = Now() - fT0;
    if (t > fEnd) { Note("c|" + g + "|" + GetLocalString(GetModule(), g)); return; }
    string ts = "@" + FloatToString(t, 0, 1);
    string w = GetTag(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, c));
    if (w != GetLocalString(c, g + "w")) { SetLocalString(c, g + "w", w); Add(g, "hand=" + (w == "" ? "-" : w) + ts); }
    int a = GetHasSpellEffect(SPELL_MAGE_ARMOR, c);
    if (a != GetLocalInt(c, g + "a")) { SetLocalInt(c, g + "a", a); Add(g, "armor" + IntToString(a) + ts); }
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, c);
    string sr = GetIsObjectValid(s) ? GetResRef(s) : "-";
    if (sr != GetLocalString(c, g + "s")) { SetLocalString(c, g + "s", sr); Add(g, "summon=" + sr + ts); }
    object at = GetAttackTarget(c);
    string ats = GetIsObjectValid(at) ? GetLocalString(at, "R") : "-";
    if (ats != GetLocalString(c, g + "t")) { SetLocalString(c, g + "t", ats); Add(g, "target=" + ats + ts); }
    int ac = GetCurrentAction(c);
    if (ac != GetLocalInt(c, g + "ac") || !GetLocalInt(c, g + "acset")) { SetLocalInt(c, g + "acset", 1); SetLocalInt(c, g + "ac", ac); Add(g, "a" + IntToString(ac) + ts); }
    DelayCommand(0.1, Watch(c, g, fT0, fEnd));
}

void Stop(object o) { SetCommandable(FALSE, o); }

void Charm(string g, float fK, float fE, float fDur)
{
    object pc = PC();
    object k = Npc("nw_bandit001", fK, 0.0, "rf_" + g, STANDARD_FACTION_HOSTILE);
    SetLocalString(k, "R", "K");
    SetImmortal(k, TRUE);
    object e = Npc("nw_bandit001", 0.0, fE, "rf_" + g, STANDARD_FACTION_HOSTILE);
    SetLocalString(e, "R", "E");
    SetImmortal(e, TRUE);
    // Stripped creatures hit by harmful spells sometimes strike back; kept out of it (as cond_charm_ai).
    Stop(e);
    DelayCommand(4.0, AssignCommand(k, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectCharmed(), pc, fDur)));
    // (Arguments of a delayed call are worked out when it runs, so the start time is taken now.)
    float t0 = Now() + 4.0;
    DelayCommand(4.0, Watch(pc, g, t0, fDur + 1.0));
    DelayCommand(4.5, Stop(k));
    DelayCommand(9.0, VShot(g + "_5s"));
    DelayCommand(fDur + 6.0, Clear("rf_" + g));
}

// ---------------------------------------------------------------- r, s: radial and sheet

void Radial(string sShot)
{
    Tap(96);
    DelayCommand(0.7, Tap(100));
    DelayCommand(1.7, VShot(sShot));
    DelayCommand(2.2, Tap(101));
    DelayCommand(2.6, Tap(101));
}

void Sheet(string sShot)
{
    Tap(67);
    DelayCommand(1.5, VShot(sShot));
    DelayCommand(2.2, Tap(67));
}

void Dagger()
{
    object pc = PC();
    object d = CreateItemOnObject("nw_wswdg001", pc);
    AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE, IP_CONST_DAMAGEBONUS_1d6), d);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, ActionEquipItem(d, INVENTORY_SLOT_RIGHTHAND));
}

void Poly()
{
    object pc = PC();
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectPolymorph(88), pc, 60.0);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetLocalLocation(GetModule(), "HOME", GetLocation(pc));
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 50.0));
    float t = 2.0;
    DelayCommand(t, Factions()); t += 2.0;
    DelayCommand(t, Charm("c", 3.0, 9.0, 30.0)); t += 37.0;
    DelayCommand(t, Home()); t += 1.0;
    DelayCommand(t, Charm("d", 9.0, 15.0, 20.0)); t += 27.0;
    DelayCommand(t, Home()); t += 2.0;
    DelayCommand(t, Radial("r_radial_abilities")); t += 3.5;
    DelayCommand(t, Dagger()); t += 2.5;
    DelayCommand(t, Sheet("s_sheet_dagger")); t += 3.0;
    DelayCommand(t, Poly()); t += 3.0;
    DelayCommand(t, Sheet("s_sheet_rakshasa")); t += 3.0;
    DelayCommand(t, Radial("s_radial_rakshasa")); t += 3.5;
    DelayCommand(t, VDone());
}

// Weapon stances (scenes weaponstance1..3): which clip the original plays for each held weapon, identified
// black-box with the public ReplaceObjectAnimation. Twelve stock bandits (or halflings) each hold one weapon set,
// in two rows of six facing the camera three-quarter; in combat each faces a skeleton dummy 1.3 m to its east
// (side on to the camera, weapon arm nearest). Each phase replaces a group of candidate clips on every actor with an unmistakable
// marker clip (sitcross: seated, castoutlp: arms held out forward, castuplp: arms raised, castarealp: area cast;
// a knockdown clip such as kdbckps, or getlowlp, leaves the actor lying or crouching even after the replacement is cleared); the actors that
// change in the frame are the ones that played the replaced clip. Phases: peace idle, walking and running (arm
// overlays), combat ready stances, swings, then the actors defending (parry, shield, dodge, damage), and baseline
// close-ups of every lane at peace and in combat.
#include "visual_inc"

const string M1 = "sitcross";
const string M2 = "castoutlp";
const string M3 = "castuplp";
const string M4 = "castarealp";

// Every clip a phase may replace; ClearAll restores them all.
const string WS_ALL = "pause1,plpause1,pausesh,xbowr,walk,walk_swordr,walk_swordl,walk_shieldl,walk_bowl,run,run_swordr,run_swordl,run_shieldl,run_bowl,1hreadyr,1hreadyl,2hreadyr,2hreadyl,plreadyr,plreadyl,2wreadyr,2wreadyl,nwreadyr,nwreadyl,bowrdy,xbowrdy,1hslashl,1hslashr,1hstab,1hcloseh,1hclosel,1hreach,1hslasho,2hslashl,2hslashr,2hstab,2hcloseh,2hclosel,2hreach,2hslasho,plslashl,plslashr,plstab,plcloseh,plclosel,plreach,plslasho,2wslashl,2wslashr,2wstab,2wcloseh,2wclosel,2wreach,2wslasho,nwslashl,nwslashr,nwstab,nwcloseh,nwclosel,nwreach,nwslasho,nwkickl,nwkickr,nwkicks,bowshot,xbowshot,throwr,1hparryl,1hparryr,2hparryl,2hparryr,plparryl,plparryr,2wparryl,2wparryr,shieldl,dodges,dodgelr,damages,damagel,damager,damageb";

object WSMod() { return GetModule(); }
object WSA(int i) { return GetLocalObject(WSMod(), "ws_a" + IntToString(i)); }
object WSD(int i) { return GetLocalObject(WSMod(), "ws_d" + IntToString(i)); }
int WSN() { return GetLocalInt(WSMod(), "ws_n"); }
void WSNote(string s) { WriteTimestampedLogEntry("VISUAL|note|ws|" + s); }

// Start position: back row (i < 6) 2 m north of the hidden player, front row 2.5 m south; 2.6 m apart.
const float WS_FACE = 300.0;
vector WSStart(int i)
{
    vector p = GetPositionFromLocation(GetLocalLocation(WSMod(), "ws_pc"));
    // WSRunSingle: one row of four, 3.4 m apart, 2 m north of the player.
    if (GetLocalInt(WSMod(), "ws_single")) return Vector(p.x - 5.8 + IntToFloat(i) * 3.4, p.y + 2.0, p.z);
    return Vector(p.x - 6.5 + IntToFloat(i % 6) * 2.6, p.y + (i < 6 ? 2.0 : -2.5), p.z);
}
location WSAt(vector v, float facing) { return Location(GetArea(GetFirstPC()), v, facing); }

string WSField(string spec, int n)
{
    int i;
    for (i = 0; i < n; i++)
    {
        int c = FindSubString(spec, ",");
        if (c < 0) return "";
        spec = GetSubString(spec, c + 1, GetStringLength(spec));
    }
    int c = FindSubString(spec, ",");
    return c < 0 ? spec : GetSubString(spec, 0, c);
}

void WSQuiet(object o)
{
    int i; for (i = 5000; i <= 5013; i++) SetEventScript(o, i, "");
    AssignCommand(o, ClearAllActions(TRUE));
    SetAILevel(o, AI_LEVEL_NORMAL);
}

void WSStrip(object o)
{
    object r = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o); if (GetIsObjectValid(r)) DestroyObject(r);
    object l = GetItemInSlot(INVENTORY_SLOT_LEFTHAND, o); if (GetIsObjectValid(l)) DestroyObject(l);
}

void WSGive(object o, string resref, int slot, int stack)
{
    if (resref == "") return;
    object it = CreateItemOnObject(resref, o, stack);
    AssignCommand(o, ActionEquipItem(it, slot));
}

// spec: "blueprint,right,left,ammo" (ammo: arrows/bolts/bullets resref, or a stack size for a thrown right hand).
void WSSpawn(int i, string spec)
{
    string bp = WSField(spec, 0);
    object o = CreateObject(OBJECT_TYPE_CREATURE, bp, WSAt(WSStart(i), WS_FACE), FALSE, "WS_A" + IntToString(i));
    SetLocalObject(WSMod(), "ws_a" + IntToString(i), o);
    SetLocalString(o, "spec", spec);
    WSQuiet(o);
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetPlotFlag(o, FALSE); SetImmortal(o, TRUE);
    WSStrip(o);
}

void WSArm(int i)
{
    object o = WSA(i); string spec = GetLocalString(o, "spec");
    string right = WSField(spec, 1), left = WSField(spec, 2), ammo = WSField(spec, 3);
    int stack = StringToInt(ammo);
    WSGive(o, right, INVENTORY_SLOT_RIGHTHAND, stack > 0 ? stack : 1);
    DelayCommand(1.2, WSGive(o, left, INVENTORY_SLOT_LEFTHAND, 1));
    if (stack == 0 && ammo != "")
    {
        int slot = FindSubString(ammo, "wamar") >= 0 ? INVENTORY_SLOT_ARROWS : FindSubString(ammo, "wambo") >= 0 ? INVENTORY_SLOT_BOLTS : INVENTORY_SLOT_BULLETS;
        DelayCommand(2.0, WSGive(o, ammo, slot, 99));
    }
}

void WSReport()
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        object o = WSA(i);
        WSNote("actor|" + IntToString(i) + "|spec=" + GetLocalString(o, "spec")
            + "|right=" + IntToString(GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o)))
            + "|left=" + IntToString(GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_LEFTHAND, o)))
            + "|size=" + IntToString(GetCreatureSize(o)) + "|app=" + IntToString(GetAppearanceType(o)));
    }
}

void WSReplace(string old, string marker)
{
    int i; for (i = 0; i < WSN(); i++) ReplaceObjectAnimation(WSA(i), old, marker);
}

// Replaces each comma-separated clip with the marker.
void WSReplaceList(string clips, string marker)
{
    while (clips != "")
    {
        int c = FindSubString(clips, ",");
        string clip = c < 0 ? clips : GetSubString(clips, 0, c);
        clips = c < 0 ? "" : GetSubString(clips, c + 1, GetStringLength(clips));
        WSReplace(clip, marker);
    }
}

void WSClearAll() { WSReplaceList(WS_ALL, ""); }

void WSPhase(string name, string c1, string c2, string c3, string c4)
{
    WSClearAll();
    WSReplaceList(c1, M1); WSReplaceList(c2, M2); WSReplaceList(c3, M3); WSReplaceList(c4, M4);
    WSNote("phase|" + name + "|M1=" + c1 + "|M2=" + c2 + "|M3=" + c3 + "|M4=" + c4);
}

void WSMove(int far, float dist, int run)
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        object o = WSA(i); vector v = WSStart(i);
        if (far) v.y += dist;
        AssignCommand(o, ClearAllActions());
        AssignCommand(o, ActionMoveToLocation(WSAt(v, far ? 90.0 : 270.0), run));
    }
}

void WSHome()
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        object o = WSA(i);
        AssignCommand(o, ClearAllActions(TRUE));
        AssignCommand(o, JumpToLocation(WSAt(WSStart(i), WS_FACE)));
    }
}

void WSWide() { VCamera(GetFirstPC(), 90.0, 15.0, 52.0); }

void WSDummies()
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        vector v = WSStart(i); v.x += 1.3;
        object d = CreateObject(OBJECT_TYPE_CREATURE, "nw_skeleton", WSAt(v, 180.0), FALSE, "WS_D" + IntToString(i));
        SetLocalObject(WSMod(), "ws_d" + IntToString(i), d);
        WSQuiet(d); WSStrip(d);
        ChangeToStandardFaction(d, STANDARD_FACTION_COMMONER);
        SetPlotFlag(d, FALSE); SetImmortal(d, TRUE);
    }
}

void WSActorsAttack()
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        object o = WSA(i);
        ChangeToStandardFaction(o, STANDARD_FACTION_HOSTILE);
        ChangeToStandardFaction(WSD(i), STANDARD_FACTION_COMMONER);
        AssignCommand(WSD(i), ClearAllActions(TRUE));
        AssignCommand(o, ClearAllActions(TRUE));
        AssignCommand(o, ActionAttack(WSD(i)));
    }
}

void WSDummiesAttack()
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        object o = WSA(i), d = WSD(i);
        AssignCommand(o, ClearAllActions(TRUE));
        ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
        ChangeToStandardFaction(d, STANDARD_FACTION_HOSTILE);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectModifyAttacks(5)), d);
        AssignCommand(d, ActionAttack(o));
    }
}

void WSManyAttacks(int on)
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        object o = WSA(i);
        if (on) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectModifyAttacks(5)), o);
        else
        {
            effect e = GetFirstEffect(o);
            while (GetIsEffectValid(e)) { if (GetEffectType(e) == EFFECT_TYPE_MODIFY_ATTACKS) RemoveEffect(o, e); e = GetNextEffect(o); }
        }
    }
}

void WSStopAll()
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        AssignCommand(WSD(i), ClearAllActions(TRUE));
        ChangeToStandardFaction(WSD(i), STANDARD_FACTION_COMMONER);
        effect e = GetFirstEffect(WSD(i));
        while (GetIsEffectValid(e)) { RemoveEffect(WSD(i), e); e = GetNextEffect(WSD(i)); }
    }
}

// Close-up of three actors of one row: the player (hidden, the camera's anchor) steps in beside the middle one.
void WSClose(int row, int third)
{
    vector v = WSStart(row * 6 + third * 3 + 1); v.x += 0.65; v.y -= 1.2;
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(WSAt(v, 90.0)));
}

void WSCloseCam() { VCamera(GetFirstPC(), 90.0, 6.0, 64.0); }

void WSPcHome()
{
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(GetLocalLocation(WSMod(), "ws_pc")));
}

// Four close-ups (three actors each), starting at t; returns the time after them.
float WSCloseUps(float t, string prefix)
{
    int c, r;
    for (c = 0; c < 2; c++) for (r = 0; r < 2; r++)
    {
        DelayCommand(t, WSClose(c, r));
        DelayCommand(t + 0.3, WSCloseCam());
        DelayCommand(t + 1.0, VShot(prefix + "_" + IntToString(c) + IntToString(r)));
        t += 1.7;
    }
    return t;
}

// Whole-row views: the player steps 1.2 m in front of the row's middle; three passes over both rows.
void WSRow(int row)
{
    vector v = WSStart(row * 6); v.x += 6.5; v.y -= 1.2;
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(WSAt(v, 90.0)));
}
void WSRowCam() { VCamera(GetFirstPC(), 90.0, 10.0, 62.0); }
float WSRowShots(float t, string prefix)
{
    int pass, row;
    for (pass = 0; pass < 3; pass++) for (row = 0; row < 2; row++)
    {
        DelayCommand(t, WSRow(row));
        DelayCommand(t + 0.3, WSRowCam());
        DelayCommand(t + 1.0, VShot(prefix + "_r" + IntToString(row) + "p" + IntToString(pass)));
        t += 1.7;
    }
    return t;
}

// Replaces the phase's clips, (re)starts the fight (actors attack, or with defend the dummies attack them) and
// takes the four close-ups.
float WSAttackPhase(float t, string name, string c1, string c2, string c3, string c4, int defend)
{
    DelayCommand(t, WSPhase(name, c1, c2, c3, c4));
    if (defend) DelayCommand(t + 0.1, WSDummiesAttack());
    else DelayCommand(t + 0.1, WSActorsAttack());
    return WSCloseUps(t + 1.0, name) + 0.3;
}

float WSShots(float t, string name, int count, float gap)
{
    int k;
    for (k = 0; k < count; k++) DelayCommand(t + IntToFloat(k) * gap, VShot(name + "_" + IntToString(k)));
    return t + IntToFloat(count) * gap;
}

// The whole scene. specs: up to twelve "blueprint,right,left,ammo" entries separated by ";".
// bPeace FALSE skips the peace phases: replacing an arm clip that is playing (plpause1, xbowr) and clearing it
// again leaves the marker on until the arm clip restarts, which spoils the later phases for those actors.
void WSRun(object pc, string specs, int bPeace = TRUE)
{
    VBegin(pc); VHidePC(pc);
    SetLocalLocation(WSMod(), "ws_pc", GetLocation(pc));
    int n = 0;
    while (specs != "" && n < 12)
    {
        int c = FindSubString(specs, ";");
        string spec = c < 0 ? specs : GetSubString(specs, 0, c);
        specs = c < 0 ? "" : GetSubString(specs, c + 1, GetStringLength(specs));
        DelayCommand(0.2, WSSpawn(n, spec));
        DelayCommand(0.8, WSArm(n));
        n++;
    }
    SetLocalInt(WSMod(), "ws_n", n);
    DelayCommand(1.0, WSWide());
    DelayCommand(4.5, WSReport());
    float t = 5.0;
    if (bPeace)
    {
        // Peace: idle and its arm overlays.
        DelayCommand(t, VShot("a0_idle")); t += 1.0;
        t = WSCloseUps(t, "a1_idleclose");
        DelayCommand(t, WSPcHome()); DelayCommand(t + 0.3, WSWide()); t += 1.2;
        DelayCommand(t, WSPhase("idle_a", "pause1", "", "plpause1", "")); t += 1.2;
        t = WSShots(t, "a2_idle_pause1_plpause1", 2, 0.8) + 0.2;
        DelayCommand(t, WSPhase("idle_b", "", "", "xbowr", "")); t += 1.2;
        t = WSShots(t, "a3_idle_xbowr", 2, 0.8) + 0.2;
        DelayCommand(t, WSPhase("idle_c", "", "", "pausesh", "")); t += 1.2;
        t = WSShots(t, "a4_idle_pausesh", 2, 0.8) + 0.2;
        // Walking and running: one candidate per phase (castuplp = M3), alternating directions.
        string walks = "base,walk_swordr,walk_swordl,walk_shieldl,walk_bowl,plpause1,xbowr,pausesh";
        int k;
        for (k = 0; k < 8; k++)
        {
            string clip = WSField(walks, k);
            DelayCommand(t, WSPhase("walk" + IntToString(k), "", "", clip == "base" ? "" : clip, ""));
            DelayCommand(t + 0.3, WSMove(k % 2 == 0, 3.5, FALSE));
            WSShots(t + 1.2, "b" + IntToString(k) + "_walk_" + clip, 2, 0.8);
            t += 3.4;
        }
        string runs = "base,run_swordr,run_swordl,run_shieldl,run_bowl,plpause1";
        for (k = 0; k < 6; k++)
        {
            string clip = WSField(runs, k);
            DelayCommand(t, WSPhase("run" + IntToString(k), "", "", clip == "base" ? "" : clip, ""));
            DelayCommand(t + 0.3, WSMove(k % 2 == 0, 5.0, TRUE));
            WSShots(t + 0.8, "c" + IntToString(k) + "_run_" + clip, 2, 0.5);
            t += 2.8;
        }

    }
    DelayCommand(t, WSClearAll());
    DelayCommand(t, WSHome()); t += 1.0;
    // Combat: actors attack their own dummy; every phase restarts the attack after replacing its clips. Two
    // unmistakable markers per phase: M1 sitcross (seated) and M3 castuplp (arms straight up).
    DelayCommand(t, WSDummies()); t += 1.0;
    t = WSAttackPhase(t, "d0_combat", "", "", "", "", 0);
    // The ready stance alternates between the r and l clips (a left slash ends in the l one).
    t = WSAttackPhase(t, "d1_1hready_2hready", "1hreadyr,1hreadyl", "", "2hreadyr,2hreadyl", "", 0);
    t = WSAttackPhase(t, "d2_plready_2wready", "plreadyr,plreadyl", "", "2wreadyr,2wreadyl", "", 0);
    t = WSAttackPhase(t, "d3_nwready_bowrdy", "nwreadyr,nwreadyl", "", "bowrdy", "", 0);
    t = WSAttackPhase(t, "d4_xbowrdy", "xbowrdy", "", "", "", 0);
    DelayCommand(t, WSManyAttacks(TRUE));
    t = WSAttackPhase(t, "e0_1h_2h", "1hslashl,1hslashr,1hstab,1hcloseh,1hclosel,1hreach,1hslasho", "",
        "2hslashl,2hslashr,2hstab,2hcloseh,2hclosel,2hreach,2hslasho", "", 0);
    t = WSAttackPhase(t, "e1_pl_2w", "plslashl,plslashr,plstab,plcloseh,plclosel,plreach,plslasho", "",
        "2wslashl,2wslashr,2wstab,2wcloseh,2wclosel,2wreach,2wslasho", "", 0);
    t = WSAttackPhase(t, "e2_nw_bowshot", "nwslashl,nwslashr,nwstab,nwcloseh,nwclosel,nwreach,nwslasho,nwkickl,nwkickr,nwkicks", "",
        "bowshot", "", 0);
    t = WSAttackPhase(t, "e3_xbowshot_throwr", "xbowshot", "", "throwr", "", 0);
    DelayCommand(t, WSManyAttacks(FALSE));
    // Defending: the dummies attack the actors.
    t = WSAttackPhase(t, "f0_defend", "", "", "", "", 1);
    t = WSAttackPhase(t, "f1_parry_shield", "1hparryl,1hparryr,2hparryl,2hparryr,plparryl,plparryr,2wparryl,2wparryr", "", "shieldl", "", 1);
    t = WSAttackPhase(t, "f2_dodge_damage", "dodges,dodgelr", "", "damages,damagel,damager,damageb", "", 1);
    DelayCommand(t, WSStopAll());
    // Baseline combat close-ups (one attack a round: mostly the ready stance).
    DelayCommand(t + 0.5, WSPhase("g1_combatclose", "", "", "", ""));
    DelayCommand(t + 0.6, WSActorsAttack());
    t = WSCloseUps(t + 1.5, "g1_combatclose");
    DelayCommand(t + 0.5, VDone());
    WSNote("scheduled|end=" + FloatToString(t, 0, 1));
}

// One phase of WSRunSingle: every listed clip becomes sitcross; three wide shots.
float WSSinglePhase(float t, string name, string clips, int defend)
{
    DelayCommand(t, WSPhase(name, clips, "", "", ""));
    if (defend) DelayCommand(t + 0.1, WSDummiesAttack());
    else DelayCommand(t + 0.1, WSActorsAttack());
    return WSShots(t + 1.2, name, 3, 1.0) + 0.2;
}

// Four actors, side by side and large in the frame, one candidate group (to sitcross) per phase: which ready
// stance, swing set and defence each weapon plays.
void WSRunSingle(object pc, string specs)
{
    VBegin(pc); VHidePC(pc);
    SetLocalInt(WSMod(), "ws_single", 1);
    SetLocalLocation(WSMod(), "ws_pc", GetLocation(pc));
    int n = 0;
    while (specs != "" && n < 4)
    {
        int c = FindSubString(specs, ";");
        string spec = c < 0 ? specs : GetSubString(specs, 0, c);
        specs = c < 0 ? "" : GetSubString(specs, c + 1, GetStringLength(specs));
        DelayCommand(0.2, WSSpawn(n, spec));
        DelayCommand(0.8, WSArm(n));
        n++;
    }
    SetLocalInt(WSMod(), "ws_n", n);
    DelayCommand(1.0, VCamera(pc, 90.0, 9.5, 62.0));
    DelayCommand(4.5, WSReport());
    float t = 5.0;
    DelayCommand(t, VShot("a0_idle")); t += 1.0;
    DelayCommand(t, WSDummies()); t += 1.0;
    t = WSSinglePhase(t, "b0_combat", "", 0);
    t = WSSinglePhase(t, "b1_ready_1h", "1hreadyr,1hreadyl", 0);
    t = WSSinglePhase(t, "b2_ready_2h", "2hreadyr,2hreadyl", 0);
    t = WSSinglePhase(t, "b3_ready_pl", "plreadyr,plreadyl", 0);
    t = WSSinglePhase(t, "b4_ready_2w", "2wreadyr,2wreadyl", 0);
    t = WSSinglePhase(t, "b5_ready_nw", "nwreadyr,nwreadyl", 0);
    t = WSSinglePhase(t, "b6_ready_bow_xbow", "bowrdy,xbowrdy", 0);
    DelayCommand(t, WSManyAttacks(TRUE));
    t = WSSinglePhase(t, "c1_swing_1h", "1hslashl,1hslashr,1hstab,1hcloseh,1hclosel,1hreach,1hslasho", 0);
    t = WSSinglePhase(t, "c2_swing_2h", "2hslashl,2hslashr,2hstab,2hcloseh,2hclosel,2hreach,2hslasho", 0);
    t = WSSinglePhase(t, "c3_swing_pl", "plslashl,plslashr,plstab,plcloseh,plclosel,plreach,plslasho", 0);
    t = WSSinglePhase(t, "c4_swing_2w", "2wslashl,2wslashr,2wstab,2wcloseh,2wclosel,2wreach,2wslasho", 0);
    t = WSSinglePhase(t, "c5_swing_nw", "nwslashl,nwslashr,nwstab,nwcloseh,nwclosel,nwreach,nwslasho,nwkickl,nwkickr,nwkicks", 0);
    t = WSSinglePhase(t, "c6_shot_throw", "bowshot,xbowshot,throwr", 0);
    t = WSSinglePhase(t, "c7_swing_close_reach", "1hcloseh,1hclosel,1hreach,2hcloseh,2hclosel,2hreach,plcloseh,plclosel,plreach,2wcloseh,2wclosel,2wreach", 0);
    DelayCommand(t, WSManyAttacks(FALSE));
    t = WSSinglePhase(t, "d0_defend", "", 1);
    t = WSSinglePhase(t, "d1_parry", "1hparryl,1hparryr,2hparryl,2hparryr,plparryl,plparryr,2wparryl,2wparryr", 1);
    t = WSSinglePhase(t, "d2_shield", "shieldl", 1);
    t = WSSinglePhase(t, "d3_dodge", "dodges,dodgelr", 1);
    t = WSSinglePhase(t, "d4_damage", "damages,damagel,damager,damageb", 1);
    DelayCommand(t, WSClearAll());
    DelayCommand(t + 1.0, VDone());
}

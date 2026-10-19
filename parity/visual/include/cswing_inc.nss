// Creature swings (scenes creatureswing, creatureswing2): which clips the original plays for non-humanoid creatures
// (the creature animation set: ca1slashl, creadyr, cdamages ...) and how it picks among them, identified black-box
// with the public ReplaceObjectAnimation. Four creatures side by side (weaponstance_inc's single row), each with one
// set of creature weapons, attack a skeleton dummy 1.3 m to their east; each phase replaces one group of candidate
// clips on every actor with the marker ckdbckdie (the creature falls on its side, a clip every creature model has);
// the actors that fall played a replaced clip. Then the dummies attack the creatures (parry, dodge, damage).
#include "weaponstance_inc"

const string CS_MARK = "ckdbckdie";
const string CS_ALL = "creadyr,creadyl,ca1slashl,ca1slashr,ca1stab,ca2slashl,ca2slashr,ca2stab,creach,ccloseh,cclosel,cparryl,cparryr,cparrys,cdodgelr,cdodges,cdamagel,cdamager,cdamages,nwreadyr,nwreadyl,nwslashl,nwslashr,nwstab,nwcloseh,nwclosel,nwreach,nwslasho,nwkickl,nwkickr,nwkicks,1hslashl,1hslashr,1hstab,dodges,dodgelr,damages,damagel,damager";

void CSPhase(string name, string clips)
{
    WSReplaceList(CS_ALL, "");
    WSReplaceList(clips, CS_MARK);
    WSNote("phase|" + name + "|M=" + clips);
}

// keep: which creature weapons stay ("lrb": left claw, right claw, bite; "" none, "*" as the blueprint has them).
void CSWeapons(int i, string keep)
{
    object o = WSA(i);
    if (keep == "*") return;
    if (FindSubString(keep, "l") < 0) DestroyObject(GetItemInSlot(INVENTORY_SLOT_CWEAPON_L, o));
    if (FindSubString(keep, "r") < 0) DestroyObject(GetItemInSlot(INVENTORY_SLOT_CWEAPON_R, o));
    if (FindSubString(keep, "b") < 0) DestroyObject(GetItemInSlot(INVENTORY_SLOT_CWEAPON_B, o));
}

// After the spawn (DelayCommand evaluates its arguments when it is scheduled, so the actor is looked up here).
void CSSetup(int i)
{
    string keep = GetLocalString(WSMod(), "cs_keep" + IntToString(i));
    SetLocalString(WSA(i), "keep", keep);
    CSWeapons(i, keep);
}

// Each actor's distance to its dummy and facing, and the dummy's (for relating the clip to the geometry).
void CSDistances(string tag)
{
    string line = "dist|" + tag;
    int i;
    for (i = 0; i < WSN(); i++)
    {
        object o = WSA(i), d = WSD(i);
        vector a = GetPosition(o), b = GetPosition(d);
        line += "|" + IntToString(i) + ":" + FloatToString(GetDistanceBetween(o, d), 0, 2)
            + "@" + FloatToString(a.x - b.x, 0, 2) + "," + FloatToString(a.y - b.y, 0, 2) + " f" + FloatToString(GetFacing(o), 0, 0);
    }
    WSNote(line);
}

void CSReport()
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        object o = WSA(i);
        WSNote("actor|" + IntToString(i) + "|spec=" + GetLocalString(o, "spec") + "|keep=" + GetLocalString(o, "keep")
            + "|cl=" + GetResRef(GetItemInSlot(INVENTORY_SLOT_CWEAPON_L, o))
            + "|cr=" + GetResRef(GetItemInSlot(INVENTORY_SLOT_CWEAPON_R, o))
            + "|cb=" + GetResRef(GetItemInSlot(INVENTORY_SLOT_CWEAPON_B, o))
            + "|size=" + IntToString(GetCreatureSize(o)) + "|app=" + IntToString(GetAppearanceType(o)));
    }
}

float CSPhaseShots(float t, string name, string clips, int defend, int shots)
{
    DelayCommand(t, CSPhase(name, clips));
    if (defend) DelayCommand(t + 0.1, WSDummiesAttack());
    else DelayCommand(t + 0.1, WSActorsAttack());
    return WSShots(t + 1.2, name, shots, 0.9) + 0.2;
}

// A phase sampled by the harness's bursts (VISUAL|burst: 5 s at 6 frames a second each), for counting how often
// each clip plays: the actors that lie down in a frame are playing a replaced clip.
float CSPhaseBursts(float t, string name, string clips, int defend, int bursts)
{
    DelayCommand(t, CSPhase(name, clips));
    if (defend) DelayCommand(t + 0.1, WSDummiesAttack());
    else DelayCommand(t + 0.1, WSActorsAttack());
    t += 1.5;
    int k;
    for (k = 0; k < bursts; k++)
    {
        DelayCommand(t - 0.1, CSDistances(name + "_" + IntToString(k)));
        DelayCommand(t + 2.5, CSDistances(name + "_" + IntToString(k) + "m"));
        DelayCommand(t, WriteTimestampedLogEntry("VISUAL|burst|" + name + "_" + IntToString(k) + "|5|6"));
        t += 5.6;
    }
    return t + 0.2;
}

void CSShiftCamera(object pc)
{
    vector v = GetPosition(pc);
    VMovePC(pc, v.x - 1.3, v.y, v.z, 90.0);
}

// specs: up to four "blueprint,,,|keep" entries separated by ";".
// Dummies at the given distances east (or "n": north) of their rooted actors: "1.0,1.6,2.2,2.8".
void CSDummiesAt(string dists)
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        // "n2.9": north of the actor instead (rows of actors 3.4 m apart leave no room east for long distances).
        string f = WSField(dists, i);
        int north = GetStringLeft(f, 1) == "n";
        if (north) f = GetSubString(f, 1, 10);
        vector v = WSStart(i);
        if (north) v.y += StringToFloat(f); else v.x += StringToFloat(f);
        object d = CreateObject(OBJECT_TYPE_CREATURE, "nw_skeleton", WSAt(v, north ? 270.0 : 180.0), FALSE, "WS_D" + IntToString(i));
        SetLocalObject(WSMod(), "ws_d" + IntToString(i), d);
        WSQuiet(d); WSStrip(d);
        ChangeToStandardFaction(d, STANDARD_FACTION_COMMONER);
        SetPlotFlag(d, FALSE); SetImmortal(d, TRUE);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectCutsceneImmobilize()), d);
        object o = WSA(i);
        AssignCommand(o, ClearAllActions(TRUE));
        AssignCommand(o, JumpToLocation(WSAt(WSStart(i), north ? 90.0 : 0.0)));
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectCutsceneImmobilize()), o);
    }
}

void CSRun(object pc, string specs, int bShort = FALSE, string dists = "")
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
        int bar = FindSubString(spec, "|");
        string keep = bar < 0 ? "*" : GetSubString(spec, bar + 1, GetStringLength(spec));
        if (bar >= 0) spec = GetSubString(spec, 0, bar);
        DelayCommand(0.2, WSSpawn(n, spec));
        SetLocalString(WSMod(), "cs_keep" + IntToString(n), keep);
        n++;
    }
    SetLocalInt(WSMod(), "ws_n", n);
    int i;
    for (i = 0; i < n; i++) DelayCommand(0.8, CSSetup(i));
    // The creatures back off to their appearance.2da PREFATCKDIST west of their dummies: centre the camera on that.
    DelayCommand(0.5, CSShiftCamera(pc));
    DelayCommand(1.0, VCamera(pc, 90.0, 9.5, 62.0));
    DelayCommand(4.5, CSReport());
    float t = 5.0;
    DelayCommand(t, VShot("a0_idle")); t += 1.0;
    if (dists != "") DelayCommand(t, CSDummiesAt(dists)); else DelayCommand(t, WSDummies());
    t += 1.0;
    t = CSPhaseShots(t, "b0_combat", "", 0, 3);
    t = CSPhaseBursts(t, "b1_ready", "creadyr,creadyl", 0, 1);
    DelayCommand(t, WSManyAttacks(TRUE));
    if (bShort == 2)
    {
        // Which of the three ca1 clips each set of creature weapons plays (actors out of close range).
        t = CSPhaseBursts(t, "f1_a1slashl", "ca1slashl", 0, 2);
        t = CSPhaseBursts(t, "f2_a1slashr", "ca1slashr", 0, 2);
        t = CSPhaseBursts(t, "f3_a1stab", "ca1stab", 0, 2);
        t = CSPhaseBursts(t, "f4_a2_reach", "ca2slashl,ca2slashr,ca2stab,creach", 0, 1);
        t = CSPhaseBursts(t, "f5_all", "ca1slashl,ca1slashr,ca1stab,ca2slashl,ca2slashr,ca2stab,creach,ccloseh,cclosel", 0, 1);
        DelayCommand(t, WSManyAttacks(FALSE));
        t = CSPhaseBursts(t, "g1_a1slashl_single", "ca1slashl", 0, 1);
        t = CSPhaseBursts(t, "g3_a1stab_single", "ca1stab", 0, 1);
        DelayCommand(t, WSStopAll());
        DelayCommand(t, WSReplaceList(CS_ALL, ""));
        DelayCommand(t + 1.0, VDone());
        return;
    }
    if (bShort)
    {
        t = CSPhaseBursts(t, "c1_a1", "ca1slashl,ca1slashr,ca1stab", 0, 2);
        t = CSPhaseBursts(t, "c4_a2", "ca2slashl,ca2slashr,ca2stab", 0, 1);
        t = CSPhaseBursts(t, "c5_reach", "creach", 0, 1);
        t = CSPhaseBursts(t, "c6_closeh", "ccloseh", 0, 2);
        t = CSPhaseBursts(t, "c7_closel", "cclosel", 0, 1);
        DelayCommand(t, WSManyAttacks(FALSE));
        t = CSPhaseBursts(t, "e1_a1_single", "ca1slashl,ca1slashr,ca1stab", 0, 2);
        t = CSPhaseBursts(t, "e6_closeh_single", "ccloseh", 0, 2);
        DelayCommand(t, WSStopAll());
        DelayCommand(t, WSReplaceList(CS_ALL, ""));
        DelayCommand(t + 1.0, VDone());
        return;
    }
    t = CSPhaseBursts(t, "c0_all", "ca1slashl,ca1slashr,ca1stab,ca2slashl,ca2slashr,ca2stab,creach,ccloseh,cclosel", 0, 2);
    t = CSPhaseBursts(t, "c1_a1slashl", "ca1slashl", 0, 2);
    t = CSPhaseBursts(t, "c2_a1slashr", "ca1slashr", 0, 2);
    t = CSPhaseBursts(t, "c3_a1stab", "ca1stab", 0, 2);
    t = CSPhaseBursts(t, "c4_a2", "ca2slashl,ca2slashr,ca2stab", 0, 2);
    t = CSPhaseBursts(t, "c5_reach", "creach", 0, 2);
    t = CSPhaseBursts(t, "c6_closeh", "ccloseh", 0, 2);
    t = CSPhaseBursts(t, "c7_closel", "cclosel", 0, 2);
    t = CSPhaseBursts(t, "c8_humanoid", "nwslashl,nwslashr,nwstab,nwcloseh,nwclosel,nwreach,nwslasho,nwkickl,nwkickr,nwkicks,1hslashl,1hslashr,1hstab", 0, 1);
    DelayCommand(t, WSManyAttacks(FALSE));
    t = CSPhaseBursts(t, "d0_parry", "cparryl,cparryr,cparrys", 1, 1);
    t = CSPhaseBursts(t, "d1_dodge", "cdodgelr,cdodges", 1, 1);
    t = CSPhaseBursts(t, "d2_damage", "cdamagel,cdamager,cdamages", 1, 1);
    t = CSPhaseBursts(t, "d3_humanoid", "dodges,dodgelr,damages,damagel,damager", 1, 1);
    DelayCommand(t, WSStopAll());
    DelayCommand(t, WSReplaceList(CS_ALL, ""));
    DelayCommand(t + 1.0, VDone());
    WSNote("scheduled|end=" + FloatToString(t, 0, 1));
}

// Where creatures with creature weapons stop to fight: a dire badger, a wolf, a badger and a giant spider each
// close on a skeleton dummy from 8 m away then again from 1 m away; the distances are
// logged as they settle (VISUAL|note|ws|dist). Relates the stand-off to appearance.2da PREFATCKDIST and HITDIST.
#include "cswing_inc"
void Approach()
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        object o = WSA(i), d = WSD(i);
        ChangeToStandardFaction(o, STANDARD_FACTION_HOSTILE);
        AssignCommand(o, ClearAllActions(TRUE));
        AssignCommand(o, ActionAttack(d));
    }
}
void Place(string dists)
{
    int i;
    for (i = 0; i < WSN(); i++)
    {
        if (GetIsObjectValid(WSD(i))) DestroyObject(WSD(i));
        vector v = WSStart(i); v.y += StringToFloat(WSField(dists, i));
        object d = CreateObject(OBJECT_TYPE_CREATURE, "nw_skeleton", WSAt(v, 270.0), FALSE, "WS_D" + IntToString(i));
        SetLocalObject(WSMod(), "ws_d" + IntToString(i), d);
        WSQuiet(d); WSStrip(d);
        ChangeToStandardFaction(d, STANDARD_FACTION_COMMONER);
        SetPlotFlag(d, FALSE); SetImmortal(d, TRUE);
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectCutsceneImmobilize()), d);
        AssignCommand(WSA(i), JumpToLocation(WSAt(WSStart(i), 90.0)));
    }
}
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    VBegin(pc); VHidePC(pc);
    SetLocalInt(WSMod(), "ws_single", 1);
    SetLocalLocation(WSMod(), "ws_pc", GetLocation(pc));
    string specs = "nw_s_badgerdire,nw_wolf,nw_badger,nw_spidgiant";
    int n;
    for (n = 0; n < 4; n++) DelayCommand(0.2, WSSpawn(n, WSField(specs, n) + ",,,"));
    SetLocalInt(WSMod(), "ws_n", 4);
    DelayCommand(1.0, CSReport());
    DelayCommand(1.5, Place("8.0,8.0,8.0,8.0"));
    DelayCommand(2.5, Approach());
    float t;
    for (t = 4.0; t <= 16.0; t += 2.0) DelayCommand(t, CSDistances("far_" + FloatToString(t, 0, 0)));
    // From 1 m: do they back off?
    DelayCommand(17.0, WSStopAll());
    DelayCommand(17.5, Place("1.0,1.0,1.0,1.0"));
    DelayCommand(18.5, Approach());
    for (t = 20.0; t <= 30.0; t += 2.0) DelayCommand(t, CSDistances("near_" + FloatToString(t, 0, 0)));
    DelayCommand(31.0, VDone());
}

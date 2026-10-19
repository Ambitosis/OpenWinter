#include "visual_inc"
// conditionpcai: which scripts run on a player whose control a condition takes away. Every stock AI event script
// (default, nw_c2_default*, nw_ch_ac*, x0_ch_hen_*, x2_def_*, x0_hen_*) is replaced in this module by a stand-in that
// only logs its name when it runs on the player. If the player still acts (the chat shows her casting), the
// engine drives her itself; if she stands, the stock scripts did it in scene conditionai.
//   c  charmed by a hostile bandit 3 m east (another hostile bandit 9 m north), 14 s
//   d  dominated by it, 10 s
//   b  confused, 10 s
//   a  frightened by a hostile goblin 3 m east, 8 s

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|pcai|" + s); }

object PC() { return GetFirstPC(); }

object Npc(string sResRef, float fDx, float fDy, string sTag, int nFaction)
{
    vector v = GetPositionFromLocation(GetLocalLocation(GetModule(), "HOME"));
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(PC()), Vector(v.x + fDx, v.y + fDy, v.z), 180.0), FALSE, sTag);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetPlotFlag(o, TRUE);
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

void Trace(string k, float t, float fEnd)
{
    if (t > fEnd) return;
    object pc = PC();
    vector v = GetPosition(pc), h = GetPositionFromLocation(GetLocalLocation(GetModule(), "HOME"));
    Log(k + "|" + FloatToString(t, 0, 1) + "|" + FloatToString(v.x - h.x, 0, 1) + "," + FloatToString(v.y - h.y, 0, 1)
        + "|a" + IntToString(GetCurrentAction(pc)) + "|c" + IntToString(GetCommandable(pc)));
    DelayCommand(1.0, Trace(k, t + 1.0, fEnd));
}

void Home()
{
    object pc = PC();
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(GetLocalLocation(GetModule(), "HOME")));
}

void Begin(string k)
{
    Log("begin|" + k);
}

void Charm(int bDominate)
{
    string k = bDominate ? "d" : "c";
    object m = Npc("nw_bandit001", 3.0, 0.0, "pcai_" + k, STANDARD_FACTION_HOSTILE);
    Npc("nw_bandit001", 0.0, 9.0, "pcai_" + k, STANDARD_FACTION_HOSTILE);
    float fDur = bDominate ? 10.0 : 14.0;
    Begin(k);
    if (bDominate) DelayCommand(1.0, AssignCommand(m, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectDominated(), PC(), fDur)));
    else DelayCommand(1.0, AssignCommand(m, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectCharmed(), PC(), fDur)));
    DelayCommand(1.0, Trace(k, 0.0, fDur + 1.0));
    DelayCommand(3.0, VShot(k + "_2s"));
    DelayCommand(fDur + 2.0, Clear("pcai_" + k));
}

void Confuse()
{
    Npc("nw_bandit001", 2.0, 0.0, "pcai_b", STANDARD_FACTION_COMMONER);
    Begin("b");
    DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectConfused(), PC(), 10.0));
    DelayCommand(1.0, Trace("b", 0.0, 11.0));
    DelayCommand(12.0, Clear("pcai_b"));
}

void Fear()
{
    object g = Npc("nw_goblina", 3.0, 0.0, "pcai_a", STANDARD_FACTION_HOSTILE);
    Begin("a");
    DelayCommand(1.0, AssignCommand(g, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectFrightened(), PC(), 8.0)));
    DelayCommand(1.0, Trace("a", 0.0, 9.0));
    DelayCommand(10.0, Clear("pcai_a"));
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    SetLocalLocation(GetModule(), "HOME", GetLocation(pc));
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 50.0));
    float t = 3.0;
    DelayCommand(t, Charm(FALSE)); t += 17.0;
    DelayCommand(t, Home()); t += 1.0;
    DelayCommand(t, Charm(TRUE)); t += 13.0;
    DelayCommand(t, Home()); t += 1.0;
    DelayCommand(t, Confuse()); t += 13.0;
    DelayCommand(t, Home()); t += 1.0;
    DelayCommand(t, Fear()); t += 11.0;
    DelayCommand(t, VDone());
}

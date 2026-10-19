// Casters crowding one target at touch range: groups of N casters (nw_bandit001, no scripts) start 8 m from a
// target and all cheat-cast Light (touch) at it at once, once they have perceived it. Per group: how many Lights
// landed (two effects each), how far the casters stood when theirs landed, when they landed, and who is still busy.
// On nwserver every caster reaches the target, running, and casts from 2.25 m + both CREPERSPACE - 0.2 (3.05 m);
// blocked ones wait and go round.
#include "parity_inc"

object Spawn(float x, float y, float fFacing, string sTag)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetObjectByTag("PARITY_AREA"), Vector(x, y, 0.0), fFacing), FALSE, sTag);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    return o;
}

int Landed(object t, object c)
{
    effect e = GetFirstEffect(t);
    while (GetIsEffectValid(e)) { if (GetEffectSpellId(e) == SPELL_LIGHT && GetEffectCreator(e) == c) return TRUE; e = GetNextEffect(t); }
    return FALSE;
}

// Each caster: the time its Light lands and its distance from the target then.
void Watch(object c, object t, float fT)
{
    if (fT > 15.0) return;
    if (Landed(t, c)) { SetLocalFloat(c, "T", fT); SetLocalFloat(c, "D", GetDistanceBetween(c, t)); return; }
    DelayCommand(0.1, Watch(c, t, fT + 0.1));
}

// Casting at a creature the caster hasn't seen yet fails: the casts start once everyone has perceived the target.
void Cast(object c, object t)
{
    AssignCommand(c, ActionCastSpellAtObject(SPELL_LIGHT, t, METAMAGIC_NONE, TRUE));
    Watch(c, t, 0.0);
}

void Read(string k, object t, int n)
{
    int lights = 0, landed = 0, busy = 0, i;
    float far = 0.0, near = 99.0, sumT = 0.0, last = 0.0;
    effect e = GetFirstEffect(t);
    while (GetIsEffectValid(e)) { if (GetEffectSpellId(e) == SPELL_LIGHT) lights++; e = GetNextEffect(t); }
    for (i = 0; i < n; i++)
    {
        object c = GetObjectByTag(k + "_c" + IntToString(i));
        busy += GetCurrentAction(c) != ACTION_INVALID;
        float fT = GetLocalFloat(c, "T"), d = GetLocalFloat(c, "D");
        if (d <= 0.0) continue;
        landed++;
        sumT += fT;
        if (fT > last) last = fT;
        if (d > far) far = d;
        if (d < near) near = d;
    }
    PLogI(k + ".lights", lights);
    PLogI(k + ".landed", landed);
    PLogI(k + ".busy", busy);
    if (landed == 0) return;
    PLogF(k + ".farthest", far, 0.15);
    PLogF(k + ".nearest", near, 0.15);
    PLogF(k + ".meanTime", sumT / IntToFloat(landed), n > 3 ? 1.2 : 0.3);
    PLogF(k + ".lastTime", last, n > 3 ? 3.0 : 0.3);
}

// nShape 0: in a line abreast 8 m south of the target, 1.5 m apart; 1: in a ring 8 m round it; 2: in a file
// (one behind another) from 8 m south.
void Group(string k, float x, float y, int n, int nShape)
{
    object t = Spawn(x, y, 270.0, k + "_t");
    SetPlotFlag(t, TRUE);
    int i;
    for (i = 0; i < n; i++)
    {
        float cx = x, cy = y - 8.0;
        if (nShape == 0) cx = x + (IntToFloat(i) - IntToFloat(n - 1) / 2.0) * 1.5;
        else if (nShape == 1) { float a = 360.0 * IntToFloat(i) / IntToFloat(n); cx = x + 8.0 * cos(a); cy = y + 8.0 * sin(a); }
        else cy = y - 8.0 - IntToFloat(i) * 1.5;
        object c = Spawn(cx, cy, 90.0, k + "_c" + IntToString(i));
        DelayCommand(5.0, Cast(c, t));
    }
    DelayCommand(20.5, Read(k, t, n));
}

void main()
{
    Group("line6", 12.0, 14.0, 6, 0);
    Group("ring6", 40.0, 14.0, 6, 1);
    Group("file6", 66.0, 30.0, 6, 2);
    Group("line10", 12.0, 45.0, 10, 0);
    Group("ring10", 40.0, 45.0, 10, 1);
    Group("ring3", 66.0, 60.0, 3, 1);
    DelayCommand(21.0, PDone());
}

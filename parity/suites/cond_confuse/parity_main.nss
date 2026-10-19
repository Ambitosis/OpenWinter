// What a confused creature does by itself, on nwserver. Each cell has a confused nw_bandit001 B (hostile faction,
// no event scripts) and neighbours that stand still, can't die and count who attacks them. Confusion lands at 1.0 s.
// Every 0.5 s from 1.5 s to 49.5 s each B's current action, distance from its start and attack target are sampled;
// rates are over all samples of a group. Measured with traces first (see docs/CONDITIONS.md, "Confusion"): every
// round (about 6.25 s, the first at once) a confused creature walks at random, attacks the nearest creature unless
// that one is of its own faction, or does nothing.
//   conf.enemy   6 cells, only a commoner E 2 m away (an enemy)
//   conf.mate    4 cells, a faction mate F 2 m away and E 3 m away: F is the nearest, so it hardly ever attacks
//   conf.creator 2 cells, only the effect's creator K (defender) 5 m away
//   conf.end     2 cells as conf.enemy, confusion for 6 s: whatever it was doing at 6.9 s it still does at 9 s
//   ctrl         1 cell as conf.mate without the effect
#include "pcond_inc"

float CellX(int c) { return 10.0 + IntToFloat(c % 5) * 20.0; }
float CellY(int c) { return 10.0 + IntToFloat(c / 5) * 20.0; }

void ApplyConf(object o, float fDur)
{
    if (fDur > 0.0) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectConfused(), o, fDur);
    else ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConfused(), o);
}

void Sampler(object o, string g, float t)
{
    if (t > 49.6) return;
    CSample(o, g);
    DelayCommand(0.5, Sampler(o, g, t + 0.5));
}

void Kept(object b, string k)
{
    PLogI(k + ".kept", GetCurrentAction(b) == GetLocalInt(b, "A69"));
}

// nNeighbours: 1 faction mate F at 2 m and commoner E at 3 m, 2 commoner E at 2 m, 4 creator K at 5 m.
void Cell(int c, string g, int nNeighbours, float fDur, int bEffect = TRUE)
{
    float x = CellX(c), y = CellY(c);
    object b = CSpawn(x, y, g, STANDARD_FACTION_HOSTILE);
    object k = OBJECT_INVALID;
    if (nNeighbours == 1)
    {
        CNeighbour(x + 2.0, y, g, "F", STANDARD_FACTION_HOSTILE);
        CNeighbour(x - 3.0, y, g, "E", STANDARD_FACTION_COMMONER);
    }
    if (nNeighbours == 2) CNeighbour(x - 2.0, y, g, "E", STANDARD_FACTION_COMMONER);
    if (nNeighbours == 4) k = CNeighbour(x, y + 5.0, g, "K", STANDARD_FACTION_DEFENDER);
    if (bEffect)
    {
        if (GetIsObjectValid(k)) DelayCommand(1.0, AssignCommand(k, ApplyConf(b, fDur)));
        else DelayCommand(1.0, ApplyConf(b, fDur));
    }
    if (fDur > 0.0)
    {
        DelayCommand(6.9, SetLocalInt(b, "A69", GetCurrentAction(b)));
        DelayCommand(9.0, Kept(b, g + IntToString(c)));
        return;
    }
    DelayCommand(1.5, Sampler(b, g, 1.5));
    if (c == 0) DelayCommand(1.6, PLogI(g + ".commandable", GetCommandable(b)));
}

void Report(string g, float fAttack, float fWalk, float fNone)
{
    object m = GetModule();
    CRate(g, "walk", fWalk);
    CRate(g, "attack", fAttack);
    CRate(g, "none", fNone);
    CRate(g, "move", 0.05);
    CRate(g, "other", 0.05);
    PLogI(g + ".atkF", GetLocalInt(m, g + ".atk.F"));
    // (A creature next to a faction mate attacks only after walking off, if at all.)
    if (g != "conf.mate") PLogI(g + ".attackedAny", GetLocalInt(m, g + ".atk.E") + GetLocalInt(m, g + ".atk.K") > 0);
}

void main()
{
    int c;
    for (c = 0; c < 6; c++) Cell(c, "conf.enemy", 2, 0.0);
    for (c = 6; c < 10; c++) Cell(c, "conf.mate", 1, 0.0);
    Cell(10, "conf.creator", 4, 0.0);
    Cell(11, "conf.creator", 4, 0.0);
    Cell(12, "conf.end", 2, 6.0);
    Cell(13, "conf.end", 2, 6.0);
    Cell(14, "ctrl", 1, 0.0, FALSE);
    DelayCommand(50.0, Report("conf.enemy", 0.3, 0.15, 0.3));
    DelayCommand(50.0, Report("conf.mate", 0.2, 0.15, 0.25));
    DelayCommand(50.0, Report("conf.creator", 0.4, 0.3, 0.4));
    DelayCommand(50.0, Report("ctrl", 0.01, 0.01, 0.01));
    DelayCommand(51.0, PDone());
}

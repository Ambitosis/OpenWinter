// Charm and domination on nwserver: how they change reputations, and what a charmed creature's stock AI then does.
// Each group has B (nw_bandit001, hostile faction, no scripts), the creator K (defender), another defender K2,
// B's faction mate H and a commoner C. The effect lands at 1.0 s for 8 s; readings before (0.8 s), during (1.5 s)
// and after (10 s).
// Then what charmed and dominated creatures without scripts do (see Ai).
#include "pcond_inc"

effect Make(int n)
{
    if (n == 1) return EffectDominated();
    if (n == 2) return EffectCutsceneDominated();
    return EffectCharmed();
}

// Made here, so its creator is whoever runs this (K inside AssignCommand, else the module).
void Apply(int n, object o) { ApplyEffectToObject(DURATION_TYPE_TEMPORARY, Make(n), o, 8.0); }

void Pair(string k, object a, object b)
{
    PLogI(k + ".enemy", GetIsEnemy(b, a));
    PLogI(k + ".friend", GetIsFriend(b, a));
    PLogI(k + ".neutral", GetIsNeutral(b, a));
    PLogI(k + ".rep", GetReputation(a, b));
}

void Read(string g, string t, object b, object k, object k2, object h, object c)
{
    string p = g + "." + t;
    Pair(p + ".BtoK", b, k);
    Pair(p + ".KtoB", k, b);
    Pair(p + ".BtoK2", b, k2);
    Pair(p + ".K2toB", k2, b);
    Pair(p + ".BtoH", b, h);
    Pair(p + ".HtoB", h, b);
    Pair(p + ".BtoC", b, c);
    PLogI(p + ".factionEqualK", GetFactionEqual(b, k));
    PLogI(p + ".factionEqualH", GetFactionEqual(b, h));
    PLogI(p + ".master", GetMaster(b) == k);
    PLogI(p + ".assoc", GetAssociateType(b));
    PLogI(p + ".commandable", GetCommandable(b));
    PLogI(p + ".henchOfK", GetHenchman(k) == b);
    PLogI(p + ".dominatedOfK", GetAssociate(ASSOCIATE_TYPE_DOMINATED, k) == b);
    PLogI(p + ".factionMembersK", GetIsObjectValid(GetFirstFactionMember(k, FALSE)));
}

void Group(int i, int n, string g, int bModule = FALSE)
{
    float y = 6.0 + IntToFloat(i) * 8.0;
    object b = CSpawn(10.0, y, g, STANDARD_FACTION_HOSTILE);
    object k = CNeighbour(13.0, y, g, "K", STANDARD_FACTION_DEFENDER);
    object k2 = CNeighbour(16.0, y, g, "K2", STANDARD_FACTION_DEFENDER);
    object h = CNeighbour(10.0, y + 3.0, g, "H", STANDARD_FACTION_HOSTILE);
    object c = CNeighbour(7.0, y, g, "C", STANDARD_FACTION_COMMONER);
    DelayCommand(0.8, Read(g, "before", b, k, k2, h, c));
    if (bModule) DelayCommand(1.0, Apply(n, b));
    else DelayCommand(1.0, AssignCommand(k, Apply(n, b)));
    DelayCommand(1.5, Read(g, "during", b, k, k2, h, c));
    DelayCommand(10.0, Read(g, "after", b, k, k2, h, c));
}

// What a charmed or dominated creature (no scripts) does by itself. The effect is made by the creator inside its
// own command (an effect's creator is whoever constructed it). K is 2.5 m east, a commoner E 11 m north.
void Ai(int i, int n, string g, int bModule)
{
    float y = 4.0 + IntToFloat(i) * 25.0;
    object b = CSpawn(60.0, y, g, STANDARD_FACTION_HOSTILE);
    object k = CNeighbour(62.5, y, g, "K", STANDARD_FACTION_DEFENDER);
    CNeighbour(60.0, y + 11.0, g, "E", STANDARD_FACTION_COMMONER);
    if (bModule) DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_PERMANENT, Make(n), b));
    else DelayCommand(1.0, AssignCommand(k, ApplyEffectToObject(DURATION_TYPE_PERMANENT, Make(n), b)));
    DelayCommand(5.0, PLogI(g + ".action5", GetCurrentAction(b)));
    DelayCommand(10.0, PLogI(g + ".action10", GetCurrentAction(b)));
    DelayCommand(16.0, PLogI(g + ".atkK", GetLocalInt(GetModule(), g + ".atk.K") > 0));
    DelayCommand(16.0, PLogI(g + ".atkE", GetLocalInt(GetModule(), g + ".atk.E") > 0));
    DelayCommand(16.0, PLogF(g + ".moved", CDist0(b), 2.0));
}

void main()
{
    Group(0, 0, "charm");
    Group(1, 1, "dominate");
    Group(2, 0, "charm.module", TRUE);
    Group(3, 2, "cutdominate");
    Ai(0, 0, "ai.charm", FALSE);
    Ai(1, 0, "ai.charm.module", TRUE);
    Ai(2, 1, "ai.dominate", FALSE);
    DelayCommand(16.5, PDone());
}

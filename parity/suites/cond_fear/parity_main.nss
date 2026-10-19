// What a frightened (or turned) creature does by itself, on nwserver. Each row has an nw_bandit001 B (hostile
// faction, no event scripts unless "stock") at x = 60 and, 4 m east of it, the creature the fear is about: the
// effect's creator or a hostile bystander. The effect lands at 1.0 s (permanent unless noted); B's offset from its
// start, its distance from that creature and its current action are read at 1.5, 2, 3, 5, 8 and 12 s.
//   fear.hostile  frightened by K (defender, hostile to B)
//   fear.module   frightened by the module, a hostile H (defender) 4 m east
//   fear.friend   frightened by F (hostile faction, B's friend), nobody hostile about
//   fear.alone    frightened by the module, nobody about
//   fear.stock    as fear.hostile, B keeps the stock nw_c2_default* scripts
//   turned        EffectTurned by K
//   fear.end      as fear.hostile for 4 s only
//   fear.attacked as fear.hostile, then K attacks B from 1.5 s (B has 300 temporary hit points)
//   fear.approach as fear.hostile, then K walks up to B from 2.0 s
//   fear.newenemy frightened by the module; a hostile H appears 5 m east at 3.0 s
//   fear.stock.attacked  as fear.attacked with the stock scripts
#include "pcond_inc"

void Read(object b, object k, string g, string t)
{
    vector v = GetPosition(b);
    // A flight stops at the next heartbeat (NW_G0_fear runs on every heartbeat, 6.0-7.2 s apart at random:
    // state_scripts), so where it stopped can differ by 1.2 s of running (about 4.2 m) from run to run.
    float tol = t == "t9" || t == "t12" ? 4.2 : 2.5;
    PLogF(g + "." + t + ".dx", v.x - GetLocalFloat(b, "X0"), tol);
    PLogF(g + "." + t + ".dy", v.y - GetLocalFloat(b, "Y0"), tol);
    if (GetIsObjectValid(k)) PLogF(g + "." + t + ".dist", GetDistanceBetween(b, k), tol);
    PLogI(g + "." + t + ".action", GetCurrentAction(b));
}

void NewEnemy(object b, string g)
{
    vector v = GetPosition(b);
    CNeighbour(v.x + 5.0, v.y, g, "H", STANDARD_FACTION_DEFENDER);
}

void ApplyFear(int bTurned, object o, float fDur)
{
    effect e = bTurned ? EffectTurned() : EffectFrightened();
    if (fDur > 0.0) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, e, o, fDur);
    else ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
}

void Row(int i, string g, int nOther, int bCreator, int bStock = FALSE, int bTurned = FALSE, float fDur = 0.0, int nAfter = 0)
{
    float y = 4.0 + IntToFloat(i) * 7.0;
    object b = CSpawn(60.0, y, g, STANDARD_FACTION_HOSTILE, bStock);
    object k = OBJECT_INVALID;
    if (nOther >= 0) k = CNeighbour(64.0, y, g, "K", nOther);
    // Immortal creatures refuse fear: hit points to spare instead.
    if (nAfter > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(300), b);
    if (nAfter == 1) DelayCommand(1.5, AssignCommand(k, ActionAttack(b)));
    if (nAfter == 2) DelayCommand(2.0, AssignCommand(k, ActionMoveToObject(b, FALSE, 1.0)));
    if (nAfter == 3) DelayCommand(3.0, NewEnemy(b, g));
    if (bCreator && GetIsObjectValid(k)) DelayCommand(1.0, AssignCommand(k, ApplyFear(bTurned, b, fDur)));
    else DelayCommand(1.0, ApplyFear(bTurned, b, fDur));
    DelayCommand(1.5, Read(b, k, g, "t1_5"));
    DelayCommand(2.0, Read(b, k, g, "t2"));
    DelayCommand(3.0, Read(b, k, g, "t3"));
    DelayCommand(5.0, Read(b, k, g, "t5"));
    DelayCommand(9.0, Read(b, k, g, "t9"));
    DelayCommand(12.0, Read(b, k, g, "t12"));
    DelayCommand(1.6, PLogI(g + ".commandable", GetCommandable(b)));
}

void main()
{
    Row(0, "fear.hostile", STANDARD_FACTION_DEFENDER, TRUE);
    Row(1, "fear.module", STANDARD_FACTION_DEFENDER, FALSE);
    Row(2, "fear.friend", STANDARD_FACTION_HOSTILE, TRUE);
    Row(3, "fear.alone", -1, FALSE);
    Row(4, "fear.stock", STANDARD_FACTION_DEFENDER, TRUE, TRUE);
    Row(5, "turned", STANDARD_FACTION_DEFENDER, TRUE, FALSE, TRUE);
    Row(6, "fear.end", STANDARD_FACTION_DEFENDER, TRUE, FALSE, FALSE, 4.0);
    Row(7, "fear.attacked", STANDARD_FACTION_DEFENDER, TRUE, FALSE, FALSE, 0.0, 1);
    Row(8, "fear.approach", STANDARD_FACTION_DEFENDER, TRUE, FALSE, FALSE, 0.0, 2);
    Row(9, "fear.newenemy", -1, FALSE, FALSE, FALSE, 0.0, 3);
    Row(10, "fear.stock.attacked", STANDARD_FACTION_DEFENDER, TRUE, TRUE, FALSE, 0.0, 1);
    DelayCommand(13.0, PDone());
}

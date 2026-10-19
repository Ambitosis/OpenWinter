// The queries the stock AI chooses targets with (docs/STOCK_AI.md):
// - near: GetNearestCreature by reputation (Prelude factions) and by each PERCEPTION_* type, with nth and
//   combined criteria (x0_i0_enemy's GetNearestSeenEnemy / GetNearestPerceivedEnemy / GetNearestSeenFriend),
//   GetObjectSeen / GetObjectHeard, GetIsEnemy / GetIsFriend / GetIsNeutral, and the same after the observer
//   loses sight of an invisible creature.
// - act: GetCurrentAction, GetAttackTarget, GetAttemptedAttackTarget, GetAttemptedSpellTarget, GetIsInCombat,
//   GetLastAttacker, GetLastHostileActor, GetLastSpellCaster, GetLastDamager of a scripted attacker, its target
//   and a caster, through attacks, ClearAllActions, a move and two spells (each state with the time it began).
#include "aq_inc"

object Mk(string sRes, object oArea, float x, float y, string sName, int nFaction = -1)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(oArea, Vector(x, y, 0.0), 0.0), FALSE, "AQ_" + sName);
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    AssignCommand(o, ClearAllActions(TRUE));
    if (nFaction >= 0) ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetLocalString(o, "AQ_NAME", sName);
    return o;
}

object Obj(string sName) { return GetObjectByTag("AQ_" + sName); }

void Near()
{
    object a = GetObjectByTag("AQ_NEAR");
    object o = Mk("nw_bandit001", a, 15.0, 25.0, "o", STANDARD_FACTION_DEFENDER);
    Mk("nw_goblina", a, 18.0, 25.0, "h1");
    Mk("nw_orca", a, 21.0, 25.0, "h2");
    Mk("nw_goblina", a, 17.0, 28.5, "h3");
    object h4 = Mk("nw_goblina", a, 15.0, 31.5, "h4");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSilence(), h4);
    Mk("nw_goblina", a, 45.0, 25.0, "h5");
    object h6 = Mk("nw_kobold001", a, 13.0, 22.0, "h6");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, SetEffectSpellId(EffectACIncrease(1), SPELL_BLESS), h6);
    object hd = Mk("nw_goblina", a, 19.0, 22.0, "hd");
    AssignCommand(hd, SetIsDestroyable(FALSE, FALSE, FALSE));
    DelayCommand(0.2, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), hd));
    Mk("nw_bandit001", a, 14.0, 27.0, "f1", STANDARD_FACTION_DEFENDER);
    Mk("nw_commale", a, 11.5, 25.0, "c1");
    Mk("nw_bandit001", a, 16.0, 21.0, "m1", STANDARD_FACTION_MERCHANT);
    Mk("nw_bandit001", a, 10.0, 29.0, "f2", STANDARD_FACTION_DEFENDER);
    Mk("nw_dog", a, 12.0, 29.5, "t1");
    Mk("nw_dog", a, 18.0, 20.0, "t2");
    int i;
    string s = "h1 h2 h3 h4 h5 h6 hd f1 c1 m1 f2 t1 t2 ";
    while (s != "")
    {
        int p = FindSubString(s, " ");
        object t = Obj(GetStringLeft(s, p));
        s = GetSubString(s, p + 1, 1000);
        SetPlotFlag(t, GetLocalString(t, "AQ_NAME") != "hd");
    }
}

string N(int t1, int v1, int nth, int t2 = -1, int v2 = -1, int t3 = -1, int v3 = -1)
{
    return AqName(GetNearestCreature(t1, v1, Obj("o"), nth, t2, v2, t3, v3));
}

string Nths(int t1, int v1, int t2 = -1, int v2 = -1, int t3 = -1, int v3 = -1)
{
    string s = "";
    int i;
    for (i = 1; i <= 6; i++) s += (i == 1 ? "" : " ") + N(t1, v1, i, t2, v2, t3, v3);
    return s;
}

void NearReport(string k)
{
    object o = Obj("o");
    PLog(k + ".enemy", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY));
    PLog(k + ".friend", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_FRIEND));
    PLog(k + ".neutral", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_NEUTRAL));
    int p;
    for (p = 0; p <= 7; p++)
    {
        PLog(k + ".per" + IntToString(p), Nths(CREATURE_TYPE_PERCEPTION, p));
        PLog(k + ".enemyper" + IntToString(p), Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_PERCEPTION, p));
    }
    PLog(k + ".friendseen", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_FRIEND, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN));
    PLog(k + ".perenemy", Nths(CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY));
    PLog(k + ".enemyseenalive", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_IS_ALIVE, TRUE));
    PLog(k + ".dead", Nths(CREATURE_TYPE_IS_ALIVE, FALSE));
    PLog(k + ".deadenemy", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_IS_ALIVE, FALSE));
    PLog(k + ".bless", Nths(CREATURE_TYPE_HAS_SPELL_EFFECT, SPELL_BLESS));
    PLog(k + ".nobless", Nths(CREATURE_TYPE_DOES_NOT_HAVE_SPELL_EFFECT, SPELL_BLESS, CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY));
    PLog(k + ".fighterenemy", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_CLASS, CLASS_TYPE_FIGHTER));
    PLog(k + ".humanoidgoblin", Nths(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMANOID_GOBLINOID, CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY));
    PLog(k + ".notpc", Nths(CREATURE_TYPE_PLAYER_CHAR, PLAYER_CHAR_NOT_PC));
    PLog(k + ".animal", Nths(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_ANIMAL));
    // Three criteria: which of them apply.
    PLog(k + ".c3.rep.per.classdragon", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_CLASS, CLASS_TYPE_DRAGON));
    PLog(k + ".c3.rep.per.racedragon", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_DRAGON));
    PLog(k + ".c3.rep.per.bless", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_HAS_SPELL_EFFECT, SPELL_BLESS));
    PLog(k + ".c3.rep.per.orc", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMANOID_ORC));
    PLog(k + ".c3.per.rep.classdragon", Nths(CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_CLASS, CLASS_TYPE_DRAGON));
    PLog(k + ".c3.alive.rep.classdragon", Nths(CREATURE_TYPE_IS_ALIVE, TRUE, CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_CLASS, CLASS_TYPE_DRAGON));
    PLog(k + ".c3.rep.alive.classdragon", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_IS_ALIVE, TRUE, CREATURE_TYPE_CLASS, CLASS_TYPE_DRAGON));
    PLog(k + ".c3.rep.race.classdragon", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMANOID_GOBLINOID, CREATURE_TYPE_CLASS, CLASS_TYPE_DRAGON));
    PLog(k + ".c3.race.class.per", Nths(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMAN, CREATURE_TYPE_CLASS, CLASS_TYPE_FIGHTER, CREATURE_TYPE_PERCEPTION, PERCEPTION_NOT_SEEN));
    PLog(k + ".c3.friend.per.classfighter", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_FRIEND, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_CLASS, CLASS_TYPE_FIGHTER));
    PLog(k + ".c3.friend.per.classcommoner", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_FRIEND, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_CLASS, CLASS_TYPE_COMMONER));
    PLog(k + ".c3.class.rep.per", Nths(CREATURE_TYPE_CLASS, CLASS_TYPE_HUMANOID, CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN));
    PLog(k + ".c2.classdragon.rep", Nths(CREATURE_TYPE_CLASS, CLASS_TYPE_DRAGON, CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY));
    PLog(k + ".c2.classfighter.rep", Nths(CREATURE_TYPE_CLASS, CLASS_TYPE_FIGHTER, CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_FRIEND));
    PLog(k + ".c3.alive.alive.classfighter", Nths(CREATURE_TYPE_IS_ALIVE, TRUE, CREATURE_TYPE_IS_ALIVE, TRUE, CREATURE_TYPE_CLASS, CLASS_TYPE_FIGHTER));
    PLog(k + ".c2.rep.classdragon", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_CLASS, CLASS_TYPE_DRAGON));
    PLog(k + ".c2.rep.classhumanoid", Nths(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, CREATURE_TYPE_CLASS, CLASS_TYPE_HUMANOID));
    PLog(k + ".c2.per.classdragon", Nths(CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN, CREATURE_TYPE_CLASS, CLASS_TYPE_DRAGON));
    PLog(k + ".c2.classhumanoid.rep", Nths(CREATURE_TYPE_CLASS, CLASS_TYPE_HUMANOID, CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY));
    PLog(k + ".c2.race.alive", Nths(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMANOID_GOBLINOID, CREATURE_TYPE_IS_ALIVE, TRUE));
    string s = "h1 h2 h3 h4 h5 h6 hd f1 c1 m1 f2 t1 t2 ", r = "";
    while (s != "")
    {
        int q = FindSubString(s, " ");
        object t = Obj(GetStringLeft(s, q));
        s = GetSubString(s, q + 1, 1000);
        r += AqName(t) + "=" + IntToString(GetObjectSeen(t, o)) + IntToString(GetObjectHeard(t, o))
            + IntToString(GetIsEnemy(t, o)) + IntToString(GetIsFriend(t, o)) + IntToString(GetIsNeutral(t, o))
            + "/" + IntToString(GetReputation(o, t)) + " ";
    }
    PLog(k + ".each", r);
}

// The act area: a scripted attacker, a passive target and a caster.
void Act()
{
    object a = GetObjectByTag("AQ_ACT");
    object oA = Mk("nw_bandit001", a, 10.0, 20.0, "A", STANDARD_FACTION_DEFENDER);
    object oT = Mk("nw_goblina", a, 16.0, 20.0, "T");
    object oC = Mk("nw_bandit005", a, 10.0, 26.0, "C", STANDARD_FACTION_DEFENDER);
    SetImmortal(oT, TRUE);
    SetLocalInt(oT, "AQ_TRACK", 1);
}

// The cs area: four scripted attackers beside passive targets; after one swing their attack is cleared with or
// without the combat state, and two of them attack again a second later.
void CombatState()
{
    object a = GetObjectByTag("AQ_CS");
    int i;
    for (i = 1; i <= 4; i++)
    {
        float y = IntToFloat(i) * 8.0;
        object oA = Mk("nw_bandit001", a, 10.0, y, "A" + IntToString(i), STANDARD_FACTION_DEFENDER);
        object oT = Mk("nw_goblina", a, 11.2, y, "T" + IntToString(i));
        SetImmortal(oT, TRUE);
        SetEventScript(oT, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "aq_atk");
        DelayCommand(1.0, AssignCommand(oA, ActionAttack(oT)));
        DelayCommand(2.0, AssignCommand(oA, ClearAllActions(i == 2 || i == 4)));
        if (i >= 3) DelayCommand(3.0, AssignCommand(oA, ActionAttack(oT)));
    }
}

void CsReport()
{
    int i;
    for (i = 1; i <= 4; i++)
    {
        string sA = "A" + IntToString(i), sT = "T" + IntToString(i);
        // The first swing comes 0.3 to 0.8 s after the order on nwserver; combat ends about eight seconds after the
        // last hostile act (the original counts the damage landing, about half a second after the swing: OpenWinter
        // lands it at the swing, hence the wider tolerance on cb).
        AqReport(Obj(sA), "cs." + sA, "act aat ", 0.45);
        AqReport(Obj(sA), "cs." + sA, "at cb ", 0.9);
        AqReport(Obj(sT), "cs." + sT, "cb la lh ", 0.9);
        object oT = Obj(sT);
        int k;
        for (k = 0; k < GetLocalInt(oT, "AQ_SWN") && k < 6; k++)
            PLogF("cs." + sT + ".swing" + IntToString(k), GetLocalFloat(oT, "AQ_SW" + IntToString(k)), 0.9);
    }
}

void Probe()
{
    int i;
    for (i = 1; i <= 4; i++)
    {
        ExecuteScript("aq_probe", Obj("A" + IntToString(i)));
        ExecuteScript("aq_probe", Obj("T" + IntToString(i)));
    }
    ExecuteScript("aq_probe", Obj("A"));
    ExecuteScript("aq_probe", Obj("T"));
    ExecuteScript("aq_probe", Obj("C"));
}

void ProbeLoop(float fUntil)
{
    Probe();
    if (PSince() < fUntil) DelayCommand(0.1, ProbeLoop(fUntil));
}

void Do(string sName, int nWhat)
{
    object o = Obj(sName);
    object t = Obj("T");
    switch (nWhat)
    {
        case 0: AssignCommand(o, ActionAttack(t)); break;
        case 1: AssignCommand(o, ClearAllActions()); break;
        case 2: AssignCommand(o, ClearAllActions(TRUE)); break;
        case 3: AssignCommand(o, ActionMoveToLocation(Location(GetArea(o), Vector(10.0, 12.0, 0.0), 0.0))); break;
        case 4: AssignCommand(o, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, t)); break;
        case 5: AssignCommand(o, ActionCastSpellAtObject(SPELL_RAY_OF_FROST, t)); break;
    }
    DelayCommand(0.0, Probe());
}

void ActReport()
{
    // The attacker: the order, the walk up (attempted target cleared), arrival and first swing, ClearAllActions,
    // the re-attack, ClearAllActions(TRUE) and the walk away. Later swings depend on where the native round stood.
    AqReport(Obj("A"), "act.A", "act:6 aat:6 at:2 cb:3 ");
    // The target: who attacked it, its last hostile actor and last spell caster through the attacks and the spells.
    AqReport(Obj("T"), "act.T", "lsc ls ");
    AqReport(Obj("T"), "act.T", "la lh ", 1.2);
    AqReport(Obj("C"), "act.C", "act ast ");
}

void main()
{
    PStart();
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    DelayCommand(1.0, Near());
    DelayCommand(9.0, NearReport("near"));
    DelayCommand(1.0, Act());
    DelayCommand(1.0, CombatState());
    DelayCommand(1.5, ProbeLoop(24.0));
    DelayCommand(3.0, Do("A", 0));
    DelayCommand(7.0, Do("A", 1));
    DelayCommand(7.5, Do("A", 0));
    DelayCommand(10.0, Do("A", 2));
    DelayCommand(10.5, Do("A", 3));
    DelayCommand(13.0, Do("C", 4));
    DelayCommand(17.0, Do("C", 5));
    DelayCommand(20.0, Do("A", 0));
    DelayCommand(25.0, ActReport());
    DelayCommand(25.0, CsReport());
    DelayCommand(26.0, PDone());
}

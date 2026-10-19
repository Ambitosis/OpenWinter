// Attackers following a target that leaves through doors and transition triggers, a summoned creature following its
// master, and stock bandits walking back to their POST_ waypoints from other areas (docs/INTER_AREA_MOVEMENT.md).
// Areas come from parity/gen_interarea.py. No players: everything happens in areas without one.
#include "pinterarea_inc"

object Mk(string sRes, string sWp, string sTag, int bNormalAI = TRUE, float fTolT = 0.75, float fTolP = 0.35)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, GetLocation(GetObjectByTag(sWp)), FALSE, sTag);
    if (bNormalAI) SetAILevel(o, AI_LEVEL_NORMAL);
    IATrack(o, fTolT, fTolP);
    return o;
}

// Fleeing targets: how far they get once their pursuer catches up depends on the fight, so their arrival at the goal
// isn't compared.
void Target(object t) { ChangeToStandardFaction(t, STANDARD_FACTION_COMMONER); SetPlotFlag(t, TRUE); SetLocalInt(t, "IA_NODONE", 1); }
object Attacker(string sRes, string sWp, string sTag)
{
    // Where a pursuer lands depends on where its target stands at that moment (it is placed clear of it).
    object a = Mk(sRes, sWp, sTag, TRUE, 1.0, 1.6);
    ChangeToStandardFaction(a, STANDARD_FACTION_HOSTILE);
    return a;
}

void Done(string sTag) { if (!GetLocalInt(GetObjectByTag(sTag), "IA_NODONE")) IADone(GetObjectByTag(sTag)); }

void Flee(string sTag, string sGoal)
{
    object t = GetObjectByTag(sTag);
    AssignCommand(t, ClearAllActions());
    AssignCommand(t, ActionMoveToObject(GetObjectByTag(sGoal), TRUE));
    AssignCommand(t, ActionDoCommand(Done(sTag)));
}

void JumpAway()
{
    object t = GetObjectByTag("JAT");
    AssignCommand(t, ClearAllActions());
    AssignCommand(t, JumpToLocation(GetLocation(GetObjectByTag("JA_JUMP"))));
}

void Drop(string sTag, string sWp)
{
    object o = GetObjectByTag(sTag);
    AssignCommand(o, ClearAllActions());
    AssignCommand(o, JumpToLocation(GetLocation(GetObjectByTag(sWp))));
}

void Summon()
{
    object m = GetObjectByTag("SM");
    AssignCommand(m, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire"), GetLocation(GetObjectByTag("SM_START")), 300.0));
}

void TrackSummon()
{
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, GetObjectByTag("SM"));
    SetTag(s, "SMS");
    IATrack(s, 0.75, 0.5);
}

void MasterGo()
{
    object sm = GetObjectByTag("SM");
    AssignCommand(sm, ActionMoveToObject(GetObjectByTag("SM_GOAL")));
    AssignCommand(sm, ActionDoCommand(Done("SM")));
}

void Attack(string sAttacker, string sTarget) { AssignCommand(GetObjectByTag(sAttacker), ActionAttack(GetObjectByTag(sTarget))); }

void Go()
{
    IAStart();
    Attack("KAA", "KAT"); Attack("KBA", "KBT"); Attack("KCA", "KCT");
    DelayCommand(4.0, Flee("KAT", "KA_GOAL")); DelayCommand(4.0, Flee("KBT", "KB_GOAL")); DelayCommand(4.0, Flee("KCT", "KC_GOAL"));
    Attack("JAA", "JAT"); DelayCommand(4.0, JumpAway());
    Attack("JBA", "JBT");
    Attack("JCA", "JCT"); DelayCommand(0.5, Flee("JCT", "JC_GOAL"));
    DelayCommand(1.0, TrackSummon());
    DelayCommand(1.0, MasterGo());
    Drop("IA_R1", "R1_DROP"); Drop("IA_R2", "R2_DROP"); Drop("IA_R3", "R3_DROP"); Drop("IA_R5", "R5_DROP");
}

void Fight(string sAttacker, string sTarget)
{
    object a = GetObjectByTag(sAttacker), t = GetObjectByTag(sTarget);
    PLogI(sAttacker + ".end.attacking", GetAttackTarget(a) == t);
    PLogI(sAttacker + ".end.samearea", GetArea(a) == GetArea(t));
}

void End()
{
    // Where fighters stand at the end depends on the fight (and fleeing targets can be bumped by their pursuers);
    // only the areas count.
    string s = "KAA KAT KBA KBT KCA KCT JAA JAT JBA JCA JCT";
    int i;
    for (i = 0; i < GetStringLength(s); i += 4) IAEnd(GetObjectByTag(GetSubString(s, i, 3)), FALSE);
    IAEnd(GetObjectByTag("SM"));
    IAEnd(GetObjectByTag("SMS"));
    Fight("KAA", "KAT"); Fight("KBA", "KBT"); Fight("KCA", "KCT"); Fight("JAA", "JAT"); Fight("JBA", "JBT"); Fight("JCA", "JCT");
    IAEnd(GetObjectByTag("IA_R1")); IAEnd(GetObjectByTag("IA_R2")); IAEnd(GetObjectByTag("IA_R3")); IAEnd(GetObjectByTag("IA_R5"));
    PDone();
}

void main()
{
    // Attackers whose target runs off in the middle of the fight: OpenWinter's attacker reacts to the target moving
    // away about half a second sooner (in-area combat, not this suite's subject) and so closes in sooner on the far
    // side; their times are looser.
    SetLocalFloat(Attacker("ia_walk", "KA_A", "KAA"), "IA_TOLT", 2.0); Target(Mk("ia_walk", "KA_T", "KAT"));
    SetLocalFloat(Attacker("ia_stock", "KB_A", "KBA"), "IA_TOLT", 2.0); Target(Mk("ia_walk", "KB_T", "KBT"));
    SetLocalFloat(Attacker("ia_walk", "KC_A", "KCA"), "IA_TOLT", 2.0); Target(Mk("ia_walk", "KC_T", "KCT"));
    Attacker("ia_walk", "JA_A", "JAA"); Target(Mk("ia_walk", "JA_T", "JAT"));
    Attacker("ia_walk", "JB_A", "JBA"); Target(Mk("ia_walk", "JB_T", "JBT"));
    Attacker("ia_walk", "JC_A", "JCA"); Target(Mk("ia_walk", "JC_T", "JCT"));
    Mk("ia_walk", "SM_START", "SM");
    // POST walkers: the first heartbeat after the drop comes at a random moment, so their times are loose.
    Mk("ia_stock", "POST_IA_R1", "IA_R1", FALSE, 7.0, 0.5);
    Mk("ia_stock", "POST_IA_R2", "IA_R2", TRUE, 7.0, 0.5);
    Mk("ia_stock", "POST_IA_R3", "IA_R3", TRUE, 7.0, 0.5);
    Mk("ia_stock", "POST_IA_R5", "IA_R5", TRUE, 7.0, 0.5);
    string d = "KA_AB KA_BC KB_AB KB_BC JA_AB JA_BC JB_AB JB_BC JC_AB SM_AB SM_BC R2_AB R2_BC R3_AB R3_CD R5_AB";
    int i;
    for (i = 0; i < GetStringLength(d); i += 6) { object o = GetObjectByTag(GetSubString(d, i, 5)); AssignCommand(o, ActionOpenDoor(o)); }
    DelayCommand(1.0, Summon());
    DelayCommand(8.0, Go());
    DelayCommand(85.0, End());
}

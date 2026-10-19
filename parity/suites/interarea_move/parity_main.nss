// Moves to objects and locations in other areas (docs/INTER_AREA_MOVEMENT.md): routes through doors and transition
// triggers, the route chosen, closed, locked, missing and script-set links, OnClick scripts, where a creature lands,
// and what a location move does after its first transition. Areas come from parity/gen_interarea.py.
#include "pinterarea_inc"

object Mk(string sPrefix, string sTag, int bNormalAI = TRUE, float fTolT = 0.75)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "ia_walk", GetLocation(GetObjectByTag(sPrefix + "_START")), FALSE, sTag);
    // Areas without players run at a very low AI level; the walkers act at the normal one (AL tests the default).
    if (bNormalAI) SetAILevel(o, AI_LEVEL_NORMAL);
    IATrack(o, fTolT);
    return o;
}

void Done(string sTag) { IADone(GetObjectByTag(sTag)); }

void ToObject(string sTag, string sGoal, int bRun = FALSE)
{
    object w = GetObjectByTag(sTag);
    AssignCommand(w, ActionMoveToObject(GetObjectByTag(sGoal), bRun));
    AssignCommand(w, ActionDoCommand(Done(sTag)));
}

void ToLocation(string sTag, string sGoal)
{
    object w = GetObjectByTag(sTag);
    AssignCommand(w, ActionMoveToLocation(GetLocation(GetObjectByTag(sGoal))));
    AssignCommand(w, ActionDoCommand(Done(sTag)));
}

void OpenD(string sTag) { object d = GetObjectByTag(sTag); AssignCommand(d, ActionOpenDoor(d)); }

void Queued(string sTag) { PLogI(sTag + ".queued.action", GetCurrentAction(GetObjectByTag(sTag))); }

void Go()
{
    IAStart();
    ToObject("MD", "MD_GOAL");
    ToObject("MT", "MT_GOAL", TRUE);
    ToObject("MR", "MR_GOAL", TRUE);
    ToLocation("LD", "LD_GOAL");
    ToLocation("LT", "LT_GOAL");
    object fd = GetObjectByTag("FD");
    AssignCommand(fd, ActionForceMoveToObject(GetObjectByTag("FD_GOAL"), FALSE, 1.0, 60.0));
    AssignCommand(fd, ActionDoCommand(Done("FD")));
    ToObject("CL", "CL_GOAL");
    AssignCommand(GetObjectByTag("AL"), ActionMoveToObject(GetObjectByTag("AL_GOAL")));
    ToObject("CK", "CK_GOAL");
    ToObject("CT", "CT_GOAL");
    ToObject("RC", "RC_GOAL");
    ToObject("LA", "LA_GOAL");
    ToObject("LK", "LK_GOAL");
    ToObject("NR", "NR_GOAL");
    ToLocation("NL", "NR_GOAL");
    ToObject("ML", "ML_GOAL");
    ToObject("ST", "ST_GOAL");
    ToObject("PF", "PF_GOAL");
    ToObject("PG", "PG_GOAL");
    ToObject("OH", "OH_GOAL");
    ToLocation("SA", "SA_GOAL");
    object ma = GetObjectByTag("MA");
    AssignCommand(ma, ActionMoveAwayFromObject(GetObjectByTag("MA_FROM"), FALSE, 10.0));
    AssignCommand(ma, ActionDoCommand(Done("MA")));
    // What a cross-area move reads on the queue straight away.
    DelayCommand(0.5, Queued("MD")); DelayCommand(0.5, Queued("NR")); DelayCommand(0.5, Queued("LD"));
    DelayCommand(0.5, Queued("MA"));
}

void End()
{
    string s = "MD MT MR LD LT FD CL CK CT RC LA LK NR NL ML ST PF PG OH SA MA";
    int i;
    for (i = 0; i < GetStringLength(s); i += 3) IAEnd(GetObjectByTag(GetSubString(s, i, 2)));
    IAEnd(GetObjectByTag("AL"), FALSE);
    // Standing at a locked door or without a route: the move is still queued.
    PLogI("LK.end.queued", GetCurrentAction(GetObjectByTag("LK")) != ACTION_INVALID);
    PLogI("NR.end.queued", GetCurrentAction(GetObjectByTag("NR")) != ACTION_INVALID);
    PLogI("ML.end.queued", GetCurrentAction(GetObjectByTag("ML")) != ACTION_INVALID);
    PLogI("CK.end.queued", GetCurrentAction(GetObjectByTag("CK")) != ACTION_INVALID);
    PDone();
}

void main()
{
    Mk("MD", "MD"); Mk("MT", "MT"); Mk("MR", "MR"); Mk("LD", "LD"); Mk("LT", "LT"); Mk("FD", "FD");
    SetLocalInt(Mk("CL", "CL"), "OPEN_ON_BLOCK", 1);
    object al = Mk("AL", "AL", FALSE, 1.5);
    Mk("CK", "CK");
    // Where a walker stands inside a trigger varies by a few tenths with the path grid.
    SetLocalFloat(Mk("CT", "CT"), "IA_TOLP", 0.6); Mk("RC", "RC", TRUE, 1.5); Mk("LA", "LA"); Mk("LK", "LK");
    Mk("NR", "NR"); Mk("NL", "NL"); Mk("ML", "ML"); Mk("ST", "ST"); Mk("PF", "PF"); Mk("PG", "PG"); Mk("OH", "OH");
    Mk("SA", "SA"); Mk("MA", "MA");
    OpenD("MD_0"); OpenD("MD_1"); OpenD("MD_2"); OpenD("MR_0"); OpenD("MR_2"); OpenD("LD_0"); OpenD("LD_1"); OpenD("LD_2");
    OpenD("LT_1"); OpenD("FD_0"); OpenD("FD_2"); OpenD("AL_0"); OpenD("AL_1"); OpenD("CK_0"); OpenD("CK_1"); OpenD("CT_1");
    OpenD("RC_L"); OpenD("RC_LD"); OpenD("RC_R"); OpenD("RC_R1"); OpenD("RC_R2"); OpenD("RC_X"); OpenD("LA_AC"); OpenD("LA_CB");
    OpenD("ML_AB"); OpenD("PF_AB"); OpenD("PG_AB"); OpenD("OH_AB"); OpenD("MA_AB");
    SetTransitionTarget(GetObjectByTag("ST_AB"), GetObjectByTag("ST_DEST"));
    DelayCommand(5.0, Go());
    DelayCommand(75.0, End());
}

// Perception order and heartbeat spacing. No players.
//  SP_A  one observer O (40, 40) and targets A (48, 40, made first) and B (34, 40). At 14 s A and B turn invisible and
//        D appears at (30, 40), west of both; at 30 s A and B are visible again. Expected: a pass evaluates the
//        creatures on the observer's list newest first, then the others west to east; a new target gives heard then
//        seen, one on the list gaining senses seen then heard, one losing them inaudible then vanished. E (40, 46)
//        jumps 38 m away (out of sight and hearing) at 14 s and back at 30 s; F (44, 34) turns invisible and silenced
//        at 14 s (both senses lost in one pass, in range) and back at 30 s.
//  SP_B  four observers along X made in the order O3 O1 O4 O2 and put on AI_LEVEL_NORMAL in the order O2 O4 O1 O3;
//        at 12 s a target T appears 10 m from all. Expected: observers are served in the order they joined the level.
//        Their first passes come in the same frame; by 12 s their clocks have drifted apart by frames, so the order in
//        which they notice T varies from run to run on nwserver (kept raw); each one's own events and the grouping
//        are compared.
//  SP_C  six creatures at AI_LEVEL_NORMAL and six left at the automatic level: heartbeat intervals (expected 6.0-7.2 s).
#include "parity_inc"
#include "parity_time"

object Mk(string sArea, float x, float y, string sName, int bObs = TRUE)
{
    object c = CreateObject(OBJECT_TYPE_CREATURE, "sp_c", Location(GetObjectByTag(sArea), Vector(x, y, 0.0), 0.0), FALSE, "SPX_" + sName);
    SetLocalString(c, "NM", sName);
    if (bObs) SetLocalString(c, "OBS", sName);
    return c;
}

void Phase(string sArea, string s) { SetLocalString(GetModule(), "PH_" + sArea, s); }

void Invis(object o) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectInvisibility(INVISIBILITY_TYPE_NORMAL), o); }
void Visible(object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_INVISIBILITY || GetEffectType(e) == EFFECT_TYPE_SILENCE) RemoveEffect(o, e);
        e = GetNextEffect(o);
    }
}

void AStep1()
{
    Phase("SP_A", "invis");
    Invis(GetObjectByTag("SPX_A")); Invis(GetObjectByTag("SPX_B")); Invis(GetObjectByTag("SPX_F"));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSilence(), GetObjectByTag("SPX_F"));
    SetAILevel(Mk("SP_A", 30.0, 40.0, "D", FALSE), AI_LEVEL_NORMAL);
    object e = GetObjectByTag("SPX_E");
    AssignCommand(e, JumpToLocation(Location(GetArea(e), Vector(40.0, 78.0, 0.0), 0.0)));
}
void AStep2()
{
    Phase("SP_A", "back");
    Visible(GetObjectByTag("SPX_A")); Visible(GetObjectByTag("SPX_B")); Visible(GetObjectByTag("SPX_F"));
    object e = GetObjectByTag("SPX_E");
    AssignCommand(e, JumpToLocation(Location(GetArea(e), Vector(40.0, 46.0, 0.0), 0.0)));
}

void BStep() { Phase("SP_B", "target"); SetAILevel(Mk("SP_B", 17.5, 20.0, "T", FALSE), AI_LEVEL_NORMAL); }

void Report(string sArea, string sPhase)
{
    if (sPhase == "target")
    {
        int i;
        for (i = 1; i <= 4; i++) PLog("sp_b.target.O" + IntToString(i), GetLocalString(GetModule(), "OBS_SP_B_target_O" + IntToString(i)));
        PLogI("sp_b.target.switches", GetLocalInt(GetModule(), "SW_SP_B_target"));
        WriteTimestampedLogEntry("RAW|SP_B.target|" + GetLocalString(GetModule(), "RAWSEQ_SP_B_target"));
        return;
    }
    string k = "SEQ_" + sArea + "_" + sPhase;
    PLog(GetStringLowerCase(sArea) + "." + sPhase, GetLocalString(GetModule(), k));
    WriteTimestampedLogEntry("RAW|" + sArea + "." + sPhase + "|" + GetLocalString(GetModule(), "RAW" + k));
}

void Beats(string g, float fTolMean)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + "N");
    PLogI(g + ".enough", n >= 20);
    PLogF(g + ".mean", GetLocalFloat(m, g + "SUM") / IntToFloat(n > 0 ? n : 1), fTolMean);
    WriteTimestampedLogEntry("RAW|" + g + "|n=" + IntToString(n) + " min=" + FloatToString(GetLocalFloat(m, g + "MIN"), 0, 2)
        + " max=" + FloatToString(GetLocalFloat(m, g + "MAX"), 0, 2) + " |" + GetLocalString(m, g + "RAW"));
}

void Beats2()
{
    object m = GetModule();
    PLogI("normal.min.ge6", GetLocalFloat(m, "normalMIN") >= 5.95);
    PLogI("normal.max.le7.3", GetLocalFloat(m, "normalMAX") <= 7.35);
}

void main()
{
    PStart();
    Phase("SP_A", "first"); Phase("SP_B", "first");
    // SP_A
    object o = Mk("SP_A", 40.0, 40.0, "O");
    object a = Mk("SP_A", 48.0, 40.0, "A", FALSE);
    object b = Mk("SP_A", 34.0, 40.0, "B", FALSE);
    SetAILevel(Mk("SP_A", 40.0, 46.0, "E", FALSE), AI_LEVEL_NORMAL);
    SetAILevel(Mk("SP_A", 44.0, 34.0, "F", FALSE), AI_LEVEL_NORMAL);
    SetAILevel(o, AI_LEVEL_NORMAL); SetAILevel(a, AI_LEVEL_NORMAL); SetAILevel(b, AI_LEVEL_NORMAL);
    DelayCommand(14.0, AStep1());
    DelayCommand(30.0, AStep2());
    // SP_B
    object o3 = Mk("SP_B", 20.0, 10.0, "O3"), o1 = Mk("SP_B", 10.0, 10.0, "O1");
    object o4 = Mk("SP_B", 25.0, 10.0, "O4"), o2 = Mk("SP_B", 15.0, 10.0, "O2");
    SetAILevel(o2, AI_LEVEL_NORMAL); SetAILevel(o4, AI_LEVEL_NORMAL); SetAILevel(o1, AI_LEVEL_NORMAL); SetAILevel(o3, AI_LEVEL_NORMAL);
    DelayCommand(12.0, BStep());
    // SP_C
    int i;
    for (i = 0; i < 6; i++)
    {
        object n = Mk("SP_C", 10.0 + IntToFloat(i) * 6.0, 10.0, "N" + IntToString(i), FALSE);
        SetLocalString(n, "HB", "normal");
        SetAILevel(n, AI_LEVEL_NORMAL);
        object v = Mk("SP_C", 10.0 + IntToFloat(i) * 6.0, 60.0, "V" + IntToString(i), FALSE);
        SetLocalString(v, "HB", "verylow");
    }
    DelayCommand(46.0, Report("SP_A", "first"));
    DelayCommand(46.0, Report("SP_A", "invis"));
    DelayCommand(46.0, Report("SP_A", "back"));
    DelayCommand(46.0, Report("SP_B", "first"));
    DelayCommand(46.0, Report("SP_B", "target"));
    DelayCommand(46.0, Beats("normal", 0.25));
    DelayCommand(46.0, Beats("verylow", 0.6));
    DelayCommand(46.0, Beats2());
    DelayCommand(46.5, PDone());
}

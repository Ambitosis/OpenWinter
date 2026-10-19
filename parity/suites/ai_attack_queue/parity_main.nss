// What an attack order does when another action is queued behind it (the stock AI queues ActionDoCommand after
// ActionAttack in DetermineCombatRound): does the attack end, when does the queued action run, do the swings go on.
// Scripted attackers (no event scripts) beside immortal targets that only log their attacks.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }

object Mk(string sRes, float x, float y, string sTag, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(Area(), Vector(x, y, 0.0), 0.0), FALSE, sTag);
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    AssignCommand(o, ClearAllActions(TRUE));
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetImmortal(o, TRUE);
    return o;
}

object A(int n) { return GetObjectByTag("FU_A" + IntToString(n)); }
object T(int n) { return GetObjectByTag("FU_T" + IntToString(n)); }

void Pair(int n)
{
    float x = 6.0 + 12.0 * IntToFloat(n % 4), y = 6.0 + 14.0 * IntToFloat(n / 4);
    Mk("nw_bandit001", x, y, "FU_A" + IntToString(n), STANDARD_FACTION_DEFENDER);
    object t = Mk("nw_bandit001", x + 1.6, y, "FU_T" + IntToString(n), STANDARD_FACTION_HOSTILE);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "fu_atk");
}

void Mark(object a, string s)
{
    SetLocalString(a, "FU_MARK", GetLocalString(a, "FU_MARK") + s + "@" + FloatToString(PSince() - GetLocalFloat(a, "FU_T0"), 0, 1) + " ");
}

void Poll(int n)
{
    object c = A(n);
    int a = GetCurrentAction(c);
    string s = (a == ACTION_INVALID ? "none" : IntToString(a)) + (GetIsObjectValid(GetAttackTarget(c)) ? "+" : "");
    if (s != GetLocalString(c, "FU_LAST"))
    {
        SetLocalString(c, "FU_LAST", s);
        SetLocalString(c, "FU_ACTS", GetLocalString(c, "FU_ACTS") + s + "@" + FloatToString(PSince() - GetLocalFloat(c, "FU_T0"), 0, 1) + " ");
    }
}

void PollAll(float fLeft)
{
    int n;
    for (n = 0; n < 8; n++) Poll(n);
    if (fLeft > 0.0) DelayCommand(0.1, PollAll(fLeft - 0.1));
}

void Go()
{
    int n;
    for (n = 0; n < 8; n++)
    {
        object a = A(n), t = T(n);
        SetLocalFloat(a, "FU_T0", PSince());
        AssignCommand(a, ActionAttack(t));
        // 0: attack and a command queued at once; 1: the command queued 3 s later; 2: a 2 s wait queued 3 s later;
        // 3: a move queued 3 s later; 4: a second attack queued 3 s later; 5: attack alone (control);
        // 6: the stock pattern, ActionAttack then ActionDoCommand at once, again 6 s later after ClearAllActions;
        // 7: a command queued 6.5 s later (mid-round of the second round).
        if (n == 0) AssignCommand(a, ActionDoCommand(Mark(a, "cmd")));
        if (n == 1) DelayCommand(3.0, AssignCommand(a, ActionDoCommand(Mark(a, "cmd"))));
        if (n == 2) DelayCommand(3.0, AssignCommand(a, ActionWait(2.0)));
        if (n == 2) DelayCommand(3.0, AssignCommand(a, ActionDoCommand(Mark(a, "afterwait"))));
        if (n == 3) DelayCommand(3.0, AssignCommand(a, ActionMoveToLocation(Location(Area(), GetPosition(a) + Vector(0.0, 5.0, 0.0), 0.0))));
        if (n == 4) DelayCommand(3.0, AssignCommand(a, ActionAttack(t)));
        if (n == 4) DelayCommand(3.0, AssignCommand(a, ActionDoCommand(Mark(a, "after2nd"))));
        if (n == 6) AssignCommand(a, ActionDoCommand(Mark(a, "cmd")));
        if (n == 6) DelayCommand(6.0, AssignCommand(a, ClearAllActions()));
        if (n == 6) DelayCommand(6.0, AssignCommand(a, ActionAttack(t)));
        if (n == 6) DelayCommand(6.0, AssignCommand(a, ActionDoCommand(Mark(a, "cmd2"))));
        if (n == 7) DelayCommand(6.5, AssignCommand(a, ActionDoCommand(Mark(a, "cmd"))));
    }
}

void Report()
{
    int n;
    for (n = 0; n < 8; n++)
    {
        object a = A(n);
        string k = "q" + IntToString(n);
        PLogSeq(k + ".acts", GetLocalString(a, "FU_ACTS"), 0.6);
        PLogSeq(k + ".marks", GetLocalString(a, "FU_MARK"), 0.6);
        PLogI(k + ".combat", GetIsInCombat(a));
    }
    PDone();
}

void Setup()
{
    int n;
    for (n = 0; n < 8; n++) Pair(n);
}

void main()
{
    PStart();
    DelayCommand(1.0, Setup());
    DelayCommand(7.0, Go());
    DelayCommand(7.0, PollAll(20.0));
    DelayCommand(28.0, Report());
}

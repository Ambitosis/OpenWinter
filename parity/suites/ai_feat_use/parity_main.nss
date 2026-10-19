// Combat feats used by script, as the stock AI uses them (TalentMeleeAttack: ClearActions, then
// ActionUseTalentOnObject of a TALENT_CATEGORY_HARMFUL_MELEE feat): the action the attacker shows, the attack types
// its target sees (GetLastAttackType / GetLastAttackMode in OnPhysicalAttacked), the effects left on the target
// (knockdown) and a disarmed weapon. Attackers have no event scripts; targets only log their attacks.
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

void Pair(int n, string sRes)
{
    float x = 6.0 + 12.0 * IntToFloat(n % 6), y = 6.0 + 14.0 * IntToFloat(n / 6);
    Mk(sRes, x, y, "FU_A" + IntToString(n), STANDARD_FACTION_DEFENDER);
    object t = Mk("nw_bandit001", x + 1.6, y, "FU_T" + IntToString(n), STANDARD_FACTION_HOSTILE);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "fu_atk");
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
    object t = T(n);
    effect e = GetFirstEffect(t);
    string sFx = "";
    while (GetIsEffectValid(e)) { sFx += IntToString(GetEffectType(e)) + ","; e = GetNextEffect(t); }
    if (sFx != GetLocalString(c, "FU_FXLAST"))
    {
        SetLocalString(c, "FU_FXLAST", sFx);
        SetLocalString(c, "FU_FX", GetLocalString(c, "FU_FX") + (sFx == "" ? "-" : sFx) + "@" + FloatToString(PSince() - GetLocalFloat(c, "FU_T0"), 0, 1) + " ");
    }
}

void PollAll(float fLeft)
{
    int n;
    for (n = 0; n < 10; n++) Poll(n);
    if (fLeft > 0.0) DelayCommand(0.1, PollAll(fLeft - 0.1));
}

void Start(int n) { SetLocalFloat(A(n), "FU_T0", PSince()); }

void Feat(int n, int nFeat, int bTalent, int bClear)
{
    object a = A(n), t = T(n);
    if (bClear) AssignCommand(a, ClearAllActions());
    if (bTalent) AssignCommand(a, ActionUseTalentOnObject(TalentFeat(nFeat), t));
    else AssignCommand(a, ActionUseFeat(nFeat, t));
}

void Go()
{
    int n;
    for (n = 0; n < 10; n++) Start(n);
    Feat(0, FEAT_KNOCKDOWN, FALSE, FALSE);
    Feat(1, FEAT_KNOCKDOWN, TRUE, FALSE);
    AssignCommand(A(2), ActionAttack(T(2)));
    DelayCommand(6.5, Feat(2, FEAT_KNOCKDOWN, TRUE, TRUE));
    AssignCommand(A(3), ActionAttack(T(3)));
    DelayCommand(6.5, Feat(3, FEAT_KNOCKDOWN, FALSE, FALSE));
    Feat(4, FEAT_IMPROVED_POWER_ATTACK, TRUE, FALSE);
    Feat(5, FEAT_POWER_ATTACK, FALSE, FALSE);
    Feat(6, FEAT_DISARM, TRUE, FALSE);
    AssignCommand(A(7), ActionAttack(T(7)));
    DelayCommand(6.5, Feat(7, FEAT_DISARM, TRUE, TRUE));
    AssignCommand(A(8), ActionAttack(T(8)));
    DelayCommand(5.0, Feat(8, FEAT_KNOCKDOWN, TRUE, TRUE));
    AssignCommand(A(9), ActionAttack(T(9)));
    DelayCommand(7.8, Feat(9, FEAT_KNOCKDOWN, TRUE, TRUE));
}

void Report()
{
    string s = "kd.fresh kd.talent kd.attacking kd.queued ipa.talent pa.feat disarm.talent disarm.attacking kd.attacking5 kd.attacking78 ";
    int n = 0;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string k = "feat." + GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 1000);
        object a = A(n), t = T(n);
        PLogSeq(k + ".acts", GetLocalString(a, "FU_ACTS"), 0.6);
        PLogSeq(k + ".fx", GetLocalString(a, "FU_FX"), 0.6);
        PLog(k + ".weapon", GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, t)) ? "held" : "gone");
        n++;
    }
    PDone();
}

void Setup()
{
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    int n;
    for (n = 0; n < 10; n++) Pair(n, n == 6 || n == 7 ? "nw_orcboss" : "nw_dwarfmerc005");
}

void main()
{
    PStart();
    DelayCommand(1.0, Setup());
    DelayCommand(7.0, Go());
    DelayCommand(7.0, PollAll(20.0));
    DelayCommand(28.0, Report());
}

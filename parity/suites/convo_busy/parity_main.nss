// When a request to talk is refused because the creature asked is busy, between two creatures (nwserver has no
// player): A<n> asks B<n> with ActionStartConversation and B's OnConversation logs whether it ran. The original
// refuses while B is in combat (fighting, or attacked lately) or dead, silently; B casting, walking, uncommandable or
// hostile, or A in combat, make no difference. Between creatures the request only runs B's OnConversation, whatever
// dialog is named. See docs/CONVERSATION_ENDINGS.md ("Busy") and the visual scenes convobusy to convobusy4.
#include "cb_inc"
#include "pcombat_inc"

const int CASES = 12;
object A(int n) { return GetObjectByTag("A" + IntToString(n)); }
object B(int n) { return GetObjectByTag("B" + IntToString(n)); }
object D(int n) { return GetObjectByTag("D" + IntToString(n)); }

object Make(string sTag, float x, float y, float f)
{
    object o = PSpawn("nw_goblina", x, y, f);
    SetTag(o, sTag);
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetPlotFlag(o, TRUE);
    return o;
}

// Prelude's faction table (suite.json) makes the hostile cases really hostile (without one every NPC faction likes
// every other on nwserver); the readings were the same.
// Each case in its own 9 x 18 m cell: A, B 2 m east of it, and a hostile dummy D beside B.
void Spawn(int n)
{
    float x = 4.0 + IntToFloat(n % 6) * 9.0, y = 5.0 + IntToFloat(n / 6) * 18.0;
    Make("A" + IntToString(n), x, y, 0.0);
    object b = Make("B" + IntToString(n), x + 2.0, y, 180.0);
    object d = Make("D" + IntToString(n), x + 2.0, y + 2.5, 270.0);
    ChangeToStandardFaction(d, STANDARD_FACTION_HOSTILE);
    SetEventScript(b, EVENT_SCRIPT_CREATURE_ON_DIALOGUE, "cb_conv");
}

void Talk(object oFrom, object oTo, string sDlg = "cb_talk") { AssignCommand(oFrom, ActionStartConversation(oTo, sDlg, FALSE, FALSE)); }
void Attack(object oFrom, object oTo) { AssignCommand(oFrom, ActionAttack(oTo)); }

void Setup()
{
    int i;
    // 1: B fights the dummy. 2: B casts (a queue of spells at itself). 3: B walks about. 4: dead.
    Attack(B(1), D(1));
    for (i = 0; i < 12; i++) AssignCommand(B(2), ActionCastSpellAtObject(SPELL_ACID_SPLASH, B(2), METAMAGIC_NONE, TRUE));
    AssignCommand(B(3), ActionRandomWalk());
    SetPlotFlag(B(4), FALSE); ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), B(4));
    // 5: B not commandable. 6: B hostile to A. 7: A fights (and is cleared without leaving combat before asking).
    SetCommandable(FALSE, B(5));
    ChangeToStandardFaction(B(6), STANDARD_FACTION_HOSTILE);
    Attack(A(7), D(7));
    // 8: the dummy attacks B, which has no script to fight back. 9: B fights; A names no dialog.
    Attack(D(8), B(8));
    Attack(B(9), D(9));
    // 10: B fought and stopped (ClearAllActions(TRUE) at 3 s). 11: B was attacked and the attacker stopped at 3 s.
    Attack(B(10), D(10));
    Attack(D(11), B(11));
}

void Start()
{
    int n;
    AssignCommand(A(7), ClearAllActions());
    for (n = 0; n < CASES; n++) Talk(A(n), B(n), n == 9 ? "" : "cb_talk");
}

void Sample(string sAt)
{
    int n;
    for (n = 0; n < CASES; n++)
    {
        object a = A(n), b = B(n);
        string k = "c" + IntToString(n) + "." + sAt;
        PLog(k + ".action", IntToString(GetCurrentAction(a)) + "/" + IntToString(GetCurrentAction(b)));
        PLog(k + ".combat", IntToString(GetIsInCombat(a)) + IntToString(GetIsInCombat(b)));
    }
}

void Finish()
{
    int n;
    for (n = 0; n < CASES; n++)
    {
        PLog("c" + IntToString(n) + ".events", GetLocalString(GetModule(), "ev" + IntToString(n)));
        PLogI("c" + IntToString(n) + ".talking", IsInConversation(A(n)) + 2 * IsInConversation(B(n)));
    }
    PDone();
}

void main()
{
    int n;
    for (n = 0; n < CASES; n++) Spawn(n);
    PClockStart(12.0);
    DelayCommand(1.0, Setup());
    DelayCommand(3.0, AssignCommand(B(10), ClearAllActions(TRUE)));
    DelayCommand(3.0, AssignCommand(D(11), ClearAllActions(TRUE)));
    DelayCommand(3.5, Sample("pre"));
    DelayCommand(4.0, Start());
    DelayCommand(9.0, Finish());
}

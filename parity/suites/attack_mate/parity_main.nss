// Does ActionAttack work on a member of the attacker's own faction? (A confused creature whose nearest creature is a
// faction mate does nothing on nwserver, cond_confuse; the stock NW_G0_confuse just calls ActionAttack on the nearest.)
// A stripped nw_bandit001 A attacks a stripped, immortal T 1.5 m away:
//   mate      both hostile faction
//   mate.conf as mate, A confused first (its state script replaced by nothing: ActionAttack is given by this script)
//   friend    A hostile, T commoner with a personal reputation of 100 both ways
//   enemy     A hostile, T commoner
//   mate.cmd  both commoner faction
//   mate.def, mate.merch  both defender, both merchant
//   mate.cmd.friend  both commoner, personal reputation 100 both ways
//   mate.enemy  both hostile, personal reputation 0 both ways
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
int nCell = 0;

object Make(float x, float y, int nFaction, string g)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(Area(), Vector(x, y, 0.0), 0.0));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, nFaction);
    SetImmortal(o, TRUE);
    SetLocalString(o, "G", g);
    return o;
}

void Cell(string g, int nA, int nT, int bFriend = FALSE)
{
    float x = 8.0 + IntToFloat(nCell % 4) * 12.0, y = 8.0 + IntToFloat(nCell / 4) * 12.0;
    nCell++;
    object a = Make(x, y, nA, g);
    object t = Make(x + 1.5, y, nT, g);
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "am_atk");
    if (bFriend == 2)
    {
        SetIsTemporaryEnemy(t, a);
        SetIsTemporaryEnemy(a, t);
    }
    else if (bFriend)
    {
        SetIsTemporaryFriend(t, a);
        SetIsTemporaryFriend(a, t);
    }
    DelayCommand(2.0, AssignCommand(a, ActionAttack(t)));
    DelayCommand(2.5, PLogI(g + ".action", GetCurrentAction(a)));
    DelayCommand(4.0, PLogI(g + ".target", GetAttackTarget(a) == t));
    DelayCommand(4.0, PLogI(g + ".rep", GetReputation(a, t)));
    DelayCommand(14.0, PLogI(g + ".attacked", GetLocalInt(GetModule(), g + ".atk") > 0));
    DelayCommand(14.0, PLogI(g + ".action14", GetCurrentAction(a)));
}

void main()
{
    Cell("mate", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_HOSTILE);
    Cell("mate.cmd", STANDARD_FACTION_COMMONER, STANDARD_FACTION_COMMONER);
    Cell("friend", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_COMMONER, TRUE);
    Cell("enemy", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_COMMONER);
    Cell("mate.def", STANDARD_FACTION_DEFENDER, STANDARD_FACTION_DEFENDER);
    Cell("mate.merch", STANDARD_FACTION_MERCHANT, STANDARD_FACTION_MERCHANT);
    Cell("mate.cmd.friend", STANDARD_FACTION_COMMONER, STANDARD_FACTION_COMMONER, TRUE);
    Cell("mate.enemy", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_HOSTILE, 2);
    DelayCommand(15.0, PDone());
}

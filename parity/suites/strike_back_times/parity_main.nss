// When strike_back's casts start and end, and when the victim's attacks come.
// As strike_back: victim V a stripped nw_bandit001 (commoner, immortal,
// facing east), caster C a level-5 wizard with Magic Missile prepared (hostile, immortal) 1.5 m east of it, facing
// it. Every 0.1 s C's current action ("a<n>") and its Defensive Casting mode ("dc<n>") are logged when they change,
// with V's attacks on C ("V") and whether V is in combat ("vc<n>").
//   adj       casts ordered at 8 and 10 s;  adj.late  at 8 and 18 s;  adj.cheat  cheat casts at 8 and 10 s
//   adj.def   as adj, SetActionMode(ACTION_MODE_DEFENSIVE_CAST) at 1 s (two cells)
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
int nCell = 0;

void Strip(object o)
{
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
}

void Note(object c, string s) { SetLocalString(c, "SEQ", GetLocalString(c, "SEQ") + s + "@" + FloatToString(PSince(), 0, 2) + " "); }

void Watch(object c, object v, float t)
{
    if (t > 38.0) return;
    int a = GetCurrentAction(c), dc = GetActionMode(c, ACTION_MODE_DEFENSIVE_CAST), vc = GetIsInCombat(v);
    if (a != GetLocalInt(c, "LA") || t == 0.0) { Note(c, "a" + IntToString(a)); SetLocalInt(c, "LA", a); }
    if (dc != GetLocalInt(c, "LD") || t == 0.0) { Note(c, "dc" + IntToString(dc)); SetLocalInt(c, "LD", dc); }
    if (vc != GetLocalInt(c, "LV") || t == 0.0) { Note(c, "vc" + IntToString(vc)); SetLocalInt(c, "LV", vc); }
    DelayCommand(0.1, Watch(c, v, t + 0.1));
}

void Cast(object c, object v)
{
    Note(c, "order.dc" + IntToString(GetActionMode(c, ACTION_MODE_DEFENSIVE_CAST)));
    if (GetLocalInt(c, "CHEAT")) AssignCommand(c, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, v, METAMAGIC_ANY, TRUE));
    else AssignCommand(c, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, v));
}

void Cell(string g, float fSecond, int bDefensive = FALSE, int bCheat = FALSE)
{
    float x = 12.0 + IntToFloat(nCell % 4) * 22.0, y = 10.0 + IntToFloat(nCell / 4) * 22.0;
    nCell++;
    object v = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(Area(), Vector(x, y, 0.0), 0.0));
    Strip(v);
    ChangeToStandardFaction(v, STANDARD_FACTION_COMMONER);
    SetImmortal(v, TRUE);
    object c = CreateObject(OBJECT_TYPE_CREATURE, "sb_wiz", Location(Area(), Vector(x + 1.5, y, 0.0), 180.0));
    Strip(c);
    ChangeToStandardFaction(c, STANDARD_FACTION_HOSTILE);
    SetImmortal(c, TRUE);
    SetLocalString(c, "G", g);
    SetLocalInt(c, "CHEAT", bCheat);
    SetLocalInt(c, "LA", -2);
    SetEventScript(c, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "sb_atk");
    SetLocalObject(GetModule(), g, c);
    if (bDefensive) DelayCommand(1.0, SetActionMode(c, ACTION_MODE_DEFENSIVE_CAST, TRUE));
    DelayCommand(0.5, Watch(c, v, 0.0));
    DelayCommand(8.0, Cast(c, v));
    DelayCommand(fSecond, Cast(c, v));
}

void Report(string g)
{
    WriteTimestampedLogEntry("RAW|" + g + "|" + GetLocalString(GetLocalObject(GetModule(), g), "SEQ"));
}

void main()
{
    PStart();
    Cell("adj", 10.0);
    Cell("adj2", 10.0);
    Cell("adj.late", 18.0);
    Cell("adj.cheat", 10.0, FALSE, TRUE);
    Cell("adj.def", 10.0, TRUE);
    Cell("adj.def2", 10.0, TRUE);
    Cell("adj.def3", 10.0, TRUE);
    string gs = "adj adj2 adj.late adj.cheat adj.def adj.def2 adj.def3 ";
    while (gs != "")
    {
        int sp = FindSubString(gs, " ");
        DelayCommand(39.0, Report(GetStringLeft(gs, sp)));
        gs = GetSubString(gs, sp + 1, 1000);
    }
    DelayCommand(40.0, PDone());
}

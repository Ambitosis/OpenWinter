// Sanctuary and perception (Prelude factions: observers hostile to their subjects). Groups of ten observer/subject
// pairs 3 m apart; the subject has EffectSanctuary:
//   early100  DC 100, applied as the pair is created (before the observer's first perception update)
//   early1    DC 1 (every observer saves but on a natural 1)
//   late100   DC 100, applied 10 s later, when the observer already sees the subject
// Every 2 s: how many observers in the group see and hear their subject; and the perception events by kind.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }

object Make(string sRes, float x, float y, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(Area(), Vector(x, y, 0.0), 0.0));
    int e; for (e = 5000; e <= 5012; e++) if (e != 5001 || sRes != "sr_obs") SetEventScript(o, e, "");
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Sanct(object h, int nDC) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSanctuary(nDC), h); }

void Count(string g, float t)
{
    int seen = 0, heard = 0, i;
    for (i = 0; i < 10; i++)
    {
        object o = GetLocalObject(GetModule(), g + IntToString(i));
        object h = GetLocalObject(o, "H");
        seen += GetObjectSeen(h, o);
        heard += GetObjectHeard(h, o);
    }
    string k = g + ".t" + IntToString(FloatToInt(t));
    PLogF(k + ".seen", IntToFloat(seen), 2.0);
    PLogF(k + ".heard", IntToFloat(heard), 2.0);
}

void Events(string g)
{
    object m = GetModule();
    PLogF(g + ".ev.seen", IntToFloat(GetLocalInt(m, g + ".ev.seen")), 3.0);
    PLogF(g + ".ev.vanished", IntToFloat(GetLocalInt(m, g + ".ev.vanished")), 3.0);
    PLogF(g + ".ev.heard", IntToFloat(GetLocalInt(m, g + ".ev.heard")), 3.0);
    PLogF(g + ".ev.inaudible", IntToFloat(GetLocalInt(m, g + ".ev.inaudible")), 3.0);
}

void Group(string g, int nBase, int nDC, float fDelay)
{
    int i;
    for (i = 0; i < 10; i++)
    {
        int n = nBase + i;
        float x = 8.0 + IntToFloat(n % 6) * 22.0, y = 8.0 + IntToFloat(n / 6) * 22.0;
        object o = Make("sr_obs", x, y, STANDARD_FACTION_COMMONER);
        object h = Make("nw_bandit001", x + 3.0, y, STANDARD_FACTION_HOSTILE);
        SetLocalObject(o, "H", h);
        SetLocalString(o, "G", g);
        SetLocalObject(GetModule(), g + IntToString(i), o);
        if (fDelay <= 0.0) Sanct(h, nDC);
        else DelayCommand(fDelay, Sanct(h, nDC));
    }
    float t;
    for (t = 2.0; t <= 40.0; t += 2.0) DelayCommand(t, Count(g, t));
    DelayCommand(41.0, Events(g));
}

void main()
{
    Group("early100", 0, 100, 0.0);
    Group("early1", 10, 1, 0.0);
    Group("late100", 20, 100, 10.0);
    DelayCommand(42.0, PDone());
}

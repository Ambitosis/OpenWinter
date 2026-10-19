// Which standard faction a henchman lands in when it is released: henchmen of an NPC master made from blueprints in
// FactionID 2, 3 and 4 (fa_f2..fa_f4), released by RemoveHenchman (they keep their master through the master's
// death). Used by faction_nofac (no repute.fac) and faction_release (Prelude's repute.fac).
#include "parity_inc"

object FrStd(int f) { return GetLocalObject(GetModule(), "fr_std" + IntToString(f)); }

string FrWhich(object o)
{
    string s = "";
    int f;
    for (f = 0; f < 4; f++) s += IntToString(GetFactionEqual(o, FrStd(f)));
    return s;
}

object FrSpawn(string r, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, r, PCentre(x, y));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    return o;
}

void FrRead(string k, object o, object m)
{
    PLog(k, FrWhich(o) + " master=" + IntToString(GetMaster(o) == m) + " withMaster=" + IntToString(GetFactionEqual(o, m)));
}

void FrCase(int i, string r)
{
    object m = FrSpawn("nw_bandit001", -15.0, IntToFloat(i) * 4.0 - 10.0);
    ChangeToStandardFaction(m, STANDARD_FACTION_DEFENDER);
    object h = FrSpawn(r, -12.0, IntToFloat(i) * 4.0 - 10.0);
    PLog("release." + r + ".start", FrWhich(h));
    AddHenchman(m, h);
    DelayCommand(0.5, FrRead("release." + r + ".hired", h, m));
    DelayCommand(1.0, RemoveHenchman(m, h));
    DelayCommand(1.5, FrRead("release." + r + ".fired", h, m));
    // A second one whose master dies.
    object d = FrSpawn(r, -9.0, IntToFloat(i) * 4.0 - 10.0);
    object m2 = FrSpawn("nw_bandit001", -6.0, IntToFloat(i) * 4.0 - 10.0);
    ChangeToStandardFaction(m2, STANDARD_FACTION_DEFENDER);
    AddHenchman(m2, d);
    DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), m2));
    DelayCommand(2.0, FrRead("release." + r + ".masterDied", d, m2));
    // (A henchman that dies is released on nwserver and keeps its master here: not compared.)
}

void FrRun()
{
    int f;
    for (f = 0; f < 4; f++)
    {
        object o = FrSpawn("nw_bandit001", 15.0, IntToFloat(f) * 3.0 - 6.0);
        ChangeToStandardFaction(o, f);
        SetLocalObject(GetModule(), "fr_std" + IntToString(f), o);
    }
    FrCase(0, "fa_f2");
    FrCase(1, "fa_f3");
    FrCase(2, "fa_f4");
}

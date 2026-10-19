// GetHasSpell for special abilities (SpecAbilityList): the imp (Invisibility 90 three times, Ghoul Touch 107 twice),
// fixtures with one, four, mixed ready/used and only used entries, and a wizard with Mage Armor (102) memorised twice
// and as a special ability three times. Read as spawned, after each DecrementRemainingSpellUses, after casting, and
// after ForceRest.
#include "parity_inc"

object Spawn(string r, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, r, PCentre(x, y));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Read(string k, object o)
{
    PLog(k, "s90=" + IntToString(GetHasSpell(90, o)) + " s107=" + IntToString(GetHasSpell(107, o))
        + " s102=" + IntToString(GetHasSpell(102, o)));
}

void Dec(object o, int nSpell) { DecrementRemainingSpellUses(o, nSpell); }

// Spend nSpell five times, reading after each.
void Spend(string k, string r, float y, int nSpell)
{
    object o = Spawn(r, -10.0, y);
    DelayCommand(0.5, Read(k + ".start", o));
    int i;
    for (i = 1; i <= 5; i++)
    {
        DelayCommand(0.5 + IntToFloat(i) * 0.5, Dec(o, nSpell));
        DelayCommand(0.7 + IntToFloat(i) * 0.5, Read(k + ".dec" + IntToString(i), o));
    }
    DelayCommand(4.0, ForceRest(o));
    DelayCommand(4.5, Read(k + ".rest", o));
}

// Cast nSpell (not a cheat) three times, reading after each.
void Cast(string k, string r, float y, int nSpell)
{
    object o = Spawn(r, 10.0, y);
    DelayCommand(0.5, Read(k + ".start", o));
    int i;
    for (i = 1; i <= 3; i++)
    {
        DelayCommand(IntToFloat(i) * 5.0 - 4.0, AssignCommand(o, ActionCastSpellAtObject(nSpell, o, METAMAGIC_ANY, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
        DelayCommand(IntToFloat(i) * 5.0, Read(k + ".cast" + IntToString(i), o));
    }
}

void main()
{
    Spend("imp90", "nw_imp", -15.0, 90);
    Spend("imp107", "nw_imp", -12.0, 107);
    Spend("one", "ha_one", -9.0, 90);
    Spend("mixed", "ha_mixed", -6.0, 90);
    Spend("unready", "ha_unready", -3.0, 90);
    Spend("four", "ha_four", 0.0, 90);
    Spend("wiz", "ha_wiz", 3.0, 102);
    Cast("impCast", "nw_imp", -15.0, 90);
    Cast("wizCast", "ha_wiz", -10.0, 102);
    Cast("fourCast", "ha_four", -5.0, 90);
    DelayCommand(18.0, PDone());
}

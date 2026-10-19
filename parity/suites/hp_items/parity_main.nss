// Hit points and equipped Constitution items: henchman blueprints nw_hen_bod_05 and nw_hen_lin_05 wear boots of
// +2 Constitution (nw_it_mboots006); nw_hen_dae_05 wears none. Read as spawned, after taking the boots off, after
// putting them back on, after giving them to Daelan, and on a copy (CopyObject writes and reads the creature back).
#include "parity_inc"

object Spawn(string r, float x)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, r, PCentre(x, 0.0));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Read(string k, object o)
{
    PLog(k, IntToString(GetMaxHitPoints(o)) + "/" + IntToString(GetCurrentHitPoints(o))
        + " con=" + IntToString(GetAbilityScore(o, ABILITY_CONSTITUTION))
        + " baseCon=" + IntToString(GetAbilityScore(o, ABILITY_CONSTITUTION, TRUE))
        + " boots=" + GetResRef(GetItemInSlot(INVENTORY_SLOT_BOOTS, o)));
}

void Off(object o) { AssignCommand(o, ActionUnequipItem(GetItemInSlot(INVENTORY_SLOT_BOOTS, o))); }

void On(object o)
{
    object b = GetItemPossessedBy(o, "NW_IT_MBOOTS006");
    if (!GetIsObjectValid(b)) b = GetFirstItemInInventory(o);
    while (GetIsObjectValid(b) && GetResRef(b) != "nw_it_mboots006") b = GetNextItemInInventory(o);
    AssignCommand(o, ActionEquipItem(b, INVENTORY_SLOT_BOOTS));
}

void Copy(string k, object o)
{
    object c = CopyObject(o, PCentre(GetPosition(o).x - 20.0 + 2.0, 6.0));
    DelayCommand(0.3, Read(k, c));
}

void Hench(string r, float x)
{
    object o = Spawn(r, x);
    DelayCommand(0.5, Read(r + ".spawn", o));
    DelayCommand(0.6, Copy(r + ".copy", o));
    DelayCommand(1.0, Off(o));
    DelayCommand(2.5, Read(r + ".off", o));
    DelayCommand(2.6, Copy(r + ".copyOff", o));
    DelayCommand(3.0, On(o));
    DelayCommand(4.5, Read(r + ".on", o));
    // Hurt by 10, then the boots come off and go back on.
    DelayCommand(5.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(10), o));
    DelayCommand(5.5, Read(r + ".hurt", o));
    DelayCommand(6.0, Off(o));
    DelayCommand(7.5, Read(r + ".hurtOff", o));
    DelayCommand(8.0, On(o));
    DelayCommand(9.5, Read(r + ".hurtOn", o));
}

void Dae()
{
    object o = Spawn("nw_hen_dae_05", 6.0);
    CreateItemOnObject("nw_it_mboots006", o);
    DelayCommand(0.5, Read("dae.spawn", o));
    DelayCommand(1.0, On(o));
    DelayCommand(2.5, Read("dae.on", o));
    DelayCommand(2.6, Copy("dae.copyOn", o));
    DelayCommand(3.0, Off(o));
    DelayCommand(4.5, Read("dae.off", o));
}

// A bandit given the boots by a script before it is first read, and one with a Constitution effect.
void Bandit()
{
    object o = Spawn("nw_bandit001", -6.0);
    object b = CreateItemOnObject("nw_it_mboots006", o);
    AssignCommand(o, ActionEquipItem(b, INVENTORY_SLOT_BOOTS));
    DelayCommand(1.5, Read("bandit.boots", o));
    object p = Spawn("nw_bandit001", -9.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_CONSTITUTION, 4), p);
    DelayCommand(0.5, Read("bandit.effect", p));
    DelayCommand(0.6, Copy("bandit.effectCopy", p));
}

void main()
{
    Hench("nw_hen_bod_05", -3.0);
    Hench("nw_hen_lin_05", 0.0);
    Dae();
    Bandit();
    DelayCommand(12.0, PDone());
}

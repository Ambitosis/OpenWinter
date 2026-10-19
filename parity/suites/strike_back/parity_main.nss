// Does a creature with no event scripts strike back at an NPC caster that hurts it with spells?
// (Expected: only through an attack of opportunity, which also gives it an attack
// order when it had none.) Victim V: stripped nw_bandit001 (commoner faction, immortal, its own melee weapon,
// facing east). Caster C: a level-5 wizard with four Magic Missiles prepared (hostile faction unless noted, immortal,
// OnPhysicalAttacked counts V's attacks). Casts are real class casts (not cheat).
//   far       C 10 m east, casts at 8 s
//   adj       C 1.5 m east (in front of V), casts at 8 s and again at 10 s (the second waits for the first's round)
//   adj.late  as adj, the second cast ordered at 18 s
//   adj.later as adj, the second cast ordered at 26 s
//   adj.once  as adj, one cast only
//   adj.bow   as adj, V holds a shortbow
//   adj.back  C 1.5 m west (behind V)
// (A caster in Defensive Casting mode drew an attack in one nwserver run of two: SetActionMode on an NPC lapses after a
// round, combat_modes, so that case isn't read here.)
//   adj.friend as adj, C in the defender faction
//   adj.cheat as adj, both casts cheat casts (bCheat)
// V's attack target and current action are sampled every 0.5 s from 8 s to 36 s.
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

void Sample(object v, object c, string g, float t)
{
    if (t > 36.0) return;
    object m = GetModule();
    SetLocalInt(m, g + ".n", GetLocalInt(m, g + ".n") + 1);
    if (GetAttackTarget(v) == c) SetLocalInt(m, g + ".target", GetLocalInt(m, g + ".target") + 1);
    if (GetCurrentAction(v) == ACTION_ATTACKOBJECT) SetLocalInt(m, g + ".attacking", GetLocalInt(m, g + ".attacking") + 1);
    DelayCommand(0.5, Sample(v, c, g, t + 0.5));
}

void Cast(object c, object v)
{
    if (GetLocalInt(c, "CHEAT")) AssignCommand(c, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, v, METAMAGIC_ANY, TRUE));
    else AssignCommand(c, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, v));
}

void Cell(string g, float fDx, float fSecond, int bBow = FALSE, int bDefensive = FALSE, int nFaction = STANDARD_FACTION_HOSTILE, int bCheat = FALSE)
{
    float x = 12.0 + IntToFloat(nCell % 4) * 22.0, y = 10.0 + IntToFloat(nCell / 4) * 22.0;
    nCell++;
    object v = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(Area(), Vector(x, y, 0.0), 0.0));
    Strip(v);
    ChangeToStandardFaction(v, STANDARD_FACTION_COMMONER);
    SetImmortal(v, TRUE);
    if (bBow)
    {
        object it = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, v);
        if (GetIsObjectValid(it)) DestroyObject(it);
        object bow = CreateItemOnObject("nw_wbwsh001", v);
        SetIdentified(bow, TRUE);
        AssignCommand(v, ActionEquipItem(bow, INVENTORY_SLOT_RIGHTHAND));
        object arrows = CreateItemOnObject("nw_wamar001", v, 99);
        AssignCommand(v, ActionEquipItem(arrows, INVENTORY_SLOT_ARROWS));
    }
    object c = CreateObject(OBJECT_TYPE_CREATURE, "sb_wiz", Location(Area(), Vector(x + fDx, y, 0.0), fDx > 0.0 ? 180.0 : 0.0));
    Strip(c);
    ChangeToStandardFaction(c, nFaction);
    SetImmortal(c, TRUE);
    SetLocalString(c, "G", g);
    SetLocalInt(c, "CHEAT", bCheat);
    SetEventScript(c, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "sb_atk");
    if (bDefensive) DelayCommand(1.0, SetActionMode(c, ACTION_MODE_DEFENSIVE_CAST, TRUE));
    DelayCommand(8.0, Cast(c, v));
    if (fSecond > 0.0) DelayCommand(fSecond, Cast(c, v));
    DelayCommand(8.0, Sample(v, c, g, 8.0));
    DelayCommand(11.0, PLogI(g + ".v.combat11", GetIsInCombat(v)));
    DelayCommand(16.0, PLogI(g + ".v.combat16", GetIsInCombat(v)));
    DelayCommand(21.0, PLogI(g + ".v.combat21", GetIsInCombat(v)));
    DelayCommand(37.0, PLogI(g + ".v.action", GetCurrentAction(v)));
    DelayCommand(37.0, PLogI(g + ".v.target", GetAttackTarget(v) == c));
    DelayCommand(37.0, PLogI(g + ".v.bow", GetWeaponRanged(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, v))));
    DelayCommand(37.0, PLogI(g + ".c.slots", GetHasSpell(SPELL_MAGIC_MISSILE, c)));
    DelayCommand(37.0, PLogF(g + ".first", GetLocalFloat(c, "FIRST"), 1.0));
}

void Report(string g)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + ".n");
    PLogI(g + ".hits", GetLocalInt(m, g + ".hits") > 0);
    PLogF(g + ".target", IntToFloat(GetLocalInt(m, g + ".target")) / IntToFloat(n), 0.1);
    PLogF(g + ".attacking", IntToFloat(GetLocalInt(m, g + ".attacking")) / IntToFloat(n), 0.1);
}

void main()
{
    PStart();
    Cell("far", 10.0, 0.0);
    Cell("adj", 1.5, 10.0);
    Cell("adj2", 1.5, 10.0);
    Cell("adj.late", 1.5, 18.0);
    Cell("adj.later", 1.5, 26.0);
    Cell("adj.once", 1.5, 0.0);
    Cell("adj.bow", 1.5, 10.0, TRUE);
    Cell("adj.back", -1.5, 10.0);
    Cell("adj.friend", 1.5, 10.0, FALSE, FALSE, STANDARD_FACTION_DEFENDER);
    Cell("adj.cheat", 1.5, 10.0, FALSE, FALSE, STANDARD_FACTION_HOSTILE, TRUE);
    string gs = "far adj adj2 adj.late adj.later adj.once adj.bow adj.back adj.friend adj.cheat ";
    while (gs != "")
    {
        int sp = FindSubString(gs, " ");
        DelayCommand(38.0, Report(GetStringLeft(gs, sp)));
        gs = GetSubString(gs, sp + 1, 1000);
    }
    DelayCommand(39.0, PDone());
}

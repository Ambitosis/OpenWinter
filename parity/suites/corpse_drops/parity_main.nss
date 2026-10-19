// What a dead creature leaves, and when its corpse goes: the drop and pickpocket flags its items get from where
// they came from (blueprint entries with and without the toolset's flags, items embedded in a placed creature,
// script-created and copied items), what lands in its bag, and how long corpses with each DecayTime last.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }

string Candidates() { return "nw_it_mpotion001 nw_it_mpotion002 nw_it_mpotion003 nw_it_mpotion004 nw_it_mpotion005 nw_aarcl009 nw_wswls001 nw_ashsw001 nw_arhe001 nw_aarcl001 nw_wblcl001 nw_wswdg001 nw_it_mpotion006 nw_it_mpotion007 nw_it_mpotion008 "; }

// Drop and pickpocket flags of everything a creature holds, keyed by the item's resref.
void Flags(object c, string tag)
{
    PLogI(tag + ".disarmable", GetIsCreatureDisarmable(c));
    int i;
    for (i = 0; i < 18; i++)
    {
        object it = GetItemInSlot(i, c);
        if (GetIsObjectValid(it))
        {
            PLogI(tag + ".eq" + IntToString(i) + "." + GetResRef(it) + ".drop", GetDroppableFlag(it));
            PLogI(tag + ".eq" + IntToString(i) + "." + GetResRef(it) + ".pick", GetPickpocketableFlag(it));
        }
    }
    object it = GetFirstItemInInventory(c);
    while (GetIsObjectValid(it))
    {
        PLogI(tag + ".inv." + GetResRef(it) + ".drop", GetDroppableFlag(it));
        PLogI(tag + ".inv." + GetResRef(it) + ".pick", GetPickpocketableFlag(it));
        it = GetNextItemInInventory(c);
    }
}

// The placeables near where a corpse lay and which candidate items they hold.
void Bags(string tag)
{
    location l = GetLocalLocation(GetModule(), "at." + tag);
    object o = GetFirstObjectInArea(Area());
    int n = 0;
    string found = "";
    while (GetIsObjectValid(o))
    {
        if (GetObjectType(o) == OBJECT_TYPE_PLACEABLE && GetDistanceBetweenLocations(GetLocation(o), l) < 1.5 && GetTag(o) != "CD_CHEST")
        {
            n++;
            object it = GetFirstItemInInventory(o);
            while (GetIsObjectValid(it)) { found += GetResRef(it) + " "; it = GetNextItemInInventory(o); }
        }
        o = GetNextObjectInArea(Area());
    }
    PLogI(tag + ".bags", n);
    string all = Candidates();
    while (all != "")
    {
        int p = FindSubString(all, " ");
        string r = GetStringLeft(all, p);
        all = GetSubString(all, p + 1, 1000);
        if (FindSubString(found, r) >= 0) PLog(tag + ".bag." + r, "1");
    }
}

void Watch(object c, string tag, float t)
{
    if (GetLocalInt(GetModule(), "gone." + tag)) return;
    if (!GetIsObjectValid(c))
    {
        SetLocalInt(GetModule(), "gone." + tag, 1);
        SetLocalFloat(GetModule(), "goneat." + tag, t);
    }
}

void Kill(object c, string tag, int nDamage = 0)
{
    SetLocalLocation(GetModule(), "at." + tag, GetLocation(c));
    SetLocalString(GetModule(), "tags", GetLocalString(GetModule(), "tags") + tag + " ");
    if (nDamage > 0) ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(nDamage), c);
    else ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), c);
    float t;
    for (t = 0.5; t <= 32.0; t += 0.5) DelayCommand(t, Watch(c, tag, t));
}

object Spawn(string res, string tag, float x, float y)
{
    object c = CreateObject(OBJECT_TYPE_CREATURE, res, PCentre(x, y), FALSE, "CD_" + tag);
    PLogI(tag + ".destroyable", GetIsDestroyable(c));
    PLogI(tag + ".raiseable", GetIsRaiseable(c));
    PLogI(tag + ".selectable", GetIsSelectableWhenDead(c));
    PLogI(tag + ".lootable", GetLootable(c));
    return c;
}

void Report()
{
    string tags = GetLocalString(GetModule(), "tags");
    while (tags != "")
    {
        int p = FindSubString(tags, " ");
        string tag = GetStringLeft(tags, p);
        tags = GetSubString(tags, p + 1, 1000);
        if (GetLocalInt(GetModule(), "gone." + tag)) PLogF(tag + ".gone", GetLocalFloat(GetModule(), "goneat." + tag), 0.6);
        else PLog(tag + ".gone", "never");
        Bags(tag);
    }
    PDone();
}

void Scripted()
{
    object c = Spawn("cd_dkabs", "scr", 0.0, -10.0);
    object a = CreateItemOnObject("nw_it_mpotion001", c);
    PLogI("scr.create.drop", GetDroppableFlag(a));
    PLogI("scr.create.pick", GetPickpocketableFlag(a));
    object g = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion002", PCentre(-10.0, -10.0));
    PLogI("ground.drop", GetDroppableFlag(g));
    PLogI("ground.pick", GetPickpocketableFlag(g));
    object chest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-15.0, -10.0), FALSE, "CD_CHEST");
    object ci = CreateItemOnObject("nw_it_mpotion003", chest);
    PLogI("chest.create.drop", GetDroppableFlag(ci));
    object cp = CopyItem(g, c);
    PLogI("scr.copy.drop", GetDroppableFlag(cp));
    SetDroppableFlag(g, FALSE);
    SetPickpocketableFlag(g, FALSE);
    object cp2 = CopyItem(g, c, TRUE);
    PLogI("scr.copyundrop.drop", GetDroppableFlag(cp2));
    PLogI("scr.copyundrop.pick", GetPickpocketableFlag(cp2));
    SetDroppableFlag(a, FALSE);
    PLogI("scr.cleared.drop", GetDroppableFlag(a));
    // Each of these is a distinct resref so the bag shows which went in.
    object b = CreateItemOnObject("nw_it_mpotion006", c);
    SetDroppableFlag(b, TRUE);
    object d = CreateItemOnObject("nw_it_mpotion007", c);
    SetDroppableFlag(d, FALSE);
    SetPickpocketableFlag(d, TRUE);
    object e = CreateItemOnObject("nw_it_mpotion008", c);
    AssignCommand(c, ActionEquipItem(e, INVENTORY_SLOT_CHEST));
    PLogI("scr.copy.pick", GetPickpocketableFlag(cp));
    PLogI("chest.create.pick", GetPickpocketableFlag(ci));
    Kill(c, "scr");
}

void Moved(object o, string tag)
{
    PLogI(tag + ".valid", GetIsObjectValid(o));
    PLogI(tag + ".drop", GetDroppableFlag(o));
    PLogI(tag + ".pick", GetPickpocketableFlag(o));
    PLogI(tag + ".withcarrier", GetIsObjectValid(GetItemPossessor(o)));
}

// Items changing hands: picked up from the ground, given, taken from a container.
void Movers()
{
    object a = CreateObject(OBJECT_TYPE_CREATURE, "cd_dkabs", PCentre(-20.0, 20.0), FALSE, "CD_MOVER_A");
    object b = CreateObject(OBJECT_TYPE_CREATURE, "cd_dkabs", PCentre(-18.0, 20.0), FALSE, "CD_MOVER_B");
    SetAILevel(a, AI_LEVEL_NORMAL);
    SetAILevel(b, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(a, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(b, STANDARD_FACTION_COMMONER);
    object g = CreateObject(OBJECT_TYPE_ITEM, "nw_it_gem001", PCentre(-19.0, 21.0));
    AssignCommand(a, ActionPickUpItem(g));
    object given = CreateItemOnObject("nw_it_gem002", a);
    SetDroppableFlag(given, FALSE);
    SetPickpocketableFlag(given, TRUE);
    AssignCommand(a, ActionGiveItem(given, b));
    object chest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-17.0, 21.0), FALSE, "CD_CHEST");
    object inside = CreateItemOnObject("nw_it_gem003", chest);
    SetDroppableFlag(inside, FALSE);
    AssignCommand(b, ActionTakeItem(inside, chest));
    object putback = CreateItemOnObject("nw_it_gem004", b);
    AssignCommand(b, ActionGiveItem(putback, chest));
    DelayCommand(8.0, Moved(g, "pickup"));
    DelayCommand(8.0, Moved(given, "given"));
    DelayCommand(8.0, Moved(inside, "taken"));
    DelayCommand(8.0, Moved(putback, "intochest"));
}

void main()
{
    object ref = Spawn("cd_ref", "ref", 0.0, -15.0);
    Flags(ref, "ref");
    object embc = Spawn("cd_emb", "embc", 5.0, -15.0);
    Flags(embc, "embc");
    object git = GetObjectByTag("CD_EMB");
    PLogI("git.destroyable", GetIsDestroyable(git));
    Flags(git, "git");
    object copy = CopyObject(ref, PCentre(10.0, -15.0), OBJECT_INVALID, "CD_COPY");
    Flags(copy, "copy");
    object thug = Spawn("cd_thug", "thug", 15.0, -15.0);
    Flags(thug, "thug");
    object gob = Spawn("cd_gob", "gob", 20.0, -15.0);
    Flags(gob, "gob");
    Kill(ref, "ref");
    Kill(embc, "embc");
    Kill(git, "git");
    Kill(copy, "copy");
    Kill(thug, "thug");
    Kill(gob, "gob");
    Scripted();
    Movers();

    Kill(Spawn("cd_dkabs", "dkabs", 0.0, 0.0), "dkabs");
    Kill(Spawn("cd_dk0", "dk0", 5.0, 0.0), "dk0");
    Kill(Spawn("cd_dk1", "dk1", 10.0, 0.0), "dk1");
    Kill(Spawn("cd_dk5", "dk5", 15.0, 0.0), "dk5");
    Kill(Spawn("cd_dk20", "dk20", 20.0, 0.0), "dk20");
    Kill(Spawn("cd_nodes", "nodes", 25.0, 0.0), "nodes");
    Kill(Spawn("cd_flags", "flags", 30.0, 0.0), "flags");
    Kill(Spawn("cd_lootf", "lootf", 30.0, 10.0), "lootf");
    Kill(Spawn("cd_lootfi", "lootfi", 30.0, 20.0), "lootfi");
    Kill(Spawn("cd_noraise", "noraise", 30.0, -10.0), "noraise");
    // Killed by damage: just enough, and far more than its hit points.
    object dmA = Spawn("cd_dkabs", "dmgjust", 35.0, 0.0);
    Kill(dmA, "dmgjust", GetCurrentHitPoints(dmA));
    object dmB = Spawn("cd_dkabs", "dmgbig", 35.0, 10.0);
    Kill(dmB, "dmgbig", GetCurrentHitPoints(dmB) + 20);
    object dmC = Spawn("cd_dkabs", "dmghuge", 35.0, 20.0);
    Kill(dmC, "dmghuge", 200);
    // Where the quick end starts: hit points left after the blow.
    int nOver;
    for (nOver = 5; nOver <= 12; nOver++)
    {
        object dmO = Spawn("cd_dkabs", "dmgover" + IntToString(nOver), 40.0, IntToFloat(nOver * 3 - 30));
        Kill(dmO, "dmgover" + IntToString(nOver), GetCurrentHitPoints(dmO) + nOver);
    }
    // The same with something to drop.
    object i0 = Spawn("cd_dk0", "dk0i", 5.0, 10.0);
    SetDroppableFlag(CreateItemOnObject("nw_it_mpotion001", i0), TRUE);
    Kill(i0, "dk0i");
    object i20 = Spawn("cd_dk20", "dk20i", 20.0, 10.0);
    SetDroppableFlag(CreateItemOnObject("nw_it_mpotion001", i20), TRUE);
    Kill(i20, "dk20i");
    object iabs = Spawn("cd_dkabs", "dkabsi", 0.0, 10.0);
    SetDroppableFlag(CreateItemOnObject("nw_it_mpotion001", iabs), TRUE);
    Kill(iabs, "dkabsi");
    // At the normal AI level, as in an area with a player.
    object ai = Spawn("cd_dk0", "dk0ai", 10.0, 10.0);
    SetAILevel(ai, AI_LEVEL_NORMAL);
    Kill(ai, "dk0ai");
    object ai20 = Spawn("cd_dk20", "dk20ai", 25.0, 10.0);
    SetAILevel(ai20, AI_LEVEL_NORMAL);
    Kill(ai20, "dk20ai");
    DelayCommand(34.0, Report());
}

// Stock henchman AI with an NPC master (docs/STOCK_AI.md), one party per area (12 areas, Prelude factions):
// a nw_bandit001 master (stock nw_c2_default* scripts, defender) and a nw_bandit001 henchman given the stock
// henchman scripts (nw_ch_ac1..e, nw_ch_ac9 run as its spawn) and added with AddHenchman. At 2 s the master walks
// 6 m east; at 14 s two hostile nw_goblina appear 12 m beyond it. Readings: the henchman's distance to its master
// while it walks and once it stops, and what each does once the goblins come.
#include "pai_inc"

object Ar(int i) { return GetObjectByTag("AI" + (i < 10 ? "0" : "") + IntToString(i)); }

void Henchman(object h)
{
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "nw_ch_ac1");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_NOTICE, "nw_ch_ac2");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, "nw_ch_ac3");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_DIALOGUE, "nw_ch_ac4");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "nw_ch_ac5");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "nw_ch_ac6");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_DEATH, "nw_ch_ac7");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_DISTURBED, "nw_ch_ac8");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT, "nw_ch_acb");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_USER_DEFINED_EVENT, "nw_ch_acd");
    SetEventScript(h, EVENT_SCRIPT_CREATURE_ON_BLOCKED_BY_DOOR, "nw_ch_ace");
}

void Setup()
{
    int i;
    for (i = 0; i < 12; i++)
    {
        object a = Ar(i);
        object m = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(a, Vector(8.0, 20.0, 0.0), 0.0));
        ChangeToStandardFaction(m, STANDARD_FACTION_DEFENDER);
        AssignCommand(m, SetIsDestroyable(FALSE, FALSE, FALSE));
        SetAILevel(m, AI_LEVEL_NORMAL);
        object h = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(a, Vector(6.0, 18.0, 0.0), 0.0));
        ChangeToStandardFaction(h, STANDARD_FACTION_DEFENDER);
        AssignCommand(h, SetIsDestroyable(FALSE, FALSE, FALSE));
        SetAILevel(h, AI_LEVEL_NORMAL);
        Henchman(h);
        AddHenchman(m, h);
        ExecuteScript("nw_ch_ac9", h);
        PaiWrap(m, "mas");
        PaiWrap(h, "hen");
        SetLocalObject(h, "MASTER", m);
        SetLocalObject(m, "HENCH", h);
        PaiStart(m);
        PaiStart(h);
    }
}

void Walk()
{
    int i;
    for (i = 0; i < 12; i++)
    {
        object a = Ar(i);
        object m = GetFirstObjectInArea(a);
        while (GetIsObjectValid(m) && PaiRole(m) != "mas") m = GetNextObjectInArea(a);
        AssignCommand(m, ActionMoveToLocation(Location(a, Vector(14.0, 20.0, 0.0), 0.0)));
    }
}

void Goblins()
{
    int i;
    for (i = 0; i < 12; i++)
    {
        object a = Ar(i);
        PaiStart(PaiSpawn("nw_goblina", a, 26.0, 19.0, 180.0, "gob", STANDARD_FACTION_HOSTILE));
        PaiStart(PaiSpawn("nw_goblina", a, 26.0, 21.0, 180.0, "gob", STANDARD_FACTION_HOSTILE));
    }
}

void Follow(int nStep)
{
    object mo = GetModule();
    int n = GetLocalInt(mo, "PAI_N");
    int i, k = 0;
    float fSum = 0.0;
    for (i = 0; i < n; i++)
    {
        object h = GetLocalObject(mo, "PAI_O" + IntToString(i));
        if (PaiRole(h) != "hen") continue;
        fSum += GetDistanceBetween(h, GetLocalObject(h, "MASTER"));
        k++;
        PaiTally("hen.assoc", IntToString(GetAssociateType(h)) + "/" + IntToString(GetMaster(h) == GetLocalObject(h, "MASTER")));
    }
    PaiMean("hen.follow." + IntToString(nStep), fSum, k, 1.0, 0.4);
}

// Whom the henchman attacks first: its master's first target, or another goblin.
void Targets()
{
    object mo = GetModule();
    int n = GetLocalInt(mo, "PAI_N");
    int i, k = 0, same = 0;
    for (i = 0; i < n; i++)
    {
        object h = GetLocalObject(mo, "PAI_O" + IntToString(i));
        if (PaiRole(h) != "hen") continue;
        object t1 = GetLocalObject(h, "PAI_FIRSTTGT"), t2 = GetLocalObject(GetLocalObject(h, "MASTER"), "PAI_FIRSTTGT");
        if (!GetIsObjectValid(t1) || !GetIsObjectValid(t2)) continue;
        k++;
        if (t1 == t2) same++;
    }
    PaiRate("hen.samefirsttarget", same, k);
}

void Report()
{
    PaiReportRole("mas");
    PaiReportRole("hen", 3);
    PaiReportRole("gob");
    PaiReportTally("hen.assoc", 12 * 3);
    Targets();
    PaiDumpRaw();
    PDone();
}

void main()
{
    PStart();
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    DelayCommand(1.0, Setup());
    DelayCommand(1.1, PaiPollLoop(90.0));
    DelayCommand(3.0, Walk());
    DelayCommand(4.5, Follow(0));
    DelayCommand(8.0, Follow(1));
    DelayCommand(13.0, Follow(2));
    DelayCommand(15.0, Goblins());
    DelayCommand(93.0, Report());
}

// Melee reach: where an attacker stops to fight, measured from 6 m away. On nwserver it is its weapon's
// PrefAttackDist (four fifths of it for small creatures) or, unarmed, its appearance's PREFATCKDIST,
// plus the target's appearance HITDIST, but never closer than the two creatures' CREPERSPACE. Also six
// attackers closing on one target from all round, and six from one side (who bump into each other on the
// way: OnBlocked in the first 4 s, 12 to 14 on nwserver) and whether all keep attacking; where a crowd
// ends up varies from run to run on nwserver, so only the first six's distance is compared. (Two ogres stop
// anywhere from 2.2 to 2.5 m apart on nwserver, and an ogre with a club 1.6 to 1.9 m from a man, so those
// aren't compared.) PPair makes the pairs enemies; a goblin with a longsword
// or bare-handed then stops 1.8 m off on nwserver, against 1.5 m from a friend, and OpenWinter doesn't match that yet.
#include "pcombat_inc"

void Late(object a, string g)
{
    PAdd(g + ".dist", FloatToInt(GetDistanceBetween(a, GetLocalObject(a, "T")) * 100.0));
    PAdd(g + ".n");
    PAdd(g + ".attacking", GetCurrentAction(a) == ACTION_ATTACKOBJECT);
}

void P(string g, string sAtt, string sWeapon, string sTarget = "nw_bandit001", int nApp = -1, int nTApp = -1)
{
    object a = PPair(g, sAtt, sTarget, 6.0);
    if (nApp >= 0) SetCreatureAppearanceType(a, nApp);
    if (nTApp >= 0) SetCreatureAppearanceType(GetLocalObject(a, "T"), nTApp);
    if (sWeapon != "keep")
    {
        PUnequip(a, INVENTORY_SLOT_RIGHTHAND);
        if (sWeapon != "") PEquip(a, sWeapon, INVENTORY_SLOT_RIGHTHAND);
    }
    DelayCommand(14.0, Late(a, g));
}

void Setup()
{
    P("dagger", "nw_drowfight010", "nw_wswdg001");
    P("longsword", "nw_drowfight010", "nw_wswls001");
    P("bastard", "nw_drowfight010", "keep");
    P("greatsword", "nw_drowfight010", "nw_wswgs001");
    P("halberd", "nw_drowfight010", "nw_wplhb001");
    P("diremace", "nw_drowfight010", "nw_wdbma001");
    P("unarmed", "nw_drowfight010", "");
    P("human.unarmed", "nw_drowfight010", "", "nw_bandit001", 6);
    P("goblin.dagger", "nw_goblina", "nw_wswdg001");
    P("goblin.longsword", "nw_goblina", "nw_wswls001");
    P("goblin.unarmed", "nw_goblina", "");
    P("halfling.longsword", "nw_drowfight010", "nw_wswls001", "nw_bandit001", 3);
    P("ogre.unarmed", "nw_ogre01", "");
    P("dog", "nw_dog", "keep");
    P("vs.ogre", "nw_drowfight010", "keep", "nw_ogre01");
    P("vs.dog", "nw_drowfight010", "keep", "nw_dog");
    P("vs.giant", "nw_drowfight010", "nw_wswls001", "nw_bandit001", -1, 80);
    // Six on one.
    object t = PSquad("six", "nw_drowfight010", "nw_bandit001", 6, 6.0);
    object o = GetFirstObjectInArea(GetArea(t));
    while (GetIsObjectValid(o))
    {
        if (GetLocalObject(o, "T") == t)
        {
            SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_BLOCKED_BY_DOOR, "pc_blocked");
            DelayCommand(14.0, Late(o, "six"));
        }
        o = GetNextObjectInArea(GetArea(t));
    }
    // Six in a line on one side, 1 m apart, 6 m away: they have to find their way round each other.
    int n = PGet("PAIRS");
    PAdd("PAIRS");
    location lc = PCell(n, 0.0);
    vector vc = GetPositionFromLocation(lc);
    object t2 = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(GetArea(t), Vector(vc.x + 6.0, vc.y + 2.5, 0.0), 180.0));
    PStrip(t2);
    SetEventScript(t2, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_atk");
    ChangeToStandardFaction(t2, STANDARD_FACTION_COMMONER);
    SetImmortal(t2, TRUE);
    int i;
    for (i = 0; i < 6; i++)
    {
        object a = CreateObject(OBJECT_TYPE_CREATURE, "nw_drowfight010", Location(GetArea(t), Vector(vc.x, vc.y + IntToFloat(i), 0.0), 0.0));
        PStrip(a);
        SetLocalString(a, "G", "crowd");
        SetLocalObject(a, "T", t2);
        SetImmortal(a, TRUE);
        ChangeToStandardFaction(a, STANDARD_FACTION_HOSTILE);
        SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_BLOCKED_BY_DOOR, "pc_blocked");
        DelayCommand(14.0, Late(a, "crowd"));
    }
    DelayCommand(9.0, PLogMean("crowd.bumps.4s", IntToFloat(PGet("crowd.blocked")), 1, 1.0, 4.0));
    // Creatures first perceive each other 4 s after they appear; start after that.
    PStartAll(5.0);
}

void Report()
{
    string s = "dagger longsword bastard greatsword halberd diremace unarmed human.unarmed goblin.dagger goblin.longsword goblin.unarmed "
        + "halfling.longsword ogre.unarmed dog vs.ogre vs.dog vs.giant six ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PLogMean(g + ".distance", IntToFloat(PGet(g + ".dist")) / 100.0, PGet(g + ".n"), 0.05, 0.12);
        s = GetSubString(s, sp + 1, 1000);
    }
    PLogI("six.attacking", PGet("six.attacking"));
    PLogI("crowd.attacking", PGet("crowd.attacking"));

    PLogMean("six.bumps", IntToFloat(PGet("six.blocked")), 1, 4.0, 8.0);
    PDone();
}

void main()
{
    PClockStart(40.0);
    DelayCommand(1.0, Setup());
    DelayCommand(16.0, Report());
}

// Archers and attacks of opportunity (suites archer_aoo at D&D Hardcore, archer_aoo_normal at Normal). A drow archer
// (short bow, one attack a round, no event scripts but logging ones, hostile faction, immortal) shoots a stripped
// bandit F (longsword, commoner faction, immortal) that regards it as a temporary enemy, facing it. Every 0.1 s the
// distance is sampled; a change of more than 0.3 m is a step. F's attacks on the archer and the archer's shots are logged with times and distances.
//   g15, g30        from 1.5 m and 3.0 m, F not in combat before the first shot
//   g15pre, g30pre, g40pre  from 1.5, 3.0 and 4.0 m, F put in combat 1 s before (1 point of damage from the archer)
//   g15back         as g15pre, F facing away from the archer
//   late15, late15pre, late30pre  as g15, g15pre and g30pre, the order given 9 s after both were made (after F's first
//                   perception pass) instead of 3 s
//   halberd, bow    F holding a halberd (from 3.0 m) or a short bow (from 1.0 m), facing away: where the archer's steps
//                   stop (with the halberd's longer PrefAttackDist: nwserver 3.8 m; against a ranged target the
//                   archer's PREFATCKDIST + F's HITDIST, 1.8 m)
#include "parity_inc"
#include "parity_time"

object AoArea() { return GetObjectByTag("PARITY_AREA"); }

void AoMark(object a, string s)
{
    SetLocalString(a, "SEQ", GetLocalString(a, "SEQ") + s + "@" + FloatToString(PSince(), 0, 2) + ":" + FloatToString(GetDistanceBetween(a, GetLocalObject(a, "F")), 0, 2) + " ");
}

void AoStrip(object o)
{
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    AssignCommand(o, ClearAllActions(TRUE));
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetImmortal(o, TRUE);
}

void AoEquip(object o, string sRes, int nSlot, int nStack = 1)
{
    object it = CreateItemOnObject(sRes, o, nStack);
    SetIdentified(it, TRUE);
    AssignCommand(o, ActionEquipItem(it, nSlot));
}

void AoSample(object a, float fLast, float t)
{
    if (t > 32.0) return;
    float d = GetDistanceBetween(a, GetLocalObject(a, "F"));
    if (fLast > 0.0 && fabs(d - fLast) > 0.3)
    {
        AoMark(a, d > fLast ? "back" : "in");
        // F's first move toward the archer is its first reaction too (it closes in before its first swing).
        if (d < fLast && GetLocalFloat(a, "R0") <= 0.0) SetLocalFloat(a, "R0", PSince());
        // Only the archer's steps back count as its steps ("in" is F closing in).
        int k = GetLocalInt(a, "TN");
        if (d > fLast) { SetLocalFloat(a, "T" + IntToString(k), PSince()); SetLocalInt(a, "TN", k + 1); }
    }
    DelayCommand(0.1, AoSample(a, d, t + 0.1));
}

void AoHurt(object f) { ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), f); }

void AoGo(object a)
{
    object f = GetLocalObject(a, "F");
    AoMark(a, "o" + IntToString(GetIsInCombat(f)) + "w" + IntToString(GetBaseItemType(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, f))));
    AssignCommand(a, ActionAttack(f));
    AoSample(a, 0.0, 0.0);
}

int nAoCell = 0;

void AoFinal(object a, string g)
{
    object m = GetModule();
    SetLocalFloat(m, g + ".final", GetLocalFloat(m, g + ".final") + GetDistanceBetween(a, GetLocalObject(a, "F")));
}

void AoPair(string g, float fDist, int bPre, int bBack = FALSE, float fStart = 3.0, string sWeapon = "")
{
    // Seven cells 16 m apart to a row, clear of the area's edges (an archer on the edge can't step back).
    float x = 8.0 + IntToFloat(nAoCell % 7) * 16.0, y = 8.0 + IntToFloat(nAoCell / 7) * 16.0;
    nAoCell++;
    object a = CreateObject(OBJECT_TYPE_CREATURE, "nw_drowfight010", Location(AoArea(), Vector(x, y, 0.0), 0.0));
    object f = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(AoArea(), Vector(x + fDist, y, 0.0), bBack ? 0.0 : 180.0));
    AoStrip(a);
    AoStrip(f);
    ChangeToStandardFaction(a, STANDARD_FACTION_HOSTILE);
    ChangeToStandardFaction(f, STANDARD_FACTION_COMMONER);
    SetIsTemporaryEnemy(a, f);
    SetIsTemporaryEnemy(f, a);
    SetBaseAttackBonus(1, a);
    object it = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, a);
    if (GetIsObjectValid(it)) DestroyObject(it);
    it = GetItemInSlot(INVENTORY_SLOT_LEFTHAND, a);
    if (GetIsObjectValid(it)) DestroyObject(it);
    AoEquip(a, "nw_wbwsh001", INVENTORY_SLOT_RIGHTHAND);
    AoEquip(a, "nw_wamar001", INVENTORY_SLOT_ARROWS, 99);
    if (sWeapon != "")
    {
        object w = GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, f);
        if (GetIsObjectValid(w)) DestroyObject(w);
        AoEquip(f, sWeapon, INVENTORY_SLOT_RIGHTHAND);
        if (sWeapon == "nw_wbwsh001") AoEquip(f, "nw_wamar001", INVENTORY_SLOT_ARROWS, 99);
    }
    SetLocalObject(a, "F", f);
    SetLocalObject(f, "A", a);
    DelayCommand(fStart + 20.0, AoFinal(a, g));
    SetEventScript(f, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "ao_shot");
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "ao_hit");
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count");
    SetLocalObject(m, g + IntToString(n), a);
    SetLocalInt(m, g + ".count", n + 1);
    if (bPre) DelayCommand(fStart - 1.0, AssignCommand(a, AoHurt(f)));
    DelayCommand(fStart, AoGo(a));
}

// Per group: pairs whose F attacked the archer at all ("attacked"); pairs whose F's first reaction (its first swing,
// or its first step toward the archer if earlier) came at a shot, from
// 0.4 s before its event (on nwserver F sometimes starts closing in a little before the shot's OnPhysicalAttacked) to
// 0.6 s after it ("atshot"); and pairs whose first reaction came within 0.6 s after one of the archer's steps back
// with no shot in that window ("atstep"). The archer's distance at F's first attack is logged uncompared: on nwserver
// F mostly closed in by its own combat steps before that first swing.
void AoReport(string g)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count"), i, j;
    int nShot = 0, nStep = 0, nAny = 0;
    string dists = "";
    for (i = 0; i < n; i++)
    {
        object a = GetLocalObject(m, g + IntToString(i));
        float h = GetLocalFloat(a, "H0"), r = GetLocalFloat(a, "R0");
        if (h > 0.0)
        {
            nAny++;
            dists += FloatToString(GetLocalFloat(a, "HD0"), 0, 2) + " ";
            // Its first reaction: the first swing, or the first step toward the archer if that came first.
            if (r > 0.0 && r < h) h = r;
            int atShot = FALSE, atStep = FALSE;
            for (j = 0; j < GetLocalInt(a, "SN"); j++)
            {
                float d = h - GetLocalFloat(a, "S" + IntToString(j));
                if (d >= -0.4 && d <= 0.6) atShot = TRUE;
            }
            for (j = 0; j < GetLocalInt(a, "TN"); j++)
            {
                float d = h - GetLocalFloat(a, "T" + IntToString(j));
                if (d >= 0.0 && d <= 0.6) atStep = TRUE;
            }
            nShot += atShot;
            nStep += atStep && !atShot;
        }
        WriteTimestampedLogEntry("RAW|" + g + IntToString(i) + "|" + GetLocalString(a, "SEQ"));
    }
    PLogF(g + ".attacked", IntToFloat(nAny), 1.01);
    // Whether F's first swing falls in the window of a shot varies from run to run on nwserver (g15 2-6 of 6; the first
    // shot of "late15", with no combat before it, provokes or not as if by a race).
    PLogF(g + ".atshot", IntToFloat(nShot), 2.01);
    // Where the archer stands 20 s after its order (once its target fights back it keeps backing off: uncompared then).
    if (nAny == 0) PLogF(g + ".final", GetLocalFloat(m, g + ".final") / IntToFloat(n), 0.15);
    else WriteTimestampedLogEntry("RAW|" + g + ".final|" + FloatToString(GetLocalFloat(m, g + ".final") / IntToFloat(n), 0, 2));
    PLogI(g + ".atstep", nStep);
    WriteTimestampedLogEntry("RAW|" + g + ".firstAttack.dist|" + dists);
}

void AoMain()
{
    PStart();
    int i;
    for (i = 0; i < 6; i++) { AoPair("g15", 1.5, FALSE); AoPair("g15pre", 1.5, TRUE); }
    for (i = 0; i < 4; i++)
    {
        AoPair("g30", 3.0, FALSE);
        AoPair("g30pre", 3.0, TRUE);
        AoPair("g40pre", 4.0, TRUE);
        AoPair("g15back", 1.5, TRUE, TRUE);
        AoPair("late15", 1.5, FALSE, FALSE, 9.0);
        AoPair("late15pre", 1.5, TRUE, FALSE, 9.0);
        AoPair("late30pre", 3.0, TRUE, FALSE, 9.0);
        AoPair("halberd", 3.0, FALSE, TRUE, 3.0, "nw_wplhb001");
        AoPair("bow", 1.0, FALSE, TRUE, 3.0, "nw_wbwsh001");
    }
    string gs = "g15 g15pre g30 g30pre g40pre g15back late15 late15pre late30pre halberd bow ";
    while (gs != "")
    {
        int sp = FindSubString(gs, " ");
        DelayCommand(40.0, AoReport(GetStringLeft(gs, sp)));
        gs = GetSubString(gs, sp + 1, 1000);
    }
    DelayCommand(41.0, PDone());
}

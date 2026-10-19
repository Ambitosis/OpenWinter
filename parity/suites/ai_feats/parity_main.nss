// Stock AI with combat feats and potions (docs/STOCK_AI.md), one fight per area (Prelude factions, 8 m apart):
// - kd (areas 0-7): nw_dwarfmerc005, a 13th-level fighter with Knockdown, Power Attack and Improved Power Attack
//   (TalentMeleeAttack picks a TALENT_CATEGORY_HARMFUL_MELEE feat), against nw_dwarfmerc003 (kdf), a 5th-level fighter
//   with Power Attack.
// - orc (areas 8-15): nw_orcboss with Disarm and two potions (Cure Serious Wounds, Bull's Strength) against
//   nw_dwarfmerc004 (orcf), a 9th-level fighter; below half its hit points the stock AI drinks the healing potion.
// The poll counts knockdowns (an effect of type 0 appearing on the target) and disarms (a target's weapon gone).
#include "pai_inc"

object Ar(int i) { return GetObjectByTag("AI" + (i < 10 ? "0" : "") + IntToString(i)); }

void Setup()
{
    int i;
    for (i = 0; i < 16; i++)
    {
        object a = Ar(i);
        object x, y;
        if (i < 8)
        {
            x = PaiSpawn("nw_dwarfmerc005", a, 11.0, 15.0, 0.0, "kd", STANDARD_FACTION_HOSTILE);
            y = PaiSpawn("nw_dwarfmerc003", a, 19.0, 15.0, 180.0, "kdf", STANDARD_FACTION_DEFENDER);
        }
        else
        {
            x = PaiSpawn("nw_orcboss", a, 11.0, 15.0, 0.0, "orc", STANDARD_FACTION_HOSTILE);
            y = PaiSpawn("nw_dwarfmerc004", a, 19.0, 15.0, 180.0, "orcf", STANDARD_FACTION_DEFENDER);
        }
        SetLocalObject(x, "FOE", y);
        SetLocalObject(y, "FOE", x);
        PaiStart(x);
        PaiStart(y);
    }
}

// Knockdowns and disarms seen on each creature.
void Watch()
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        if (!GetIsObjectValid(o) || GetIsDead(o)) continue;
        int kd = FALSE;
        effect e = GetFirstEffect(o);
        while (GetIsEffectValid(e)) { if (GetEffectType(e) == 0) kd = TRUE; e = GetNextEffect(o); }
        if (kd && !GetLocalInt(o, "W_KD")) PaiAdd(PaiRole(o) + ".knockeddown");
        SetLocalInt(o, "W_KD", kd);
        int armed = GetIsObjectValid(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND, o));
        if (!armed && GetLocalInt(o, "W_ARMED")) PaiAdd(PaiRole(o) + ".disarmed");
        SetLocalInt(o, "W_ARMED", armed);
    }
}

void WatchLoop(float f)
{
    Watch();
    if (f > 0.0) DelayCommand(0.25, WatchLoop(f - 0.25));
}

void Report()
{
    string s = "kd kdf orc orcf ";
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string r = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 1000);
        PaiReportRole(r);
        int n = PaiGet(r + ".count");
        PaiMean(r + ".knockeddown", IntToFloat(PaiGet(r + ".knockeddown")), n, 1.5, 0.3);
        PaiMean(r + ".disarmed", IntToFloat(PaiGet(r + ".disarmed")), n, 0.6, 0.3);
    }
    PaiDumpRaw();
    PDone();
}

void main()
{
    PStart();
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    DelayCommand(1.0, Setup());
    DelayCommand(1.1, PaiPollLoop(90.0));
    DelayCommand(1.5, WatchLoop(90.0));
    DelayCommand(93.0, Report());
}

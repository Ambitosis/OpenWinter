// Stealth and detect modes on NPCs: the flags scripts read (stealth and detect can both be on), movement speed in
// either mode, entering stealth in combat (with and without Hide in Plain Sight), and what ends stealth: an attack
// (when the swing comes), a spell (when the casting starts), but not damage, being attacked, invisibility or
// sanctuary. GetDetectMode also reports 2 after some actions; the cases below record when.
#include "parity_inc"
#include "parity_time"

location L(float x, float y) { return Location(GetObjectByTag("PARITY_AREA"), Vector(x, y, 0.0), 0.0); }

object Actor(string sTag, float x, float y, int nFaction = STANDARD_FACTION_COMMONER)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", L(x, y), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Modes(object o, string k)
{
    PLogI(k + ".stealth", GetActionMode(o, ACTION_MODE_STEALTH));
    PLogI(k + ".detect", GetActionMode(o, ACTION_MODE_DETECT));
    PLogI(k + ".gstealth", GetStealthMode(o));
    PLogI(k + ".gdetect", GetDetectMode(o));
}

void Mode(object o, int m, int on) { SetActionMode(o, m, on); }

void Pos(object o, object from, string k) { PLogF(k, GetDistanceBetween(o, from), 0.15); }

void Speed(string sTag, float y, int nMode, int bRun)
{
    object o = Actor(sTag, 5.0, y);
    object m = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", L(5.0, y), FALSE, sTag + "_W");
    if (nMode >= 0) SetActionMode(o, nMode, TRUE);
    DelayCommand(1.0, AssignCommand(o, ActionMoveToLocation(L(75.0, y), bRun)));
    DelayCommand(5.0, Pos(o, m, sTag + ".d4"));
    DelayCommand(5.1, Modes(o, sTag + ".after"));
}

// Samples the detect flag every 0.1 s and logs when it first reads 0, or "kept".
void WatchDetect(object o, string k, float t, float fEnd)
{
    if (!GetActionMode(o, ACTION_MODE_DETECT)) { PLogF(k + ".ends", t, 0.35); return; }
    if (t >= fEnd) { PLog(k + ".ends", "kept"); return; }
    DelayCommand(0.1, WatchDetect(o, k, t + 0.1, fEnd));
}

// Samples the stealth flag every 0.1 s and logs when it first reads 0 (seconds after the order), or "kept".
void Watch(object o, string k, float t, float fEnd)
{
    if (GetLocalInt(o, "GONE")) return;
    if (!GetActionMode(o, ACTION_MODE_STEALTH))
    {
        SetLocalInt(o, "GONE", 1);
        PLogF(k + ".ends", t, 0.35);
        return;
    }
    if (t >= fEnd) { PLog(k + ".ends", "kept"); return; }
    DelayCommand(0.1, Watch(o, k, t + 0.1, fEnd));
}

void Track(object o, string k, float fEnd) { Watch(o, k, 0.0, fEnd); }

void Attack(object a, object t) { AssignCommand(a, ActionAttack(t)); }

void main()
{
    PStart();
    // Flags: stealth and detect are independent.
    object a = Actor("FLAGS", 5.0, 75.0);
    Modes(a, "flags.start");
    DelayCommand(0.5, Mode(a, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(0.6, Modes(a, "flags.stealth"));
    DelayCommand(1.0, Mode(a, ACTION_MODE_DETECT, TRUE));
    DelayCommand(1.1, Modes(a, "flags.detect"));
    DelayCommand(2.0, AssignCommand(a, ClearAllActions(TRUE)));
    DelayCommand(2.1, Modes(a, "flags.clear"));
    DelayCommand(2.5, Mode(a, ACTION_MODE_STEALTH, FALSE));
    DelayCommand(2.6, Modes(a, "flags.off"));

    // Speeds: normal-rate humans over 4 seconds.
    Speed("RUN", 5.0, -1, TRUE);
    Speed("WALK", 8.0, -1, FALSE);
    Speed("STEALTH_RUN", 11.0, ACTION_MODE_STEALTH, TRUE);
    Speed("STEALTH_WALK", 14.0, ACTION_MODE_STEALTH, FALSE);
    Speed("DETECT_RUN", 17.0, ACTION_MODE_DETECT, TRUE);
    Speed("DETECT_WALK", 20.0, ACTION_MODE_DETECT, FALSE);

    // Stealth in combat: an attacker refuses it (it may still turn detect on).
    object f1 = Actor("FIGHT", 20.0, 40.0);
    object dm1 = Actor("FIGHT_D", 21.5, 40.0, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(dm1, TRUE);
    DelayCommand(1.0, Attack(f1, dm1));
    DelayCommand(5.0, PLogI("fight.incombat", GetIsInCombat(f1)));
    DelayCommand(5.0, Mode(f1, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(5.1, Modes(f1, "fight.plain"));
    DelayCommand(5.0, Mode(f1, ACTION_MODE_DETECT, TRUE));
    DelayCommand(5.2, Modes(f1, "fight.detect"));
    // A creature under attack that does not fight back, without and with Hide in Plain Sight.
    object v1 = Actor("VICTIM", 30.0, 40.0);
    object b1 = Actor("VICTIM_A", 31.5, 40.0, STANDARD_FACTION_HOSTILE);
    object v2 = Actor("VICTIM_HIPS", 40.0, 40.0);
    object b2 = Actor("VICTIM_HIPS_A", 41.5, 40.0, STANDARD_FACTION_HOSTILE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_HIDE_IN_PLAIN_SIGHT), v2);
    SetPlotFlag(v1, TRUE); SetPlotFlag(v2, TRUE);
    DelayCommand(1.0, Attack(b1, v1));
    DelayCommand(1.0, Attack(b2, v2));
    DelayCommand(5.0, PLogI("victim.incombat", GetIsInCombat(v1)));
    DelayCommand(5.0, Mode(v1, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(5.0, Mode(v2, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(5.1, Modes(v1, "victim.plain"));
    DelayCommand(5.1, Modes(v2, "victim.hips"));
    DelayCommand(9.0, Modes(v2, "victim.hips4s"));
    // Hide in Plain Sight while fighting: accepted, then the next swing ends it.
    object f2 = Actor("FIGHT_HIPS", 50.0, 40.0);
    object dm2 = Actor("FIGHT_HIPS_D", 51.5, 40.0, STANDARD_FACTION_HOSTILE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBonusFeat(FEAT_HIDE_IN_PLAIN_SIGHT), f2);
    SetPlotFlag(dm2, TRUE);
    DelayCommand(1.0, Attack(f2, dm2));
    DelayCommand(5.0, Mode(f2, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(5.05, Modes(f2, "fighthips.now"));
    DelayCommand(5.05, Track(f2, "fighthips", 8.0));

    // Attacking from stealth: next to the target, from 8 m (the walk up), and with a bow from 8 m.
    object s1 = Actor("SATK", 10.0, 55.0);
    object t1 = Actor("SATK_T", 11.5, 55.0, STANDARD_FACTION_HOSTILE);
    object s2 = Actor("SATKFAR", 20.0, 55.0);
    object t2 = Actor("SATKFAR_T", 28.0, 55.0, STANDARD_FACTION_HOSTILE);
    object s3 = Actor("SATKBOW", 35.0, 55.0);
    object t3 = Actor("SATKBOW_T", 43.0, 55.0, STANDARD_FACTION_HOSTILE);
    object bow = CreateItemOnObject("nw_wbwln001", s3);
    object arrows = CreateItemOnObject("nw_wamar001", s3, 99);
    AssignCommand(s3, ActionEquipItem(bow, INVENTORY_SLOT_RIGHTHAND));
    AssignCommand(s3, ActionEquipItem(arrows, INVENTORY_SLOT_ARROWS));
    SetPlotFlag(t1, TRUE); SetPlotFlag(t2, TRUE); SetPlotFlag(t3, TRUE);
    SetActionMode(s1, ACTION_MODE_STEALTH, TRUE);
    SetActionMode(s2, ACTION_MODE_STEALTH, TRUE);
    DelayCommand(3.0, SetActionMode(s3, ACTION_MODE_STEALTH, TRUE));
    DelayCommand(10.0, Attack(s1, t1));
    DelayCommand(10.0, Attack(s2, t2));
    DelayCommand(10.0, Attack(s3, t3));
    DelayCommand(10.0, Track(s1, "atk.melee", 8.0));
    DelayCommand(10.0, Track(s2, "atk.far", 12.0));
    DelayCommand(10.0, Track(s3, "atk.bow", 8.0));
    DelayCommand(13.0, PLogF("atk.far.dist", GetDistanceBetween(s2, t2), 0.5));

    // Attacking in detect mode.
    object s4 = Actor("DATK", 10.0, 62.0);
    object t5 = Actor("DATK_T", 11.5, 62.0, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(t5, TRUE);
    SetActionMode(s4, ACTION_MODE_DETECT, TRUE);
    DelayCommand(10.0, Attack(s4, t5));
    DelayCommand(10.0, WatchDetect(s4, "detect.atk", 0.0, 8.0));
    // Casting in detect mode.
    object c4 = Actor("DCAST", 20.0, 62.0);
    SetActionMode(c4, ACTION_MODE_DETECT, TRUE);
    DelayCommand(10.0, AssignCommand(c4, ActionCastSpellAtObject(SPELL_MAGE_ARMOR, c4, METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(10.0, WatchDetect(c4, "detect.cast", 0.0, 6.0));

    // Casting from stealth: a self buff and a hostile spell; when the stealth flag drops.
    object c1 = Actor("SCAST", 55.0, 55.0);
    object c2 = Actor("SCASTH", 65.0, 55.0);
    object t4 = Actor("SCASTH_T", 67.0, 55.0, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(t4, TRUE);
    SetActionMode(c1, ACTION_MODE_STEALTH, TRUE);
    SetActionMode(c2, ACTION_MODE_STEALTH, TRUE);
    DelayCommand(10.0, AssignCommand(c1, ActionCastSpellAtObject(SPELL_MAGE_ARMOR, c1, METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(10.0, AssignCommand(c2, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, t4, METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(10.0, Track(c1, "cast.self", 6.0));
    DelayCommand(10.0, Track(c2, "cast.hostile", 6.0));
    DelayCommand(16.5, Modes(c1, "cast.self.after"));

    // Drinking a potion from stealth.
    object c3 = Actor("SPOTION", 55.0, 62.0);
    object potion = CreateItemOnObject("nw_it_mpotion001", c3);
    SetActionMode(c3, ACTION_MODE_STEALTH, TRUE);
    DelayCommand(10.0, AssignCommand(c3, ActionUseItemOnObject(potion, GetFirstItemProperty(potion), c3)));
    DelayCommand(10.0, Track(c3, "potion", 6.0));

    // Damage, being attacked, invisibility and sanctuary leave stealth on.
    object g1 = Actor("SDMG", 45.0, 70.0);
    SetActionMode(g1, ACTION_MODE_STEALTH, TRUE);
    DelayCommand(10.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), g1));
    DelayCommand(10.5, Modes(g1, "dmg.05"));
    DelayCommand(14.0, Modes(g1, "dmg.4"));
    object g2 = Actor("SHIT", 55.0, 70.0);
    object b3 = Actor("SHIT_A", 56.5, 70.0, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(g2, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTrueSeeing(), b3);
    SetActionMode(g2, ACTION_MODE_STEALTH, TRUE);
    DelayCommand(10.0, Attack(b3, g2));
    DelayCommand(16.0, Modes(g2, "hit.6"));
    object i1 = Actor("SINV", 65.0, 70.0);
    DelayCommand(10.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectInvisibility(INVISIBILITY_TYPE_NORMAL), i1, 6.0));
    DelayCommand(10.5, Modes(i1, "inv.05"));
    DelayCommand(18.0, Modes(i1, "inv.after"));
    object i2 = Actor("SSANC", 75.0, 70.0);
    DelayCommand(10.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectSanctuary(10), i2, 6.0));
    DelayCommand(10.5, Modes(i2, "sanc.05"));
    object i3 = Actor("SINV2", 75.0, 60.0);
    SetActionMode(i3, ACTION_MODE_STEALTH, TRUE);
    DelayCommand(10.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectInvisibility(INVISIBILITY_TYPE_NORMAL), i3, 6.0));
    DelayCommand(18.0, Modes(i3, "inv2.after"));

    // GetDetectMode on a plain creature that is damaged, one that only sees an enemy, and one under attack.
    object dd1 = Actor("DM_DMG", 10.0, 70.0);
    object dd2 = Actor("DM_SEE", 20.0, 70.0);
    object e2 = Actor("DM_SEE_E", 23.0, 70.0, STANDARD_FACTION_HOSTILE);
    DelayCommand(10.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(1), dd1));
    DelayCommand(10.5, PLogI("dm.dmg.05", GetDetectMode(dd1)));
    DelayCommand(14.0, PLogI("dm.dmg.4", GetDetectMode(dd1)));
    DelayCommand(20.0, PLogI("dm.dmg.10", GetDetectMode(dd1)));
    DelayCommand(14.0, PLogI("dm.see", GetDetectMode(dd2)));
    DelayCommand(14.0, PLogI("dm.victim", GetDetectMode(v1)));
    DelayCommand(14.0, PLogI("dm.attacker", GetDetectMode(f1)));

    DelayCommand(25.0, PDone());
}

// Stealth rules with deterministic skills (+50 against nothing, or nothing against +50): who a hider is hidden from,
// what invisibility, sanctuary and the vision effects do, perception events when stealth starts and stops, and
// whether a spotted hider stays spotted.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }

object Make(string sRes, float x, float y, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(Area(), Vector(x, y, 0.0), 0.0));
    int e; for (e = 5000; e <= 5012; e++) if (e != 5001 || sRes != "sr_obs") SetEventScript(o, e, "");
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Perm(object o, effect e) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o); }

void Read(object o, object h, string k)
{
    PLogI(k + ".seen", GetObjectSeen(h, o));
    PLogI(k + ".heard", GetObjectHeard(h, o));
}

// A case: an observer and its subject 3 m east. nHide/nMs: subject boosts; nSpot/nListen: observer boosts.
object Case(string k, int n, int nHide, int nMs, int nSpot, int nListen, int nFaction = STANDARD_FACTION_HOSTILE)
{
    float x = 10.0 + IntToFloat(n % 5) * 22.0, y = 10.0 + IntToFloat(n / 5) * 22.0;
    object o = Make("sr_obs", x, y, STANDARD_FACTION_COMMONER);
    object h = Make("nw_bandit001", x + 3.0, y, nFaction);
    SetLocalObject(o, "H", h);
    SetLocalString(o, "K", k);
    SetLocalObject(GetModule(), k, o);
    if (nHide > 0) Perm(h, EffectSkillIncrease(SKILL_HIDE, nHide));
    if (nMs > 0) Perm(h, EffectSkillIncrease(SKILL_MOVE_SILENTLY, nMs));
    if (nSpot > 0) Perm(o, EffectSkillIncrease(SKILL_SPOT, nSpot));
    if (nListen > 0) Perm(o, EffectSkillIncrease(SKILL_LISTEN, nListen));
    return h;
}

object Obs(string k) { return GetLocalObject(GetModule(), k); }

void Reads(string k, object h)
{
    object o = Obs(k);
    DelayCommand(14.0, Read(o, h, k + ".t14"));
    DelayCommand(30.0, Read(o, h, k + ".t30"));
}

void Stealth(object h, int on) { SetActionMode(h, ACTION_MODE_STEALTH, on); }

void Jump(object h, float dx)
{
    vector v = GetPosition(h); v.x += dx;
    AssignCommand(h, JumpToLocation(Location(Area(), v, 0.0)));
}

void main()
{
    object h;
    int n = 0;
    // Unbeatable and hopeless hiders, against a hostile observer.
    h = Case("hidden", n++, 50, 50, 0, 0); Stealth(h, TRUE); Reads("hidden", h);
    h = Case("spotted", n++, 0, 50, 50, 0); Stealth(h, TRUE); Reads("spotted", h);
    h = Case("heardonly", n++, 50, 0, 0, 50); Stealth(h, TRUE); Reads("heardonly", h);
    // Hidden from a neutral (merchant) observer? and from one of its own faction?
    h = Case("neutral", n++, 50, 50, 0, 0, STANDARD_FACTION_MERCHANT); Stealth(h, TRUE); Reads("neutral", h);
    h = Case("samefaction", n++, 50, 50, 0, 0, STANDARD_FACTION_COMMONER); Stealth(h, TRUE); Reads("samefaction", h);
    // Vision effects on the observer against an unbeatable hider.
    h = Case("trueseeing", n++, 50, 50, 0, 0); Stealth(h, TRUE); Perm(Obs("trueseeing"), EffectTrueSeeing()); Reads("trueseeing", h);
    h = Case("seeinvis", n++, 50, 50, 0, 0); Stealth(h, TRUE); Perm(Obs("seeinvis"), EffectSeeInvisible()); Reads("seeinvis", h);
    h = Case("ultravision", n++, 50, 50, 0, 0); Stealth(h, TRUE); Perm(Obs("ultravision"), EffectUltravision()); Reads("ultravision", h);
    // Invisible, not in stealth mode: unbeatable / hopeless Move Silently.
    h = Case("invis_quiet", n++, 0, 50, 0, 0); Perm(h, EffectInvisibility(INVISIBILITY_TYPE_NORMAL)); Reads("invis_quiet", h);
    h = Case("invis_loud", n++, 0, 0, 0, 50); Perm(h, EffectInvisibility(INVISIBILITY_TYPE_NORMAL)); Reads("invis_loud", h);
    h = Case("invis_loud_si", n++, 0, 0, 0, 50); Perm(h, EffectInvisibility(INVISIBILITY_TYPE_NORMAL)); Perm(Obs("invis_loud_si"), EffectSeeInvisible()); Reads("invis_loud_si", h);
    h = Case("invis_quiet_ts", n++, 0, 50, 0, 0); Perm(h, EffectInvisibility(INVISIBILITY_TYPE_NORMAL)); Perm(Obs("invis_quiet_ts"), EffectTrueSeeing()); Reads("invis_quiet_ts", h);
    // Invisible and in stealth, hopeless Hide but unbeatable Move Silently, against See Invisibility.
    h = Case("invis_stealth_si", n++, 0, 50, 50, 0); Perm(h, EffectInvisibility(INVISIBILITY_TYPE_NORMAL)); Stealth(h, TRUE); Perm(Obs("invis_stealth_si"), EffectSeeInvisible()); Reads("invis_stealth_si", h);
    // (Sanctuary is left out: the original's readings vary from run to run, see docs/STEALTH.md.)
    n += 2;
    // Darkness on the subject (quiet), with and without Ultravision on the observer; and darkness on the observer.
    h = Case("dark_quiet", n++, 0, 50, 0, 0); Perm(h, EffectDarkness()); Reads("dark_quiet", h);
    h = Case("dark_quiet_uv", n++, 0, 50, 0, 0); Perm(h, EffectDarkness()); Perm(Obs("dark_quiet_uv"), EffectUltravision()); Reads("dark_quiet_uv", h);
    h = Case("dark_loud", n++, 0, 0, 0, 50); Perm(h, EffectDarkness()); Reads("dark_loud", h);
    h = Case("obsdark", n++, 0, 0, 0, 0); Perm(Obs("obsdark"), EffectDarkness()); Reads("obsdark", h);
    h = Case("obsblind", n++, 0, 0, 0, 0); Perm(Obs("obsblind"), EffectBlindness()); Reads("obsblind", h);
    // Events when a seen creature enters stealth (unbeatable), and leaves it again.
    h = Case("enter", n++, 50, 50, 0, 0); Reads("enter", h);
    DelayCommand(15.0, Stealth(h, TRUE));
    DelayCommand(30.5, Read(Obs("enter"), h, "enter.t30b"));
    DelayCommand(40.0, Stealth(h, FALSE));
    DelayCommand(50.0, Read(Obs("enter"), h, "enter.t50"));
    // A spotted hider (+0 Hide, observer +50 Spot) whose observer then loses its Spot bonus: still seen?
    h = Case("sticky", n++, 0, 50, 0, 0); Stealth(h, TRUE);
    Perm(Obs("sticky"), EffectSkillIncrease(SKILL_SPOT, 50));
    DelayCommand(14.0, Read(Obs("sticky"), h, "sticky.t14"));
    DelayCommand(15.0, RemoveEffect(Obs("sticky"), GetFirstEffect(Obs("sticky"))));
    DelayCommand(15.1, Perm(h, EffectSkillIncrease(SKILL_HIDE, 50)));
    DelayCommand(30.0, Read(Obs("sticky"), h, "sticky.t30"));
    // The same, then the hider steps out of sight range (10 m) and back.
    h = Case("rehide", n++, 0, 50, 0, 0); Stealth(h, TRUE);
    Perm(Obs("rehide"), EffectSkillIncrease(SKILL_SPOT, 50));
    DelayCommand(15.0, RemoveEffect(Obs("rehide"), GetFirstEffect(Obs("rehide"))));
    DelayCommand(15.1, Perm(h, EffectSkillIncrease(SKILL_HIDE, 50)));
    DelayCommand(16.0, Jump(h, 10.0));
    DelayCommand(26.0, Jump(h, -10.0));
    DelayCommand(23.0, Read(Obs("rehide"), h, "rehide.away"));
    DelayCommand(40.0, Read(Obs("rehide"), h, "rehide.back"));
    // A spotted hider that leaves stealth and re-enters it (now unbeatable).
    h = Case("retoggle", n++, 0, 50, 0, 0); Stealth(h, TRUE);
    Perm(Obs("retoggle"), EffectSkillIncrease(SKILL_SPOT, 50));
    DelayCommand(15.0, RemoveEffect(Obs("retoggle"), GetFirstEffect(Obs("retoggle"))));
    DelayCommand(15.1, Perm(h, EffectSkillIncrease(SKILL_HIDE, 50)));
    DelayCommand(16.0, Stealth(h, FALSE));
    DelayCommand(17.0, Stealth(h, TRUE));
    DelayCommand(40.0, Read(Obs("retoggle"), h, "retoggle.t40"));
    // Hide in Plain Sight: a seen creature that enters stealth (unbeatable) disappears.
    h = Case("hips", n++, 50, 50, 0, 0); Reads("hips", h);
    Perm(h, EffectBonusFeat(FEAT_HIDE_IN_PLAIN_SIGHT));
    DelayCommand(15.0, Stealth(h, TRUE));
    DelayCommand(30.5, Read(Obs("hips"), h, "hips.t30b"));
    // A summoned associate watching its master hide.
    h = Case("master", n++, 50, 50, 0, 0, STANDARD_FACTION_COMMONER);
    DelayCommand(1.0, AssignCommand(h, ActionCastSpellAtLocation(SPELL_SUMMON_CREATURE_I, GetLocation(Obs("master")), METAMAGIC_ANY, TRUE, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(10.0, Stealth(h, TRUE));
    DelayCommand(10.0, PLogI("master.hassummon", GetIsObjectValid(GetAssociate(ASSOCIATE_TYPE_SUMMONED, h))));
    DelayCommand(30.0, PLogI("master.summonsees", GetObjectSeen(h, GetAssociate(ASSOCIATE_TYPE_SUMMONED, h))));
    DelayCommand(30.0, PLogI("master.summonhears", GetObjectHeard(h, GetAssociate(ASSOCIATE_TYPE_SUMMONED, h))));
    DelayCommand(60.0, PDone());
}

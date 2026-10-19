// How the original serializes an object's state (ObjectToJson with bSaveObjectState): the EffectList entry
// of each effect constructor, links and modifiers, action queue entries, door/trigger transitions and the
// combined area format's object lists. Entry details are compared; full dumps are RAW notes.
#include "parity_inc"

sqlquery Q(string s) { return SqlPrepareQueryObject(GetModule(), s); }
void Raw(string k, string v) { WriteTimestampedLogEntry("RAW|" + k + "|" + v); }

string Keys(json j)
{
    if (JsonGetType(j) != JSON_TYPE_OBJECT) return "<" + IntToString(JsonGetType(j)) + ">";
    sqlquery q = Q("SELECT group_concat(value, ',') FROM (SELECT value FROM json_each(@j) ORDER BY value)");
    SqlBindJson(q, "@j", JsonObjectKeys(j));
    return SqlStep(q) ? SqlGetString(q, 0) : "";
}

// A GFF field's value ("value" member of the field object).
json F(json s, string k) { return JsonObjectGet(JsonObjectGet(s, k), "value"); }
int FI(json s, string k) { return JsonGetInt(F(s, k)); }
// A list field's values; integers of 100000 and more (calendar days and times of day: when an effect expires or
// ticks next, which depends on the moment it was applied) read as "t".
string FList(json s, string k, string sub)
{
    json l = F(s, k);
    string r = "";
    int i;
    for (i = 0; i < JsonGetLength(l); i++)
    {
        json v = JsonObjectGet(JsonObjectGet(JsonArrayGet(l, i), sub), "value");
        string x = JsonGetType(v) == JSON_TYPE_STRING ? JsonGetString(v) : JsonDump(v);
        if (JsonGetType(v) == JSON_TYPE_INTEGER && JsonGetInt(v) >= 100000) x = "t";
        r += (i ? "," : "") + x;
    }
    return r;
}

// One EffectList entry, compact.
string Entry(json s)
{
    return "T" + IntToString(FI(s, "Type")) + " st" + IntToString(FI(s, "SubType")) + " d=" + JsonDump(F(s, "Duration"))
        + " n=" + IntToString(FI(s, "NumIntegers")) + " i=" + FList(s, "IntList", "Value")
        + " f=" + FList(s, "FloatList", "Value") + " s=" + FList(s, "StringList", "Value")
        + " o=" + IntToString(JsonGetLength(F(s, "ObjectList"))) + " v=" + IntToString(JsonGetLength(F(s, "VectorList")))
        + " cl=" + IntToString(FI(s, "CasterLevel")) + " sp=" + IntToString(FI(s, "SpellId"))
        + " tag=" + JsonGetString(F(s, "CustomTag")) + " ign=" + IntToString(FI(s, "IgnoreImmunity"))
        + " exp=" + IntToString(FI(s, "IsExposed")) + " icon=" + IntToString(FI(s, "IsIconShown"))
        + " skip=" + IntToString(FI(s, "SkipOnLoad"));
}

object Crt(int n) { return CreateObject(OBJECT_TYPE_CREATURE, "pe_crt", PCentre(IntToFloat(n % 8) - 4.0, IntToFloat(n / 8) - 4.0)); }

effect Make(int n)
{
    switch (n)
    {
        case 0: return EffectACIncrease(2);
        case 1: return EffectACIncrease(3, AC_NATURAL_BONUS);
        case 2: return EffectAbilityIncrease(ABILITY_STRENGTH, 2);
        case 3: return EffectAbilityDecrease(ABILITY_DEXTERITY, 1);
        case 4: return EffectAttackIncrease(2);
        case 5: return EffectAttackDecrease(1);
        case 6: return EffectDamageIncrease(2, DAMAGE_TYPE_FIRE);
        case 7: return EffectDamageDecrease(1);
        case 8: return EffectSavingThrowIncrease(SAVING_THROW_FORT, 2);
        case 9: return EffectSavingThrowDecrease(SAVING_THROW_ALL, 1);
        case 10: return EffectSkillIncrease(SKILL_HIDE, 5);
        case 11: return EffectSkillDecrease(SKILL_SPOT, 2);
        case 12: return EffectDamageResistance(DAMAGE_TYPE_FIRE, 10, 20);
        case 13: return EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE, 30);
        case 14: return EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 50);
        case 15: return EffectImmunity(IMMUNITY_TYPE_FEAR);
        case 16: return EffectHaste();
        case 17: return EffectSlow();
        case 18: return EffectInvisibility(INVISIBILITY_TYPE_NORMAL);
        case 19: return EffectConcealment(20);
        case 20: return EffectMissChance(30);
        case 21: return EffectTemporaryHitpoints(10);
        case 22: return EffectRegenerate(2, 6.0);
        case 23: return EffectMovementSpeedIncrease(50);
        case 24: return EffectMovementSpeedDecrease(30);
        case 25: return EffectSpellResistanceIncrease(15);
        case 26: return EffectVisualEffect(VFX_DUR_GLOW_RED);
        case 27: return EffectParalyze();
        case 28: return EffectStunned();
        case 29: return EffectSleep();
        case 30: return EffectBlindness();
        case 31: return EffectDeaf();
        case 32: return EffectSilence();
        case 33: return EffectEntangle();
        case 34: return EffectFrightened();
        case 35: return EffectCharmed();
        case 36: return EffectDazed();
        case 37: return EffectConfused();
        case 38: return EffectSanctuary(20);
        case 39: return EffectTrueSeeing();
        case 40: return EffectSeeInvisible();
        case 41: return EffectUltravision();
        case 42: return EffectDarkness();
        case 43: return EffectSpellFailure(50);
        case 44: return EffectNegativeLevel(2);
        case 45: return EffectCurse(2, 2, 2, 2, 2, 2);
        case 46: return EffectSpellImmunity(SPELL_FIREBALL);
        case 47: return EffectSpellLevelAbsorption(3, 10);
        case 48: return EffectDamageShield(5, DAMAGE_BONUS_1d6, DAMAGE_TYPE_FIRE);
        case 49: return EffectModifyAttacks(1);
        case 50: return EffectTurnResistanceIncrease(4);
        case 51: return EffectPetrify();
        case 52: return EffectCutsceneParalyze();
        case 53: return EffectCutsceneGhost();
        case 54: return EffectCutsceneImmobilize();
        case 55: return EffectEthereal();
        case 56: return EffectIcon(5);
        case 57: return EffectBonusFeat(FEAT_ALERTNESS);
        case 58: return EffectPacified();
        case 59: return EffectEnemyAttackBonus(2);
        case 60: return EffectRunScript("", "", "", 6.0, "data");
        case 61: return EffectTimeStopImmunity();
        case 62: return EffectForceWalk();
        case 63: return EffectTurned();
        case 64: return EffectDominated();
        case 65: return EffectTurnResistanceDecrease(2);
        case 66: return EffectSpellResistanceDecrease(5);
        case 67: return EffectACDecrease(2);
        case 68: return EffectDamageImmunityDecrease(DAMAGE_TYPE_FIRE, 25);
        case 69: return EffectLinkEffects(EffectACIncrease(1), EffectAttackIncrease(1));
        case 70: return VersusRacialTypeEffect(EffectACIncrease(2), RACIAL_TYPE_HUMANOID_ORC);
        case 71: return ExtraordinaryEffect(EffectAbilityIncrease(ABILITY_WISDOM, 1));
        case 72: return SupernaturalEffect(TagEffect(EffectSkillIncrease(SKILL_LORE, 3), "pe_tag"));
        case 73: return VersusAlignmentEffect(EffectSavingThrowIncrease(SAVING_THROW_WILL, 2), ALIGNMENT_LAWFUL, ALIGNMENT_GOOD);
        case 74: return EffectHitPointChangeWhenDying(1.0);
        // (A poison lands only when the creature fails its saving throw: not compared. Measured once: number 35.)
        case 75: return EffectAttackIncrease(1);
        case 76: return EffectDisease(DISEASE_BLINDING_SICKNESS);
        case 77: return EffectBeam(VFX_BEAM_FIRE, GetObjectByTag("PE_DOOR"), BODY_NODE_CHEST);
        case 78: return SetEffectSpellId(EffectAttackIncrease(3), SPELL_BLESS);
        case 79: return IgnoreEffectImmunity(EffectSkillIncrease(SKILL_TAUNT, 1));
    }
    effect eNone;
    return eNone;
}

int Count = 80;

void Measure(int n)
{
    object o = Crt(n);
    // A baseline: entries the creature has before the effect (equipment).
    json before = F(ObjectToJson(o, TRUE), "EffectList");
    effect e = Make(n);
    int nDur = (n >= 74 && n <= 76) ? DURATION_TYPE_PERMANENT : DURATION_TYPE_TEMPORARY;
    ApplyEffectToObject(nDur, e, o, 600.0);
    json after = F(ObjectToJson(o, TRUE), "EffectList");
    string k = "fx." + IntToString(n);
    PLog(k + ".script", IntToString(GetEffectType(e)) + " st=" + IntToString(GetEffectSubType(e)));
    // Entries the creature's equipment adds (SkipOnLoad) differ between engines; counted here, uncompared.
    Raw(k + ".counts", "base=" + IntToString(JsonGetLength(before)) + " after=" + IntToString(JsonGetLength(after)));
    int i;
    int m = 0;
    for (i = 0; i < JsonGetLength(after); i++)
    {
        json s = JsonArrayGet(after, i);
        if (FI(s, "SkipOnLoad") != 0) continue;
        PLog(k + "." + IntToString(m), Entry(s));
        if (n == 0 && m == 0) Raw("fx.full", JsonDump(s));
        m++;
    }
    DestroyObject(o, 0.5);
}

void Actions()
{
    object w = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(6.0, 6.0), FALSE, "PE_WP");
    int n;
    for (n = 0; n < 6; n++)
    {
        object o = Crt(n + 40);
        SetAILevel(o, AI_LEVEL_NORMAL);
        switch (n)
        {
            case 0: AssignCommand(o, ActionWait(30.0)); break;
            case 1: AssignCommand(o, ActionWait(30.0)); AssignCommand(o, ActionMoveToLocation(PCentre(8.0, 8.0))); break;
            case 2: AssignCommand(o, ActionWait(30.0)); AssignCommand(o, ActionSpeakString("hello")); break;
            case 3: AssignCommand(o, ActionWait(30.0)); AssignCommand(o, ActionDoCommand(SetLocalInt(o, "done", 1))); break;
            case 4: AssignCommand(o, ActionPlayAnimation(ANIMATION_LOOPING_SIT_CROSS, 1.0, 30.0)); break;
            case 5: AssignCommand(o, ActionWait(30.0)); AssignCommand(o, ActionJumpToObject(w)); break;
        }
        SetLocalObject(GetModule(), "act" + IntToString(n), o);
    }
}

void ReportActions()
{
    int n;
    for (n = 0; n < 6; n++)
    {
        object o = GetLocalObject(GetModule(), "act" + IntToString(n));
        json l = F(ObjectToJson(o, TRUE), "ActionList");
        string s = "cur=" + IntToString(GetCurrentAction(o)) + " count=" + IntToString(JsonGetLength(l));
        int i;
        for (i = 0; i < JsonGetLength(l); i++)
        {
            json a = JsonArrayGet(l, i);
            s += " [" + IntToString(FI(a, "ActionId")) + " g" + IntToString(FI(a, "GroupActionId")) + " p" + IntToString(FI(a, "NumParams"))
                + " types=" + FList(a, "Paramaters", "Type") + "]";
        }
        PLog("act." + IntToString(n), s);
        Raw("act." + IntToString(n) + ".full", JsonDump(l));
        object back = JsonToObject(ObjectToJson(o, TRUE), PCentre(8.0, -6.0), OBJECT_INVALID, TRUE);
        PLog("act." + IntToString(n) + ".back", IntToString(GetCurrentAction(back)) + " " + IntToString(JsonGetLength(F(ObjectToJson(back, TRUE), "ActionList"))));
        DestroyObject(back, 0.2);
    }
}

void Transitions()
{
    object door = GetObjectByTag("PE_DOOR");
    object trig = GetObjectByTag("PE_TRIG");
    object w = GetObjectByTag("PE_WP");
    SetTransitionTarget(door, w);
    SetTransitionTarget(trig, door);
    json jd = ObjectToJson(door, TRUE);
    json jt = ObjectToJson(trig, TRUE);
    Raw("trans.door", JsonDump(JsonObjectGet(jd, "Transition")));
    Raw("trans.trig", JsonDump(JsonObjectGet(jt, "Transition")));
    PLog("trans.door.keys", Keys(F(jd, "Transition")));
    PLog("trans.door.linked", JsonGetString(F(jd, "LinkedTo")) + " flags=" + IntToString(FI(jd, "LinkedToFlags")));
    PLog("trans.trig.linked", JsonGetString(F(jt, "LinkedTo")) + " flags=" + IntToString(FI(jt, "LinkedToFlags")));
    json jw = ObjectToJson(w, TRUE);
    Raw("trans.wp", JsonDump(JsonObjectGet(jw, "Transition")));
}

void Area()
{
    object area = GetObjectByTag("PARITY_AREA");
    SetLocalInt(area, "ai", 3);
    json caf = ObjectToJson(area);
    json git = F(caf, "GIT");
    json are = F(caf, "ARE");
    PLog("caf.git.counts", "crt=" + IntToString(JsonGetLength(F(git, "Creature List"))) + " door=" + IntToString(JsonGetLength(F(git, "Door List")))
        + " trig=" + IntToString(JsonGetLength(F(git, "TriggerList"))) + " wp=" + IntToString(JsonGetLength(F(git, "WaypointList")))
        + " var=" + IntToString(JsonGetLength(F(git, "VarTable"))) + " arevar=" + IntToString(JsonGetLength(F(are, "VarTable"))));
    json c0 = JsonArrayGet(F(git, "Creature List"), 0);
    Raw("caf.crt0.keys", Keys(c0));
    PLog("caf.crt0.has", "ObjectId=" + IntToString(JsonGetType(JsonObjectGet(c0, "ObjectId")) != 0)
        + " VarTable=" + IntToString(JsonGetType(JsonObjectGet(c0, "VarTable")) != 0)
        + " EffectList=" + IntToString(JsonGetType(JsonObjectGet(c0, "EffectList")) != 0)
        + " ActionList=" + IntToString(JsonGetType(JsonObjectGet(c0, "ActionList")) != 0));
    PLog("caf.are", "resref=" + JsonGetString(F(are, "ResRef")) + " tag=" + JsonGetString(F(are, "Tag")));
    object back = JsonToObject(caf, PCentre());
    int nObj = 0;
    object x = GetFirstObjectInArea(back);
    while (GetIsObjectValid(x)) { nObj++; x = GetNextObjectInArea(back); }
    int nOrig = 0;
    x = GetFirstObjectInArea(area);
    while (GetIsObjectValid(x)) { nOrig++; x = GetNextObjectInArea(area); }
    PLog("caf.back", "valid=" + IntToString(GetIsObjectValid(back)) + " same=" + IntToString(back == area) + " resref=" + GetResRef(back)
        + " tag=" + GetTag(back) + " ai=" + IntToString(GetLocalInt(back, "ai")) + " objects=" + IntToString(nObj) + "/" + IntToString(nOrig)
        + " w=" + IntToString(GetAreaSize(AREA_WIDTH, back)));
    object oDoor2 = GetNearestObjectByTag("PE_DOOR", GetFirstObjectInArea(back));
    PLog("caf.back.door", GetIsObjectValid(oDoor2) ? GetTag(oDoor2) + " link=" + GetTag(GetTransitionTarget(oDoor2)) + " area=" + IntToString(GetArea(oDoor2) == back) : "none");
}

void Step(int n)
{
    if (n < Count) { Measure(n); DelayCommand(0.1, Step(n + 1)); return; }
    switch (n - Count)
    {
        case 0: Actions(); DelayCommand(2.0, Step(n + 1)); return;
        case 1: ReportActions(); break;
        case 2: Transitions(); break;
        case 3: Area(); break;
        case 4: PDone(); return;
    }
    DelayCommand(0.5, Step(n + 1));
}

void main()
{
    DelayCommand(1.0, Step(0));
}

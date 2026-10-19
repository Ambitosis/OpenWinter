#include "visual_inc"
// encmodes (docs/ENCUMBRANCE.md): stealth and detect mode together, with and without a load,
// for a character without Keen Sense (Aluvian with the feat removed: "removeFeats": [240]); a pick-up while overloaded; dropping everything.

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|encm|" + s); }
void Mark(string s) { SendMessageToPC(GetFirstPC(), "ENC " + s); }
void Key(int vk, string s) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(vk) + "|" + s); }

int Normal(object pc) { return StringToInt(Get2DAString("encumbrance", "Normal", GetAbilityScore(pc, ABILITY_STRENGTH))); }
int Heavy(object pc) { return StringToInt(Get2DAString("encumbrance", "Heavy", GetAbilityScore(pc, ABILITY_STRENGTH))); }

void State(string sCase)
{
    object pc = GetFirstPC();
    Note(sCase + "|weight=" + IntToString(GetWeight(pc)) + "|str=" + IntToString(GetAbilityScore(pc, ABILITY_STRENGTH))
        + "|normal=" + IntToString(Normal(pc)) + "|heavy=" + IntToString(Heavy(pc)));
}

// Each added item gets its own tag so that potions never merge into one stack.
int Add(object pc, string sRes, int nStep, int nNeed)
{
    int nGuard = 0;
    while (nNeed >= nStep && nGuard < 60)
    {
        int n = GetLocalInt(pc, "ENC_N") + 1;
        SetLocalInt(pc, "ENC_N", n);
        object it = CreateItemOnObject(sRes, pc, 1, "ENC_ADD" + IntToString(n));
        int w = GetWeight(it);
        if (w <= 0) { Note("bad_item|" + sRes); return nNeed; }
        nNeed -= w;
        nGuard++;
    }
    return nNeed;
}

void Clear()
{
    object pc = GetFirstPC();
    object it = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(it))
    {
        if (GetStringLeft(GetTag(it), 7) == "ENC_ADD") DestroyObject(it);
        it = GetNextItemInInventory(pc);
    }
    effect e = GetFirstEffect(pc);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_ABILITY_INCREASE || GetEffectType(e) == EFFECT_TYPE_ABILITY_DECREASE) RemoveEffect(pc, e);
        e = GetNextEffect(pc);
    }
}

// nBase: what she carries after Clear (read at the start); nStr: a Strength effect.
void LoadTo(string sCase, int nTarget, int nStr)
{
    object pc = GetFirstPC();
    Mark(sCase);
    if (nStr > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, nStr), pc);
    if (nStr < 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityDecrease(ABILITY_STRENGTH, -nStr), pc);
    int nNeed = nTarget - GetLocalInt(pc, "ENC_BASE");
    nNeed = Add(pc, "nw_ashto001", 450, nNeed);
    nNeed = Add(pc, "nw_wswls001", 40, nNeed);
    nNeed = Add(pc, "nw_wswdg001", 10, nNeed);
    nNeed = Add(pc, "nw_it_mpotion001", 1, nNeed);
    Note(sCase + "|target=" + IntToString(nTarget) + "|left=" + IntToString(nNeed));
    DelayCommand(0.5, State(sCase));
}

void Home()
{
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions());
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(15.0, 30.0, 0.0), 0.0)));
}

void Sample(string sKey, int i)
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    Note(sKey + "|" + IntToString(i) + "|x=" + FloatToString(p.x, 0, 3) + "|y=" + FloatToString(p.y, 0, 3)
        + "|facing=" + FloatToString(GetFacing(pc), 0, 1) + "|action=" + IntToString(GetCurrentAction(pc)));
}

// W held for 3.2 s (with Shift held for bWalk), positions every 0.5 s.
void Drive(string sKey, int bWalk, float t)
{
    DelayCommand(t - 1.0, Home());
    if (bWalk) DelayCommand(t - 0.1, Key(16, "down"));
    DelayCommand(t, Key(87, "down"));
    int i;
    for (i = 0; i <= 6; i++) DelayCommand(t + 0.5 * IntToFloat(i), Sample(sKey, i));
    DelayCommand(t + 3.2, Key(87, "up"));
    if (bWalk) DelayCommand(t + 3.3, Key(16, "up"));
}

// A load, then running and walking: 12 s from t.
void Case(string sCase, int nTarget, int nStr, float t)
{
    DelayCommand(t, Clear());
    DelayCommand(t + 0.6, LoadTo(sCase, nTarget, nStr));
    Drive(sCase + ".run", FALSE, t + 2.5);
    Drive(sCase + ".walk", TRUE, t + 7.5);
}

void Base()
{
    object pc = GetFirstPC();
    SetLocalInt(pc, "ENC_BASE", GetWeight(pc));
    State("base");
}

void Modes(int nDetect, int nStealth)
{
    object pc = GetFirstPC();
    SetActionMode(pc, ACTION_MODE_DETECT, nDetect);
    SetActionMode(pc, ACTION_MODE_STEALTH, nStealth);
}
void ReadModes(string sKey)
{
    object pc = GetFirstPC();
    Note(sKey + "|detect=" + IntToString(GetActionMode(pc, ACTION_MODE_DETECT)) + "|stealth=" + IntToString(GetActionMode(pc, ACTION_MODE_STEALTH))
        + "|weight=" + IntToString(GetWeight(pc)));
}
void At(string sKey) { object pc = GetFirstPC(); SetLocalLocation(pc, sKey, GetLocation(pc)); }
void Speed(string sKey)
{
    object pc = GetFirstPC();
    Note(sKey + "|speed=" + FloatToString(GetDistanceBetweenLocations(GetLocalLocation(pc, sKey), GetLocation(pc)) / 2.0, 0, 3));
}
// A scripted run (ActionMoveToLocation, bRun TRUE) under a load and modes; the speed over 2 s, 1 s after it starts.
void Run(string sKey, int nLoad, int nDetect, int nStealth, float t)
{
    DelayCommand(t, Clear());
    DelayCommand(t + 0.5, Modes(FALSE, FALSE));
    DelayCommand(t + 0.6, LoadTo(sKey, nLoad, 0));
    DelayCommand(t + 1.5, Home());
    DelayCommand(t + 2.0, Modes(nDetect, nStealth));
    DelayCommand(t + 2.3, ReadModes(sKey));
    DelayCommand(t + 2.5, AssignCommand(GetFirstPC(), ActionMoveToLocation(Location(GetArea(GetFirstPC()), Vector(55.0, 30.0, 0.0), 0.0), TRUE)));
    DelayCommand(t + 3.5, At(sKey));
    DelayCommand(t + 5.5, Speed(sKey));
    DelayCommand(t + 5.6, ReadModes(sKey + ".after"));
}

// While overloaded she picks up an item from the ground: does it reach her inventory?
void Drop()
{
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    object it = CreateObject(OBJECT_TYPE_ITEM, "nw_wswdg001", Location(GetArea(pc), Vector(v.x + 1.5, v.y, v.z), 0.0), FALSE, "ENC_ADDGROUND");
    SetLocalObject(pc, "GROUND", it);
    Note("ground|weight=" + IntToString(GetWeight(pc)));
    AssignCommand(pc, ActionPickUpItem(it));
}
void Picked()
{
    object pc = GetFirstPC();
    object it = GetLocalObject(pc, "GROUND");
    Note("pickup|valid=" + IntToString(GetIsObjectValid(it)) + "|mine=" + IntToString(GetItemPossessor(it) == pc) + "|weight=" + IntToString(GetWeight(pc)));
}
void DropAll()
{
    object pc = GetFirstPC();
    Mark("dropall");
    object it = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(it))
    {
        if (GetStringLeft(GetTag(it), 7) == "ENC_ADD") DestroyObject(it);
        it = GetNextItemInInventory(pc);
    }
    DelayCommand(0.5, State("dropall"));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(pc, i, "");
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(1.0, VCamera(pc, 0.0, 14.0, 50.0));
    DelayCommand(2.0, Base());
    DelayCommand(2.1, Note("feat_keen_sense|" + IntToString(GetHasFeat(FEAT_KEEN_SENSE, pc)) + "|race=" + IntToString(GetRacialType(pc))));
    float t = 3.0;
    // Unloaded: no mode, detect, stealth, both.
    Run("w0.none", 0, FALSE, FALSE, t); t += 7.0;
    Run("w0.detect", 0, TRUE, FALSE, t); t += 7.0;
    Run("w0.stealth", 0, FALSE, TRUE, t); t += 7.0;
    Run("w0.both", 0, TRUE, TRUE, t); t += 7.0;
    // Encumbered (just above Normal): none, detect, stealth, both.
    int nNormal = Normal(pc) + 1, nHeavy = Heavy(pc) + 1;
    Run("enc.none", nNormal, FALSE, FALSE, t); t += 7.0;
    Run("enc.detect", nNormal, TRUE, FALSE, t); t += 7.0;
    Run("enc.stealth", nNormal, FALSE, TRUE, t); t += 7.0;
    Run("enc.both", nNormal, TRUE, TRUE, t); t += 7.0;
    // Overloaded (just above Heavy): none, detect, stealth, both.
    Run("over.none", nHeavy, FALSE, FALSE, t); t += 7.0;
    Run("over.detect", nHeavy, TRUE, FALSE, t); t += 7.0;
    Run("over.stealth", nHeavy, FALSE, TRUE, t); t += 7.0;
    Run("over.both", nHeavy, TRUE, TRUE, t); t += 7.0;
    // Overloaded, picking an item up from the ground; then everything dropped at once.
    DelayCommand(t, Modes(FALSE, FALSE));
    DelayCommand(t + 0.5, Home());
    DelayCommand(t + 1.0, Mark("pickup"));
    DelayCommand(t + 1.5, Drop());
    DelayCommand(t + 6.0, Picked()); t += 7.0;
    DelayCommand(t, DropAll()); t += 2.0;
    DelayCommand(t, VShot("a_end"));
    DelayCommand(t + 1.0, Mark("end"));
    DelayCommand(t + 2.0, VDone());
}

#include "visual_inc"
// encumberdrive (docs/ENCUMBRANCE.md): the player's own speed at each load, driven by the keyboard (W held) and by
// ActionMoveToLocation, with Strength effects, stealth and detect mode, speed effects and the plot flag. Positions
// every 0.5 s while the key is held. Measured in three runs of parts of this script; the first had the
// player plot throughout, which is how plot's exemption showed (encumberspeed, also plot, saw no slowing either).

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|enc|" + s); }
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

// Controls: detect mode (a movement stage the original applies), scripted walking, and a long wait under load.
void Detect(int bOn) { SetActionMode(GetFirstPC(), ACTION_MODE_DETECT, bOn); Note("detect|" + IntToString(GetActionMode(GetFirstPC(), ACTION_MODE_DETECT))); }
void Stealth(int bOn) { SetActionMode(GetFirstPC(), ACTION_MODE_STEALTH, bOn); Note("stealth|" + IntToString(GetActionMode(GetFirstPC(), ACTION_MODE_STEALTH))); }
void At(string sKey) { object pc = GetFirstPC(); SetLocalLocation(pc, sKey, GetLocation(pc)); }
void Speed(string sKey)
{
    object pc = GetFirstPC();
    Note(sKey + "|scripted=" + FloatToString(GetDistanceBetweenLocations(GetLocalLocation(pc, sKey), GetLocation(pc)) / 2.0, 0, 3));
}
void Scripted(string sKey, int bRun, float t)
{
    DelayCommand(t - 1.0, Home());
    DelayCommand(t, AssignCommand(GetFirstPC(), ActionMoveToLocation(Location(GetArea(GetFirstPC()), Vector(55.0, 30.0, 0.0), 0.0), bRun)));
    DelayCommand(t + 0.5, At(sKey));
    DelayCommand(t + 2.5, Speed(sKey));
}

void Fx(int n)
{
    object pc = GetFirstPC();
    effect e;
    if (n == 1) e = EffectHaste();
    else if (n == 2) e = EffectMovementSpeedDecrease(50);
    else if (n == 3) e = EffectMovementSpeedIncrease(50);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, e, pc, 7.0);
}
void Plot(int b) { SetPlotFlag(GetFirstPC(), b); Note("plot|" + IntToString(GetPlotFlag(GetFirstPC()))); }

// A load with a mode or an effect, then W held: 9 s from t. nWith: 1 stealth, 2 haste, 3 slow 50%, 4 plot, 5 +50%.
void With(string sCase, int nTarget, int nWith, float t)
{
    DelayCommand(t, Clear());
    DelayCommand(t + 0.6, LoadTo(sCase, nTarget, 0));
    if (nWith == 1) DelayCommand(t + 1.0, Stealth(TRUE));
    if (nWith == 2) DelayCommand(t + 1.0, Fx(1));
    if (nWith == 3) DelayCommand(t + 1.0, Fx(2));
    if (nWith == 4) DelayCommand(t + 1.0, Plot(TRUE));
    if (nWith == 5) DelayCommand(t + 1.0, Fx(3));
    Drive(sCase + ".run", FALSE, t + 2.5);
    if (nWith == 1) DelayCommand(t + 6.0, Stealth(FALSE));
    if (nWith == 4) DelayCommand(t + 6.0, Plot(FALSE));
}

void LateLoad() { LoadTo("late", 5500, 0); }

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(pc, i, "");
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(1.0, VCamera(pc, 0.0, 14.0, 50.0));
    DelayCommand(2.0, Base());
    float t = 3.0;
    // Each load: W held, then Shift+W (the posted Shift did not make her walk: both read as her run).
    Case("w0", 0, 0, t); t += 13.0;
    Case("w861", 861, 0, t); t += 13.0;
    Case("w1100", 1100, 0, t); t += 13.0;
    Case("w1101", 1101, 0, t); t += 13.0;
    Case("w2200", 2200, 0, t); t += 13.0;
    Case("w5500", 5500, 0, t); t += 13.0;
    Case("str14.w1000", 1000, 4, t); t += 13.0;
    Case("str6.w650", 650, -4, t); t += 13.0;
    Case("str6.w701", 701, -4, t); t += 13.0;
    // Controls, unloaded: scripted walking, detect mode (Aluvian's Keen Sense: no stage), stealth.
    DelayCommand(t, Clear()); t += 1.0;
    Scripted("ctl.scripted_walk", FALSE, t + 1.0); t += 4.0;
    DelayCommand(t, Detect(TRUE));
    Drive("ctl.detect.run", FALSE, t + 1.5); t += 5.5;
    DelayCommand(t, Detect(FALSE));
    DelayCommand(t + 0.2, Stealth(TRUE));
    Drive("ctl.stealth.run", FALSE, t + 1.5); t += 5.5;
    DelayCommand(t, Stealth(FALSE));
    DelayCommand(t + 0.2, Clear());
    DelayCommand(t + 0.6, LoadTo("w5500.scripted", 5500, 0)); t += 2.0;
    Scripted("w5500.scripted_walk", FALSE, t + 1.0); t += 4.0;
    // Stages with modes and effects.
    With("stealth.w0", 0, 1, t); t += 8.0;
    With("stealth.w861", 861, 1, t); t += 8.0;
    With("stealth.w1101", 1101, 1, t); t += 8.0;
    With("haste.w861", 861, 2, t); t += 8.0;
    With("haste.w1101", 1101, 2, t); t += 8.0;
    With("slow50.w861", 861, 3, t); t += 8.0;
    With("slow50.w1101", 1101, 3, t); t += 8.0;
    With("inc50.w1101", 1101, 5, t); t += 8.0;
    // Plot made after the load (stays slowed), then plot before it (never slowed).
    With("plot.after.w1101", 1101, 4, t); t += 8.0;
    DelayCommand(t, Plot(TRUE));
    Case("plot.before.w1101", 1101, 0, t + 0.2); t += 13.0;
    DelayCommand(t, Plot(FALSE));
    // Weight added while she runs: 13 samples, the load at sample 3 (1.5 s).
    DelayCommand(t, Clear());
    DelayCommand(t + 1.5, Home());
    DelayCommand(t + 2.5, Key(87, "down"));
    for (i = 0; i <= 12; i++) DelayCommand(t + 2.5 + 0.5 * IntToFloat(i), Sample("late.run", i));
    DelayCommand(t + 4.0, LateLoad());
    DelayCommand(t + 8.7, Key(87, "up")); t += 10.0;
    DelayCommand(t, Clear());
    DelayCommand(t + 1.0, VShot("a_end"));
    DelayCommand(t + 2.0, Mark("end"));
    DelayCommand(t + 3.0, VDone());
}

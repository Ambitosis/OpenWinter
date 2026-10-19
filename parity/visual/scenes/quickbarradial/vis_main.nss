#include "visual_inc"
// quickbarradial (docs/QUICKBAR_RADIAL.md): the radial a right-click on a quickbar slot opens. Run with
// --real-mouse --native-framebuffer --silent: the original reads the real pointer's resting place, so the harness
// rests it (after checking the window there is the game's) and posts the right button; VISUAL|pointer rests it on
// each option for its label and real left presses choose. A first run found the ring centred 100 px above the slot
// (the slot's contents on the large disc) with options at the eight 100 px hooks: on F12 (Aluvian's Light) north
// an eye, south-east the spell again and south a star ("Empty Quickslot"). This run reads those labels, empties F12
// with the star, then reads the empty slot's ring and opens each of its directions.

void Log(string s) { WriteTimestampedLogEntry(s); }
// A point in client pixels at 1280x720, as the fractions the harness takes.
string P(float x, float y) { return FloatToString(x / 1280.0, 0, 4) + "," + FloatToString(y / 720.0, 0, 4); }

const float SX = 1010.0;   // F12's centre
const float SY = 703.0;
const float CX = 1010.0;   // the ring's centre, 100 px above the slot's top
const float CY = 565.0;

float HookX(int i) { return CX + 100.0 * sin(45.0 * IntToFloat(i)); }
float HookY(int i) { return CY - 100.0 * cos(45.0 * IntToFloat(i)); }
string Dir(int i)
{
    switch (i) { case 0: return "n"; case 1: return "ne"; case 2: return "e"; case 3: return "se"; case 4: return "s";
        case 5: return "sw"; case 6: return "w"; } return "nw";
}

void Open(float t)
{
    DelayCommand(t, Log("VISUAL|mouse|" + P(SX, SY) + "|down|right"));
    DelayCommand(t + 0.3, Log("VISUAL|mouse|" + P(SX, SY) + "|up|right"));
}
void Close(float t)
{
    DelayCommand(t, Log("VISUAL|mouse|0.50,0.25|down"));
    DelayCommand(t + 0.15, Log("VISUAL|mouse|0.50,0.25|up"));
}
void Click(float x, float y, float t)
{
    DelayCommand(t, Log("VISUAL|mouse|" + P(x, y) + "|down"));
    DelayCommand(t + 0.15, Log("VISUAL|mouse|" + P(x, y) + "|up"));
}
void Point(float x, float y, float t) { DelayCommand(t, Log("VISUAL|pointer|" + P(x, y))); }

// Opens the ring and rests the pointer on the centre and each hook in turn, a shot at each; returns the time after.
float Hovers(string sPrefix, float t)
{
    Open(t); t += 1.0;
    Point(CX, CY, t); DelayCommand(t + 0.7, VShot(sPrefix + "_c")); t += 1.2;
    int i;
    for (i = 0; i < 8; i++) { Point(HookX(i), HookY(i), t); DelayCommand(t + 0.7, VShot(sPrefix + "_" + Dir(i))); t += 1.2; }
    Close(t); t += 1.0;
    return t;
}

// Opens the ring, chooses direction i and reads the sub-ring that opens: the pointer on the centre and each hook.
float Sub(int i, float t)
{
    Open(t); t += 1.0;
    Click(HookX(i), HookY(i), t); t += 1.2;
    DelayCommand(t, VShot("e_" + Dir(i) + "_open")); t += 0.6;
    Point(CX, CY, t); DelayCommand(t + 0.6, VShot("e_" + Dir(i) + "_c")); t += 1.0;
    int j;
    for (j = 0; j < 8; j++) { Point(HookX(j), HookY(j), t); DelayCommand(t + 0.6, VShot("e_" + Dir(i) + "_" + Dir(j))); t += 1.0; }
    Close(t); t += 1.2;
    return t;
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(pc, i, "");
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 50.0));
    DelayCommand(3.0, VShot("a_bar"));
    // Empty Quickslot (south) empties F12.
    float t = 4.0;
    Open(t); t += 1.0;
    Click(HookX(4), HookY(4), t); t += 1.5;
    DelayCommand(t, VShot("c_emptied")); t += 1.0;
    // The empty slot's sub-rings (direction 2, Attack, and 0, Custom Text Macro, are chosen last).
    t = Sub(1, t);
    t = Sub(3, t);
    t = Sub(4, t);
    t = Sub(5, t);
    t = Sub(6, t);
    t = Sub(7, t);
    // Attack: what a choice that opens nothing does to the slot.
    Open(t); t += 1.0;
    Click(HookX(2), HookY(2), t); t += 1.2;
    Point(640.0, 300.0, t); DelayCommand(t + 0.6, VShot("f_attack_chosen")); t += 1.5;
    // Custom Text Macro on F11 (still Aluvian's), after emptying it the same way.
    DelayCommand(t, Log("VISUAL|mouse|" + P(SX - 67.0, SY) + "|down|right"));
    DelayCommand(t + 0.3, Log("VISUAL|mouse|" + P(SX - 67.0, SY) + "|up|right")); t += 1.0;
    Click(HookX(4) - 67.0, HookY(4), t); t += 1.5;
    DelayCommand(t, Log("VISUAL|mouse|" + P(SX - 67.0, SY) + "|down|right"));
    DelayCommand(t + 0.3, Log("VISUAL|mouse|" + P(SX - 67.0, SY) + "|up|right")); t += 1.0;
    Click(HookX(0) - 67.0, HookY(0), t); t += 1.5;
    DelayCommand(t, VShot("g_macro")); t += 1.0;
    DelayCommand(t, VDone());
    DelayCommand(t + 0.5, Log("VISUAL|key|27|tap"));
}

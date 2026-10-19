#include "visual_inc"
// quickbaremote2 (docs/QUICKBAR_RADIAL.md): the emotes quickbaremote left unclear (short ones, and how long the
// looping ones last); each of the quickbar slot's Emote rings put on F10 and used, the player
// in window bursts (2 a second for 7 s from the click) for what it plays and for how long. Run with
// python tools/visual/combat_audio.py quickbaremote --sounds vs_fshaldrf_ --real-mouse: each of Aluvian's voice set
// sounds becomes its own tone, so the recording says which one an emote speaks. Steps run on the harness's wall
// clock (VISUAL|wall); each emote's click is also logged as a phase at the same offset.

void Log(string s) { WriteTimestampedLogEntry(s); }
string P(float x, float y) { return FloatToString(x / 1280.0, 0, 4) + "," + FloatToString(y / 720.0, 0, 4); }
void W(float t, string s) { Log("VISUAL|wall|" + FloatToString(t, 0, 2) + "|" + s); }
void Phase(float t, string s) { }

const float SY = 703.0;
const float CY = 565.0;
float SX(int f) { return 1010.0 - 67.0 * IntToFloat(12 - f); }
float HX(int f, int i) { return SX(f) + 100.0 * sin(45.0 * IntToFloat(i)); }
float HY(int i) { return CY - 100.0 * cos(45.0 * IntToFloat(i)); }
void Open(int f, float t) { W(t, "mouse|" + P(SX(f), SY) + "|down|right|real"); W(t + 0.3, "mouse|" + P(SX(f), SY) + "|up|right|real"); }
void Click(float x, float y, float t) { W(t, "mouse|" + P(x, y) + "|down"); W(t + 0.15, "mouse|" + P(x, y) + "|up"); }
void Hook(int f, int i, float t) { Click(HX(f, i), HY(i), t); }

// The bar is shot before the click (the emote must be on F10); short emotes in one 3 s burst at 6 a second, long
// ones in three 5 s bursts a frame a second.
float Emote(int level, int i, string s, int bLong, float t)
{
    Open(10, t); t += 1.0;
    Hook(10, 7, t); t += 1.2;
    if (level > 1) { Hook(10, 7, t); t += 1.2; }
    if (level > 2) { Hook(10, 7, t); t += 1.2; }
    Hook(10, i, t); t += 1.2;
    W(t, "pointer|" + P(640.0, 150.0)); W(t + 0.1, "burst|em_" + s + "_bar|0.25|4"); t += 0.4;
    Click(SX(10), SY, t);
    if (bLong) { W(t + 0.2, "burst|em_" + s + "_a|5|1"); W(t + 5.3, "burst|em_" + s + "_b|5|1"); W(t + 10.4, "burst|em_" + s + "_c|5|1"); t += 15.8; }
    else { W(t + 0.1, "burst|em_" + s + "|3|6"); t += 3.5; }
    Open(10, t); Hook(10, 4, t + 1.0); t += 2.2;
    return t;
}

void Steps()
{
    float t = 1.0;
    Open(10, t); Hook(10, 4, t + 1.0); t += 2.5;
    t = Emote(1, 0, "bow", FALSE, t); t = Emote(1, 1, "threaten", FALSE, t); t = Emote(1, 2, "greet", FALSE, t);
    t = Emote(1, 4, "salute", FALSE, t); t = Emote(2, 0, "nod", FALSE, t);
    t = Emote(2, 3, "poisoned", TRUE, t); t = Emote(3, 0, "fallforward", TRUE, t); t = Emote(3, 2, "sit", TRUE, t);
    t = Emote(3, 3, "meditate", TRUE, t); t = Emote(3, 6, "worship", TRUE, t);
    W(t, "done|1");
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(pc, i, "");
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 8.0, 50.0));
    DelayCommand(3.0, Steps());
}

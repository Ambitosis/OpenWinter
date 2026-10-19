#include "visual_inc"
// quickbaremote (docs/QUICKBAR_RADIAL.md): each emote of the quickbar slot's Emote rings put on F10 and used, the player
// in window bursts (2 a second for 7 s from the click) for what it plays and for how long. Run with
// python tools/visual/combat_audio.py quickbaremote --sounds vs_fshaldrf_ --real-mouse: each of Aluvian's voice set
// sounds becomes its own tone, so the recording says which one an emote speaks. Steps run on the harness's wall
// clock (VISUAL|wall); each emote's click is also logged as a phase at the same offset.

void Log(string s) { WriteTimestampedLogEntry(s); }
string P(float x, float y) { return FloatToString(x / 1280.0, 0, 4) + "," + FloatToString(y / 720.0, 0, 4); }
void W(float t, string s) { Log("VISUAL|wall|" + FloatToString(t, 0, 2) + "|" + s); }
void Phase(float t, string s) { DelayCommand(t, Log("VISUAL|note|quickbaremote|phase|" + s)); }

const float SY = 703.0;
const float CY = 565.0;
float SX(int f) { return 1010.0 - 67.0 * IntToFloat(12 - f); }
float HX(int f, int i) { return SX(f) + 100.0 * sin(45.0 * IntToFloat(i)); }
float HY(int i) { return CY - 100.0 * cos(45.0 * IntToFloat(i)); }
void Open(int f, float t) { W(t, "mouse|" + P(SX(f), SY) + "|down|right|real"); W(t + 0.3, "mouse|" + P(SX(f), SY) + "|up|right|real"); }
void Click(float x, float y, float t) { W(t, "mouse|" + P(x, y) + "|down"); W(t + 0.15, "mouse|" + P(x, y) + "|up"); }
void Hook(int f, int i, float t) { Click(HX(f, i), HY(i), t); }

float Emote(int level, int i, string s, float t)
{
    Open(10, t); t += 1.0;
    Hook(10, 7, t); t += 1.2;
    if (level > 1) { Hook(10, 7, t); t += 1.2; }
    if (level > 2) { Hook(10, 7, t); t += 1.2; }
    Hook(10, i, t); t += 1.2;
    W(t, "pointer|" + P(640.0, 150.0)); t += 0.3;
    Phase(t, s); Click(SX(10), SY, t); W(t + 0.2, "burst|em_" + s + "|7|2"); t += 7.6;
    Open(10, t); Hook(10, 4, t + 1.0); t += 2.2;
    return t;
}

void Steps()
{
    float t = 1.0;
    Open(10, t); Hook(10, 4, t + 1.0); t += 2.5;
    t = Emote(1, 0, "bow", t); t = Emote(1, 1, "threaten", t); t = Emote(1, 2, "greet", t); t = Emote(1, 3, "laugh", t);
    t = Emote(1, 4, "salute", t); t = Emote(1, 5, "cheer", t); t = Emote(1, 6, "goodbye", t);
    t = Emote(2, 0, "nod", t); t = Emote(2, 1, "tired", t); t = Emote(2, 2, "beg", t); t = Emote(2, 3, "poisoned", t);
    t = Emote(2, 4, "bored", t); t = Emote(2, 5, "lookfar", t); t = Emote(2, 6, "scratch", t);
    t = Emote(3, 0, "fallforward", t); t = Emote(3, 2, "sit", t); t = Emote(3, 3, "meditate", t);
    t = Emote(3, 4, "drink", t); t = Emote(3, 5, "read", t); t = Emote(3, 6, "worship", t); t = Emote(3, 7, "fallback", t);
    W(t, "done|1");
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(pc, i, "");
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 6.0, 50.0));
    DelayCommand(3.0, Steps());
}

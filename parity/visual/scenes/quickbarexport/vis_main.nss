#include "visual_inc"
// quickbarexport (docs/QUICKBAR_RADIAL.md): how the original saves each emote on the quickbar. The emotes of the slot
// ring's Emote rings go on the bar in ring order and the character is exported (local vault) twice. Run with --real-mouse --native-framebuffer --silent; read QBList from both exports.

void Log(string s) { WriteTimestampedLogEntry(s); }
string P(float x, float y) { return FloatToString(x / 1280.0, 0, 4) + "," + FloatToString(y / 720.0, 0, 4); }
void W(float t, string s) { Log("VISUAL|wall|" + FloatToString(t, 0, 2) + "|" + s); }

const float SY = 703.0;
const float CY = 565.0;
float SX(int f) { return 1010.0 - 67.0 * IntToFloat(12 - f); }
float HX(int f, int i) { return SX(f) + 100.0 * sin(45.0 * IntToFloat(i)); }
float HY(int i) { return CY - 100.0 * cos(45.0 * IntToFloat(i)); }
void Open(int f, float t) { W(t, "mouse|" + P(SX(f), SY) + "|down|right|real"); W(t + 0.3, "mouse|" + P(SX(f), SY) + "|up|right|real"); }
void Click(float x, float y, float t) { W(t, "mouse|" + P(x, y) + "|down"); W(t + 0.15, "mouse|" + P(x, y) + "|up"); }
void Hook(int f, int i, float t) { Click(HX(f, i), HY(i), t); }

void Export(string s)
{
    object pc = GetFirstPC();
    ExportSingleCharacter(pc);
    Log("VISUAL|note|exported|" + s);
}

// Empties F<f> and puts emote <i> of ring <level> on it.
float Put(int f, int level, int i, float t)
{
    Open(f, t); Hook(f, 4, t + 1.0); t += 2.2;
    Open(f, t); t += 1.0;
    Hook(f, 7, t); t += 1.2;
    if (level > 1) { Hook(f, 7, t); t += 1.2; }
    if (level > 2) { Hook(f, 7, t); t += 1.2; }
    Hook(f, i, t); t += 1.2;
    return t;
}

void Steps()
{
    // The first run (22:20) saved Bow 7, Threaten 8, Greet 6, Laugh 4, Salute 3 and Cheer 1 from F1-F6, then lost its
    // place at F7 (a potion's ring has no Empty Quickslot at south). This run skips F7 and puts the other fifteen.
    float t = 1.0;
    t = Put(1, 1, 6, t); t = Put(2, 2, 0, t); t = Put(3, 2, 1, t); t = Put(4, 2, 2, t); t = Put(5, 2, 3, t);
    t = Put(6, 2, 4, t); t = Put(8, 2, 5, t); t = Put(9, 2, 6, t); t = Put(10, 3, 0, t); t = Put(11, 3, 2, t);
    t = Put(12, 3, 3, t);
    W(t, "shot|a_first"); DelayCommand(t + 1.0, Export("first")); t += 3.0;
    t = Put(1, 3, 4, t); t = Put(2, 3, 5, t); t = Put(3, 3, 6, t); t = Put(4, 3, 7, t);
    W(t, "shot|b_second"); DelayCommand(t + 1.0, Export("second")); t += 3.0;
    W(t, "done|1");
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(pc, i, "");
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 50.0));
    DelayCommand(3.0, Steps());
}

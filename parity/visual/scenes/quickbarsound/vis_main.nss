#include "visual_inc"
// quickbarsound (docs/QUICKBAR_RADIAL.md): the sounds of assigning and emptying a quickbar slot through its ring, and
// of a text macro. Run with python tools/visual/combat_audio.py quickbarsound --sounds gui_ --real-mouse: every gui_*
// sound becomes its own tone. Steps run on the harness's wall clock (VISUAL|wall); each phase is also logged as a note
// at the same offset (the game isn't paused until the macro's window, the last phase).

void Log(string s) { WriteTimestampedLogEntry(s); }
string P(float x, float y) { return FloatToString(x / 1280.0, 0, 4) + "," + FloatToString(y / 720.0, 0, 4); }
void W(float t, string s) { Log("VISUAL|wall|" + FloatToString(t, 0, 2) + "|" + s); }
void Phase(float t, string s) { DelayCommand(t, Log("VISUAL|note|quickbarsound|phase|" + s)); }

const float SY = 703.0;
const float CY = 565.0;
float SX(int f) { return 1010.0 - 67.0 * IntToFloat(12 - f); }
float HX(int f, int i) { return SX(f) + 100.0 * sin(45.0 * IntToFloat(i)); }
float HY(int i) { return CY - 100.0 * cos(45.0 * IntToFloat(i)); }
void Open(int f, float t) { W(t, "mouse|" + P(SX(f), SY) + "|down|right|real"); W(t + 0.3, "mouse|" + P(SX(f), SY) + "|up|right|real"); }
void Click(float x, float y, float t) { W(t, "mouse|" + P(x, y) + "|down"); W(t + 0.15, "mouse|" + P(x, y) + "|up"); }
void Hook(int f, int i, float t) { Click(HX(f, i), HY(i), t); }

// 40 characters: digits that show where a cut falls.
string L40() { return "0123456789112345678921234567893123456789"; }

void Steps()
{
    float t = 1.0;
    Phase(t, "open_filled"); Open(12, t); t += 2.5;
    Phase(t, "empty_f12"); Hook(12, 4, t); t += 2.5;
    Phase(t, "open_empty"); Open(12, t); t += 2.5;
    Phase(t, "assign_attack"); Hook(12, 2, t); t += 2.5;
    Open(12, t); t += 1.5;
    Phase(t, "empty_attack"); Hook(12, 4, t); t += 2.5;
    Open(12, t); t += 1.5;
    Phase(t, "open_abilities"); Hook(12, 6, t); t += 2.5;
    Phase(t, "assign_taunt"); Hook(12, 1, t); t += 2.5;
    Phase(t, "idle"); W(t, "pointer|" + P(640.0, 150.0)); t += 2.0;
    Open(11, t); t += 1.5;
    Phase(t, "empty_f11"); Hook(11, 4, t); t += 2.5;
    Open(11, t); t += 1.5;
    // The label and text are long, to find how many characters each box takes (the export holds what was kept).
    Phase(t, "macro_open"); Hook(11, 0, t); t += 2.0;
    W(t, "text|" + L40() + "abcdefghij"); t += 3.0; W(t, "shot|m_label_long"); t += 1.0;
    Phase(t, "macro_label_ok"); Click(481.0, 456.0, t); t += 2.0;
    string s = L40(); s = s + s + s + s + s + s + s + s;   // 320
    W(t, "text|" + s); t += 12.0; W(t, "shot|m_text_long"); t += 1.0;
    Phase(t, "macro_text_ok"); Click(481.0, 456.0, t); t += 2.5;
    W(t, "shot|m_slot"); t += 1.0;
    Phase(t, "macro_use"); Click(SX(11), SY, t); t += 2.5;
    // Edit Label (E): what the box holds at first; then X typed and OK. Edit Command (W): its box, then Cancel.
    Open(11, t); t += 1.5;
    Phase(t, "edit_label"); Hook(11, 2, t); t += 2.0; W(t, "shot|m_edit_label"); t += 1.0;
    W(t, "text|X"); t += 1.0; W(t, "shot|m_edit_label_typed"); t += 1.0;
    Phase(t, "edit_label_ok"); Click(481.0, 456.0, t); t += 2.0; W(t, "shot|m_edit_label_done"); t += 1.0;
    Open(11, t); t += 1.5;
    Phase(t, "edit_command"); Hook(11, 6, t); t += 2.0; W(t, "shot|m_edit_command"); t += 1.0;
    Phase(t, "edit_command_cancel"); Click(801.0, 456.0, t); t += 2.0; W(t, "shot|m_edit_command_cancel"); t += 1.0;
    Phase(t, "macro_use2"); Click(SX(11), SY, t); t += 2.5;
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

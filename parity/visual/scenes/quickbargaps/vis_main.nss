#include "visual_inc"
// quickbargaps (docs/QUICKBAR_RADIAL.md, "Not matched yet"): Examine on a spell, feat and skill slot, the More Emotes
// ring, Assign Other Actions with Animal Empathy, Open Lock and Pick Pocket ranks, and what Custom Text Macro opens.
// Run with --real-mouse --native-framebuffer --silent. Every step is on the harness's wall clock (VISUAL|wall), so
// windows that pause single player don't stop the scene.

void Log(string s) { WriteTimestampedLogEntry(s); }
string P(float x, float y) { return FloatToString(x / 1280.0, 0, 4) + "," + FloatToString(y / 720.0, 0, 4); }
void W(float t, string s) { Log("VISUAL|wall|" + FloatToString(t, 0, 2) + "|" + s); }

const float SY = 703.0;
const float CY = 565.0;
float SX(int f) { return 1010.0 - 67.0 * IntToFloat(12 - f); }   // F<f>'s centre
float HX(int f, int i) { return SX(f) + 100.0 * sin(45.0 * IntToFloat(i)); }
float HY(int i) { return CY - 100.0 * cos(45.0 * IntToFloat(i)); }
string Dir(int i)
{
    switch (i) { case 0: return "n"; case 1: return "ne"; case 2: return "e"; case 3: return "se"; case 4: return "s";
        case 5: return "sw"; case 6: return "w"; } return "nw";
}
void Open(int f, float t) { W(t, "mouse|" + P(SX(f), SY) + "|down|right|real"); W(t + 0.3, "mouse|" + P(SX(f), SY) + "|up|right|real"); }
void Click(float x, float y, float t) { W(t, "mouse|" + P(x, y) + "|down"); W(t + 0.15, "mouse|" + P(x, y) + "|up"); }
void Hook(int f, int i, float t) { Click(HX(f, i), HY(i), t); }
void Point(int f, int i, float t) { W(t, "pointer|" + P(HX(f, i), HY(i))); }
void Shot(string s, float t) { W(t, "shot|" + s); }
void Esc(float t) { W(t, "key|27|tap"); }
// The description window's OK button (Esc leaves the chat line focused, which swallows the screenshot key).
void Ok(float t) { Click(760.0, 550.0, t); }
void Close(float t) { W(t, "mouse|0.50,0.25|down"); W(t + 0.15, "mouse|0.50,0.25|up"); }
// The pointer on the ring's centre and each hook, a shot each.
float Hovers(int f, string s, float t)
{
    W(t, "pointer|" + P(SX(f), CY)); Shot(s + "_c", t + 0.7); t += 1.0;
    int i; for (i = 0; i < 8; i++) { Point(f, i, t); Shot(s + "_" + Dir(i), t + 0.7); t += 1.0; }
    return t;
}

// A macro on F<f>: the ring's Custom Text Macro (N), the label, OK, the text, OK; then a left click on the slot uses it.
float Macro(int f, string sLabel, string sText, string s, float t)
{
    Open(f, t); Hook(f, 4, t + 1.0); t += 2.5;     // empty the slot first
    Open(f, t); Hook(f, 0, t + 1.0); Shot(s + "_label", t + 2.5);
    W(t + 3.0, "text|" + sLabel); Shot(s + "_label_typed", t + 4.0); Click(481.0, 456.0, t + 4.5);
    Shot(s + "_text", t + 6.0); W(t + 6.5, "text|" + sText); Shot(s + "_text_typed", t + 8.0); Click(481.0, 456.0, t + 8.5);
    Shot(s + "_assigned", t + 10.0); W(t + 10.5, "pointer|" + P(SX(f), SY)); Shot(s + "_hover", t + 12.0);
    W(t + 12.5, "pointer|" + P(640.0, 300.0)); Click(SX(f), SY, t + 13.0); Shot(s + "_used", t + 14.5);
    return t + 15.5;
}

void B(float t, string s) { W(t, "burst|" + s + "|0.25|4"); }

// Emote <i> of ring <level> (1 the Emote ring, 2 and 3 its More Emotes) assigned to the empty F10 and used by a click
// on the slot, the player shot 0.8, 2.0 and 3.2 s after; then F10 is emptied again. Bursts check each step.
float Emote(int level, int i, string s, float t)
{
    Open(10, t); B(t + 0.8, "em_" + s + "_0ring"); t += 1.0;
    Hook(10, 7, t); t += 1.2;
    if (level > 1) { Hook(10, 7, t); t += 1.2; }
    if (level > 2) { Hook(10, 7, t); t += 1.2; }
    B(t - 0.2, "em_" + s + "_1sub");
    Hook(10, i, t); t += 1.2;
    W(t, "pointer|" + P(640.0, 150.0)); B(t + 0.1, "em_" + s + "_2bar"); Click(SX(10), SY, t + 0.3);
    B(t + 1.1, "em_" + s + "_a"); B(t + 2.3, "em_" + s + "_b"); B(t + 3.5, "em_" + s + "_c");
    t += 4.0;
    Open(10, t); Hook(10, 4, t + 1.0); t += 2.2;
    return t;
}

void Steps()
{
    float t = 0.0;
    Shot("a_bar", t); t += 1.0;
    Open(10, t); Hook(10, 4, t + 1.0); B(t + 2.0, "a_emptied"); t += 2.5;     // F10 emptied
    t = Emote(1, 0, "1n", t); t = Emote(1, 1, "1ne", t); t = Emote(1, 2, "1e", t); t = Emote(1, 3, "1se", t);
    t = Emote(1, 4, "1s", t); t = Emote(1, 5, "1sw", t); t = Emote(1, 6, "1w", t);
    t = Emote(2, 0, "2n", t); t = Emote(2, 1, "2ne", t); t = Emote(2, 2, "2e", t); t = Emote(2, 3, "2se", t);
    t = Emote(2, 4, "2s", t); t = Emote(2, 5, "2sw", t); t = Emote(2, 6, "2w", t);
    t = Emote(3, 0, "3n", t); t = Emote(3, 2, "3e", t); t = Emote(3, 3, "3se", t);
    t = Emote(3, 4, "3s", t); t = Emote(3, 5, "3sw", t); t = Emote(3, 6, "3w", t); t = Emote(3, 7, "3nw", t);
    // An emote left on F10 for the export; a long label and a /tk line on F11 (its use exports the character).
    Open(10, t); Hook(10, 7, t + 1.0); Hook(10, 0, t + 2.2); t += 3.5;
    Shot("b_bar_emote", t); t += 1.0;
    t = Macro(11, "A long macro label", "/tk talk macro", "h", t);
    W(t, "done|1");
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(pc, i, "");
    SetPlotFlag(pc, TRUE);
    Log("VISUAL|note|character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    Log("VISUAL|note|skills|ae=" + IntToString(GetSkillRank(SKILL_ANIMAL_EMPATHY, pc)) + "|ol=" + IntToString(GetSkillRank(SKILL_OPEN_LOCK, pc))
        + "|pp=" + IntToString(GetSkillRank(SKILL_PICK_POCKET, pc)) + "|taunt=" + IntToString(GetSkillRank(SKILL_TAUNT, pc)));
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 50.0));
    DelayCommand(3.0, Steps());
}

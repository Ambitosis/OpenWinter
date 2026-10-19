#include "visual_inc"
// encumberspeed (docs/ENCUMBRANCE.md): the player's speed and the lines she is told at each load. Aluvian
// (Strength 10: encumbrance.2da Normal 860, Heavy 1100 tenths) is loaded to exact weights with installed items
// (tower shields, longswords, daggers, potions; each item's own GetWeight is counted), then runs east and walks
// west; positions 0.5 s and 2.5 s after each order give the speed. The character record and inventory are opened
// at two loads. "ENC <case>" lines in the chat window mark each step. The player is plot here, which (encumberdrive
// found) leaves her unslowed: this scene measures the lines, the weights and the inventory's weight display.

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|enc|" + s); }
void Mark(string s) { SendMessageToPC(GetFirstPC(), "ENC " + s); }

int Normal(object pc) { return StringToInt(Get2DAString("encumbrance", "Normal", GetAbilityScore(pc, ABILITY_STRENGTH))); }
int Heavy(object pc) { return StringToInt(Get2DAString("encumbrance", "Heavy", GetAbilityScore(pc, ABILITY_STRENGTH))); }

void State(string sCase)
{
    object pc = GetFirstPC();
    Note(sCase + "|weight=" + IntToString(GetWeight(pc)) + "|str=" + IntToString(GetAbilityScore(pc, ABILITY_STRENGTH))
        + "|normal=" + IntToString(Normal(pc)) + "|heavy=" + IntToString(Heavy(pc)) + "|rate=" + IntToString(GetMovementRate(pc)));
}

// Adds tagged items until the player carries exactly nTarget tenths.
int Add(object pc, string sRes, int nStep, int nNeed)
{
    int nGuard = 0;
    while (nNeed >= nStep && nGuard < 40)
    {
        object it = CreateItemOnObject(sRes, pc, 1, "ENC_ADD");
        int w = GetWeight(it);
        if (w <= 0) { Note("bad_item|" + sRes); return nNeed; }
        nNeed -= w;
        nGuard++;
    }
    return nNeed;
}

void LoadTo(string sCase, int nTarget)
{
    object pc = GetFirstPC();
    Mark(sCase);
    int nNeed = nTarget - GetWeight(pc);
    nNeed = Add(pc, "nw_ashto001", 450, nNeed);
    nNeed = Add(pc, "nw_wswls001", 40, nNeed);
    nNeed = Add(pc, "nw_wswdg001", 10, nNeed);
    nNeed = Add(pc, "nw_it_mpotion001", 1, nNeed);
    Note(sCase + "|target=" + IntToString(nTarget) + "|left=" + IntToString(nNeed));
    DelayCommand(0.5, State(sCase));
}

void Clear(string sCase)
{
    object pc = GetFirstPC();
    Mark(sCase);
    object it = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(it))
    {
        if (GetTag(it) == "ENC_ADD") DestroyObject(it);
        it = GetNextItemInInventory(pc);
    }
    DelayCommand(0.5, State(sCase));
}

void DropOne(string sCase, string sRes)
{
    object pc = GetFirstPC();
    Mark(sCase);
    object it = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(it))
    {
        if (GetTag(it) == "ENC_ADD" && GetResRef(it) == sRes) { DestroyObject(it); break; }
        it = GetNextItemInInventory(pc);
    }
    DelayCommand(0.5, State(sCase));
}

void At(string sKey) { SetLocalFloat(GetFirstPC(), sKey, GetPosition(GetFirstPC()).x); }
void Speed(string sKey)
{
    object pc = GetFirstPC();
    float v = (GetPosition(pc).x - GetLocalFloat(pc, sKey)) / 2.0;
    if (v < 0.0) v = -v;
    Note(sKey + "|speed=" + FloatToString(v, 0, 3) + "|action=" + IntToString(GetCurrentAction(pc)));
}

// Runs east or walks west from x=30; the speed between 0.5 s and 2.5 s.
void Move(string sKey, int bRun, int bEast)
{
    object pc = GetFirstPC();
    vector p = GetPosition(pc);
    Mark(sKey);
    AssignCommand(pc, ClearAllActions());
    AssignCommand(pc, ActionMoveToLocation(Location(GetArea(pc), Vector(bEast ? 55.0 : 5.0, 30.0, 0.0), 0.0), bRun));
    DelayCommand(0.5, At(sKey));
    DelayCommand(2.5, Speed(sKey));
}

void Home()
{
    object pc = GetFirstPC();
    AssignCommand(pc, ClearAllActions());
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(30.0, 30.0, 0.0), 0.0)));
}

// Run east then walk west, each from x=30, from t.
void Pair(string sCase, float t)
{
    DelayCommand(t - 0.8, Home());
    DelayCommand(t, Move(sCase + ".run", TRUE, TRUE));
    DelayCommand(t + 4.2, Home());
    DelayCommand(t + 5.0, Move(sCase + ".walk", FALSE, FALSE));
    DelayCommand(t + 8.5, Home());
}

void Key(int vk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(vk) + "|tap"); }

void Windows(string sPrefix, float t)
{
    DelayCommand(t, Key(67));            // C: character record
    DelayCommand(t + 1.5, VShot(sPrefix + "_record"));
    DelayCommand(t + 3.0, Key(67));
    DelayCommand(t + 4.0, Key(73));      // I: inventory
    DelayCommand(t + 5.5, VShot(sPrefix + "_inventory"));
    DelayCommand(t + 7.0, Key(73));
}

void Buff()
{
    object pc = GetFirstPC();
    Mark("str_up");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 4), pc);
    DelayCommand(0.5, State("str_up"));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    int i; for (i = 5000; i <= 5012; i++) SetEventScript(pc, i, "");
    SetPlotFlag(pc, TRUE);
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    DelayCommand(1.0, VCamera(pc, 90.0, 14.0, 50.0));
    DelayCommand(2.0, State("base"));
    DelayCommand(3.0, VShot("a_base"));
    Pair("base", 5.0);
    DelayCommand(16.0, LoadTo("w860", 860));
    Pair("w860", 18.0);
    DelayCommand(29.0, LoadTo("w861", 861));
    Pair("w861", 31.0);
    DelayCommand(42.0, VShot("b_encumbered"));
    Windows("c_enc", 43.0);
    DelayCommand(51.0, LoadTo("w1100", 1100));
    Pair("w1100", 53.0);
    DelayCommand(64.0, LoadTo("w1101", 1101));
    Pair("w1101", 66.0);
    DelayCommand(77.0, VShot("d_heavy"));
    Windows("e_heavy", 78.0);
    DelayCommand(86.0, LoadTo("w3000", 3000));
    Pair("w3000", 88.0);
    DelayCommand(99.0, Clear("clear"));
    DelayCommand(101.0, LoadTo("w1101b", 1101));
    DelayCommand(103.0, DropOne("drop_shield", "nw_ashto001"));
    DelayCommand(105.0, Clear("clear2"));
    DelayCommand(107.0, LoadTo("w1000", 1000));
    DelayCommand(109.0, Buff());
    Pair("str14.w1000", 111.0);
    DelayCommand(122.0, VShot("f_end"));
    DelayCommand(124.0, Mark("end"));
    DelayCommand(125.0, VDone());
}

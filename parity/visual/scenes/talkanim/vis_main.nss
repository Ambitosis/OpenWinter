// What each conversation line Animation value plays. For every value a fresh creature (A) stands 3 m in front
// of the player and begins a conversation whose second line has that value (the original flashes a creature
// white for about a second as it begins a conversation; the 1 key brings up the line under test). R, beside
// it, plays at the same moment the ANIMATION_* constant the value was found to mean (nothing where unknown).
// Five shots per value, then Esc ends the conversation and A is removed.
#include "visual_inc"

void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }

object Npc(object oPC, float fX, float fY, float fFacing, string sDlg)
{
    location l = Location(GetArea(oPC), Vector(fX, fY, 0.0), fFacing);
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", l);
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetPlotFlag(o, TRUE);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetLocalString(o, "DLG", sDlg);
    SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_DIALOGUE, "vis_talk_conv");
    return o;
}

void BeginAsSelf(string sDlg, object oPC) { BeginConversation(sDlg, oPC); }

void Begin(object oA, object oPC, string sDlg)
{
    AssignCommand(oA, BeginAsSelf(sDlg, oPC));
    WriteTimestampedLogEntry("VISUAL|note|begin " + sDlg + "|conv " + IntToString(IsInConversation(oA)));
}

void Check(object oA, string sDlg)
{
    WriteTimestampedLogEntry("VISUAL|note|check " + sDlg + "|conv " + IntToString(IsInConversation(oA)) + "|facing " + FloatToString(GetFacing(oA), 0, 1));
}

void Test(object oPC, object oR, string sName, int nRef)
{
    string sDlg = "vis_an_" + sName;
    object oA = Npc(oPC, 20.0, 15.0, 270.0, sDlg);
    DelayCommand(1.0, Begin(oA, oPC, sDlg));
    // The second line carries the value under test: 1 brings it up, R plays the reference at the same moment.
    DelayCommand(2.6, Tap(49));
    if (nRef >= 0) DelayCommand(2.6, AssignCommand(oR, PlayAnimation(nRef, 1.0, 4.0)));
    DelayCommand(2.7, Check(oA, sDlg));
    DelayCommand(2.6 + 0.3, VShot(sName + "_0"));
    DelayCommand(2.6 + 0.8, VShot(sName + "_1"));
    DelayCommand(2.6 + 1.4, VShot(sName + "_2"));
    DelayCommand(2.6 + 2.2, VShot(sName + "_3"));
    DelayCommand(2.6 + 3.2, VShot(sName + "_4"));
    DelayCommand(2.6 + 3.7, Tap(27));
    DelayCommand(6.8, DestroyObject(oA));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(oPC, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(oPC, TRUE);
    SetTime(12, 0, 0, 0);
    object oR = Npc(oPC, 21.6, 15.3, 250.0, "");
    DelayCommand(1.0, VCamera(oPC, 80.0, 4.5, 74.0));
    float t = 3.0;
    DelayCommand(t, Test(oPC, oR, "0", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "28", 108)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "29", 107)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "30", 2)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "31", 102)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "32", 3)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "33", 4)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "34", 104)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "35", 105)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "36", 106)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "37", 13)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "38", 8)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "39", 9)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "40", 10)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "41", 11)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "42", 116)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "43", 103)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "44", 109)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "45", 110)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "46", 111)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "47", 12)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "48", 5)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "49", 14)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "50", 15)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "51", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "52", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "53", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "54", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "55", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "56", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "57", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "58", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "59", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "60", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "70", 113)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "71", 112)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "72", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "73", -1)); t += 7.0;
    DelayCommand(t, Test(oPC, oR, "88", -1)); t += 7.0;
    DelayCommand(t + 1.0, VDone());
}

// Conversations: who turns to face whom, and what the speaker plays while a line is shown.
// A: the player starts it (ActionStartConversation on the player; the creature's OnDialogue begins it), then lines
//    with different Animation/AnimLoop fields, advanced with the 1 key. R, beside A, plays the ANIMATION_* constant
//    each line is thought to mean at the same moment, for comparison. B: a creature starts it with the player.
// D: the player talks to a creature sitting (a looping animation on its queue); E: to one that is not
// commandable; P: to a placeable; F: to a creature already in reach, the player facing away.
// VISUAL|note lines carry facings (GetFacing), positions, current actions and IsInConversation.
#include "visual_inc"

void Tap(int nVk) { WriteTimestampedLogEntry("VISUAL|key|" + IntToString(nVk) + "|tap"); }

string F(float f) { return FloatToString(f, 0, 1); }
string P(object o) { vector v = GetPosition(o); return F(v.x) + "," + F(v.y); }

void Note(string sPhase, object oPC, object oOther)
{
    WriteTimestampedLogEntry("VISUAL|note|" + sPhase + "|pc " + F(GetFacing(oPC)) + " @" + P(oPC) + " act " + IntToString(GetCurrentAction(oPC))
        + "|npc " + F(GetFacing(oOther)) + " @" + P(oOther) + " act " + IntToString(GetCurrentAction(oOther))
        + "|conv " + IntToString(IsInConversation(oPC)) + IntToString(IsInConversation(oOther)));
}

void Notes(string sPhase, object oPC, object oOther, float fStart, float fSeconds, float fStep = 0.25)
{
    float t;
    for (t = 0.0; t <= fSeconds; t += fStep)
        DelayCommand(fStart + t, Note(sPhase + " " + F(t), oPC, oOther));
}

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

void PcTalk(object oPC, object oTarget, string sDlg)
{
    AssignCommand(oPC, ActionStartConversation(oTarget, sDlg));
}

// One line of A's conversation: the key that brings it up, the reference animation on R, and shots.
void Line(string sName, float fAt, object oR, int nRef, float fRefSeconds)
{
    DelayCommand(fAt, Tap(49));
    if (nRef >= 0) DelayCommand(fAt, AssignCommand(oR, PlayAnimation(nRef, 1.0, fRefSeconds)));
    DelayCommand(fAt + 0.4, VShot(sName + "_0"));
    DelayCommand(fAt + 1.0, VShot(sName + "_1"));
    DelayCommand(fAt + 1.6, VShot(sName + "_2"));
    DelayCommand(fAt + 2.4, VShot(sName + "_3"));
    DelayCommand(fAt + 3.4, VShot(sName + "_4"));
}

void SetupF(object oPC)
{
    vector v = GetPosition(oPC);
    object oF = Npc(oPC, v.x + 1.6, v.y, 180.0, "vis_talk_c");
    AssignCommand(oPC, SetFacing(90.0));
    Notes("F", oPC, oF, 1.75, 4.0);
    DelayCommand(1.9, VShot("f_before"));
    DelayCommand(2.0, PcTalk(oPC, oF, ""));
    DelayCommand(3.0, VShot("f_talk0"));
    DelayCommand(5.0, VShot("f_talk"));
    DelayCommand(6.0, Tap(27));
    DelayCommand(7.0, Note("F end", oPC, oF));
    DelayCommand(7.2, VShot("f_after"));
}

void main()
{
    object oPC = GetEnteringObject();
    if (!GetIsPC(oPC)) return;
    SetCameraMode(oPC, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(oPC, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(oPC, TRUE);
    AssignCommand(oPC, SetFacing(0.0));
    object oA = Npc(oPC, 20.0, 18.5, 0.0, "vis_talk");
    object oR = Npc(oPC, 22.5, 18.5, 270.0, "");
    object oB = Npc(oPC, 14.0, 22.0, 270.0, "vis_talk_c");
    object oD = Npc(oPC, 27.0, 22.0, 0.0, "vis_talk_c");
    object oE = Npc(oPC, 27.0, 9.0, 0.0, "vis_talk_c");
    object oP = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", Location(GetArea(oPC), Vector(13.0, 9.0, 0.0), 0.0));
    SetUseableFlag(oP, TRUE);
    AssignCommand(oD, ActionPlayAnimation(ANIMATION_LOOPING_SIT_CROSS, 1.0, 1000.0));
    SetCommandable(FALSE, oE);

    // A: the camera looks north over the player at A (and R, to its right).
    DelayCommand(1.5, VCamera(oPC, 90.0, 7.0, 70.0));
    Notes("A", oPC, oA, 2.75, 2.0);
    Notes("A", oPC, oA, 5.0, 42.0, 1.0);
    DelayCommand(3.0, PcTalk(oPC, oA, ""));
    DelayCommand(3.0, VShot("a0_00"));
    DelayCommand(3.5, VShot("a0_01"));
    DelayCommand(4.0, VShot("a0_02"));
    DelayCommand(4.5, VShot("a0_03"));
    DelayCommand(5.0, VShot("a0_04"));
    DelayCommand(5.5, VShot("a0_05"));
    DelayCommand(6.0, VShot("a0_06"));
    DelayCommand(7.0, VShot("a0_07"));
    DelayCommand(8.0, VShot("a0_08"));
    Line("a1_37", 9.0, oR, ANIMATION_LOOPING_TALK_NORMAL, 4.0);
    Line("a2_38", 13.5, oR, ANIMATION_LOOPING_TALK_PLEADING, 4.0);
    Line("a3_39", 18.0, oR, ANIMATION_LOOPING_TALK_FORCEFUL, 4.0);
    Line("a4_40", 22.5, oR, ANIMATION_LOOPING_TALK_LAUGHING, 4.0);
    Line("a5_39once", 27.0, oR, -1, 0.0);
    Line("a6_35", 31.5, oR, ANIMATION_FIREFORGET_BOW, 0.0);
    Line("a7_29", 36.0, oR, ANIMATION_FIREFORGET_GREETING, 0.0);
    Line("a8_28", 40.5, oR, ANIMATION_FIREFORGET_TAUNT, 0.0);
    DelayCommand(45.0, Tap(49));           // Goodbye: the conversation ends
    DelayCommand(45.5, VShot("a9_end0"));
    DelayCommand(46.8, VShot("a9_end1"));

    // From here a steep camera close above the player, to see who faces whom.
    DelayCommand(47.5, VCamera(oPC, 90.0, 7.0, 30.0));

    // B: a creature starts the conversation with the player.
    DelayCommand(48.5, VShot("b_before"));
    Notes("B", oPC, oB, 48.75, 5.0);
    DelayCommand(49.0, AssignCommand(oB, ActionStartConversation(oPC, "vis_talk_c", FALSE, FALSE)));
    DelayCommand(53.0, VShot("b_talk"));
    DelayCommand(54.0, Tap(27));
    DelayCommand(55.0, Note("B end", oPC, oB));
    DelayCommand(55.2, VShot("b_after"));

    // D: a creature sitting on the floor.
    Notes("D", oPC, oD, 55.75, 5.0);
    DelayCommand(55.7, VShot("d_before"));
    DelayCommand(56.0, PcTalk(oPC, oD, ""));
    DelayCommand(61.0, VShot("d_talk"));
    DelayCommand(62.0, Tap(27));
    DelayCommand(63.0, Note("D end", oPC, oD));
    DelayCommand(63.2, VShot("d_after"));

    // E: a creature that is not commandable.
    Notes("E", oPC, oE, 63.75, 5.0);
    DelayCommand(63.7, VShot("e_before"));
    DelayCommand(64.0, PcTalk(oPC, oE, ""));
    DelayCommand(69.0, VShot("e_talk"));
    DelayCommand(70.0, Tap(27));
    DelayCommand(71.0, Note("E end", oPC, oE));
    DelayCommand(71.2, VShot("e_after"));

    // P: a placeable.
    Notes("P", oPC, oP, 71.75, 5.0);
    DelayCommand(72.0, PcTalk(oPC, oP, "vis_talk_c"));
    DelayCommand(77.0, VShot("p_talk"));
    DelayCommand(78.0, Tap(27));
    DelayCommand(79.0, Note("P end", oPC, oP));

    // F: a creature already in reach, the player facing away from it.
    DelayCommand(80.0, SetupF(oPC));
    DelayCommand(89.0, VDone());
}

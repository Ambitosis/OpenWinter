// Same-instant ordering:
//  ties     DelayCommands due at the same time, on one object and on four objects; zero-delay commands
//  talk     a creature's SpeakString heard by listeners created in a shuffled order along X (SE_A): expected:
//           OnConversation runs west to east, after the speaker's script, only for listeners that hear the speaker
//  shout    a creature's shout (SE_B) to listeners 15, 18, 30 and 48 m away (hearing range 20 m, sight 35 m)
//  stalk / sshout  the same with TALKVOLUME_SILENT_TALK and TALKVOLUME_SILENT_SHOUT
//  ptalk / pshout  a placeable talking and shouting to listeners 8, 16, 25 and 40 m away (SE_C)
// No players; every creature runs at AI_LEVEL_NORMAL.
#include "parity_inc"
#include "se_inc"

void M(string s) { SeqAdd(s); }

object L(string sArea, float x, float y, string sTag)
{
    object c = CreateObject(OBJECT_TYPE_CREATURE, "se_c", Location(GetObjectByTag(sArea), Vector(x, y, 0.0), 0.0), FALSE, sTag);
    SetAILevel(c, AI_LEVEL_NORMAL);
    SetListening(c, TRUE);
    SetListenPattern(c, "**", 7);
    return c;
}

void Phase(string s) { SetLocalString(GetModule(), "PH", s); }

void Ties()
{
    Phase("ties");
    DelayCommand(0.5, M("m1"));
    DelayCommand(0.5, M("m2"));
    DelayCommand(0.5, M("m3"));
    int i;
    for (i = 1; i <= 4; i++) AssignCommand(GetObjectByTag("SE_P" + IntToString(i)), DelayCommand(0.5, M("p" + IntToString(i))));
    // Zero-delay: DelayCommand(0.0) and AssignCommand interleaved.
    DelayCommand(0.0, M("z1"));
    AssignCommand(GetObjectByTag("SE_P1"), M("a1"));
    DelayCommand(0.0, M("z2"));
    AssignCommand(GetObjectByTag("SE_P2"), M("a2"));
    M("now");
}

void Say(int nVolume, string sPhase) { Phase(sPhase); SpeakString("hello", nVolume); M("after"); }

void Report(string s) { PLog(s, GetLocalString(GetModule(), "SEQ_" + s)); }

void main()
{
    int i;
    object a = GetObjectByTag("SE_A");
    for (i = 1; i <= 4; i++) CreateObject(OBJECT_TYPE_PLACEABLE, "se_p", Location(a, Vector(5.0 + IntToFloat(i) * 2.0, 70.0, 0.0), 0.0), FALSE, "SE_P" + IntToString(i));
    // Talk: speaker at (40, 40); listeners in a shuffled order along X, one 22 m off (beyond talk range), one deaf, one blind.
    object s = CreateObject(OBJECT_TYPE_CREATURE, "se_c", Location(a, Vector(40.0, 40.0, 0.0), 0.0), FALSE, "S_TALK");
    SetAILevel(s, AI_LEVEL_NORMAL);
    L("SE_A", 50.0, 44.0, "T50"); L("SE_A", 34.0, 44.0, "T34"); L("SE_A", 46.0, 44.0, "T46"); L("SE_A", 38.0, 44.0, "T38");
    L("SE_A", 42.0, 44.0, "T42"); L("SE_A", 41.0, 57.0, "T41FAR"); L("SE_A", 62.0, 40.0, "T62OUT");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectDeaf(), L("SE_A", 44.0, 36.0, "T44DEAF"));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBlindness(), L("SE_A", 36.0, 36.0, "T36BLIND"));
    // Shout: speaker at (10, 40); listeners 48, 15, 30 and 18 m east.
    object b = GetObjectByTag("SE_B");
    object sb = CreateObject(OBJECT_TYPE_CREATURE, "se_c", Location(b, Vector(10.0, 40.0, 0.0), 0.0), FALSE, "S_SHOUT");
    SetAILevel(sb, AI_LEVEL_NORMAL);
    L("SE_B", 58.0, 40.0, "H48"); L("SE_B", 25.0, 40.0, "H15"); L("SE_B", 40.0, 40.0, "H30"); L("SE_B", 28.0, 42.0, "H18");
    // Placeable: at (10, 40); listeners 40, 8, 25 and 16 m east.
    object c = GetObjectByTag("SE_C");
    CreateObject(OBJECT_TYPE_PLACEABLE, "se_p", Location(c, Vector(10.0, 40.0, 0.0), 0.0), FALSE, "SE_PSPEAK");
    L("SE_C", 50.0, 40.0, "P40"); L("SE_C", 18.0, 40.0, "P8"); L("SE_C", 35.0, 40.0, "P25"); L("SE_C", 26.0, 40.0, "P16");

    DelayCommand(2.0, Ties());
    DelayCommand(14.0, AssignCommand(s, Say(TALKVOLUME_TALK, "talk")));
    DelayCommand(16.0, AssignCommand(sb, Say(TALKVOLUME_SHOUT, "shout")));
    DelayCommand(18.0, AssignCommand(GetObjectByTag("SE_PSPEAK"), Say(TALKVOLUME_TALK, "ptalk")));
    DelayCommand(20.0, AssignCommand(GetObjectByTag("SE_PSPEAK"), Say(TALKVOLUME_SHOUT, "pshout")));
    DelayCommand(22.0, AssignCommand(s, Say(TALKVOLUME_SILENT_TALK, "stalk")));
    DelayCommand(24.0, AssignCommand(sb, Say(TALKVOLUME_SILENT_SHOUT, "sshout")));
    DelayCommand(27.0, Report("stalk"));
    DelayCommand(27.0, Report("sshout"));
    DelayCommand(27.0, Report("ties"));
    DelayCommand(27.0, Report("talk"));
    DelayCommand(27.0, Report("shout"));
    DelayCommand(27.0, Report("ptalk"));
    DelayCommand(27.0, Report("pshout"));
    DelayCommand(27.5, PDone());
}

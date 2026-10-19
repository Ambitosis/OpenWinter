#include "visual_inc"
// mp_join: the server's lines when a player joins and moves between areas (docs/MULTIPLAYER_HARNESS.md): the join
// line, then "You are now in a ... PVP area." on entering areas whose PlayerVsPlayer is none (0), full (2),
// party (1) and the server's default (3), and back. Each move is announced in the chat window ("MPJ <case>").

void Mark(string sCase) { SendMessageToPC(GetFirstPC(), "MPJ " + sCase); }

void Go(string sTag)
{
    object pc = GetFirstPC();
    Mark("to " + sTag);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetObjectByTag(sTag), Vector(10.0, 10.0, 0.0), 0.0)));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    if (GetLocalInt(GetModule(), "mpj")) return;
    SetLocalInt(GetModule(), "mpj", 1);
    DelayCommand(4.0, Go("MP_NOPVP"));
    DelayCommand(9.0, Go("MP_FULLPVP"));
    DelayCommand(14.0, Go("MP_PARTYPVP"));
    DelayCommand(19.0, Go("MP_DEFAULT"));
    DelayCommand(24.0, Go("VISUAL_AREA"));
    DelayCommand(29.0, Mark("end"));
    DelayCommand(30.0, VShot("a_end"));
    DelayCommand(32.0, VDone());
}

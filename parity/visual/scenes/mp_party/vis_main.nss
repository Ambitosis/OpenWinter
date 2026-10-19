#include "mp_inc"
// mp_party: party management between two players (docs/PARTY.md; tools/multiplayer/mp.py). The players' own clients
// press the player list's buttons and the invitation window's (MPDo "ui|<action>|<object id>", OpenWinter only: the original's harness
// has no such input, and two original clients can't share a server yet). Each phase ends in a checkpoint where the
// server and both clients record the party roster, the party bar, the player list, the invitation window and the chat.
// MPA is a level 15 fighter (fighter.bic), MPB a level 1 wizard (Aluvian Darkstar).
//   a_start     both alone (One Party Only off)
//   b_invited   MPA invites MPB from her player list: MPB's invitation window
//   c_joined    MPB accepts from the window: "has joined the party", one party on both party bars
//   d_killed    MPA kills a goblin beside MPB: the party's experience split by level
//   e_leader    MPA passes the leadership to MPB
//   f_refused   MPA, no longer the leader, tries to remove MPB ("Only the party leader may perform this action.")
//   g_kicked    MPB removes MPA from the party
//   h_rejected  MPA invites MPB again and MPB rejects it
//   i_scripted  AddToParty(MPA, MPB) from a script
//   j_left      MPA leaves the party from her list
//   k_dislike   MPB dislikes MPA; l_list MPA's player list open

object A() { return MPPlayer("MPA"); }
object B() { return MPPlayer("MPB"); }

void Note(string sCase)
{
    object a = A(), b = B();
    MPNote(sCase + "|leaderA=" + GetPCPlayerName(GetFactionLeader(a)) + "|leaderB=" + GetPCPlayerName(GetFactionLeader(b))
        + "|same=" + IntToString(GetFactionEqual(a, b)) + "|xpA=" + IntToString(GetXP(a)) + "|xpB=" + IntToString(GetXP(b)));
    string sMembers = "";
    object m = GetFirstFactionMember(a, TRUE);
    while (GetIsObjectValid(m)) { sMembers += GetPCPlayerName(m) + ","; m = GetNextFactionMember(a, TRUE); }
    MPNote(sCase + "|membersA=" + sMembers);
}

void Shot(string sName) { Note(sName); MPShot(sName); }

void Done() { Shot("z_end"); DelayCommand(2.0, MPDone()); }

void ListClose() { MPDo("MPA", "ui|list"); DelayCommand(2.0, Done()); }

void List()
{
    MPDo("MPA", "ui|list");
    DelayCommand(2.0, Shot("l_list"));
    DelayCommand(3.0, ListClose());
}

void Dislike()
{
    MPDo("MPB", "ui|dislike|" + ObjectToString(A()));
    DelayCommand(2.0, Shot("k_dislike"));
    DelayCommand(3.0, List());
}

void LeaveParty()
{
    MPDo("MPA", "ui|leave");
    DelayCommand(2.0, Shot("j_left"));
    DelayCommand(3.0, Dislike());
}

void Scripted()
{
    AddToParty(A(), B());
    DelayCommand(2.0, Shot("i_scripted"));
    DelayCommand(3.0, LeaveParty());
}

void Reject()
{
    MPDo("MPB", "ui|reject");
    DelayCommand(2.0, Shot("h_rejected"));
    DelayCommand(3.0, Scripted());
}

void InviteAgain()
{
    MPDo("MPA", "ui|invite|" + ObjectToString(B()));
    DelayCommand(2.0, Reject());
}

void Kick()
{
    MPDo("MPB", "ui|kick|" + ObjectToString(A()));
    DelayCommand(2.0, Shot("g_kicked"));
    DelayCommand(3.0, InviteAgain());
}

void Refused()
{
    MPDo("MPA", "ui|kick|" + ObjectToString(B()));
    DelayCommand(2.0, Shot("f_refused"));
    DelayCommand(3.0, Kick());
}

void Leader()
{
    MPDo("MPA", "ui|leader|" + ObjectToString(B()));
    DelayCommand(2.0, Shot("e_leader"));
    DelayCommand(3.0, Refused());
}

void Kill()
{
    object a = A();
    vector p = GetPosition(a);
    object g = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(GetArea(a), Vector(p.x + 2.0, p.y, p.z), 180.0), FALSE, "MP_GOBLIN");
    MPNote("goblin|cr=" + FloatToString(GetChallengeRating(g), 0, 2));
    DelayCommand(0.5, AssignCommand(a, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), g)));
    DelayCommand(3.0, Shot("d_killed"));
    DelayCommand(4.0, Leader());
}

void Accept()
{
    MPDo("MPB", "ui|accept");
    DelayCommand(2.0, Shot("c_joined"));
    DelayCommand(3.0, Kill());
}

void Invite()
{
    MPDo("MPA", "ui|invite|" + ObjectToString(B()));
    DelayCommand(2.0, Shot("b_invited"));
    DelayCommand(3.0, Accept());
}

void Start()
{
    object a = A(), b = B();
    SetImmortal(a, TRUE);
    SetImmortal(b, TRUE);
    MPJump(a, 16.0, 15.0, 0.0);
    MPJump(b, 21.0, 15.0, 180.0);
    MPCameras(90.0, 14.0, 50.0);
    DelayCommand(3.0, Shot("a_start"));
    DelayCommand(4.0, Invite());
}

void WaitForPlayers()
{
    object a = A(), b = B();
    if (GetIsObjectValid(a) && GetIsObjectValid(b) && GetArea(a) == OBJECT_SELF && GetArea(b) == OBJECT_SELF)
    {
        DelayCommand(5.0, Start());
        return;
    }
    DelayCommand(1.0, WaitForPlayers());
}

void main()
{
    if (GetLocalInt(GetModule(), "mp_started")) return;
    SetLocalInt(GetModule(), "mp_started", TRUE);
    WaitForPlayers();
}

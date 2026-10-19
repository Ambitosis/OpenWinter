#include "mp_inc"
// mp_barter: two players barter (docs/BARTER.md), each step driven through the players' own clients (OpenWinter
// only: the original can't run two clients on one licence). Checkpoints record both windows and the chat.
//   refuse   MPA asks, MPB declines: MPA reads "<FirstName> refused to barter with you."
//   offers   MPA asks again, MPB accepts; MPA offers a longsword and 25 gold, MPB a stack of potions
//   locks    both press Offer; MPA's later change of gold is refused (locked)
//   trade    both press Accept: the sword and gold go to MPB, the potions to MPA
//   cancel   a second barter, cancelled by MPB: "Barter cancelled" for both

object A() { return MPPlayer("MPA"); }
object B() { return MPPlayer("MPB"); }

void Note(string phase)
{
    object a = A(), b = B();
    MPNote(phase + "|Agold=" + IntToString(GetGold(a)) + "|Bgold=" + IntToString(GetGold(b))
        + "|sword=" + GetPCPlayerName(GetItemPossessor(GetObjectByTag("MP_SWORD")))
        + "|potion=" + GetPCPlayerName(GetItemPossessor(GetObjectByTag("MP_POTION"))));
}

void Done() { MPShot("z_end"); DelayCommand(2.0, MPDone()); }

void Cancel()
{
    MPDo("MPA", "request|barter-request|{\"target\":\"@MP_B\"}");
    DelayCommand(1.5, MPDo("MPB", "request|barter-answer|{\"from\":\"@MP_A\",\"accept\":true}"));
    DelayCommand(3.0, MPShot("e1_second_open"));
    DelayCommand(4.0, MPDo("MPB", "request|barter-cancel|{}"));
    DelayCommand(6.0, MPShot("e2_cancelled"));
    DelayCommand(6.0, Note("cancelled"));
    DelayCommand(8.0, Done());
}

void Trade()
{
    MPDo("MPA", "request|barter-accept|{}");
    DelayCommand(1.5, MPShot("d1_a_accepted"));
    DelayCommand(2.5, MPDo("MPB", "request|barter-accept|{}"));
    DelayCommand(4.5, MPShot("d2_traded"));
    DelayCommand(4.5, Note("traded"));
    DelayCommand(6.0, Cancel());
}

void Locks()
{
    MPDo("MPA", "request|barter-lock|{}");
    DelayCommand(1.0, MPDo("MPA", "request|barter-gold|{\"gold\":60}"));
    DelayCommand(2.0, MPDo("MPB", "request|barter-lock|{}"));
    DelayCommand(3.5, MPShot("c_locked"));
    DelayCommand(4.5, Trade());
}

void Offers()
{
    MPDo("MPA", "request|barter-request|{\"target\":\"@MP_B\"}");
    DelayCommand(1.5, MPShot("b1_asked"));
    DelayCommand(2.5, MPDo("MPB", "request|barter-answer|{\"from\":\"@MP_A\",\"accept\":true}"));
    DelayCommand(4.0, MPDo("MPA", "request|barter-offer|{\"item\":\"@MP_SWORD\"}"));
    DelayCommand(4.5, MPDo("MPA", "request|barter-gold|{\"gold\":25}"));
    DelayCommand(5.0, MPDo("MPB", "request|barter-offer|{\"item\":\"@MP_POTION\"}"));
    DelayCommand(6.5, MPShot("b2_offers"));
    DelayCommand(7.5, Locks());
}

void Refuse()
{
    MPDo("MPA", "request|barter-request|{\"target\":\"@MP_B\"}");
    DelayCommand(1.5, MPDo("MPB", "request|barter-answer|{\"from\":\"@MP_A\",\"accept\":false}"));
    DelayCommand(3.0, MPShot("a2_refused"));
    DelayCommand(4.0, Offers());
}

void Start()
{
    object a = A(), b = B();
    MPNote("start|pcs=" + IntToString(MPCountPCs()) + "|A=" + GetName(a) + "|B=" + GetName(b));
    SetTag(a, "MP_A");
    SetTag(b, "MP_B");
    MPJump(a, 18.0, 15.0, 0.0);
    MPJump(b, 21.0, 15.0, 180.0);
    MPCameras(90.0, 14.0, 50.0);
    TakeGoldFromCreature(GetGold(a), a, TRUE);
    TakeGoldFromCreature(GetGold(b), b, TRUE);
    GiveGoldToCreature(a, 100);
    GiveGoldToCreature(b, 50);
    CreateItemOnObject("nw_wswls001", a, 1, "MP_SWORD");
    CreateItemOnObject("nw_it_mpotion001", b, 3, "MP_POTION");
    DelayCommand(2.0, Note("start"));
    DelayCommand(3.0, MPShot("a1_start"));
    DelayCommand(4.0, Refuse());
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

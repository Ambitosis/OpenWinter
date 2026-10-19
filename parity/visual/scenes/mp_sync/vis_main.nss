#include "mp_inc"
// mp_sync: two players on one server (tools/multiplayer/mp.py, docs/MULTIPLAYER_HARNESS.md). The server's scripts
// drive both players, so the same scene runs on the original and on OpenWinter; each phase ends in a checkpoint
// (MPShot) where OpenWinter's server and both clients record what they hold and show, and the original's harness
// takes both clients' screenshots and chat windows. MPA is a level 15 fighter (fighter.bic), MPB a level 1 wizard (Aluvian Darkstar).
//   walk / run    MPA walks 8 m east and runs back, seen by MPB (checkpoints mid-way and at the end)
//   click         MPA's own client clicks the ground (OpenWinter only; the original's MPA is moved by script)
//   door          MPB opens the door, then closes it
//   item          MPA drops a longsword, MPB picks it up
//   chat (solo)   talk, shout, a server message to MPA only, floating text to MPA's faction and to MPA only, party chat
//   party         MPB joins MPA's party (AddToParty)
//   combat        a goblin attacks MPA and MPA kills it: animations, HP bars, chat lines and the party's XP split
//   spell         MPB casts Barkskin on herself (cheat cast): casting animation and the lasting visual
//   chat (party)  the same lines again now that they share a party
//   transition    MPB jumps to another area and back: MPA loses her, then sees her again with her Barkskin;
//                 meanwhile MPA opens the door, drops a dagger and starts to glow, and a goblin appears hurt
//   keys          MPA's own client walks her forward with the keyboard (OpenWinter only)
//   missile       MPB's Magic Missile at that goblin
//   emote         MPA salutes
//   leave         MPB's client quits (OpenWinter only)

object A() { return MPPlayer("MPA"); }
object B() { return MPPlayer("MPB"); }

void Done() { MPShot("z_end"); DelayCommand(2.0, MPDone()); }

// MPB's client quits (OpenWinter only): MPA must lose her. The original's MPB stays.
void Leave()
{
    MPDo("MPB", "quit");
    DelayCommand(4.0, MPShot("y_left"));
    DelayCommand(5.0, Done());
}

// MPA salutes (a one-shot animation), seen by MPB.
void Emote()
{
    AssignCommand(A(), ActionPlayAnimation(ANIMATION_FIREFORGET_SALUTE));
    DelayCommand(0.6, MPShot("m_emote"));
    DelayCommand(4.0, Leave());
}

// MPB's Magic Missile at the second goblin: the projectiles and the damage, seen by MPA.
void Missile()
{
    object b = B();
    AssignCommand(b, ClearAllActions(TRUE));
    AssignCommand(b, ActionCastSpellAtObject(SPELL_MAGIC_MISSILE, GetObjectByTag("MP_GOBLIN2"), METAMAGIC_ANY, TRUE));
    DelayCommand(1.8, MPShot("l1_missile"));
    DelayCommand(5.0, MPShot("l2_missile_hit"));
    DelayCommand(6.0, Emote());
}

// MPA's own client walks her forward with the keyboard for 1.5 s (OpenWinter only; the original's harness has no
// such input and she stays put).
void Keys()
{
    MPDo("MPA", "key|move_forward|down");
    DelayCommand(1.0, MPShot("k1_keys_walking"));
    DelayCommand(1.5, MPDo("MPA", "key|move_forward|up"));
    DelayCommand(4.0, MPShot("k2_keys_stopped"));
    DelayCommand(5.0, Missile());
}

void TransitionBack()
{
    AssignCommand(B(), JumpToLocation(Location(GetArea(A()), Vector(24.0, 15.0, 0.0), 180.0)));
    DelayCommand(4.0, MPShot("t2_back"));
    DelayCommand(6.0, Keys());
}

// While MPB is away the area changes: MPA opens the door and drops a dagger, she starts to glow, a second goblin
// appears and is hurt. MPB must see all of it as it is when she comes back.
void Changes()
{
    object a = A();
    AssignCommand(a, ActionOpenDoor(GetObjectByTag("MP_DOOR")));
    object dagger = CreateItemOnObject("nw_wswdg001", a, 1, "MP_DAGGER");
    AssignCommand(a, ActionPutDownItem(dagger));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectVisualEffect(VFX_DUR_GLOW_RED), a);
    vector p = GetPosition(a);
    object g = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(GetArea(a), Vector(p.x - 3.0, p.y - 3.0, p.z), 45.0), FALSE, "MP_GOBLIN2");
    ChangeToStandardFaction(g, STANDARD_FACTION_COMMONER);
    SetImmortal(g, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(20), g);
    DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(12), g));
}

void Transition()
{
    object far = GetObjectByTag("MP_FAR");
    AssignCommand(B(), ClearAllActions(TRUE));
    AssignCommand(B(), JumpToLocation(Location(far, Vector(10.0, 10.0, 0.0), 0.0)));
    DelayCommand(1.0, Changes());
    DelayCommand(6.0, MPShot("t1_away"));
    DelayCommand(8.0, TransitionBack());
}

// Talk, shout, a server message and floating text from MPA, logged as sCase in both chat windows first.
void ChatLines(string sCase)
{
    object a = A();
    SendMessageToPC(a, "MP case " + sCase);
    SendMessageToPC(B(), "MP case " + sCase);
    AssignCommand(a, SpeakString("MP talk from A", TALKVOLUME_TALK));
    AssignCommand(a, SpeakString("MP shout from A", TALKVOLUME_SHOUT));
    SendMessageToPC(a, "MP server message to A");
    FloatingTextStringOnCreature("MP float to A's faction", a, TRUE);
    FloatingTextStringOnCreature("MP float to A only", a, FALSE);
    MPDo("MPA", "chat|party|MP party chat from A's client");
}

void Chat()
{
    ChatLines("party");
    DelayCommand(3.0, MPShot("h_chat"));
    DelayCommand(5.0, Transition());
}

void Spell()
{
    object b = B();
    AssignCommand(b, ClearAllActions(TRUE));
    AssignCommand(b, ActionCastSpellAtObject(SPELL_BARKSKIN, b, METAMAGIC_ANY, TRUE));
    DelayCommand(1.0, MPShot("g1_casting"));
    DelayCommand(5.0, MPShot("g2_cast"));
    DelayCommand(7.0, Chat());
}

void CombatWait(int nTries)
{
    object g = GetObjectByTag("MP_GOBLIN");
    if (nTries == 3) MPShot("f2_fighting");
    if ((!GetIsObjectValid(g) || GetIsDead(g)) || nTries > 40)
    {
        MPNote("combat|dead=" + IntToString(GetIsDead(g)) + "|valid=" + IntToString(GetIsObjectValid(g)) + "|tries=" + IntToString(nTries));
        DelayCommand(2.0, MPShot("f3_killed"));
        DelayCommand(4.0, Spell());
        return;
    }
    DelayCommand(1.0, CombatWait(nTries + 1));
}

void Combat()
{
    object a = A();
    vector p = GetPosition(a);
    object g = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(GetArea(a), Vector(p.x + 2.5, p.y, p.z), 180.0), FALSE, "MP_GOBLIN");
    SetAILevel(g, AI_LEVEL_NORMAL);
    // MPA never dies, and the goblin goes down within a few rounds.
    SetImmortal(a, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACDecrease(10), g);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTemporaryHitpoints(70), g);
    MPNote("combat|goblin_hp=" + IntToString(GetCurrentHitPoints(g)) + "|xpA=" + IntToString(GetXP(a)) + "|xpB=" + IntToString(GetXP(B())));
    DelayCommand(0.5, MPShot("f1_goblin"));
    DelayCommand(1.0, AssignCommand(a, ActionAttack(g)));
    DelayCommand(2.0, CombatWait(0));
}

void Party()
{
    MPShot("e0_chat_solo");
    AddToParty(B(), A());
    DelayCommand(2.0, MPShot("e_party"));
    DelayCommand(3.0, Combat());
}

void ItemTake()
{
    object item = GetObjectByTag("MP_SWORD");
    AssignCommand(B(), ActionPickUpItem(item));
    DelayCommand(4.0, MPNote("item|possessor=" + GetPCPlayerName(GetItemPossessor(GetObjectByTag("MP_SWORD")))));
    DelayCommand(4.0, MPShot("d2_taken"));
    DelayCommand(5.0, ChatLines("solo"));
    DelayCommand(8.0, Party());
}

void Item()
{
    object a = A();
    object item = CreateItemOnObject("nw_wswls001", a, 1, "MP_SWORD");
    DelayCommand(0.5, AssignCommand(a, ActionPutDownItem(item)));
    DelayCommand(3.5, MPShot("d1_dropped"));
    DelayCommand(4.0, ItemTake());
}

void DoorClose()
{
    AssignCommand(B(), ActionCloseDoor(GetObjectByTag("MP_DOOR")));
    DelayCommand(3.0, MPShot("c2_door_closed"));
    DelayCommand(4.0, Item());
}

void Door()
{
    AssignCommand(B(), ActionOpenDoor(GetObjectByTag("MP_DOOR")));
    DelayCommand(4.0, MPShot("c1_door_open"));
    DelayCommand(5.0, DoorClose());
}

// The click: MPA's client asks to run to (17, 18). The original's harness has no such input, so the script moves her there.
void ClickCheck()
{
    object a = A();
    vector p = GetPosition(a);
    MPNote("click|arrived=" + IntToString(fabs(p.x - 17.0) < 1.0 && fabs(p.y - 18.0) < 1.0));
    MPShot("b5_clicked");
    if (fabs(p.x - 17.0) >= 1.0 || fabs(p.y - 18.0) >= 1.0) AssignCommand(a, ActionMoveToLocation(Location(GetArea(a), Vector(17.0, 18.0, 0.0), 0.0), TRUE));
    DelayCommand(3.0, Door());
}

void Click()
{
    MPDo("MPA", "moveto|17,18|run");
    DelayCommand(4.0, ClickCheck());
}

void Run()
{
    object a = A();
    AssignCommand(a, ActionMoveToLocation(Location(GetArea(a), Vector(14.0, 15.0, 0.0), 180.0), TRUE));
    DelayCommand(1.2, MPShot("b3_run_mid"));
    DelayCommand(4.0, MPShot("b4_run_end"));
    DelayCommand(5.0, Click());
}

void Walk()
{
    object a = A();
    AssignCommand(a, ActionMoveToLocation(Location(GetArea(a), Vector(22.0, 15.0, 0.0), 0.0), FALSE));
    DelayCommand(2.0, MPShot("b1_walk_mid"));
    DelayCommand(6.0, MPShot("b2_walk_end"));
    DelayCommand(7.0, Run());
}

void Start()
{
    object a = A(), b = B();
    MPNote("start|pcs=" + IntToString(MPCountPCs()) + "|A=" + GetName(a) + "|B=" + GetName(b));
    // Nobody dies: the goblins can't kill either player.
    SetImmortal(a, TRUE);
    SetImmortal(b, TRUE);
    MPJump(a, 14.0, 15.0, 0.0);
    MPJump(b, 24.0, 18.0, 180.0);
    MPCameras(90.0, 18.0, 50.0);
    DelayCommand(3.0, MPShot("a_start"));
    DelayCommand(4.0, Walk());
}

// Waits until both players are in the area, then gives perception a few seconds before starting.
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

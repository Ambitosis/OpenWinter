// NWNX_Events trap and detection events: entering a trap, examining, flagging, recovering and disarming one, and the
// spot and listen checks against a hider.
#include "pnwnxev_inc"

object R() { return GetObjectByTag("ROG"); }
object G() { return GetObjectByTag("GOB"); }
object H() { return GetObjectByTag("HIDER"); }

void Watch()
{
    string sTrap = "o:TRAP_OBJECT_ID,o:TARGET_OBJECT_ID,x:TARGET_POSITION_X,NEEDS_TO_MOVE,TRAP_FORCE_SET,x:ACTION_RESULT";
    EvWatch("NWNX_ON_TRAP_ENTER", sTrap);
    EvWatch("NWNX_ON_TRAP_DISARM", sTrap);
    EvWatch("NWNX_ON_TRAP_EXAMINE", sTrap);
    EvWatch("NWNX_ON_TRAP_FLAG", sTrap);
    EvWatch("NWNX_ON_TRAP_RECOVER", sTrap);
    EvWatch("NWNX_ON_TRAP_SET", sTrap);
    EvWatch("NWNX_ON_TIMING_BAR_START", "EVENT_ID,DURATION");
    EvWatch("NWNX_ON_TIMING_BAR_STOP", "");
    EvWatch("NWNX_ON_TIMING_BAR_CANCEL", "");
}

void Step(string sName)
{
    object oMod = GetModule();
    EvFlush(GetLocalString(oMod, "STEP"));
    SetLocalString(oMod, "STEP", sName);
}

void Enter()
{
    Step("enter");
    object oTrap = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PCentre(-5.0, 5.0), 2.0, "TRAP1");
    SetTrapDetectable(oTrap, TRUE);
    AssignCommand(G(), ActionMoveToLocation(PCentre(-8.0, 5.0)));
}

void Flag()
{
    Step("flag");
    object oTrap = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PCentre(5.0, -5.0), 2.0, "TRAP2");
    SetTrapDetectedBy(oTrap, R());
    AssignCommand(R(), ActionUseSkill(SKILL_DISABLE_TRAP, oTrap, SUBSKILL_FLAGTRAP));
}

void Examine()
{
    Step("examine");
    AssignCommand(R(), ActionUseSkill(SKILL_DISABLE_TRAP, GetObjectByTag("TRAP2"), SUBSKILL_EXAMINETRAP));
}

void Disarm()
{
    Step("disarm");
    AssignCommand(R(), ActionUseSkill(SKILL_DISABLE_TRAP, GetObjectByTag("TRAP2")));
}

void Recover()
{
    Step("recover");
    object oTrap = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PCentre(5.0, 5.0), 2.0, "TRAP3");
    SetTrapDetectedBy(oTrap, R());
    SetTrapRecoverable(oTrap, TRUE);
    AssignCommand(R(), ActionUseSkill(SKILL_DISABLE_TRAP, oTrap, SUBSKILL_RECOVERTRAP));
}

void Detect()
{
    Step("detect");
    NWNX_Events_UnsubscribeEvent("NWNX_ON_TRAP_ENTER_BEFORE", "pev_log");
    NWNX_Events_UnsubscribeEvent("NWNX_ON_TRAP_ENTER_AFTER", "pev_log");
    // Only around these checks, and only the goblin looking at the hider.
    EvWatch("NWNX_ON_DO_SPOT_DETECTION", "o:TARGET,TARGET_INVISIBLE,x:BEFORE_RESULT");
    EvWatch("NWNX_ON_DO_LISTEN_DETECTION", "o:TARGET,TARGET_INVISIBLE,x:BEFORE_RESULT");
    SetLocalInt(GetModule(), "EVMAX:NWNX_ON_DO_SPOT_DETECTION_BEFORE", 1);
    SetLocalInt(GetModule(), "EVMAX:NWNX_ON_DO_SPOT_DETECTION_AFTER", 1);
    SetLocalInt(GetModule(), "EVMAX:NWNX_ON_DO_LISTEN_DETECTION_BEFORE", 1);
    SetLocalInt(GetModule(), "EVMAX:NWNX_ON_DO_LISTEN_DETECTION_AFTER", 1);
    SetActionMode(H(), ACTION_MODE_STEALTH, TRUE);
    AssignCommand(H(), ActionMoveToLocation(PCentre(-3.0, -6.0)));
}

void Counts()
{
    Step("end");
    object oMod = GetModule();
    PLogI("spot.any", GetLocalInt(oMod, "EVSEEN:NWNX_ON_DO_SPOT_DETECTION_BEFORE") > 0);
    PLogI("listen.any", GetLocalInt(oMod, "EVSEEN:NWNX_ON_DO_LISTEN_DETECTION_BEFORE") > 0);
    PLogI("trap2", GetIsObjectValid(GetObjectByTag("TRAP2")));
    PDone();
}

void main()
{
    object oR = CreateObject(OBJECT_TYPE_CREATURE, "pev_rog", PCentre(5.0, -2.0), FALSE, "ROG");
    object oG = CreateObject(OBJECT_TYPE_CREATURE, "pev_gob", PCentre(-2.0, 5.0), FALSE, "GOB");
    object oH = CreateObject(OBJECT_TYPE_CREATURE, "pev_rog", PCentre(-3.0, -2.0), FALSE, "HIDER");
    object o;
    for (o = GetFirstObjectInArea(GetArea(oR)); GetIsObjectValid(o); o = GetNextObjectInArea(GetArea(oR)))
        if (GetObjectType(o) == OBJECT_TYPE_CREATURE) { SetAILevel(o, AI_LEVEL_NORMAL); SetImmortal(o, TRUE); }
    ChangeToStandardFaction(oR, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(oH, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(oG, STANDARD_FACTION_HOSTILE);
    DelayCommand(1.0, Watch());
    DelayCommand(2.0, Enter());
    DelayCommand(10.0, Flag());
    DelayCommand(20.0, Examine());
    DelayCommand(30.0, Disarm());
    DelayCommand(40.0, Recover());
    DelayCommand(50.0, Detect());
    DelayCommand(60.0, Counts());
}

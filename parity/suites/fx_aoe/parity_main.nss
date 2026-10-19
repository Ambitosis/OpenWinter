// Areas of effect: enter, exit and heartbeat events (and when they happen, in 1 s phases), the
// objects inside, the creator, and an area of effect carried by a creature.
#include "parity_fx"

object Make(string sTag, float fX, float fY)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(fX, fY));
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetTag(o, sTag);
    return o;
}

// Phases are one second each; every one has its own delay so they don't drift.
void Phase(int n)
{
    SetLocalInt(GetModule(), "phase", n);
}

void Name(string sName, location l)
{
    object a = GetNearestObjectToLocation(OBJECT_TYPE_AREA_OF_EFFECT, l);
    SetLocalString(a, "name", sName);
    PLog("aoe." + sName + ".object", "tag=" + GetTag(a) + " creator=" + GetTag(GetAreaOfEffectCreator(a)));
}

void Run()
{
    int i;
    for (i = 0; i <= 20; i++) DelayCommand(IntToFloat(i), Phase(i));
    object oCaster = Make("CASTER", -15.0, -15.0);
    object oIn = Make("INSIDE", 0.0, 0.0);
    object oLeaver = Make("LEAVER", 1.0, 1.0);
    object oComer = Make("COMER", 12.0, 0.0);
    location l = PCentre(0.0, 0.0);
    // The creator: a creature (via AssignCommand) makes it.
    AssignCommand(oCaster, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectAreaOfEffect(AOE_PER_FOGACID, "fxa_enter", "fxa_hb", "fxa_exit"), l, 13.5));
    DelayCommand(0.05, Name("cloud", l));
    DelayCommand(3.0, AssignCommand(oLeaver, ActionMoveToLocation(PCentre(15.0, 15.0))));
    DelayCommand(3.0, AssignCommand(oComer, ActionMoveToLocation(PCentre(0.5, -0.5))));
    // One carried by a creature.
    object oCarrier = Make("CARRIER", -12.0, 12.0);
    object oNear = Make("NEAR", -11.0, 12.0);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectAreaOfEffect(AOE_PER_FOGACID, "fxa_enter", "fxa_hb", "fxa_exit"), oCarrier, 8.5);
    DelayCommand(0.05, Name("carried", GetLocation(oCarrier)));
    DelayCommand(16.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}

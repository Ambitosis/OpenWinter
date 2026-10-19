// Animations: how long ActionPlayAnimation holds the action queue (looping with and without a duration,
// fire-and-forget, speed), what GetCurrentAction reports meanwhile, and ActionSit.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float f = 0.0) { return Location(Area(), Vector(x, y, 0.0), f); }

void Done(string k)
{
    float t = PSince() - GetLocalFloat(OBJECT_SELF, "T0");
    if (!GetLocalInt(OBJECT_SELF, "MIN")) { PLogF(k, t, 0.06); return; }
    // The original now and then stalls its creatures' actions for up to 0.8 s: long animations play
    // three times, seconds apart, and report the quickest.
    object m = GetModule();
    if (GetLocalFloat(m, "MIN_" + k) == 0.0 || t < GetLocalFloat(m, "MIN_" + k)) SetLocalFloat(m, "MIN_" + k, t);
    SetLocalInt(m, "N_" + k, GetLocalInt(m, "N_" + k) + 1);
    if (GetLocalInt(m, "N_" + k) == 3) PLogF(k, GetLocalFloat(m, "MIN_" + k), 0.12);
}

void Act(object o, string k)
{
    PLogI(k + ".action", GetCurrentAction(o));
}

void Anim(string sRes, int nAnim, float fSpeed, float fDur, int i, int bMin = FALSE)
{
    string k = "anim." + GetSubString(sRes, 2, 3) + "." + IntToString(nAnim) + ".x" + FloatToString(fSpeed, 0, 1) + ".d" + FloatToString(fDur, 0, 1);
    float x = 2.0 + IntToFloat(i % 10) * 3.8, y = 2.0 + IntToFloat(i / 10) * 4.0;
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetLocalFloat(o, "T0", PSince());
    SetLocalInt(o, "MIN", bMin);
    AssignCommand(o, ActionPlayAnimation(nAnim, fSpeed, fDur));
    AssignCommand(o, ActionDoCommand(Done(k)));
    if (i < 3) DelayCommand(0.2, Act(o, k));
}

void Long(int n)
{
    int i = 40 + n * 7;
    {
    Anim("p_human", ANIMATION_LOOPING_WORSHIP, 1.0, 0.0, i++, TRUE);
    Anim("p_dog", ANIMATION_LOOPING_WORSHIP, 1.0, 0.0, i++, TRUE);
    Anim("p_human", ANIMATION_LOOPING_PAUSE_DRUNK, 1.0, 0.0, i++, TRUE);
    Anim("p_human", ANIMATION_FIREFORGET_PAUSE_BORED, 1.0, 0.0, i++, TRUE);
    Anim("p_human", ANIMATION_LOOPING_PAUSE, 1.0, 3.0, i++, TRUE);
    Anim("p_human", ANIMATION_LOOPING_PAUSE, 2.0, 3.0, i++, TRUE);
    Anim("p_human", ANIMATION_LOOPING_SIT_CHAIR, 1.0, 4.0, i++, TRUE);
    }
}

void Durations()
{
    int i = 0;
    // Looping, no duration: one play of the animation's length (the same for every model).
    Anim("p_human", ANIMATION_LOOPING_PAUSE, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_LOOPING_LOOK_FAR, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_LOOPING_SIT_CROSS, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_LOOPING_GET_LOW, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_LOOPING_GET_MID, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_LOOPING_PAUSE_TIRED, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_LOOPING_DEAD_BACK, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_LOOPING_CUSTOM5, 1.0, 0.0, i++);
    Anim("p_dog", ANIMATION_LOOPING_TALK_FORCEFUL, 1.0, 0.0, i++);
    // Looping with a duration: that long, whatever the speed; LISTEN ignores it.
    Anim("p_human", ANIMATION_LOOPING_WORSHIP, 1.0, 1.5, i++);
    Anim("p_human", ANIMATION_LOOPING_LISTEN, 1.0, 5.0, i++);
    Anim("p_human", ANIMATION_LOOPING_LISTEN, 2.0, 5.0, i++);
    // Speed divides the length.
    Anim("p_human", ANIMATION_LOOPING_TALK_NORMAL, 2.0, 0.0, i++);
    Anim("p_human", ANIMATION_FIREFORGET_BOW, 4.0, 0.0, i++);
    Anim("p_human", ANIMATION_LOOPING_PAUSE, 0.0, 0.0, i++);
    // Fire and forget: the duration is not used.
    Anim("p_human", ANIMATION_FIREFORGET_GREETING, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_FIREFORGET_GREETING, 1.0, 5.0, i++);
    Anim("p_human", ANIMATION_FIREFORGET_SALUTE, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_FIREFORGET_VICTORY1, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_FIREFORGET_VICTORY2, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_FIREFORGET_VICTORY3, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_FIREFORGET_TAUNT, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_FIREFORGET_DRINK, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_FIREFORGET_DODGE_SIDE, 1.0, 0.0, i++);
    Anim("p_dog", ANIMATION_FIREFORGET_HEAD_TURN_LEFT, 1.0, 0.0, i++);
    Anim("p_dog", ANIMATION_FIREFORGET_SPASM, 1.0, 0.0, i++);
    Anim("p_human", ANIMATION_PLACEABLE_OPEN, 1.0, 0.0, i++);
}

void SitState(object o, object chair, string k)
{
    vector p = GetPosition(o);
    PLogF(k + ".x", p.x, 0.01);
    PLogF(k + ".y", p.y, 0.01);
    PLogF(k + ".facing", GetFacing(o), 0.5);
    PLogI(k + ".action", GetCurrentAction(o));
    PLogI(k + ".sitting", GetSittingCreature(chair) == o);
}

void Sit(string k, float x, float y, string sChair, float fFacing, float fStart)
{
    object chair = CreateObject(OBJECT_TYPE_PLACEABLE, sChair, L(x, y, fFacing), FALSE, k + "_CHAIR");
    object o = CreateObject(OBJECT_TYPE_CREATURE, "p_human", L(x - fStart, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    AssignCommand(o, ActionSit(chair));
    AssignCommand(o, ActionDoCommand(PLog(k + ".after", "ran")));
    DelayCommand(4.0, SitState(o, chair, k + ".seated"));
    // Sitting lasts until the queue is cleared; standing up steps off the seat.
    DelayCommand(5.0, AssignCommand(o, ClearAllActions()));
    DelayCommand(6.0, SitState(o, chair, k + ".up"));
}

void Sits()
{
    Sit("sit.near", 5.0, 30.0, "p_chair", 0.0, 1.0);
    Sit("sit.far", 15.0, 30.0, "p_chair", 90.0, 6.0);
    Sit("sit.unuseable", 25.0, 30.0, "p_crate", 200.0, 1.0);
}

void main()
{
    PStart();
    DelayCommand(1.0, Durations());
    DelayCommand(12.0, Long(0));
    DelayCommand(16.0, Long(1));
    DelayCommand(20.0, Long(2));
    DelayCommand(4.0, Sits());
    DelayCommand(28.0, PDone());
}

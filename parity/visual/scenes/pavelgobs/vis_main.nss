// The two goblins the mystic mage summons beside Pavel after the attack on the academy (Prelude m0q1d,
// m0q0_mystmage_d: m1q0d19goblin at WP_M0Q0_GOB_1/2), killed by the player, with the player in the area:
// how long do their corpses last? Beside them the same goblin without its scripts killed by EffectDeath, and a
// stock bandit (no DecayTime field).
#include "visual_inc"

void Note(string s) { WriteTimestampedLogEntry("VISUAL|note|" + s); }

void Watch(object c, string tag, float t)
{
    if (GetLocalInt(GetModule(), "gone." + tag)) return;
    if (!GetIsObjectValid(c)) { SetLocalInt(GetModule(), "gone." + tag, 1); Note(tag + " gone " + FloatToString(t, 0, 1)); }
}

void Kill(object pc, object c, string tag, int byPC)
{
    Note(tag + " hp " + IntToString(GetCurrentHitPoints(c)) + " dead " + IntToString(GetIsDead(c)));
    if (byPC) AssignCommand(pc, ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(20), c));
    else ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), c);
    float t;
    for (t = 0.5; t <= 50.0; t += 0.5) DelayCommand(t, Watch(c, tag, t));
    DelayCommand(51.0, Note(tag + " valid at 51 " + IntToString(GetIsObjectValid(c))));
}

object At(object pc, string res, string wp, float dx, float dy)
{
    location l = wp != "" ? GetLocation(GetWaypointByTag(wp)) : Location(GetArea(pc), GetPosition(pc) + Vector(dx, dy, 0.0), 0.0);
    return CreateObject(OBJECT_TYPE_CREATURE, res, l);
}

void Bags()
{
    object o = GetFirstObjectInArea(GetArea(GetFirstPC()));
    while (GetIsObjectValid(o))
    {
        if (GetObjectType(o) == OBJECT_TYPE_PLACEABLE && GetTag(o) == "BodyBag") Note("bag " + GetName(o));
        o = GetNextObjectInArea(GetArea(GetFirstPC()));
    }
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    object g1 = At(pc, "cd_gobs", "WP_M0Q0_GOB_1", 0.0, 0.0);
    object g2 = At(pc, "cd_gobs", "WP_M0Q0_GOB_2", 0.0, 0.0);
    object g3 = At(pc, "cd_gob", "", -3.0, 3.0);
    object b5 = At(pc, "nw_bandit001", "", 3.0, 3.0);
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(b5, i, "");
    Note("pavel " + IntToString(GetIsObjectValid(GetObjectByTag("M1Q0DPavel"))));
    DelayCommand(1.0, VCamera(pc, 90.0, 16.0, 50.0));
    DelayCommand(3.0, VShot("alive"));
    DelayCommand(5.0, Kill(pc, g1, "gob1", TRUE));
    DelayCommand(5.0, Kill(pc, g2, "gob2", TRUE));
    DelayCommand(5.0, Kill(pc, g3, "gob3", FALSE));
    DelayCommand(5.0, Kill(pc, b5, "bandit", FALSE));
    DelayCommand(7.5, VShot("dead2"));
    DelayCommand(13.0, VShot("dead8"));
    DelayCommand(35.0, VShot("dead30"));
    DelayCommand(57.0, Bags());
    DelayCommand(58.0, VDone());
}

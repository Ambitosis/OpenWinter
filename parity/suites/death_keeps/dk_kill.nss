// EffectRunScript interval script: the creature kills itself (once).
void main()
{
    if (GetLocalInt(OBJECT_SELF, "dk_done")) return;
    SetLocalInt(OBJECT_SELF, "dk_done", 1);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), OBJECT_SELF);
}

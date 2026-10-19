// Effects package test: EffectRunScript and EffectRegenerate on OBJECT_SELF.
void main()
{
    effect e = EffectRunScript("effects_rs_event", "effects_rs_event", "effects_rs_event", 1.0, "payload");
    SetLocalString(GetModule(), "rs_data", GetEffectString(e, 0));
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, e, OBJECT_SELF, 3.5);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectRegenerate(2, 1.0), OBJECT_SELF, 10.0);
}

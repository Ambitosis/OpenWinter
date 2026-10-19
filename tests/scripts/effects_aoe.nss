// Effects package test: an acid fog at the caster's feet with custom enter/heartbeat/exit scripts.
void main()
{
    effect eAoE = EffectAreaOfEffect(AOE_PER_FOGACID, "effects_aoe_in", "effects_aoe_hb", "effects_aoe_out");
    ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, eAoE, GetLocation(OBJECT_SELF), 12.0);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectVisualEffect(VFX_IMP_HEALING_S), OBJECT_SELF);
}

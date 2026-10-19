// World presentation test: a lightning beam from the caller's hand to the test cow.
#include "fx_target"
void main()
{
    object oTarget = FxTarget();
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectBeam(VFX_BEAM_LIGHTNING, OBJECT_SELF, BODY_NODE_HAND), oTarget, 6.0);
}

// World presentation test: the test cow grows, turns and rises with different lerps.
#include "fx_target"
void main()
{
    object oTarget = FxTarget();
    SetObjectVisualTransform(oTarget, OBJECT_VISUAL_TRANSFORM_SCALE, 2.0, OBJECT_VISUAL_TRANSFORM_LERP_SMOOTHSTEP, 2.0);
    SetObjectVisualTransform(oTarget, OBJECT_VISUAL_TRANSFORM_ROTATE_Z, 90.0, OBJECT_VISUAL_TRANSFORM_LERP_LINEAR, 2.0);
    SetObjectVisualTransform(oTarget, OBJECT_VISUAL_TRANSFORM_TRANSLATE_Z, 0.6, OBJECT_VISUAL_TRANSFORM_LERP_EASE_OUT, 2.0);
    SetObjectVisualTransform(oTarget, OBJECT_VISUAL_TRANSFORM_ANIMATION_SPEED, 0.25, OBJECT_VISUAL_TRANSFORM_LERP_NONE, 0.0);
}

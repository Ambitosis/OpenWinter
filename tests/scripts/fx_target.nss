// Shared by the world presentation tests: the test cow, else the creature nearest the caller.
object FxTarget()
{
    object oCow = GetObjectByTag("FX_COW");
    return GetIsObjectValid(oCow) ? oCow : GetNearestObject(OBJECT_TYPE_CREATURE, OBJECT_SELF);
}

// World presentation test: a cow five metres south of the caller to try object effects on.
void main()
{
    vector v = GetPosition(OBJECT_SELF);
    CreateObject(OBJECT_TYPE_CREATURE, "nw_cow", Location(GetArea(OBJECT_SELF), Vector(v.x, v.y - 5.0, v.z), 90.0), FALSE, "FX_COW");
}

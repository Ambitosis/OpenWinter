// Where each encountermix test's encounter lies (the player walks there).
vector MxSpot(string s)
{
    if (s == "ma") return Vector(12.0, 12.0, 0.0);
    if (s == "ma2") return Vector(30.0, 12.0, 0.0);
    if (s == "mb") return Vector(48.0, 12.0, 0.0);
    if (s == "mc") return Vector(66.0, 12.0, 0.0);
    if (s == "md") return Vector(84.0, 12.0, 0.0);
    if (s == "me") return Vector(102.0, 12.0, 0.0);
    if (s == "mf") return Vector(12.0, 30.0, 0.0);
    if (s == "mg") return Vector(30.0, 30.0, 0.0);
    if (s == "mh") return Vector(48.0, 30.0, 0.0);
    if (s == "mi") return Vector(66.0, 30.0, 0.0);
    if (s == "mj") return Vector(84.0, 30.0, 0.0);
    if (s == "mk") return Vector(102.0, 30.0, 0.0);
    if (s == "sa") return Vector(12.0, 48.0, 0.0);
    if (s == "sb") return Vector(30.0, 48.0, 0.0);
    if (s == "no") return Vector(54.0, 54.0, 0.0);
    if (s == "nobig") return Vector(66.0, 48.0, 0.0);
    if (s == "dz") return Vector(84.0, 48.0, 0.0);
    if (s == "dk") return Vector(102.0, 48.0, 0.0);
    return Vector(0.0, 0.0, 0.0);
}

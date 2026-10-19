void main()
{
    object m = GetModule();
    int d = GetLocalInt(m, "depth") + 1;
    SetLocalInt(m, "depth", d);
    if (GetScriptRecursionLevel() > GetLocalInt(m, "maxlevel")) SetLocalInt(m, "maxlevel", GetScriptRecursionLevel());
    if (d < 100) ExecuteScript("ex_recurse");
}

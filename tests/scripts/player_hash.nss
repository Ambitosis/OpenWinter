void main()
{
    object m = GetModule();
    SetLocalInt(m, "hash_empty", HashString(""));
    SetLocalInt(m, "hash_abc", HashString("abc"));
    int n = 0;
    switch (HashString("BBB"))
    {
        case "AAA": n = 1; break;
        case "BBB": n = 2; break;
    }
    SetLocalInt(m, "hash_switch", n);
}

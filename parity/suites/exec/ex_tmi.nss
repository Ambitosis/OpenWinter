void main()
{
    object m = GetModule();
    SetLocalInt(m, "tmi.first", GetScriptInstructionsRemaining());
    int i = 0;
    while (TRUE)
    {
        i++;
        SetLocalInt(m, "tmi.loops", i);
    }
}

// Player package: 2DA access, misc getters, event scripts, script meta, SetJmp/LongJmp.
int Thrower(int n)
{
    if (n > 2) LongJmp("err", 42);
    return n;
}

void main()
{
    object m = GetModule();
    SetLocalString(m, "2da_label", Get2DAString("classes", "Label", 0));
    SetLocalString(m, "2da_hitdie", Get2DAString("classes", "hitdie", 0));
    SetLocalString(m, "2da_missing", Get2DAString("classes", "NoSuchColumn", 0) + "|" + Get2DAString("no_such_2da", "Label", 0));
    SetLocalString(m, "2da_col0", Get2DAColumn("classes", 0));
    SetLocalString(m, "2da_colbad", Get2DAColumn("classes", 9999));
    SetLocalInt(m, "2da_rows", Get2DARowCount("classes"));

    SetLocalString(m, "name", RandomName(NAME_FIRST_HUMAN_MALE));
    SetLocalFloat(m, "feet", FeetToMeters(10.0));

    SetLocalInt(m, "evt_set", SetEventScript(m, EVENT_SCRIPT_MODULE_ON_HEARTBEAT, "my_hb"));
    SetLocalString(m, "evt_get", GetEventScript(m, EVENT_SCRIPT_MODULE_ON_HEARTBEAT));
    SetLocalInt(m, "evt_bad", SetEventScript(m, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "x"));

    SetLocalString(m, "script_name", GetScriptName());
    SetLocalInt(m, "recursion", GetScriptRecursionLevel());
    SetLocalInt(m, "instr_ok", GetScriptInstructionsRemaining() > 1000);

    int r = SetJmp("err");
    if (r == 0)
    {
        SetLocalInt(m, "jmp_valid", GetIsValidJmp("err"));
        int i;
        for (i = 0; i < 10; i++) Thrower(i);
        SetLocalInt(m, "jmp_notreached", 1);
    }
    SetLocalInt(m, "jmp_result", r);

    SetLocalInt(m, "done", 1);
    AbortRunningScript();
    SetLocalInt(m, "after_abort", 1);
}

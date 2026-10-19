// A StartingConditional with globals, run through a conversation-style check.
int gBase = 40;
int Helper() { return gBase + 2; }
int StartingConditional()
{
    SetLocalInt(GetModule(), "cond", Helper());
    return Helper() > 0;
}

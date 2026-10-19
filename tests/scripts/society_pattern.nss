// Listen pattern matching from a script.
void main()
{
    object m = GetModule();
    SetLocalInt(m, "t1", TestStringAgainstPattern("give *n gold", "give 250 gold"));
    SetLocalInt(m, "cnt", GetMatchedSubstringsCount());
    SetLocalString(m, "s0", GetMatchedSubstring(0));
    SetLocalString(m, "s1", GetMatchedSubstring(1));
    SetLocalString(m, "s2", GetMatchedSubstring(2));
    SetLocalString(m, "s9", GetMatchedSubstring(9));
    SetLocalInt(m, "t2", TestStringAgainstPattern("(hi|hello) **", "hey you"));
    SetLocalInt(m, "t3", TestStringAgainstPattern("(hi|hello) **", "hello you"));
    SetLocalInt(m, "lpn", GetListenPatternNumber());
}

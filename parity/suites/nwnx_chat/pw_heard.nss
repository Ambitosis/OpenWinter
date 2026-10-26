void main()
{
    object oMod = GetModule();
    if (GetListenPatternNumber() == 100)
        SetLocalString(oMod, "HEARD", GetLocalString(oMod, "HEARD") + "[" + GetMatchedSubstring(0) + ":" + GetTag(GetLastSpeaker()) + "]");
}

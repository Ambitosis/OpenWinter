// OnConversation: records what was heard.
void main()
{
    object o = OBJECT_SELF;
    SetLocalInt(o, "heard", GetLocalInt(o, "heard") + 1);
    SetLocalInt(o, "pattern", GetListenPatternNumber());
    SetLocalInt(o, "count", GetMatchedSubstringsCount());
    SetLocalString(o, "sub0", GetMatchedSubstring(0));
    SetLocalString(o, "sub1", GetMatchedSubstring(1));
    SetLocalObject(o, "speaker", GetLastSpeaker());
    SetLocalInt(o, "command", GetLastAssociateCommand());
}

// OnPerception and OnConversation: appends "<tag><kind>" (h heard, s seen, i inaudible, v vanished; c a conversation)
// to the module's running "order" string.
void main()
{
    object m = GetModule();
    string k;
    if (GetLastSpeaker() != OBJECT_INVALID) k = GetTag(OBJECT_SELF) + "c";
    else k = GetTag(GetLastPerceived()) + (GetLastPerceptionHeard() ? "h" : GetLastPerceptionSeen() ? "s" : GetLastPerceptionInaudible() ? "i" : "v");
    SetLocalString(m, "order", GetLocalString(m, "order") + k + " ");
}

#include "cv_inc"
// The probe's OnConversation: logged, then the stock answer (nw_c2_default4 clears and begins).
void main()
{
    Note("onconv:" + GetName(GetLastSpeaker()));
    ClearAllActions();
    Note("begin=" + IntToString(BeginConversation("cv_talk")));
}

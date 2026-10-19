#include "cb_inc"
// The partner's OnConversation: logged only (nothing begins a conversation between the two creatures).
void main() { CbEvent("onconv" + IntToString(GetListenPatternNumber())); }

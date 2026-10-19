#include "parity_inc"
#include "aex_inc"
void main(){object t=GetExitingObject();string key=GetLocalString(t,"key");PLogF(key+".elapsed",Clock()-GetLocalFloat(t,"start"),0.15);SetLocalInt(t,"exits",GetLocalInt(t,"exits")+1);}

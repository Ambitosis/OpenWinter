#include "parity_inc"
void main(){string k=GetTag(OBJECT_SELF)+".close."+IntToString(GetLocalInt(OBJECT_SELF,"closes"));SetLocalInt(OBJECT_SELF,"closes",GetLocalInt(OBJECT_SELF,"closes")+1);PLog(k+".by",GetTag(GetLastClosedBy()));PLogI(k+".A",GetIsOpen(GetObjectByTag("A")));PLogI(k+".B",GetIsOpen(GetObjectByTag("B")));}

#include "parity_inc"
void main(){string k=GetTag(OBJECT_SELF)+".open."+IntToString(GetLocalInt(OBJECT_SELF,"opens"));SetLocalInt(OBJECT_SELF,"opens",GetLocalInt(OBJECT_SELF,"opens")+1);PLog(k+".by",GetTag(GetLastOpenedBy()));PLogI(k+".A",GetIsOpen(GetObjectByTag("A")));PLogI(k+".B",GetIsOpen(GetObjectByTag("B")));}

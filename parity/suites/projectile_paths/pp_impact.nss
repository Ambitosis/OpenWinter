#include "parity_inc"
#include "parity_time"
void main(){string k=GetLocalString(OBJECT_SELF,"case");int wave=GetLocalInt(OBJECT_SELF,"wave");float elapsed=PNow()-GetLocalFloat(OBJECT_SELF,"start");SetLocalInt(OBJECT_SELF,"hits",GetLocalInt(OBJECT_SELF,"hits")+1);PLogF(k+".impact"+IntToString(wave),elapsed,.12);if(wave==0)SetLocalFloat(OBJECT_SELF,"release",elapsed);else PLogF(k+".flight",elapsed-GetLocalFloat(OBJECT_SELF,"release"),.09);}

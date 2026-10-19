#include "aex_inc"
void main(){SetLocalFloat(OBJECT_SELF,"start",Clock());ApplyEffectToObject(DURATION_TYPE_TEMPORARY,EffectAreaOfEffect(37,"aex_none","aex_none","aex_exit"),OBJECT_SELF,GetLocalFloat(OBJECT_SELF,"duration"));}

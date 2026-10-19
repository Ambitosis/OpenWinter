#include "parity_inc"
void main(){object a=GetLastAttacker();if(GetLocalInt(a,"executed"))return;SetLocalInt(a,"executed",1);string k=GetLocalString(a,"key");PLogF(k+".distance",GetDistanceBetween(a,OBJECT_SELF),0.5);PLogF(k+".moved",GetDistanceBetweenLocations(GetLocation(a),GetLocalLocation(a,"start")),0.5);}

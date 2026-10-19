#include "parity_inc"
#include "parity_time"
void Finished(object c){PLogF(GetTag(c)+".done",PNow()-GetLocalFloat(c,"start"),0.15);}
void Start(object c){SetLocalFloat(c,"start",PNow());object p=GetLocalObject(c,"potion");AssignCommand(c,ActionUseItemOnObject(p,GetFirstItemProperty(p),c));AssignCommand(c,ActionDoCommand(Finished(c)));}
void main(){int i;for(i=0;i<2;i++){object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(IntToFloat(i)*5.0,0.0));SetTag(c,"case"+IntToString(i));SetAILevel(c,AI_LEVEL_NORMAL);int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");object p=CreateItemOnObject("nw_it_mpotion001",c,i+1);SetLocalObject(c,"potion",p);DelayCommand(2.0,Start(c));}DelayCommand(8.0,PDone());}

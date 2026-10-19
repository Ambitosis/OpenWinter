#include "parity_itemev"
void Drop(object c,object bag){AssignCommand(c,ActionPutDownItem(bag));}
void Pick(object c,object bag){AssignCommand(c,ActionPickUpItem(bag));}
void Give(object c,object b,object bag){AssignCommand(c,ActionGiveItem(bag,b));}
void CopyBag(object bag,object a){CopyObject(bag,GetLocation(a),a,"copiedbag");}
void Destroy(object bag){DestroyObject(bag);}
void main(){object a=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(0.0,0.0),FALSE,"a");
 object b=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(1.0,0.0),FALSE,"b");
 int i;for(i=5000;i<=5012;i++){SetEventScript(a,i,"");SetEventScript(b,i,"");}SetAILevel(a,AI_LEVEL_NORMAL);SetAILevel(b,AI_LEVEL_NORMAL);
 object bag=CreateItemOnObject("nw_it_contain001",a,1,"bag");CreateItemOnObject("nw_it_mring001",bag,1,"inner");
 DelayCommand(1.0,PEvLog("created"));DelayCommand(2.0,Drop(a,bag));DelayCommand(6.0,PEvLog("dropped"));
 DelayCommand(7.0,Pick(a,bag));DelayCommand(11.0,PEvLog("picked"));DelayCommand(12.0,Give(a,b,bag));
 DelayCommand(16.0,PEvLog("given"));DelayCommand(17.0,CopyBag(bag,a));DelayCommand(18.0,PEvLog("copied"));
 DelayCommand(19.0,Destroy(bag));DelayCommand(23.0,PEvLog("destroyed"));DelayCommand(24.0,PDone());}

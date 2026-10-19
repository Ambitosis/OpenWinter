#include "parity_inc"
#include "parity_time"
void Finish(object o){PLogF("finish."+GetTag(o),PNow()-GetLocalFloat(o,"start"),0.2);}
void Cast(object o, int n) {
 SetLocalFloat(o,"start",PNow());
 AssignCommand(o,ActionCastSpellAtObject(n,o,METAMAGIC_NONE,TRUE));
 AssignCommand(o,ActionDoCommand(Finish(o)));
}
void Run() {
 object o0=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(0.0,0.0));
 SetTag(o0,"6"); SetAILevel(o0,AI_LEVEL_NORMAL); SetEventScript(o0,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o0,6));
 object o1=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(8.0,0.0));
 SetTag(o1,"3"); SetAILevel(o1,AI_LEVEL_NORMAL); SetEventScript(o1,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o1,3));
 object o2=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(16.0,0.0));
 SetTag(o2,"1"); SetAILevel(o2,AI_LEVEL_NORMAL); SetEventScript(o2,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o2,1));
 object o3=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(24.0,0.0));
 SetTag(o3,"107"); SetAILevel(o3,AI_LEVEL_NORMAL); SetEventScript(o3,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o3,107));
 object o4=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(32.0,0.0));
 SetTag(o4,"58"); SetAILevel(o4,AI_LEVEL_NORMAL); SetEventScript(o4,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o4,58));
 object o5=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(0.0,8.0));
 SetTag(o5,"2"); SetAILevel(o5,AI_LEVEL_NORMAL); SetEventScript(o5,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o5,2));
 object o6=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(8.0,8.0));
 SetTag(o6,"0"); SetAILevel(o6,AI_LEVEL_NORMAL); SetEventScript(o6,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o6,0));
 object o7=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(16.0,8.0));
 SetTag(o7,"716"); SetAILevel(o7,AI_LEVEL_NORMAL); SetEventScript(o7,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o7,716));
 object o8=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(24.0,8.0));
 SetTag(o8,"769"); SetAILevel(o8,AI_LEVEL_NORMAL); SetEventScript(o8,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o8,769));
 object o9=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(32.0,8.0));
 SetTag(o9,"802"); SetAILevel(o9,AI_LEVEL_NORMAL); SetEventScript(o9,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
 DelayCommand(1.0,Cast(o9,802));
 DelayCommand(12.0,PDone());
}
void main(){DelayCommand(0.2,Run());}

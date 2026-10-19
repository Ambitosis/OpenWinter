#include "parity_inc"
string Effects(object o){string s="";effect e=GetFirstEffect(o);int n=0;while(GetIsEffectValid(e)&&n<20){s+=IntToString(GetEffectType(e,TRUE))+":"+IntToString(GetEffectDurationType(e))+":"+IntToString(GetEffectDuration(e))+":"+IntToString(GetEffectSubType(e))+" ";e=GetNextEffect(o);n++;}return s;}
void Report(string key,object c){PLog(key+".effects",Effects(c));object s=GetAssociate(ASSOCIATE_TYPE_SUMMONED,c);PLogI(key+".valid",GetIsObjectValid(s));PLogI(key+".oldvalid",GetIsObjectValid(GetLocalObject(c,"OLD")));if(GetIsObjectValid(s)){PLog(key+".resref",GetResRef(s));PLog(key+".summoneffects",Effects(s));}}
void Summon(float duration,float delay=0.0){ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,EffectSummonCreature("NW_S_badgerdire",-1,delay,FALSE,-1),GetLocation(OBJECT_SELF),duration);}
void Remove(object c){effect e=GetFirstEffect(c);RemoveEffect(c,e);}
void Remember(object c){SetLocalObject(c,"OLD",GetAssociate(ASSOCIATE_TYPE_SUMMONED,c));}
void Detach(object c){RemoveSummonedAssociate(c,GetAssociate(ASSOCIATE_TYPE_SUMMONED,c));}
void KillSummon(object c){DestroyObject(GetAssociate(ASSOCIATE_TYPE_SUMMONED,c));}
void Case(int index,string key,int mode){object c=CreateObject(OBJECT_TYPE_CREATURE,"sum_wiz20",PCentre(IntToFloat(index%4)*25.0,IntToFloat(index/4)*25.0));SetAILevel(c,AI_LEVEL_VERY_LOW);AssignCommand(c,Summon(5.0,2.0));DelayCommand(0.2,Report(key+".pending",c));DelayCommand(2.5,Remember(c));DelayCommand(2.6,Report(key+".spawned",c));
 if(mode==1)DelayCommand(3.0,Remove(c));if(mode==2)DelayCommand(3.0,AssignCommand(c,Summon(10.0)));if(mode==3)DelayCommand(1.0,Remove(c));if(mode==4)DelayCommand(3.0,Detach(c));if(mode==5)DelayCommand(3.0,KillSummon(c));
 DelayCommand(4.0,Report(key+".changed",c));DelayCommand(5.5,Report(key+".afterapplyexpiry",c));DelayCommand(8.0,Report(key+".afterspawnexpiry",c));}
void main(){Case(0,"expiry",0);Case(1,"remove",1);Case(2,"replace",2);Case(3,"cancel",3);Case(4,"detach",4);Case(5,"destroy",5);
 object c=CreateObject(OBJECT_TYPE_CREATURE,"NW_BANDIT001",PCentre(-10.0,-10.0));PLog("case.creature",GetResRef(c));object item=CreateItemOnObject("NW_WSWLS001",c);PLog("case.inventory",GetResRef(item));object ground=CreateObject(OBJECT_TYPE_ITEM,"NW_IT_MPOTION001",PCentre(-10.0,-10.0));PLog("case.ground",GetResRef(ground));object place=CreateObject(OBJECT_TYPE_PLACEABLE,"PLC_CHEST1",PCentre(-10.0,-5.0));PLogI("case.placevalid",GetIsObjectValid(place));PLog("case.placeable",GetResRef(place));DelayCommand(10.0,PDone());}

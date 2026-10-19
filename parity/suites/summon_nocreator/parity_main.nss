#include "parity_inc"
string Effects(object o){string s="";effect e=GetFirstEffect(o);while(GetIsEffectValid(e)){s+=IntToString(GetEffectType(e,TRUE))+":"+IntToString(GetEffectDurationType(e))+":"+IntToString(GetEffectDuration(e))+" ";e=GetNextEffect(o);}return s;}
void Report(string key,object owner,location loc){int n=0;int mastervalid=0;int type=0;object o=GetFirstObjectInArea(GetAreaFromLocation(loc));while(GetIsObjectValid(o)){if(GetResRef(o)=="nw_s_badgerdire"&&GetDistanceBetweenLocations(loc,GetLocation(o))<8.0){n++;mastervalid=GetIsObjectValid(GetMaster(o));type=GetAssociateType(o);}o=GetNextObjectInArea(GetAreaFromLocation(loc));}PLogI(key+".mastervalid",mastervalid);PLogI(key+".type",type);PLogI(key+".count",n);PLog(key+".effects",Effects(owner));}
void Create(location loc,object target,int kind){effect e=EffectSummonCreature("nw_s_badgerdire",-1,1.0,FALSE,-1);if(GetIsObjectValid(target))ApplyEffectToObject(kind,e,target,3.0);else ApplyEffectAtLocation(kind,e,loc,3.0);}
void Case(int i,string key,int creator,int targettype,int kind){location loc=PCentre(IntToFloat(i%5)*30.0,IntToFloat(i/5)*30.0);object owner=GetModule();object target=OBJECT_INVALID;object caster=GetModule();if(creator==1)caster=CreateObject(OBJECT_TYPE_PLACEABLE,"plc_chest1",loc);if(creator==2)caster=CreateObject(OBJECT_TYPE_CREATURE,"sum_wiz20",loc);owner=caster;if(targettype==1)target=CreateObject(OBJECT_TYPE_PLACEABLE,"plc_chest1",loc);if(targettype==2)target=CreateObject(OBJECT_TYPE_CREATURE,"sum_wiz20",loc);if(GetIsObjectValid(target))owner=target;if(creator==0)Create(loc,target,kind);else AssignCommand(caster,Create(loc,target,kind));DelayCommand(0.2,Report(key+".pending",owner,loc));DelayCommand(2.0,Report(key+".arrived",owner,loc));DelayCommand(6.0,Report(key+".expired",owner,loc));}
void main(){
Case(0,"module.0",0,0,0);
Case(1,"module.1",0,0,1);
Case(2,"module.2",0,0,2);
Case(3,"placecreator.0",1,0,0);
Case(4,"placecreator.1",1,0,1);
Case(5,"placecreator.2",1,0,2);
Case(6,"placetarget.0",0,1,0);
Case(7,"placetarget.1",0,1,1);
Case(8,"placetarget.2",0,1,2);
Case(9,"creatureplace.0",2,1,0);
Case(10,"creatureplace.1",2,1,1);
Case(11,"creatureplace.2",2,1,2);
Case(12,"modulecreature.0",0,2,0);
Case(13,"modulecreature.1",0,2,1);
Case(14,"modulecreature.2",0,2,2);
DelayCommand(8.0,PDone());}

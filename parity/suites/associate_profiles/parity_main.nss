#include "parity_inc"
void Use(object c,int feat){AssignCommand(c,ActionUseFeat(feat,c));}
void Report(string key,object c,int type,int feat,int phase){object a=GetAssociate(type,c);PLogI(key+".valid",GetIsObjectValid(a));PLogI(key+".uses",GetFeatRemainingUses(feat,c));PLogI(key+".oldvalid",GetIsObjectValid(GetLocalObject(c,"OLD")));if(!GetIsObjectValid(a))return;PLog(key+".resref",GetResRef(a));PLog(key+".name",GetName(a));PLogI(key+".master",GetMaster(a)==c);PLogI(key+".type",GetAssociateType(a));PLogI(key+".second",GetIsObjectValid(GetAssociate(type,c,2)));if(phase==0){SetLocalObject(c,"OLD",a);object control=CreateObject(OBJECT_TYPE_CREATURE,GetResRef(a),PCentre(-10.0,-10.0));PLogI(key+".hd_delta",GetHitDice(a)-GetHitDice(control));PLogI(key+".hp_delta",GetMaxHitPoints(a)-GetMaxHitPoints(control));PLogI(key+".ac_delta",GetAC(a)-GetAC(control));PLogI(key+".appearance_delta",GetAppearanceType(a)-GetAppearanceType(control));int i;for(i=0;i<6;i++)PLogI(key+".ability"+IntToString(i),GetAbilityScore(a,i)-GetAbilityScore(control,i));DestroyObject(control);}}
void Recharge(object c,int feat){IncrementRemainingFeatUses(c,feat);}
void Case(int index,string key,int type,int feat){object c=CreateObject(OBJECT_TYPE_CREATURE,"as_"+key,PCentre(IntToFloat(index%6)*35.0,IntToFloat(index/6)*35.0));SetAILevel(c,AI_LEVEL_NORMAL);PLogI(key+".beforeuses",GetFeatRemainingUses(feat,c));DelayCommand(2.0,Use(c,feat));DelayCommand(9.0,Report(key+".first",c,type,feat,0));DelayCommand(10.0,Use(c,feat));DelayCommand(14.0,Report(key+".retry",c,type,feat,1));DelayCommand(15.0,Recharge(c,feat));DelayCommand(16.0,Use(c,feat));DelayCommand(23.0,Report(key+".replacement",c,type,feat,2));}
void main(){
Case(0,"fam0",3,303);
Case(1,"fam1",3,303);
Case(2,"fam2",3,303);
Case(3,"fam3",3,303);
Case(4,"fam4",3,303);
Case(5,"fam5",3,303);
Case(6,"fam6",3,303);
Case(7,"fam7",3,303);
Case(8,"fam8",3,303);
Case(9,"fam9",3,303);
Case(10,"fam10",3,303);
Case(11,"comp0",2,199);
Case(12,"comp1",2,199);
Case(13,"comp2",2,199);
Case(14,"comp3",2,199);
Case(15,"comp4",2,199);
Case(16,"comp5",2,199);
Case(17,"comp6",2,199);
Case(18,"comp7",2,199);
Case(19,"comp8",2,199);
Case(20,"wiz1",3,303);
Case(21,"sorc1",3,303);
Case(22,"arcane7",3,303);
Case(23,"druid1",2,199);
Case(24,"ranger6",2,199);
Case(25,"ranger10",2,199);
Case(26,"divine9",2,199);
Case(27,"wiz40",3,303);
Case(28,"druid40",2,199);
Case(29,"d10r6",2,199);
Case(30,"r6d10",2,199);
Case(31,"d6r10",2,199);
Case(32,"r10d6",2,199);
DelayCommand(25.0,PDone());}

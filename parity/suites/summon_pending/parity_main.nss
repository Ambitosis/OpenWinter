#include "parity_inc"
void Make(int kind, float delay)
{
    ApplyEffectAtLocation(kind, EffectSummonCreature("nw_s_badgerdire", -1, delay, FALSE), GetLocation(OBJECT_SELF), 20.0);
}
void ReportOne(string phase, string tag)
{
    object c=GetObjectByTag(tag);object a=GetAssociate(ASSOCIATE_TYPE_SUMMONED,c);
    string key=phase+"."+tag;
    PLogI(key+".valid",GetIsObjectValid(a));
    if(GetIsObjectValid(a)){PLog(key+".resref",GetResRef(a));PLogI(key+".master",GetMaster(a)==c);}
    int count=0;effect e=GetFirstEffect(c);
    while(GetIsEffectValid(e)){if(GetEffectType(e,TRUE)==100){PLogI(key+".kind",GetEffectDurationType(e));PLogF(key+".remaining",IntToFloat(GetEffectDurationRemaining(e)),1.0);count++;}e=GetNextEffect(c);}
    PLogI(key+".effects",count);
}
void Report(string phase){ReportOne(phase,"ARRIVED");ReportOne(phase,"SHORT");ReportOne(phase,"TEMP");ReportOne(phase,"PERM");ReportOne(phase,"INSTANT");}
void Case(string tag,float x,int kind,float delay){object c=CreateObject(OBJECT_TYPE_CREATURE,"sum_wiz20",PCentre(x,0.0),FALSE,tag);AssignCommand(c,Make(kind,delay));}
void main(){
    if(GetLocalInt(GetModule(),"SETUP"))return;SetLocalInt(GetModule(),"SETUP",1);
    Case("ARRIVED",0.0,DURATION_TYPE_TEMPORARY,2.0);Case("SHORT",25.0,DURATION_TYPE_TEMPORARY,5.0);
    Case("TEMP",50.0,DURATION_TYPE_TEMPORARY,12.0);Case("PERM",75.0,DURATION_TYPE_PERMANENT,12.0);Case("INSTANT",100.0,DURATION_TYPE_INSTANT,12.0);
    DelayCommand(4.0,PLog("__save__","1"));DelayCommand(6.0,Report("early"));DelayCommand(15.0,Report("arrival"));DelayCommand(23.0,Report("expiry"));DelayCommand(25.0,PDone());
}

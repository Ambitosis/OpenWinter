#include "parity_inc"
void Snapshot(object target,string key) {
    int count=0;int visuals=0;effect e=GetFirstEffect(target);
    while(GetIsEffectValid(e)) {
        count++;
        if(GetEffectType(e)==EFFECT_TYPE_VISUALEFFECT)visuals++;
        e=GetNextEffect(target);
    }
    PLogI(key+".effects",count);PLogI(key+".visuals",visuals);
    PLogI(key+".strength",GetAbilityScore(target,ABILITY_STRENGTH));
}
void Remove(object target,string key) {
    effect e=GetFirstEffect(target);
    while(GetIsEffectValid(e)) {
        RemoveEffect(target,e);e=GetFirstEffect(target);
    }
    DelayCommand(0.1,Snapshot(target,key+".removed"));
}
void Probe(int row,int linked,int durationType,int index) {
    string key="row"+IntToString(row)+".linked"+IntToString(linked)+".duration"+IntToString(durationType);
    object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(10.0+IntToFloat(index%10)*5.0,10.0+IntToFloat(index/10)*5.0,0.0),0.0));
    int j;for(j=5000;j<=5012;j++)SetEventScript(target,j,"");
    SetPlotFlag(target,TRUE);
    effect e=TagEffect(EffectVisualEffect(row),key);
    if(linked)e=EffectLinkEffects(e,EffectAbilityIncrease(ABILITY_STRENGTH,3));
    ApplyEffectToObject(durationType,e,target,0.5);
    Snapshot(target,key+".immediate");
    DelayCommand(0.1,Snapshot(target,key+".early"));
    DelayCommand(0.8,Snapshot(target,key+".late"));
    DelayCommand(1.0,Remove(target,key));
}
void main() {
    int i;int linked;int durationType;int index=0;
    for(i=0;i<5;i++)for(linked=0;linked<2;linked++)for(durationType=0;durationType<3;durationType++) {
        int row=i==0?18:i==1?181:i==2?147:i==3?73:127;
        Probe(row,linked,durationType,index++);
    }
    DelayCommand(2.0,PDone());
}

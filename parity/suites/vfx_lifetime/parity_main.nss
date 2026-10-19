#include "parity_inc"
void Probe(object target, int row, int durationType) {
    string key="row"+IntToString(row)+".duration"+IntToString(durationType);
    effect original=TagEffect(EffectVisualEffect(row),key);
    ApplyEffectToObject(durationType,original,target,5.0);
    effect e=GetFirstEffect(target);int count=0;
    while(GetIsEffectValid(e)) {
        if(GetEffectTag(e)==key)count++;
        e=GetNextEffect(target);
    }
    PLogI(key+".count",count);
    e=GetFirstEffect(target);
    while(GetIsEffectValid(e)) {
        RemoveEffect(target,e);
        e=GetFirstEffect(target);
    }
}
void Batch(int start) {
    object target=GetLocalObject(GetModule(),"target");
    int row;
    for(row=start;row<start+20 && row<2000;row++) {
        string kind=Get2DAString("visualeffects","Type_FD",row);
        if(kind=="D" || kind=="F" || kind=="P" || kind=="B" || row==127 || row==399) {
            Probe(target,row,DURATION_TYPE_INSTANT);
            Probe(target,row,DURATION_TYPE_TEMPORARY);
            Probe(target,row,DURATION_TYPE_PERMANENT);
        }
    }
    if(row<2000)DelayCommand(0.1,Batch(row));
    else PDone();
}
void main() {
    object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(10.0,10.0,0.0),0.0));
    int j;for(j=5000;j<=5012;j++)SetEventScript(target,j,"");
    SetPlotFlag(target,TRUE);
    SetLocalObject(GetModule(),"target",target);
    DelayCommand(1.0,Batch(0));
}

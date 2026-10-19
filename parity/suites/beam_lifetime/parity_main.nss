#include "parity_inc"
void Snapshot(object target,string key) {
    PLogI(key+".valid",GetIsObjectValid(target));
    if(!GetIsObjectValid(target))return;
    int count=0;effect e=GetFirstEffect(target);
    while(GetIsEffectValid(e)) {
        if(GetEffectTag(e)=="beam")count++;
        e=GetNextEffect(target);
    }
    PLogI(key+".effects",count);
}
object Actor(int index) {
    object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(5.0+IntToFloat(index%16)*6.0,5.0+IntToFloat(index/16)*6.0,0.0),0.0));
    int j;for(j=5000;j<=5012;j++)SetEventScript(o,j,"");
    return o;
}
void Remove(object target,string key) {
    effect e=GetFirstEffect(target);
    while(GetIsEffectValid(e)) {
        RemoveEffect(target,e);e=GetFirstEffect(target);
    }
    Snapshot(target,key+".removed");
}
void Kill(object target){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),target);}
void Probe(int row,int durationType,int mode,int index) {
    object source=Actor(index*2);object target=Actor(index*2+1);
    string key="row"+IntToString(row)+".duration"+IntToString(durationType)+".mode"+IntToString(mode);
    effect e=TagEffect(EffectBeam(row,source,BODY_NODE_CHEST),"beam");
    ApplyEffectToObject(durationType,e,target,5.0);
    Snapshot(target,key+".immediate");
    DelayCommand(0.1,Snapshot(target,key+".early"));
    if(mode==1)DelayCommand(1.0,Kill(source));
    if(mode==2)DelayCommand(1.0,DestroyObject(source));
    if(mode==3)DelayCommand(1.0,Kill(target));
    DelayCommand(1.5,Snapshot(target,key+".changed"));
    DelayCommand(5.5,Snapshot(target,key+".expired"));
    DelayCommand(6.0,Remove(target,key));
}
void main() {
    int i;int mode;int durationType;int index=0;
    for(i=0;i<4;i++)for(durationType=0;durationType<3;durationType++)for(mode=0;mode<4;mode++)
        Probe(i==0?73:i==1?210:i==2?211:216,durationType,mode,index++);
    DelayCommand(7.0,PDone());
}

#include "parity_inc"
void Snapshot(object target,string key) {
    int count=0;int beams=0;int visuals=0;int icons=0;int abilities=0;
    effect e=GetFirstEffect(target);
    while(GetIsEffectValid(e)) {
        count++;
        int t=GetEffectType(e);
        if(t==EFFECT_TYPE_BEAM)beams++;
        if(t==EFFECT_TYPE_VISUALEFFECT)visuals++;
        if(t==EFFECT_TYPE_ICON)icons++;
        if(t==EFFECT_TYPE_ABILITY_INCREASE)abilities++;
        e=GetNextEffect(target);
    }
    PLogI(key+".count",count);PLogI(key+".beams",beams);
    PLogI(key+".visuals",visuals);PLogI(key+".icons",icons);PLogI(key+".abilities",abilities);
}
void Kill(object target){ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDeath(),target);}
void Probe(int kind,int subtype,int linked,int durationType,int index) {
    object target=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(5.0+IntToFloat(index%10)*6.0,5.0+IntToFloat(index/10)*6.0,0.0),0.0));
    int j;for(j=5000;j<=5012;j++)SetEventScript(target,j,"");
    effect e=EffectBeam(73,GetLocalObject(GetModule(),"source"),BODY_NODE_CHEST);
    if(kind==1)e=EffectVisualEffect(147);
    if(kind==2)e=EffectIcon(25);
    if(kind==3)e=EffectAbilityIncrease(ABILITY_STRENGTH,3);
    if(linked)e=EffectLinkEffects(e,EffectAbilityIncrease(ABILITY_DEXTERITY,3));
    if(subtype==1)e=SupernaturalEffect(e);
    if(subtype==2)e=ExtraordinaryEffect(e);
    if(subtype==3)e=UnyieldingEffect(e);
    string key="kind"+IntToString(kind)+".subtype"+IntToString(subtype)+".linked"+IntToString(linked)+".duration"+IntToString(durationType);
    ApplyEffectToObject(durationType,e,target,5.0);
    Snapshot(target,key+".before");
    DelayCommand(1.0,Kill(target));
    DelayCommand(1.5,Snapshot(target,key+".dead"));
    DelayCommand(5.5,Snapshot(target,key+".expired"));
}
void main() {
    object source=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(2.0,2.0,0.0),0.0));
    int j;for(j=5000;j<=5012;j++)SetEventScript(source,j,"");SetLocalObject(GetModule(),"source",source);
    int i;int subtype;int linked;int durationType;int index=0;
    for(i=0;i<4;i++)for(subtype=0;subtype<4;subtype++)for(linked=0;linked<2;linked++)for(durationType=1;durationType<3;durationType++)
        Probe(i,subtype,linked,durationType,index++);
    DelayCommand(7.0,PDone());
}

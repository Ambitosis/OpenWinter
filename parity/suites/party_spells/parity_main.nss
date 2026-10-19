#include "parity_fx"
void Quiet(object o) {
    int i; for(i=5000;i<=5012;i++) SetEventScript(o,i,"");
    SetAILevel(o,AI_LEVEL_NORMAL); AssignCommand(o,ClearAllActions(TRUE));
}
object Actor(string resref,string tag,float x,float y) {
    object o=CreateObject(OBJECT_TYPE_CREATURE,resref,Location(GetObjectByTag("PARITY_AREA"),Vector(x,y,0.0),0.0),FALSE,tag);
    Quiet(o); return o;
}
void Read(object o,object c,int spell,string key) {
    PLogI(key+".valid",GetIsObjectValid(o));
    PLogI(key+".has",GetHasSpellEffect(spell,o));
    PLog(key+".delta",PFxDiff(o));
    string effects="";effect e=GetFirstEffect(o);
    while(GetIsEffectValid(e)) {
        if(GetEffectSpellId(e)==spell) {
            if(effects!="")effects+=" ";
            effects+=PFxShort(e)+"c"+IntToString(GetEffectCreator(e)==c)+"l"+IntToString(GetEffectCasterLevel(e));
        }
        e=GetNextEffect(o);
    }
    PLog(key+".effects",effects==""?"-":effects);
}
void Watch(object o,object c,int spell,string key) {
    PLogI(key+".friend",GetIsFriend(o,c));
    PLogI(key+".enemy",GetIsEnemy(o,c));
    PLogI(key+".sameFaction",GetFactionEqual(o,c));
    PLogI(key+".reactionFriend",GetIsReactionTypeFriendly(o,c));
    PLogI(key+".reactionEnemy",GetIsReactionTypeHostile(o,c));
    PLogI(key+".repCaster",GetReputation(c,o));
    PLogI(key+".repTarget",GetReputation(o,c));
    PFxSnap(o);
    DelayCommand(6.0,Read(o,c,spell,key+".t6"));
    DelayCommand(18.0,Read(o,c,spell,key+".t18"));
}
void Cast(object c,int spell,int cls,int level,int meta,string key) {
    PLogI(key+".prepared",GetMemorizedSpellId(c,cls,level,0)==spell && GetMemorizedSpellReady(c,cls,level,0));
    AssignCommand(c,ActionCastSpellAtObject(spell,c,meta,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,cls));
    DelayCommand(6.0,PLogI(key+".consumed",!GetMemorizedSpellReady(c,cls,level,0)));
    int i;
    for(i=0;i<7;i++)Watch(GetLocalObject(c,"target"+IntToString(i)),c,spell,key+".target"+IntToString(i));
}
void Group(int index,int spell,object faction) {
    string key="spell."+IntToString(spell);
    float x=30.0+IntToFloat(index%3)*60.0;float y=30.0+IntToFloat(index/3)*60.0;
    int cls=spell==SPELL_MASS_HASTE?CLASS_TYPE_WIZARD:CLASS_TYPE_CLERIC;
    object c=Actor(cls==CLASS_TYPE_WIZARD?"nw_drowmage020":"nw_drowcler020",key+"caster",x,y);
    ChangeFaction(c,faction);
    object ally=Actor("nw_bandit006",key+"ally",x+2.0,y);
    object hench=Actor("nw_bandit006",key+"hench",x-2.0,y);
    object enemy=Actor("nw_bandit006",key+"enemy",x,y+2.0);
    object neutral=Actor("nw_bandit006",key+"neutral",x,y-2.0);
    object far=Actor("nw_bandit006",key+"far",x+16.0,y);
    object farEnemy=Actor("nw_bandit006",key+"farEnemy",x-16.0,y);
    PLogI(key+".neutralBefore",GetReputation(c,neutral));
    SetIsTemporaryFriend(ally,c);SetIsTemporaryFriend(c,ally);
    SetIsTemporaryFriend(far,c);SetIsTemporaryFriend(c,far);
    SetIsTemporaryEnemy(enemy,c);SetIsTemporaryEnemy(c,enemy);
    SetIsTemporaryEnemy(farEnemy,c);SetIsTemporaryEnemy(c,farEnemy);
    SetIsTemporaryNeutral(neutral,c);SetIsTemporaryNeutral(c,neutral);
    PLogI(key+".neutralAfter",GetReputation(c,neutral));
    AddHenchman(c,hench);Quiet(hench);
    SetLocalObject(c,"target0",c);SetLocalObject(c,"target1",ally);SetLocalObject(c,"target2",hench);
    SetLocalObject(c,"target3",enemy);SetLocalObject(c,"target4",neutral);SetLocalObject(c,"target5",far);SetLocalObject(c,"target6",farEnemy);
    PLogI(key+".master",GetMaster(hench)==c);
    if(spell==SPELL_HEALING_CIRCLE || spell==SPELL_MASS_HEAL) {
        int i;for(i=0;i<7;i++)SetCurrentHitPoints(GetLocalObject(c,"target"+IntToString(i)),1);
    }
    int meta=spell==SPELL_HEALING_CIRCLE?METAMAGIC_MAXIMIZE:METAMAGIC_NONE;
    int level=GetSpellLevelByClass(cls,spell)+(meta==METAMAGIC_MAXIMIZE?3:0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectBonusFeat(FEAT_MAXIMIZE_SPELL),c);
    SetMemorizedSpell(c,cls,level,0,spell,TRUE,meta);
    DelayCommand(9.0,Cast(c,spell,cls,level,meta,key));
}
void main() {
    object faction=Actor("nw_hen_sha_11","faction_anchor",190.0,190.0);
    Group(0,SPELL_BLESS,faction);Group(1,SPELL_PRAYER,faction);Group(2,SPELL_MASS_HASTE,faction);
    Group(3,SPELL_HEALING_CIRCLE,faction);Group(4,SPELL_MASS_HEAL,faction);
    DelayCommand(29.0,PDone());
}

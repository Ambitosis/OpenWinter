// Dragon breath timing: a red dragon (event scripts removed) breathes fire at a goblin, by an
// assigned ActionCastSpellAtObject. The breath's impact script is replaced by a logger, so the time from the
// assignment to the impact, how far the dragon walked first and how far from its target it stood are recorded,
// with the time the next queued command runs. Cases: the target at 3, 5, 8 and 12 m, hasted, an instant cast, the
// breath as the dragon's own ability (no cheat),
// Wing Buffet, a bandit breathing (cheat) at 3 to 12 m, the dragon at 4.6 and 4.8 m (short walks), and the dragon only walking to where it breathes from.
#include "parity_inc"
#include "parity_time"
void Finish(object c){PLogF(GetLocalString(c,"case")+".finish",PNow()-GetLocalFloat(c,"start"),.2);}
void Go(object c,int spell,int cheat,int instant)
{
    object t=GetLocalObject(c,"target");string k=GetLocalString(c,"case");
    PLogI(k+".seen",GetObjectSeen(t,c));PLogF(k+".distance",GetDistanceBetween(c,t),.05);
    SetLocalLocation(c,"from",GetLocation(c));SetLocalFloat(c,"start",PNow());
    AssignCommand(c,ActionCastSpellAtObject(spell,t,METAMAGIC_ANY,cheat,0,0,instant));
    AssignCommand(c,ActionDoCommand(Finish(c)));
}
void Arrived(object c){string k=GetLocalString(c,"case");PLogF(k+".finish",PNow()-GetLocalFloat(c,"start"),.2);PLogF(k+".moved",GetDistanceBetweenLocations(GetLocation(c),GetLocalLocation(c,"from")),.15);}
void Walk(object c)
{
    object t=GetLocalObject(c,"target");SetLocalLocation(c,"from",GetLocation(c));SetLocalFloat(c,"start",PNow());
    AssignCommand(c,ActionMoveToObject(t,TRUE,4.45));AssignCommand(c,ActionDoCommand(Arrived(c)));
}
void Report(object c){string k=GetLocalString(c,"case");PLogI(k+".impacts",GetLocalInt(c,"impacts"));PLogI(k+".has",GetHasSpell(SPELLABILITY_DRAGON_BREATH_FIRE,c));}
object Quiet(object o,int faction){int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");AssignCommand(o,ClearAllActions(TRUE));ChangeToStandardFaction(o,faction);SetAILevel(o,AI_LEVEL_NORMAL);SetPlotFlag(o,TRUE);return o;}
void Make(string k,int n,float gap,int spell,int cheat,int instant,int haste,string res="nw_drgred001")
{
    object a=GetObjectByTag("PARITY_AREA");float x=15.0+IntToFloat(n%5)*30.0;float y=15.0+IntToFloat(n/5)*35.0;
    object c=Quiet(CreateObject(OBJECT_TYPE_CREATURE,res,Location(a,Vector(x,y,0.0),0.0)),STANDARD_FACTION_DEFENDER);
    object t=Quiet(CreateObject(OBJECT_TYPE_CREATURE,"nw_goblina",Location(a,Vector(x+gap,y,0.0),180.0)),STANDARD_FACTION_HOSTILE);
    SetLocalString(c,"case",k);SetLocalObject(c,"target",t);
    if(haste)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);
    if(spell<0)DelayCommand(7.0+IntToFloat(n)*0.05,Walk(c));
    else DelayCommand(7.0+IntToFloat(n)*0.05,Go(c,spell,cheat,instant));DelayCommand(22.0,Report(c));
}
void main()
{
    PStart();
    Make("fire.d3",0,3.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE);
    Make("fire.d5",1,5.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE);
    Make("fire.d8",2,8.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE);
    Make("fire.d12",3,12.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE);
    Make("haste.d3",4,3.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,TRUE);
    Make("instant.d3",5,3.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,TRUE,FALSE);
    Make("own.d3",6,3.0,SPELLABILITY_DRAGON_BREATH_FIRE,FALSE,FALSE,FALSE);
    Make("wing.d3",7,3.0,246,TRUE,FALSE,FALSE);
    Make("haste.d8",8,8.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,TRUE);
    Make("bandit.d3",9,3.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE,"nw_bandit001");
    Make("bandit.d8",10,8.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE,"nw_bandit001");
    Make("bandit.d12",11,12.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE,"nw_bandit001");
    Make("walk.d12",12,12.0,-1,FALSE,FALSE,FALSE);
    Make("bandit.d32",13,3.2,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE,"nw_bandit001");
    Make("bandit.d35",14,3.5,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE,"nw_bandit001");
    Make("bandit.d4",15,4.0,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE,"nw_bandit001");
    Make("fire.d46",16,4.6,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE);
    Make("fire.d48",17,4.8,SPELLABILITY_DRAGON_BREATH_FIRE,TRUE,FALSE,FALSE);
    DelayCommand(23.0,PDone());
}

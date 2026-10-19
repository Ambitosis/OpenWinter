// Timing swings by the target's OnDamaged instead of OnPhysicalAttacked, on a nearly empty and on a populated server.
// Attackers are stripped nw_bandit001 (hostile, +20 to hit, AI normal), each
// against a passive immortal stripped bandit 1.5 m east that logs swings (OnPhysicalAttacked) and blows
// (OnDamaged, assigned to the attacker's latest swing).
//   two:   two attacks a round (SetBaseAttackBonus 2), made at load, attack ordered at 12 s.
//   haste: one attack, hasted at creation, attack at 12 s.
//   early: one attack, hasted at creation, attack at 2 s (the long first gap of round_clear).
// Within-round gap = second swing minus first, by swing (s) and by blow (d); first.* = the first round only.
// FILLERS idle stripped bandits (AI normal, commoner) stand in a 3 m grid elsewhere in the area.
#include "parity_inc"
#include "parity_time"
const int FILLERS = 300;
object Area(){return GetObjectByTag("PARITY_AREA");}
int nCell=0;
object Make(float x,float y,float f,int nFaction)
{
    object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(Area(),Vector(x,y,0.0),f));
    int ev;for(ev=5000;ev<=5012;ev++)SetEventScript(o,ev,"");
    SetAILevel(o,AI_LEVEL_NORMAL);ChangeToStandardFaction(o,nFaction);SetImmortal(o,TRUE);return o;
}
void Pair(string g,int nAttacks,int bHaste,float fOrder)
{
    float x=6.0+IntToFloat(nCell%6)*10.0,y=6.0+IntToFloat(nCell/6)*10.0;nCell++;
    object a=Make(x,y,0.0,STANDARD_FACTION_HOSTILE),t=Make(x+1.5,y,180.0,STANDARD_FACTION_COMMONER);
    SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"sm_atk");SetEventScript(t,EVENT_SCRIPT_CREATURE_ON_DAMAGED,"sm_dmg");
    SetBaseAttackBonus(nAttacks,a);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectAttackIncrease(20),a);
    if(bHaste)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),a);
    object m=GetModule();int n=GetLocalInt(m,g+".count");SetLocalObject(m,g+IntToString(n),a);SetLocalInt(m,g+".count",n+1);
    DelayCommand(fOrder,AssignCommand(a,ActionAttack(t)));
}
// Mean within-round gap (swing 2j to 2j+1) by marker sKind ("s" or "d"), rounds from nFrom to nTo.
void Gaps(string k,string g,string sKind,int nFrom,int nTo)
{
    object m=GetModule();int n=GetLocalInt(m,g+".count"),i,j,c=0;float sum=0.0,lo=99.0,hi=0.0;
    for(i=0;i<n;i++){object a=GetLocalObject(m,g+IntToString(i));int ns=GetLocalInt(a,"sn");
        for(j=nFrom;j<=nTo&&2*j+1<ns;j++){float f0=GetLocalFloat(a,sKind+IntToString(2*j)),f1=GetLocalFloat(a,sKind+IntToString(2*j+1));
            if(f0<=0.0||f1<=0.0)continue;float d=f1-f0;sum+=d;c++;if(d<lo)lo=d;if(d>hi)hi=d;}}
    if(c==0){PLog(k,"none");return;}
    PLogF(k,sum/IntToFloat(c),c<12?0.4:0.15);
    WriteTimestampedLogEntry("RAW|"+k+"|n="+IntToString(c)+" min="+FloatToString(lo,0,3)+" max="+FloatToString(hi,0,3));
}
void Filler(int i){object o=Make(64.0+IntToFloat(i%12)*3.0,4.0+IntToFloat(i/12)*3.0,0.0,STANDARD_FACTION_COMMONER);}
void main()
{
    PStart();int i;
    for(i=0;i<FILLERS;i++)Filler(i);
    for(i=0;i<6;i++){Pair("two",2,FALSE,12.0);Pair("haste",1,TRUE,12.0);Pair("early",1,TRUE,2.0);}
    string gs="two haste early ";
    while(gs!=""){int sp=FindSubString(gs," ");string g=GetStringLeft(gs,sp);gs=GetSubString(gs,sp+1,1000);
        DelayCommand(46.0,Gaps(g+".first.s",g,"s",0,0));DelayCommand(46.0,Gaps(g+".first.d",g,"d",0,0));
        DelayCommand(46.0,Gaps(g+".later.s",g,"s",1,99));DelayCommand(46.0,Gaps(g+".later.d",g,"d",1,99));}
    DelayCommand(47.0,PDone());
}

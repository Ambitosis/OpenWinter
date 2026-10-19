// When damage interrupts a spell. Wizards with Concentration 0, 20 or 37 (Constitution 10, no
// feats, 1000 hit points) cast an authored self spell (ConjTime 3 s, CastTime 1 s: committed at 3.0 s, released at
// 3.5 s) from a prepared slot, one every 0.3 s, and a bystander beside each deals magical damage at a set time. Per group: the share of casts whose
// impact ran and the share whose prepared spell was used up.
//   early/commit/recover: 30 damage (DC 41+) during conjuring, between commit and release, and after release.
//   double: two 25-point hits at once on Concentration 37 (each DC 36 passes; their sum would fail).
//   lvl1/lvl9: 20 damage on Concentration 20 for a level 1 and a level 9 spell (DC 31 against 39 if the level counts).
//   noconc: a spell with UseConcentration 0.  ent*: entangled casters (a flat DC 15 at commit, or 24 with the level).
//   clear*: ClearAllActions during conjuring and between commit and release, no damage.
//   *cheat: the same as cheat casts (bCheat TRUE: the spells.2da Innate level, 9).
#include "pcombat_inc"
#include "parity_time"
void Hit(object c,int d){AssignCommand(GetLocalObject(c,"hitter"),ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(d,DAMAGE_TYPE_MAGICAL),c));}
void Poll(object c,int n)
{
    string a=IntToString(GetCurrentAction(c));
    if(a!=GetLocalString(c,"last")){SetLocalString(c,"last",a);SetLocalString(c,"log",GetLocalString(c,"log")+a+"@"+FloatToString(PNow()-GetLocalFloat(c,"start"),0,2)+",");}
    if(n<70)DelayCommand(0.1,Poll(c,n+1));
}
void Cast(object c)
{
    int row=GetLocalInt(c,"row");SetLocalInt(c,"has0",GetHasSpell(row,c));SetLocalFloat(c,"start",PNow());
    AssignCommand(c,ActionCastSpellAtObject(row,c,METAMAGIC_ANY,GetLocalInt(c,"cheat")));
    Poll(c,0);
    float at=GetLocalFloat(c,"at");int d=GetLocalInt(c,"damage");
    if(d>0){DelayCommand(at,Hit(c,d));if(GetLocalInt(c,"double"))DelayCommand(at,Hit(c,d));}
    if(GetLocalInt(c,"clear"))DelayCommand(at,AssignCommand(c,ClearAllActions()));
}
void Count(string g,object c)
{
    object m=GetModule();
    WriteTimestampedLogEntry("RAW|"+g+"."+IntToString(GetLocalInt(m,g+".n"))+"|"+"has0="+IntToString(GetLocalInt(c,"has0"))+" has="+IntToString(GetHasSpell(GetLocalInt(c,"row"),c))
        +" impacts="+IntToString(GetLocalInt(c,"impacts"))+" at="+FloatToString(GetLocalFloat(c,"impactAt"),0,3)+" hp="+IntToString(GetCurrentHitPoints(c))
        +" dead="+IntToString(GetIsDead(c))+" act="+IntToString(GetCurrentAction(c))+" log="+GetLocalString(c,"log"));
    SetLocalInt(m,g+".n",GetLocalInt(m,g+".n")+1);
    if(GetLocalInt(c,"impacts")>0)SetLocalInt(m,g+".impact",GetLocalInt(m,g+".impact")+1);
    if(GetHasSpell(GetLocalInt(c,"row"),c)<GetLocalInt(c,"has0"))SetLocalInt(m,g+".spent",GetLocalInt(m,g+".spent")+1);
    if(GetLocalInt(c,"impacts")>0)SetLocalFloat(m,g+".at",GetLocalFloat(m,g+".at")+GetLocalFloat(c,"impactAt"));
}
void Group(string g,string res,int row,int damage,float at,int n,int dbl=FALSE,int ent=FALSE,int clr=FALSE,int cheat=FALSE)
{
    object m=GetModule();int base=GetLocalInt(m,"next");SetLocalInt(m,"next",base+n);
    int i;for(i=0;i<n;i++)
    {
        int k=base+i;object c=PSpawn(res,8.0+IntToFloat(k%10)*7.0,8.0+IntToFloat(k/10)*7.0);
        ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);SetLocalString(c,"group",g);
        object h=PSpawn("nw_bandit001",8.0+IntToFloat(k%10)*7.0,11.0+IntToFloat(k/10)*7.0);ChangeToStandardFaction(h,STANDARD_FACTION_COMMONER);
        SetPlotFlag(h,TRUE);SetLocalObject(c,"hitter",h);
        SetLocalInt(c,"row",row);SetLocalInt(c,"damage",damage);SetLocalFloat(c,"at",at);
        SetLocalInt(c,"double",dbl);SetLocalInt(c,"clear",clr);SetLocalInt(c,"cheat",cheat);
        if(ent)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectEntangle(),c);
        if(i==0)SetLocalObject(m,g+".first",c);
        float t=12.0+IntToFloat(k)*0.3;DelayCommand(t,Cast(c));DelayCommand(t+7.0,Count(g,c));
    }
    SetLocalString(m,"groups",GetLocalString(m,"groups")+g+" ");
}
void Report()
{
    object m=GetModule();string s=GetLocalString(m,"groups");
    while(s!=""){int p=FindSubString(s," ");string g=GetStringLeft(s,p);s=GetSubString(s,p+1,1000);
        int n=GetLocalInt(m,g+".n"),hit=GetLocalInt(m,g+".impact");
        object f=GetLocalObject(m,g+".first");
        PLogI(g+".conc",GetSkillRank(SKILL_CONCENTRATION,f));PLogI(g+".n",n);
        PLogRate(g+".impact",hit,n);PLogRate(g+".spent",GetLocalInt(m,g+".spent"),n);
        if(hit>0&&GetLocalInt(f,"damage")==0)PLogF(g+".impactAt",GetLocalFloat(m,g+".at")/IntToFloat(hit),0.2);}
    PDone();
}
// The first damage and the first cast of a run can stall nwserver for a second (a cast hit at 1.0 s then went on and
// released over a second late): one of each before the measured casts.
void WarmUp()
{
    object w=PSpawn("ccn_r37",80.0,80.0);ChangeToStandardFaction(w,STANDARD_FACTION_COMMONER);
    object h=PSpawn("nw_bandit001",80.0,83.0);ChangeToStandardFaction(h,STANDARD_FACTION_COMMONER);SetPlotFlag(h,TRUE);
    AssignCommand(w,ActionCastSpellAtObject(2,w,METAMAGIC_ANY,TRUE));
    DelayCommand(1.0,AssignCommand(h,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(5,DAMAGE_TYPE_MAGICAL),w)));
}
void main()
{
    DelayCommand(8.0,WarmUp());
    Group("double","ccn_r37",0,25,1.0,4,TRUE);
    Group("early","ccn_r0",0,30,1.0,4);
    Group("commit","ccn_r0",0,30,3.2,4);
    Group("recover","ccn_r0",0,30,3.8,4);
    Group("lvl1","ccn_r20",0,20,1.0,14);
    Group("lvl9","ccn_r20",1,20,1.0,14);
    Group("noconc","ccn_r0",2,30,1.0,4);
    Group("ent9","ccn_r0",1,0,0.0,14,FALSE,TRUE);
    Group("ent9c37","ccn_r37",1,0,0.0,4,FALSE,TRUE);
    Group("entnoconc","ccn_r0",2,0,0.0,4,FALSE,TRUE);
    Group("ent9cheat","ccn_r0",1,0,0.0,10,FALSE,TRUE,FALSE,TRUE);
    Group("lvl9cheat","ccn_r20",1,20,1.0,10,FALSE,FALSE,FALSE,TRUE);
    Group("clearconj","ccn_r0",0,0,1.0,3,FALSE,FALSE,TRUE);
    Group("clearcommit","ccn_r0",0,0,3.2,3,FALSE,FALSE,TRUE);
    DelayCommand(12.0+IntToFloat(GetLocalInt(GetModule(),"next"))*0.3+8.0,Report());
}

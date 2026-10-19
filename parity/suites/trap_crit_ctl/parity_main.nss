// A disarm in combat that misses by 11 or more, with its controls.
// Disarmers are rogue 10 fixtures (Disable Trap 10 + 2 synergy, no feats: Skill Mastery read back), immortal, not plot,
// each beside its own trap with DisarmDC = skill + 36 (every roll misses by 14 to 33). A hostile hitter deals 1 point of
// magical damage to the disarmer every 2 s from 1 s before the order to the end, so it stays in combat; GetIsInCombat is
// read when its disarm action ends (current action sampled every 0.1 s). Prelude faction table.
//   tcom:   trap trigger (CreateTrapAtLocation, size 1), trap faction Hostile, disarmer Commoner.
//   town:   the same, disarmer Hostile (the trap's own faction).
//   tplot:  as tcom, the disarmer also plot (as in trap_disarm_crit).
//   tcalm:  as tcom, no hitter (out of combat: takes 20).
//   tthr:   as tcom with DisarmDC = skill + 17 (rolls 17-20 succeed, roll r misses by 17 - r): the critical-failure threshold.
//   ccom:   plc_chest1 (Hostile) with a Hostile trap, disarmer Commoner.
//   chos:   plc_chest1 (Hostile) with a Defender trap, disarmer Hostile.
//   cplot:  as ccom, the disarmer also plot.
//   walk:   a Commoner walks through a Hostile trap trigger (control: the trap can fire on it). wplot: the same, plot.
// Trap events are kept on the module by the trap's tag (a one-shot trap trigger is destroyed when it goes off).
// Search split (detect mode, Search +50, DC 1, 2 m away, read after 12 s): plc_chest1 (Hostile) trapped with faction
// Commoner, searcher Commoner (split) or Merchant (ctrl).
#include "parity_inc"
#include "parity_time"
object Ar(){return GetObjectByTag("PARITY_AREA");}
location L(float x,float y){return Location(Ar(),Vector(x,y,0.0),0.0);}
object Actor(string sTag,string sRes,float x,float y,int nFaction)
{
    object o=CreateObject(OBJECT_TYPE_CREATURE,sRes,L(x,y),FALSE,sTag);
    int e;for(e=5000;e<=5012;e++)SetEventScript(o,e,"");ChangeToStandardFaction(o,nFaction);SetAILevel(o,AI_LEVEL_NORMAL);
    return o;
}
void Hit(object h,object a,int n)
{
    AssignCommand(h,ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(1,DAMAGE_TYPE_MAGICAL),a));
    if(n>0)DelayCommand(2.0,Hit(h,a,n-1));
}
void Watch(object a,int was,int n)
{
    int act=GetCurrentAction(a);
    if(was!=ACTION_INVALID&&act!=was&&GetLocalInt(a,"END")==0){SetLocalInt(a,"END",1);SetLocalInt(a,"COMBATEND",GetIsInCombat(a));SetLocalFloat(a,"TEND",PSince());}
    if(act!=ACTION_INVALID&&GetLocalFloat(a,"TSTART")==0.0)SetLocalFloat(a,"TSTART",PSince());
    if(n>0)DelayCommand(0.1,Watch(a,act,n-1));
}
void Case(string g,int i,int n)
{
    float x=5.0+IntToFloat(n%12)*7.0,y=5.0+IntToFloat(n/12)*8.0;string k=g+IntToString(i);
    int fac=(g=="town"||g=="chos")?STANDARD_FACTION_HOSTILE:STANDARD_FACTION_COMMONER;
    object t;
    if(GetStringLeft(g,1)=="c"){t=CreateObject(OBJECT_TYPE_PLACEABLE,"plc_chest1",L(x,y),FALSE,"C_"+k);
        CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE,t,g=="chos"?STANDARD_FACTION_DEFENDER:STANDARD_FACTION_HOSTILE,"tc_disarm","tc_trig");}
    else t=CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE,L(x,y),1.0,"C_"+k,STANDARD_FACTION_HOSTILE,"tc_disarm","tc_trig");
    object a=Actor("A_"+k,"tc_rogue",x-1.6,y,fac);SetImmortal(a,TRUE);
    if(g=="tplot"||g=="cplot"||g=="wplot")SetPlotFlag(a,TRUE);
    SetLocalObject(a,"T",t);
    if(GetStringLeft(g,1)=="w"){SetTrapDetectable(t,FALSE);return;}
    SetTrapDisarmDC(t,GetSkillRank(SKILL_DISABLE_TRAP,a)+(g=="tthr"?17:36));SetTrapDetectedBy(t,a,TRUE);
    if(g!="tcalm"){object h=Actor("H_"+k,"nw_bandit001",x-1.6,y+3.0,fac==STANDARD_FACTION_HOSTILE?STANDARD_FACTION_DEFENDER:STANDARD_FACTION_HOSTILE);
        SetPlotFlag(h,TRUE);SetLocalObject(a,"H",h);}
}
void Go(string g,int i)
{
    object a=GetObjectByTag("A_"+g+IntToString(i));object t=GetLocalObject(a,"T");
    if(GetStringLeft(g,1)=="w"){vector p=GetPosition(a);AssignCommand(a,ActionMoveToLocation(L(p.x+3.2,p.y)));return;}
    object h=GetLocalObject(a,"H");if(GetIsObjectValid(h))Hit(h,a,6);
    DelayCommand(1.0,AssignCommand(a,ActionUseSkill(SKILL_DISABLE_TRAP,t,0)));DelayCommand(1.0,Watch(a,ACTION_INVALID,120));
}
void Search(string k,int nSearcher,float x)
{
    object c=CreateObject(OBJECT_TYPE_PLACEABLE,"plc_chest1",L(x,98.0),FALSE,"S_"+k);
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE,c,STANDARD_FACTION_COMMONER);SetTrapDetectDC(c,1);
    object s=Actor("SR_"+k,"tc_rogue",x,96.0,nSearcher);SetPlotFlag(s,TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSkillIncrease(SKILL_SEARCH,50),s);SetActionMode(s,ACTION_MODE_DETECT,TRUE);
}
void Report(string g,int n)
{
    int i,nFire=0,nDis=0,nComb=0,nSm=0;
    for(i=0;i<n;i++)
    {
        string k=g+IntToString(i);object a=GetObjectByTag("A_"+k);object t=GetLocalObject(a,"T");string ev=GetLocalString(GetModule(),"EV_C_"+k);
        nFire+=FindSubString(ev,"trig")>=0;nDis+=FindSubString(ev,"disarm")>=0;nComb+=GetLocalInt(a,"COMBATEND");nSm+=GetHasFeat(FEAT_SKILL_MASTERY,a);
        WriteTimestampedLogEntry("RAW|"+k+"|ev="+ev+" start="+FloatToString(GetLocalFloat(a,"TSTART"),0,2)+" end="+FloatToString(GetLocalFloat(a,"TEND"),0,2)
            +" combatEnd="+IntToString(GetLocalInt(a,"COMBATEND"))+" trapped="+IntToString(GetIsTrapped(t))+" skill="+IntToString(GetSkillRank(SKILL_DISABLE_TRAP,a))
            +" dc="+IntToString(GetTrapDisarmDC(t))+" rep(trapObj->a)="+IntToString(GetReputation(t,a))+" hp="+IntToString(GetCurrentHitPoints(a)));
    }
    PLogI(g+".n",n);
    if(g=="tthr"){PLog(g+".fired",IntToString(nFire)+"|~8");PLog(g+".disarmed",IntToString(nDis)+"|~8");}else{PLogI(g+".fired",nFire);PLogI(g+".disarmed",nDis);}
    PLogI(g+".combatEnd",nComb);PLogI(g+".skillMastery",nSm);
}
void SearchRead(string k)
{
    object s=GetObjectByTag("SR_"+k),c=GetObjectByTag("S_"+k);
    PLogI("search."+k+".found",GetTrapDetectedBy(c,s));
    WriteTimestampedLogEntry("RAW|search."+k+"|found="+IntToString(GetTrapDetectedBy(c,s))+" rep(chest->s)="+IntToString(GetReputation(c,s))+" mode="+IntToString(GetActionMode(s,ACTION_MODE_DETECT)));
}
void main()
{
    PStart();int n=0,i;
    for(i=0;i<10;i++)Case("tcom",i,n++);
    for(i=0;i<6;i++){Case("town",i,n++);Case("tplot",i,n++);Case("tcalm",i,n++);Case("chos",i,n++);}
    for(i=0;i<10;i++)Case("ccom",i,n++);
    for(i=0;i<48;i++)Case("tthr",i,n++);
    for(i=0;i<4;i++){Case("walk",i,n++);Case("wplot",i,n++);}
    for(i=0;i<6;i++)Case("cplot",i,n++);
    for(i=0;i<3;i++){Search("split"+IntToString(i),STANDARD_FACTION_COMMONER,5.0+IntToFloat(i)*8.0);Search("ctrl"+IntToString(i),STANDARD_FACTION_MERCHANT,45.0+IntToFloat(i)*8.0);}
    string gs="tcom:10 town:6 tplot:6 tcalm:6 chos:6 ccom:10 tthr:48 walk:4 wplot:4 cplot:6 ";
    while(gs!=""){int sp=FindSubString(gs," ");string tok=GetStringLeft(gs,sp);gs=GetSubString(gs,sp+1,1000);
        int c=FindSubString(tok,":");string g=GetStringLeft(tok,c);int m=StringToInt(GetSubString(tok,c+1,9));
        for(i=0;i<m;i++)DelayCommand(10.0,Go(g,i));DelayCommand(32.0,Report(g,m));}
    for(i=0;i<3;i++){DelayCommand(32.0,SearchRead("split"+IntToString(i)));DelayCommand(32.0,SearchRead("ctrl"+IntToString(i)));}
    DelayCommand(33.0,PDone());
}

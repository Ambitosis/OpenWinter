// Who makes an attack of opportunity against a caster, and when it lands.
// Per case: caster C (stripped bandit, Concentration 40, hostile, immortal, 1000 hit points) and threatener V (stripped
// commoner bandit with a longsword) 1.5 m west of C facing it. A prodder P 6 m north cheat-casts Magic Missile at V at
// 7 s (after its first perception pass), so V is in combat and has seen C long before C's cast. C cheat-casts Ray of Frost (an observer script) at P at 12 s.
//   imm:    V immortal (as in cast_provoke).               plot: V plot (flagged at 11 s, after the missiles put it in combat).
//   fight:  V immortal and attacking C.                    fightnc: as fight, C never casts (control: swings per round).
//   plotfight: V plot and attacking C.   fightself: as fight, C's Ray at V itself.   fightslot: as fight, C a wizard
//   (nw_bandit005) casting Magic Missile from its class slot at P.   fightmm: as fight, a cheat Magic Missile at P.
//   fighthos: as fight with the factions swapped (V Hostile, C and P Defender).   fightaway: as fight, V made facing away
//   from C (as ai_cast_rounds' attackers were).
// The fighting groups' V is ordered to attack C at 9 s.
// Logged: the cast's start (C's current action turning to "cast spell", sampled every 0.05 s), every swing at C
// (C's OnPhysicalAttacked) and blow (C's OnDamaged), and V's OnEndCombatRound, as RAW notes (see Report).
#include "parity_inc"
#include "parity_time"
object Area(){return GetObjectByTag("PARITY_AREA");}
object Mk(string tag,float x,float y,float facing,int faction,string res="nw_bandit001")
{
    object o=CreateObject(OBJECT_TYPE_CREATURE,res,Location(Area(),Vector(x,y,0.0),facing),FALSE,tag);
    int e;for(e=5000;e<=5012;e++)SetEventScript(o,e,"");SetAILevel(o,AI_LEVEL_NORMAL);ChangeToStandardFaction(o,faction);SetImmortal(o,TRUE);
    return o;
}
void Poll(object c,int n)
{
    if(GetCurrentAction(c)==ACTION_CASTSPELL){SetLocalFloat(c,"cs",PSince());return;}
    if(n>0)DelayCommand(0.05,Poll(c,n-1));
}
void Cast(object c,object t){AssignCommand(c,ActionCastSpellAtObject(SPELL_RAY_OF_FROST,t,METAMAGIC_ANY,TRUE));Poll(c,120);}
void CastMM(object c,object t,int bCheat){AssignCommand(c,ActionCastSpellAtObject(SPELL_MAGIC_MISSILE,t,METAMAGIC_ANY,bCheat));Poll(c,120);}
void Prod(object p,object t){AssignCommand(p,ActionCastSpellAtObject(SPELL_MAGIC_MISSILE,t,METAMAGIC_ANY,TRUE));}
void Plot(object v){SetPlotFlag(v,TRUE);}
void Probe(object c,object v){SetLocalInt(c,"vcombat",GetIsInCombat(v));SetLocalInt(c,"vsees",GetObjectSeen(c,v));SetLocalInt(c,"vplot",GetPlotFlag(v));}
// The first time in "t t t" at or after f0 (and before f1), minus f0; -1 if none.
float First(string s,float f0,float f1)
{
    while(s!=""){int p=FindSubString(s," ");float t=StringToFloat(GetStringLeft(s,p));s=GetSubString(s,p+1,10000);
        if(t>=f0-0.01&&t<f1)return t-f0;}
    return -1.0;
}
int Count(string s,float f0,float f1)
{
    int n=0;
    while(s!=""){int p=FindSubString(s," ");float t=StringToFloat(GetStringLeft(s,p));s=GetSubString(s,p+1,10000);
        if(t>=f0&&t<f1)n++;}
    return n;
}
void Case(string g,int i,int n)
{
    float x=8.0+IntToFloat(n%6)*12.0,y=8.0+IntToFloat(n/6)*12.0;
    string k=g+IntToString(i);
    int cf=g=="fighthos"?STANDARD_FACTION_DEFENDER:STANDARD_FACTION_HOSTILE,vf=g=="fighthos"?STANDARD_FACTION_HOSTILE:STANDARD_FACTION_COMMONER;
    object c=Mk(k,x,y,180.0,cf,g=="fightslot"?"al_wiz":"al_caster");
    SetEventScript(c,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"al_atk");SetEventScript(c,EVENT_SCRIPT_CREATURE_ON_DAMAGED,"al_dmg");
    object v=Mk(k+"V",x-1.5,y,g=="fightaway"?180.0:0.0,vf);
    SetEventScript(v,EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND,"al_eor");SetLocalObject(c,"v",v);
    object p=Mk(k+"P",x,y+6.0,270.0,cf);
    DelayCommand(7.0,Prod(p,v));
    if(g=="plot")DelayCommand(11.0,Plot(v));
    if(g=="plotfight")Plot(v);
    if(GetStringLeft(g,5)=="fight"||g=="plotfight")DelayCommand(9.0,AssignCommand(v,ActionAttack(c)));
    DelayCommand(11.9,Probe(c,v));
    if(g=="fightself")DelayCommand(12.0,Cast(c,v));
    else if(g=="fightslot")DelayCommand(12.0,CastMM(c,p,FALSE));
    else if(g=="fightmm")DelayCommand(12.0,CastMM(c,p,TRUE));
    else if(g!="fightnc")DelayCommand(12.0,Cast(c,p));
    SetLocalString(GetModule(),"cases",GetLocalString(GetModule(),"cases")+k+" ");
}
// The time in "t t t" just at or before f (-1 if none).
float Before(string s,float f)
{
    float r=-1.0;
    while(s!=""){int p=FindSubString(s," ");float t=StringToFloat(GetStringLeft(s,p));s=GetSubString(s,p+1,10000);if(t<=f)r=t;}
    return r;
}
// The n-th (0-based) time in "t t t" at or after f0 and before f1, or -1.
float Nth(string s,float f0,float f1,int n)
{
    while(s!=""){int p=FindSubString(s," ");float t=StringToFloat(GetStringLeft(s,p));s=GetSubString(s,p+1,10000);
        if(t>=f0&&t<f1){if(n==0)return t;n--;}}
    return -1.0;
}
// Idle groups: whether a swing came within 3 s of the cast's start and when; the blow's lag behind it.
// Fighting groups: the swings in the threatener's round that was running when the cast started (from its last
// OnEndCombatRound before the cast to the next), and when its second swing came after the cast's start.
// The times vary from run to run (nwserver: the idle swing 0.09-0.47 s after the cast's start with the prod at 5 s, then
// 1.0-1.4 s on average in three runs with it at 7 s; the fighting one's extra swing anywhere from at once to 1.6 s): wide
// tolerances. A group of four sometimes has one round without the extra swing (fightmm 7 of 8).
// The count of swings in the round is the reading that matters.
void Report(string g,int n)
{
    int i,nAoo=0,nCast=0,nComb=0,nSee=0,nSw=0,nX=0,nB=0;float fA=0.0,fX=0.0,fB=0.0;
    int fighting=GetStringLeft(g,5)=="fight"||g=="plotfight";
    for(i=0;i<n;i++)
    {
        object c=GetObjectByTag(g+IntToString(i));object v=GetLocalObject(c,"v");
        float cs=GetLocalFloat(c,"cs");if(g=="fightnc")cs=12.05;
        string atk=GetLocalString(c,"atk"),dmg=GetLocalString(c,"dmg"),eor=GetLocalString(v,"eor");
        WriteTimestampedLogEntry("RAW|"+GetTag(c)+"|cast="+FloatToString(cs,0,2)+" atk="+atk+" dmg="+dmg+" eor="+eor
            +" combat="+IntToString(GetLocalInt(c,"vcombat"))+" sees="+IntToString(GetLocalInt(c,"vsees"))+" plot="+IntToString(GetLocalInt(c,"vplot")));
        if(cs>0.0)nCast++;
        nComb+=GetLocalInt(c,"vcombat");nSee+=GetLocalInt(c,"vsees");
        if(fighting)
        {
            float e0=Before(eor,cs),e1=Nth(eor,cs,99.0,0);if(e1<0.0)e1=99.0;
            nSw+=Count(atk,e0,e1);
            float x=Nth(atk,e0,e1,1);if(x>=0.0){nX++;fX+=x-cs;}
            continue;
        }
        float a=First(atk,cs,cs+3.0);
        if(a>=0.0){nAoo++;fA+=a-0.0;float d=First(dmg,cs+a,cs+a+1.0);if(d>=0.0){nB++;fB+=d;}}
    }
    PLogI(g+".n",n);PLogI(g+".cast",nCast);PLogI(g+".combat",nComb);PLogI(g+".sees",nSee);
    // fightaway: on nwserver two of four made no extra swing and two did after a late first swing (they had turned);
    // OpenWinter turns none: a tolerance of 2.
    if(fighting){if(g=="fightaway")PLog(g+".roundSwings",IntToString(nSw)+"|~3");else if(n==4)PLog(g+".roundSwings",IntToString(nSw)+"|~1");else PLogI(g+".roundSwings",nSw);
        if(g=="fightaway")return;if(nX>0)PLogF(g+".secondSwingAt",fX/IntToFloat(nX),2.0);return;}
    PLogI(g+".swing3s",nAoo);
    if(nAoo>0)PLogF(g+".swingAt",fA/IntToFloat(nAoo),1.2);
    if(nB>0)PLogF(g+".blowLag",fB/IntToFloat(nB),0.2);
}
void main()
{
    PStart();
    int n=0,i;
    for(i=0;i<6;i++){Case("imm",i,n++);Case("plot",i,n++);Case("fight",i,n++);Case("fightnc",i,n++);}
    for(i=0;i<4;i++)Case("plotfight",i,n++);
    for(i=0;i<4;i++){Case("fightself",i,n++);Case("fightslot",i,n++);Case("fightmm",i,n++);Case("fighthos",i,n++);Case("fightaway",i,n++);}
    DelayCommand(30.0,Report("imm",6));DelayCommand(30.0,Report("plot",6));DelayCommand(30.0,Report("fight",6));
    DelayCommand(30.0,Report("fightnc",6));DelayCommand(30.0,Report("plotfight",4));
    DelayCommand(30.0,Report("fightself",4));DelayCommand(30.0,Report("fightslot",4));DelayCommand(30.0,Report("fightmm",4));DelayCommand(30.0,Report("fighthos",4));DelayCommand(30.0,Report("fightaway",4));
    DelayCommand(31.0,PDone());
}

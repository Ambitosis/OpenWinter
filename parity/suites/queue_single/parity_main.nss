// How often a long scroll use gets a single gap instead of a doubled one (expected:
// mostly 2 x D, now and then D, the single share for CastTime 4000 about twice that of 3500, 5000 and 7000),
// and whether instant casts skip the round wait that cheat casts keep. Six casters per CastTime (3450, 3500, 4000,
// 5000, 7000 ms; D = CastTime + 2500) each queue seven uses of a scroll; every impact is logged.
//   instant: a cheat cast (ConjTime 1500, CastTime 1000, D = 3000) then three queued instant casts (bInstantSpell).
//   cheat:   the same cast then three queued cheat casts.          inst0: three instant casts from idle.
#include "parity_inc"
#include "parity_time"
int Uses(){return 7;}
void UseItem(object c){object it=GetLocalObject(c,"item");itemproperty ip=GetFirstItemProperty(it);AssignCommand(c,ActionUseItemOnObject(it,ip,c));}
void Start(object c)
{
    SetLocalFloat(c,"start",PNow());int i;string m=GetLocalString(c,"mode");
    if(m=="scroll"){for(i=0;i<Uses();i++)UseItem(c);return;}
    if(m!="inst0")AssignCommand(c,ActionCastSpellAtObject(5,c,METAMAGIC_ANY,TRUE));
    for(i=0;i<3;i++)AssignCommand(c,ActionCastSpellAtObject(5,c,METAMAGIC_ANY,TRUE,0,PROJECTILE_PATH_TYPE_DEFAULT,m!="cheat"));
}
object Setup(string name,int n,string mode,int sub)
{
    object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_elfmage015",Location(GetObjectByTag("PARITY_AREA"),Vector(6.0+IntToFloat(n%8)*11.0,6.0+IntToFloat(n/8)*11.0,0.0),0.0));
    SetTag(c,name);SetAILevel(c,AI_LEVEL_NORMAL);SetPlotFlag(c,TRUE);int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");
    ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);SetLocalString(c,"mode",mode);
    if(mode=="scroll")
    {
        object it=CreateItemOnObject("nw_it_sparscr104",c,Uses());
        itemproperty old=GetFirstItemProperty(it);while(GetIsItemPropertyValid(old)){RemoveItemProperty(it,old);old=GetFirstItemProperty(it);}
        AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyCastSpell(sub,1),it);SetLocalObject(c,"item",it);
    }
    DelayCommand(10.0+IntToFloat(n%10)*0.13,Start(c));
    return c;
}
// Gaps between successive impacts of the casters named p0..p5: count, singles (under 1.5 x D) and doubles.
void Count(string p,float fD)
{
    int i,n=0,ns=0,nd=0;string all="";
    for(i=0;i<6;i++)
    {
        object c=GetObjectByTag(p+IntToString(i));string s=GetLocalString(c,"impacts");all+=s+"| ";float prev=-1.0;
        while(s!=""){int q=FindSubString(s," ");float t=StringToFloat(GetStringLeft(s,q));s=GetSubString(s,q+1,10000);
            if(prev>=0.0){float g=t-prev;n++;if(g<1.5*fD)ns++;else nd++;}prev=t;}
    }
    WriteTimestampedLogEntry("RAW|"+p+"|"+all);
    PLogI(p+".gaps",n);// Singles vary between nwserver runs (CastTime 7000: 0 and 8 of 36): a wide tolerance.
    PLog(p+".single",IntToString(ns)+"|~9");PLog(p+".double",IntToString(nd)+"|~9");
}
void Seq(string k){object c=GetObjectByTag(k);
    string s=GetLocalString(c,"impacts");int i=0;
    while(s!=""){int q=FindSubString(s," ");PLogF(k+".t"+IntToString(i),StringToFloat(GetStringLeft(s,q)),0.3);s=GetSubString(s,q+1,10000);i++;}
    PLogI(k+".impacts",i);}
void main()
{
    int i,n=0;
    for(i=0;i<6;i++){string c=IntToString(i);
        Setup("s3450_"+c,n++,"scroll",66);Setup("s3500_"+c,n++,"scroll",67);Setup("s4000_"+c,n++,"scroll",68);
        Setup("s5000_"+c,n++,"scroll",69);Setup("s7000_"+c,n++,"scroll",70);}
    Setup("instant",n++,"instant",0);Setup("cheat",n++,"cheat",0);Setup("inst0",n++,"inst0",0);
    DelayCommand(150.0,Count("s3450_",5.95));DelayCommand(150.0,Count("s3500_",6.0));DelayCommand(150.0,Count("s4000_",6.5));
    DelayCommand(150.0,Count("s5000_",7.5));DelayCommand(150.0,Count("s7000_",9.5));
    DelayCommand(150.0,Seq("instant"));DelayCommand(150.0,Seq("cheat"));DelayCommand(150.0,Seq("inst0"));
    DelayCommand(151.0,PDone());
}

// What a failed damage Concentration check costs (expected: a plain copy of the spell from
// the class of the creature's previous committed cast; nothing on a creature's first cast). Wizards with
// Concentration 0 cast an authored self spell (ConjTime 3 s, UseConcentration 1, level 1) and take 30 magical damage
// 1 s in (DC 41: always fails). Every wizard slot (level 1 and 3) is read before and after as spell:ready:metamagic.
//   fresh:    two plain copies, first cast since creation.
//   after:    the same, after a completed cast of another wizard spell (NoConc, level 1).
//   twice:    two interrupted casts in a row, no completed cast (an interrupted cast doesn't count).
//   meta:     a plain and an empowered copy, after a completed cast; the empowered copy is cast.
//   metaonly: only an empowered copy, after a completed cast.
#include "parity_inc"
#include "parity_time"
string Slots(object c)
{
    string s="";int l,i;
    for(l=1;l<=3;l+=2)for(i=0;i<GetMemorizedSpellCountByLevel(c,CLASS_TYPE_WIZARD,l);i++)
        s+=IntToString(l)+":"+IntToString(GetMemorizedSpellId(c,CLASS_TYPE_WIZARD,l,i))+":"+IntToString(GetMemorizedSpellReady(c,CLASS_TYPE_WIZARD,l,i))
          +":"+IntToString(GetMemorizedSpellMetaMagic(c,CLASS_TYPE_WIZARD,l,i))+" ";
    return s;
}
void Hit(object c){AssignCommand(GetLocalObject(c,"hitter"),ApplyEffectToObject(DURATION_TYPE_INSTANT,EffectDamage(30,DAMAGE_TYPE_MAGICAL),c));}
void Cast(object c,int row,int meta,int hit)
{
    SetLocalString(c,"log",GetLocalString(c,"log")+"cast"+IntToString(row)+"@"+FloatToString(PSince(),0,2)+"["+Slots(c)+"] ");
    AssignCommand(c,ActionCastSpellAtObject(row,c,meta));
    if(hit)DelayCommand(1.0,Hit(c));
}
void Case(string g,int i,int n,string res)
{
    object c=CreateObject(OBJECT_TYPE_CREATURE,res,Location(GetObjectByTag("PARITY_AREA"),Vector(6.0+IntToFloat(n%8)*9.0,6.0+IntToFloat(n/8)*9.0,0.0),0.0),FALSE,g+IntToString(i));
    int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");SetAILevel(c,AI_LEVEL_NORMAL);ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);
    object h=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(6.0+IntToFloat(n%8)*9.0,9.0+IntToFloat(n/8)*9.0,0.0),0.0));
    for(e=5000;e<=5012;e++)SetEventScript(h,e,"");ChangeToStandardFaction(h,STANDARD_FACTION_COMMONER);SetPlotFlag(h,TRUE);SetLocalObject(c,"hitter",h);
    float t=12.0+IntToFloat(n)*0.2;
    int meta=(g=="meta"||g=="metaonly")?METAMAGIC_EMPOWER:METAMAGIC_NONE;
    if(g=="fresh")DelayCommand(t+8.0,Cast(c,0,meta,TRUE));
    else if(g=="twice"){DelayCommand(t,Cast(c,0,meta,TRUE));DelayCommand(t+8.0,Cast(c,0,meta,TRUE));}
    else{DelayCommand(t,Cast(c,2,METAMAGIC_NONE,FALSE));DelayCommand(t+8.0,Cast(c,0,meta,TRUE));}
    DelayCommand(t+16.0,SetLocalString(c,"after",Slots(c)));
}
void Report(string g,int n)
{
    int i;
    for(i=0;i<n;i++){object c=GetObjectByTag(g+IntToString(i));
        WriteTimestampedLogEntry("RAW|"+GetTag(c)+"|"+GetLocalString(c,"log")+"end["+GetLocalString(c,"after")+"] impacts="+IntToString(GetLocalInt(c,"impacts")));
        PLog(GetTag(c)+".after",GetLocalString(c,"after"));PLogI(GetTag(c)+".impacts",GetLocalInt(c,"impacts"));}
}
void main()
{
    PStart();int n=0,i;
    for(i=0;i<4;i++){Case("fresh",i,n++,"cpv_plain");Case("after",i,n++,"cpv_plain");Case("twice",i,n++,"cpv_plain");
        Case("meta",i,n++,"cpv_meta");Case("metaonly",i,n++,"cpv_metaonly");}
    string gs="fresh after twice meta metaonly ";
    while(gs!=""){int sp=FindSubString(gs," ");DelayCommand(48.0,Report(GetStringLeft(gs,sp),4));gs=GetSubString(gs,sp+1,1000);}
    DelayCommand(49.0,PDone());
}

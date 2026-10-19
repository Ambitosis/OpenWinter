#include "parity_inc"
void Clear(object c,int cls){int l;int n;for(l=0;l<10;l++)for(n=0;n<GetMemorizedSpellCountByLevel(c,cls,l);n++)SetMemorizedSpellReady(c,cls,l,n,FALSE);}
void Case(object c,string key,int cls,int spell,int level,int meta,int marker,int prepared=-1){
 Clear(c,cls);if(prepared<0)prepared=spell;
 SetMemorizedSpell(c,cls,level,0,prepared,TRUE,meta,marker);
 PLogI(key+".stored",GetMemorizedSpellId(c,cls,level,0));
 PLogI(key+".ready",GetMemorizedSpellReady(c,cls,level,0));
 PLogI(key+".count",GetHasSpell(spell,c));
 SetMemorizedSpellReady(c,cls,level,0,FALSE);PLogI(key+".spent",GetHasSpell(spell,c));
}
void Group(object c,string key,int cls,int spell,int level){
 int m;int marker;for(m=0;m<8;m++){int meta=0;if(m==1)meta=1;if(m==2)meta=2;if(m==3)meta=4;if(m==4)meta=8;if(m==5)meta=16;if(m==6)meta=32;if(m==7)meta=255;
 for(marker=0;marker<2;marker++)Case(c,key+"."+IntToString(level)+"."+IntToString(meta)+"."+IntToString(marker),cls,spell,level,meta,marker);}
}
void Setup(string key,string profile,int cls,int spell){
 object c=CreateObject(OBJECT_TYPE_CREATURE,profile,PCentre());
 PLogI(key+".classlevel",GetLevelByClass(cls,c));PLogI(key+".domain1",GetDomain(c,1,cls));PLogI(key+".domain2",GetDomain(c,2,cls));
 int l;for(l=0;l<10;l++)DelayCommand(0.1,Group(c,key,cls,spell,l));DelayCommand(2.0,DestroyObject(c));
}
void Aggregate(){
 object c=CreateObject(OBJECT_TYPE_CREATURE,"sc_multi",PCentre());Clear(c,2);Clear(c,10);
 SetMemorizedSpell(c,2,1,0,102,TRUE,0,1);SetMemorizedSpell(c,10,1,0,102,TRUE,0,0);SetMemorizedSpell(c,10,2,0,102,TRUE,2,0);
 PLogI("aggregate.all",GetHasSpell(102,c));SetMemorizedSpellReady(c,10,2,0,FALSE);PLogI("aggregate.two",GetHasSpell(102,c));
 SetMemorizedSpellReady(c,2,1,0,FALSE);PLogI("aggregate.one",GetHasSpell(102,c));SetMemorizedSpellReady(c,10,1,0,FALSE);PLogI("aggregate.empty",GetHasSpell(102,c));DestroyObject(c);
}
void Convert(int evil){
 object c=CreateObject(OBJECT_TYPE_CREATURE,"sc_healing",PCentre());AdjustAlignment(c,ALIGNMENT_GOOD,100,FALSE);if(evil)AdjustAlignment(c,ALIGNMENT_EVIL,100,FALSE);
 int l;int m;int marker;for(l=1;l<6;l++)for(m=0;m<3;m++)for(marker=0;marker<2;marker++)Case(c,"convert."+IntToString(evil)+"."+IntToString(l)+"."+IntToString(m)+"."+IntToString(marker),2,35,l,m,marker,6);DestroyObject(c);
}
void Raw(int level){object c=CreateObject(OBJECT_TYPE_CREATURE,"sc_animal",PCentre());int m;for(m=0;m<256;m++)Case(c,"raw."+IntToString(level)+"."+IntToString(m),2,186,level,m,0);DestroyObject(c);}
void main(){
 DelayCommand(0.1,Setup("allfeats","sc_allfeats",2,62));
 DelayCommand(0.1,Setup("partial","sc_partial",2,62));
 DelayCommand(0.1,Setup("cantrip","sc_cantrip",10,144));
 DelayCommand(0.1,Setup("magic","sc_magic",2,102));
 DelayCommand(0.1,Setup("other","sc_other",2,102));
 DelayCommand(0.1,Setup("travel","sc_travel",2,62));
 DelayCommand(0.1,Setup("reverse","sc_reverse",2,62));
 DelayCommand(0.1,Setup("animal","sc_animal",2,186));
 DelayCommand(0.1,Setup("knowledge","sc_knowledge",2,186));
 DelayCommand(0.1,Setup("both","sc_both",2,186));
 DelayCommand(0.1,Setup("duplicate","sc_duplicate",2,186));
 DelayCommand(0.1,Setup("healing","sc_healing",2,35));
 DelayCommand(0.1,Setup("heal","sc_heal",2,79));
 DelayCommand(0.1,Setup("identify","sc_identify",2,86));
 DelayCommand(0.1,Setup("sun","sc_sun",2,156));
 DelayCommand(0.1,Setup("strength","sc_strength",2,42));
 DelayCommand(0.1,Setup("stoneskin","sc_stoneskin",2,172));
 DelayCommand(0.1,Setup("haste","sc_haste",2,78));
 DelayCommand(0.1,Setup("druid","sc_druid",3,35));
 DelayCommand(0.1,Setup("wizard","sc_wizard",10,102));
 DelayCommand(0.1,Setup("undead","sc_undead",10,30));
 DelayCommand(0.1,Setup("inflict","sc_inflict",2,434));
 DelayCommand(0.5,Aggregate());DelayCommand(0.5,Convert(0));DelayCommand(0.5,Convert(1));
 DelayCommand(0.5,Raw(3));DelayCommand(0.5,Raw(4));DelayCommand(0.5,Raw(5));
 DelayCommand(3.0,PDone());
}

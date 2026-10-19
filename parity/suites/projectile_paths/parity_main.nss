// Spell projectile flight time by spells.2da ProjType: bounce, linked, an empty ProjType,
// the logarithm's pole just above e^(-2/3) = 0.5134 m, and a script path type above 4. Each stationary caster
// first casts a no-projectile control at the same spot to time its release, then the projectile.
#include "parity_inc"
#include "parity_time"
void Finish(object c){PLogF(GetLocalString(c,"case")+".finish"+IntToString(GetLocalInt(c,"wave")),PNow()-GetLocalFloat(c,"start"),.1);}
void Cast(object c,int spell,int path){SetLocalInt(c,"wave",spell==0?0:1);SetLocalInt(c,"hits",0);SetLocalFloat(c,"start",PNow());AssignCommand(c,ActionCastSpellAtLocation(spell,GetLocalLocation(c,"target"),METAMAGIC_NONE,TRUE,path));AssignCommand(c,ActionDoCommand(Finish(c)));}
void Verify(object c,int wave){PLogI(GetLocalString(c,"case")+".hits"+IntToString(wave),GetLocalInt(c,"hits"));}
void Make(string k,int col,int row,int spell,int path,float distance)
{
    object area=GetObjectByTag("PARITY_AREA");float x=10.0+IntToFloat(col)*35.0;float y=12.0+IntToFloat(row)*12.0;
    object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(area,Vector(x,y,0.0),0.0));
    int i;for(i=5000;i<=5013;i++)SetEventScript(c,i,"");SetAILevel(c,AI_LEVEL_NORMAL);SetPlotFlag(c,TRUE);
    SetLocalLocation(c,"target",Location(area,Vector(x+distance,y,0.0),0.0));SetLocalString(c,"case",k);
    PLogI(k+".valid",GetIsObjectValid(c));PLogF(k+".distance",GetDistanceBetweenLocations(GetLocation(c),GetLocalLocation(c,"target")),.001);
    float offset=IntToFloat(col*4+row)*.03;
    DelayCommand(3.0+offset,Cast(c,0,0));DelayCommand(9.0+offset,Verify(c,0));
    DelayCommand(11.0+offset,Cast(c,spell,path));DelayCommand(100.0+offset,Verify(c,1));
}
void main()
{
    Make("bounce.d1",0,0,1,0,1.0);Make("bounce.d5",0,1,1,0,5.0);Make("bounce.d20",0,2,1,0,20.0);
    Make("linked.d1",1,0,2,0,1.0);Make("linked.d5",1,1,2,0,5.0);Make("linked.d20",1,2,2,0,20.0);
    Make("default.d5",2,0,3,0,5.0);Make("default.d20",2,1,3,0,20.0);Make("default.d1",2,2,3,0,1.0);
    Make("ballistic.d0513",3,0,4,0,0.513);Make("ballistic.d05145",3,1,4,0,0.5145);
    Make("path5.d5",3,2,4,5,5.0);Make("path4.d5",3,3,4,4,5.0);
    DelayCommand(102.0,PDone());
}

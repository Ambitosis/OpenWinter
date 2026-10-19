#include "visual_inc"
#include "pa_inc"
void Quiet(object o)
{
    int i;for(i=5000;i<=5013;i++)SetEventScript(o,i,"");
    AssignCommand(o,ClearAllActions(TRUE));SetAILevel(o,AI_LEVEL_NORMAL);SetPlotFlag(o,TRUE);
}
void Cast(object c,int spell)
{
    SetLocalFloat(c,"start",PANow());
    PANote("cast="+GetTag(c)+"|spell="+IntToString(spell)+"|distance="+FloatToString(GetDistanceBetweenLocations(GetLocation(c),GetLocalLocation(c,"target")),0,3));
    AssignCommand(c,ActionCastSpellAtLocation(spell,GetLocalLocation(c,"target"),METAMAGIC_NONE,TRUE));
}
object Actor(object pc,string tag,float x,float y,float tx,float ty,float facing)
{
    object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetArea(pc),Vector(x,y,0.0),facing));
    SetTag(c,tag);Quiet(c);SetLocalLocation(c,"target",Location(GetArea(pc),Vector(tx,ty,0.0),0.0));
    DelayCommand(5.0,Cast(c,0));DelayCommand(12.0,Cast(c,1));return c;
}
void End(object a,object b,object c)
{
    PANote("final_east="+IntToString(GetLocalInt(a,"impacts"))+"|final_north="+IntToString(GetLocalInt(b,"impacts"))+"|final_diagonal="+IntToString(GetLocalInt(c,"impacts")));
    VDone();
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;
    VBegin(pc);VHidePC(pc);
    object a=Actor(pc,"AXIS_EAST",15.0,15.0,35.0,15.0,0.0);
    object b=Actor(pc,"AXIS_NORTH",15.0,20.0,15.0,40.0,90.0);
    object c=Actor(pc,"AXIS_DIAGONAL",35.0,20.0,23.0,36.0,126.87);
    DelayCommand(1.0,VCamera(pc,90.0,28.0,25.0));
    DelayCommand(4.0,VShot("a_before"));
    DelayCommand(5.75,VShot("b_travel_075"));
    DelayCommand(6.1,VShot("c_travel_110"));
    DelayCommand(7.0,VShot("d_after"));
    DelayCommand(12.75,VShot("e_hidden_075"));
    DelayCommand(13.1,VShot("f_hidden_110"));
    DelayCommand(14.0,VShot("g_hidden_after"));
    DelayCommand(17.0,End(a,b,c));
}

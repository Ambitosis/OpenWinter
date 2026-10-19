#include "dy_inc"
#include "visual_inc"
// Rest refusals: a hostile with no AI 25 m away, then 35 m away (REST_ENEMY_CHECK_DISTANCE is 30); then a whole rest.
void Goblin(object pc,float d)
{
    vector v=GetPosition(pc);
    object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_goblina",Location(GetArea(pc),Vector(v.x+d,v.y,v.z),180.0),FALSE,"dy_goblin");
    int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");
    ChangeToStandardFaction(o,STANDARD_FACTION_HOSTILE);SetPlotFlag(o,TRUE);AssignCommand(o,ClearAllActions(TRUE));
    DyNote(pc,"goblin_at_"+FloatToString(GetDistanceBetween(o,pc),0,2));
}
void Rest(object pc,string tag){DyNote(pc,"rest_request_"+tag);AssignCommand(pc,ActionRest());DelayCommand(0.3,DyNote(pc,"rest_0.3_"+tag));}
void Gone(){DestroyObject(GetObjectByTag("dy_goblin"));}
void Stop(object pc){AssignCommand(pc,ClearAllActions());}
void Done(){SetLocalInt(GetModule(),"stop",1);VDone();}
void main()
{
    object pc=OBJECT_SELF;
    Goblin(pc,25.0);
    DelayCommand(1.0,Rest(pc,"near25"));
    DelayCommand(2.0,VShot("k_rest_enemy25"));
    DelayCommand(4.0,Gone());
    DelayCommand(4.5,Goblin(pc,35.0));
    DelayCommand(5.5,Rest(pc,"far35"));
    DelayCommand(7.0,DyNote(pc,"far35_1.5"));
    DelayCommand(8.0,Stop(pc));
    // A whole rest with nobody near: its chat lines and the panel half way.
    DelayCommand(9.0,Gone());
    DelayCommand(10.0,Rest(pc,"clear"));
    DelayCommand(15.0,VShot("l_resting"));
    DelayCommand(23.0,DyNote(pc,"rest_after"));
    DelayCommand(24.0,Done());
}

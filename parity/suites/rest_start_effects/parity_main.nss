#include "rs_inc"
object Actor(string tag,float x,float y)
{
    object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",Location(GetObjectByTag("PARITY_AREA"),Vector(x,y,0.0),90.0));
    SetTag(c,tag);int i;for(i=5000;i<=5013;i++)SetEventScript(c,i,"");
    SetEventScript(c,EVENT_SCRIPT_CREATURE_ON_RESTED,"rs_rested");
    AssignCommand(c,ClearAllActions(TRUE));SetAILevel(c,AI_LEVEL_NORMAL);ChangeToStandardFaction(c,STANDARD_FACTION_MERCHANT);
    return c;
}
void Finished(object c){SetLocalInt(c,"finished",GetLocalInt(c,"finished")+1);RState(c,"queue_finished");}
void Cancel(object c){RState(c,"before_cancel");AssignCommand(c,ClearAllActions(TRUE));RState(c,"after_cancel");}
void Late(object c)
{
    RState(c,"before_late");
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,TagEffect(EffectSkillIncrease(SKILL_SPOT,1),"late"),c,300.0);
    RState(c,"after_late");
}
void Start(object c,int mode)
{
    RKit(c);RState(c,"before");
    PLogI(GetTag(c)+".level",GetHitDice(c));PLog(GetTag(c)+".duration",Get2DAString("restduration","DURATION",GetHitDice(c)));
    if(mode==3)ForceRest(c);
    else {AssignCommand(c,ActionRest());AssignCommand(c,ActionDoCommand(Finished(c)));}
    RState(c,"submitted");DelayCommand(.10,RState(c,"t010"));DelayCommand(1.0,RState(c,"t100"));
    if(mode==1)DelayCommand(.5,Cancel(c));
    if(mode==0)DelayCommand(2.0,Late(c));
    DelayCommand(25.0,RState(c,"final"));
}
void main()
{
    object finished=Actor("finish",20.0,20.0),cancelled=Actor("cancel",80.0,20.0);
    object rejected=Actor("reject",80.0,80.0),forced=Actor("force",20.0,80.0);
    object enemy=Actor("enemy",85.0,80.0);ChangeToStandardFaction(enemy,STANDARD_FACTION_HOSTILE);
    PLogI("reject.enemy",GetIsEnemy(enemy,rejected));PLogF("reject.distance",GetDistanceBetween(enemy,rejected),.01);
    DelayCommand(3.0,Start(finished,0));DelayCommand(3.0,Start(cancelled,1));
    DelayCommand(3.0,Start(rejected,2));DelayCommand(3.0,Start(forced,3));
    DelayCommand(31.0,PDone());
}

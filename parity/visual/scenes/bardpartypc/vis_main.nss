#include "visual_inc"
#include "parity_fx"
void Log(string key,string value){WriteTimestampedLogEntry("VISUAL|note|"+key+"|"+value);}
void LogI(string key,int value){Log(key,IntToString(value));}
void Quiet(object o,int frozen=TRUE)
{
    int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");
    SetAILevel(o,AI_LEVEL_NORMAL);AssignCommand(o,ClearAllActions(TRUE));
    if(frozen)SetCommandable(FALSE,o);
}
object Actor(object pc,string resref,float x,float y)
{
    object o=VSpawn(pc,resref,x,y,0.0);SetPlotFlag(o,FALSE);Quiet(o,FALSE);return o;
}
void Snapshot(object pc)
{
    int i;
    for(i=0;i<6;i++)
    {
        object o=GetLocalObject(pc,"target"+IntToString(i));PFxSnap(o);
        SetLocalInt(o,"bp_str",GetAbilityScore(o,ABILITY_STRENGTH));
        SetLocalInt(o,"bp_dex",GetAbilityScore(o,ABILITY_DEXTERITY));
        SetLocalInt(o,"bp_con",GetAbilityScore(o,ABILITY_CONSTITUTION));
    }
}
void SongPayload(object o, object bard, string key)
{
    int attack; int damage; int skill;
    effect e=GetFirstEffect(o);
    while(GetIsEffectValid(e))
    {
        if(GetEffectSpellId(e)==411)
        {
            int type=GetEffectType(e,TRUE);string name="";
            if(type==EFFECT_TYPE_ATTACK_INCREASE){name="attack";attack++;}
            if(type==EFFECT_TYPE_DAMAGE_INCREASE){name="damage";damage++;}
            if(type==EFFECT_TYPE_SKILL_INCREASE){name="skill";skill++;}
            if(name!="")
            {
                Log(key+"."+name+"Payload",PFxInts(e));
                LogI(key+"."+name+"Creator",GetEffectCreator(e)==bard);
                LogI(key+"."+name+"Duration",GetEffectDuration(e));
            }
        }
        e=GetNextEffect(o);
    }
    LogI(key+".attackCount",attack);LogI(key+".damageCount",damage);LogI(key+".skillCount",skill);
}
void Report(object pc,string phase,int spell)
{
    object bard=GetLocalObject(pc,"target0");
    LogI(phase+".songUses",GetFeatRemainingUses(257,bard));
    LogI(phase+".dirgeUses",GetSpellUsesLeft(bard,CLASS_TYPE_BARD,445));
    int i;
    for(i=0;i<6;i++)
    {
        object o=GetLocalObject(pc,"target"+IntToString(i));string key=phase+".target"+IntToString(i);
        LogI(key+".has",GetHasSpellEffect(spell,o));
        if(spell==411){Log(key+".stats",PFxDiff(o));SongPayload(o,bard,key);}
        else
        {
            LogI(key+".strDecreased",GetAbilityScore(o,ABILITY_STRENGTH)<GetLocalInt(o,"bp_str"));
            LogI(key+".dexDecreased",GetAbilityScore(o,ABILITY_DEXTERITY)<GetLocalInt(o,"bp_dex"));
            LogI(key+".conDecreased",GetAbilityScore(o,ABILITY_CONSTITUTION)<GetLocalInt(o,"bp_con"));
        }
    }
}
void RemoveSong(object pc)
{
    int i;
    for(i=0;i<6;i++)
    {
        object o=GetLocalObject(pc,"target"+IntToString(i));effect e=GetFirstEffect(o);
        while(GetIsEffectValid(e)){if(GetEffectSpellId(e)==411)RemoveEffect(o,e);e=GetNextEffect(o);}
    }
}
void Song(object bard){AssignCommand(bard,ActionUseFeat(257,bard));}
void Dirge(object bard){AssignCommand(bard,ActionCastSpellAtObject(445,bard,METAMAGIC_NONE,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,CLASS_TYPE_BARD));}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc)||GetLocalInt(pc,"BP_STARTED"))return;SetLocalInt(pc,"BP_STARTED",TRUE);VBegin(pc);SetMaxHenchmen(5);
    object bard=Actor(pc,"bp_bard20",0.0,2.0);
    object near=Actor(pc,"nw_bandit001",2.0,1.0);
    object deaf=Actor(pc,"nw_bandit001",-2.0,1.0);
    object enemy=Actor(pc,"nw_bandit006",0.0,5.0);
    object far=Actor(pc,"nw_bandit001",20.0,0.0);
    AddHenchman(pc,bard);AddHenchman(pc,near);AddHenchman(pc,deaf);AddHenchman(pc,far);
    Quiet(bard,FALSE);Quiet(near);Quiet(deaf);Quiet(enemy);Quiet(far);
    ChangeToStandardFaction(enemy,STANDARD_FACTION_HOSTILE);
    SetIsTemporaryEnemy(enemy,bard);SetIsTemporaryEnemy(bard,enemy);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDeaf(),deaf);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectSavingThrowDecrease(SAVING_THROW_FORT,50),enemy);
    SetLocalObject(pc,"target0",bard);SetLocalObject(pc,"target1",pc);SetLocalObject(pc,"target2",near);
    SetLocalObject(pc,"target3",deaf);SetLocalObject(pc,"target4",enemy);SetLocalObject(pc,"target5",far);
    LogI("context.pc",GetIsPC(pc));Log("context.name",GetName(pc));
    LogI("context.pcClass",GetClassByPosition(1,pc));LogI("context.pcHD",GetHitDice(pc));
    LogI("context.bardLevel",GetLevelByClass(CLASS_TYPE_BARD,bard));
    LogI("context.perform",GetSkillRank(SKILL_PERFORM,bard));
    LogI("context.songFeat",GetHasFeat(257,bard,TRUE));
    LogI("context.dirgeKnown",GetKnownSpellId(bard,CLASS_TYPE_BARD,6,0));
    int i;
    for(i=0;i<6;i++)
    {
        object o=GetLocalObject(pc,"target"+IntToString(i));string key="context.target"+IntToString(i);
        LogI(key+".valid",GetIsObjectValid(o));LogI(key+".master",GetMaster(o)==pc);
        LogI(key+".friend",GetIsFriend(o,bard));LogI(key+".enemy",GetIsEnemy(o,bard));
    }
    Snapshot(pc);Report(pc,"beforeSong",411);
    DelayCommand(1.0,VCamera(pc,90.0,12.0,60.0));
    DelayCommand(8.0,Song(bard));DelayCommand(16.0,Report(pc,"song16",411));
    DelayCommand(17.0,VShot("song"));DelayCommand(28.0,Report(pc,"song28",411));
    DelayCommand(30.0,RemoveSong(pc));DelayCommand(34.0,Snapshot(pc));
    DelayCommand(35.0,Report(pc,"beforeDirge",445));DelayCommand(36.0,Dirge(bard));
    DelayCommand(44.0,Report(pc,"dirge44",445));DelayCommand(60.0,Report(pc,"dirge60",445));
    DelayCommand(61.0,VShot("dirge"));DelayCommand(64.0,VDone());
}

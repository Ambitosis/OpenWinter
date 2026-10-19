// Research fixture: vary one blocker property per otherwise identical lane.
// Original nwserver via the sandbox parity harness; no engine code is inspected.
// All actors have empty event scripts except the four explicit default-OnBlocked cases.
// Most pairs are mutually friendly across two factions; neutral/hostile lanes are controls.
#include "parity_inc"
#include "parity_time"
object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x,y,0.0),0.0); }
string Key(int i) {
    switch(i) {
      case 0: return "idle"; case 1: return "plot"; case 2: return "immortal";
      case 3: return "uncommandable"; case 4: return "wait"; case 5: return "immobile";
      case 6: return "cutscene_immobilize"; case 7: return "paralyze";
      case 8: return "ghost"; case 9: return "hostile"; case 10: return "friendly";
      case 11: return "animation"; case 12: return "different_faction";
      case 13: return "different_friend"; case 14: return "different_enemy";
      case 15: return "friend_script_mover"; case 16: return "neutral_script_mover";
      case 17: return "friend_script_blocker"; case 18: return "neutral_script_blocker";
      case 19: return "blocker_friend_only"; case 20: return "mover_friend_only";
    }
    return "unknown";
}
void Setup() {
    int i;
    for(i=0;i<21;i++) {
        float y=5.0+IntToFloat(i)*6.0;
        string k=Key(i);
        object a=CreateObject(OBJECT_TYPE_CREATURE,"p_quiet",L(10.0,y),FALSE,"m_"+k);
        string res="p_quiet"; if(i==5) res="p_immobile";
        object b=CreateObject(OBJECT_TYPE_CREATURE,res,L(16.0,y),FALSE,k);
        SetAILevel(a,AI_LEVEL_NORMAL); SetAILevel(b,AI_LEVEL_NORMAL);
        SetLocalLocation(b,"start",GetLocation(b));
        ChangeToStandardFaction(b,STANDARD_FACTION_MERCHANT);
        if(i!=12 && i!=14 && i!=16 && i!=18 && i!=19 && i!=20) { SetIsTemporaryFriend(a,b,FALSE); SetIsTemporaryFriend(b,a,FALSE); }
        if(i==15 || i==16) SetEventScript(a,EVENT_SCRIPT_CREATURE_ON_BLOCKED_BY_DOOR,"nw_c2_defaulte");
        if(i==17 || i==18) SetEventScript(b,EVENT_SCRIPT_CREATURE_ON_BLOCKED_BY_DOOR,"nw_c2_defaulte");
        if(i==19) SetIsTemporaryFriend(a,b,FALSE);
        if(i==20) SetIsTemporaryFriend(b,a,FALSE);
        if(i==1) SetPlotFlag(b,TRUE);
        if(i==2) SetImmortal(b,TRUE);
        if(i==3) SetCommandable(FALSE,b);
        if(i==4) AssignCommand(b,ActionWait(60.0));
        if(i==6) ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectCutsceneImmobilize(),b);
        if(i==7) ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectParalyze(),b);
        if(i==8) ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectCutsceneGhost(),b);
        if(i==9) SetIsTemporaryEnemy(a,b,FALSE);
        if(i==10) SetIsTemporaryFriend(a,b,FALSE);

        if(i==13) { SetIsTemporaryFriend(a,b,FALSE); SetIsTemporaryFriend(b,a,FALSE); }
        if(i==14) { SetIsTemporaryEnemy(a,b,FALSE); SetIsTemporaryEnemy(b,a,FALSE); }
        if(i==11) AssignCommand(b,ActionPlayAnimation(ANIMATION_LOOPING_PAUSE,1.0,60.0));
    }
}
void Start() {
    int i;
    for(i=0;i<21;i++) {
        string k=Key(i); object b=GetObjectByTag(k); object a=GetObjectByTag("m_"+k);
        PLogI(k+".same_faction",GetFactionEqual(a,b));
        PLogI(k+".friendly",GetIsFriend(b,a));
        PLogI(k+".blocker_friendly",GetIsFriend(a,b));
        PLogI(k+".enemy",GetIsEnemy(b,a));
        PLogI(k+".commandable",GetCommandable(b));
        PLogI(k+".action_before",GetCurrentAction(b));
        PLogI(k+".perceived",GetObjectSeen(b,a)||GetObjectHeard(b,a));
        AssignCommand(a,ActionMoveToLocation(L(24.0,5.0+IntToFloat(i)*6.0)));
    }
}
void Report() {
    int i;
    for(i=0;i<21;i++) {
        string k=Key(i); object b=GetObjectByTag(k);
        PLogI(k+".pushed",GetDistanceBetweenLocations(GetLocalLocation(b,"start"),GetLocation(b))>0.2);
        PLogF(k+".displacement",GetDistanceBetweenLocations(GetLocalLocation(b,"start"),GetLocation(b)),0.15);
        PLogI(k+".mover_arrived",GetPosition(GetObjectByTag("m_"+k)).x>23.0);
    }
    PDone();
}
void main() { PStart(); DelayCommand(0.5,Setup()); DelayCommand(6.0,Start()); DelayCommand(18.0,Report()); }

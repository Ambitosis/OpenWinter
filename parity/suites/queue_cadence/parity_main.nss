// Queued uses and casts back to back: one script queues six uses of a scroll or wand
// (authored spells with ConjTime 0 and CastTime 1000, 3000, 4000, 5000 or 7000 ms), six potions, or six casts
// (ConjTime 1500, CastTime 1000, 2000 or 5000; cheat, and five prepared; hasted). Every impact time is logged, and each gap
// between impacts (impacts come a fixed time after each use starts, so the gaps are the gaps between starts). The
// caster's OnCombatRoundEnd times are kept as RAW notes. Casters have no other scripts and nothing near them.
#include "parity_inc"
#include "parity_time"
int Uses(){return 6;}
void UseItem(object c){object it=GetLocalObject(c,"item");itemproperty ip=GetFirstItemProperty(it);while(GetIsItemPropertyValid(ip)&&!(GetItemPropertyType(ip)==15&&GetItemPropertySubType(ip)==GetLocalInt(c,"subtype")))ip=GetNextItemProperty(it);AssignCommand(c,ActionUseItemOnObject(it,ip,c));}
void Start(object c)
{
    SetLocalFloat(c,"start",PNow());int i;int spell=GetLocalInt(c,"spell");
    int uses=Uses();if(!GetLocalInt(c,"cheat")&&spell>=0)uses=5; // prepared: five slots per level
    for(i=0;i<uses;i++){if(spell>=0)AssignCommand(c,ActionCastSpellAtObject(spell,c,METAMAGIC_ANY,GetLocalInt(c,"cheat")));else UseItem(c);}
}
void Report(object c)
{
    string k=GetTag(c);string s=GetLocalString(c,"impacts");float prev=-1.0;int n=0;
    WriteTimestampedLogEntry("RAW|"+k+".impacts|"+s);WriteTimestampedLogEntry("RAW|"+k+".eor|"+GetLocalString(c,"eor"));
    while(s!=""){int p=FindSubString(s," ");float t=StringToFloat(GetStringLeft(s,p));s=GetSubString(s,p+1,10000);
        if(n==0)PLogF(k+".first",t,0.2);else PLogF(k+".gap"+IntToString(n),t-prev,0.3);prev=t;n++;}
    PLogI(k+".impacts",n);
}
void Setup(string name,int n,string res,int sub,int mode,int spell=-1,int cheat=TRUE,int haste=FALSE)
{
    object c=CreateObject(OBJECT_TYPE_CREATURE,res,PCentre(-36.0+IntToFloat(n%7)*12.0,-12.0+IntToFloat(n/7)*12.0));
    SetTag(c,name);SetAILevel(c,AI_LEVEL_NORMAL);SetPlotFlag(c,TRUE);int e;for(e=5000;e<=5012;e++)SetEventScript(c,e,"");
    SetEventScript(c,EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND,"qc_eor");ChangeToStandardFaction(c,STANDARD_FACTION_COMMONER);
    SetLocalInt(c,"spell",spell);SetLocalInt(c,"cheat",cheat);
    if(haste)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectHaste(),c);
    if(spell<0)
    {
        object it=CreateItemOnObject(res=="nw_elfmage015"?GetLocalString(GetModule(),"item"+IntToString(mode)):"",c,mode==2?1:Uses());
        if(mode==2)SetItemCharges(it,20);
        itemproperty old=GetFirstItemProperty(it);while(GetIsItemPropertyValid(old)){RemoveItemProperty(it,old);old=GetFirstItemProperty(it);}
        AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyCastSpell(sub,mode==2?6:1),it);
        SetLocalObject(c,"item",it);SetLocalInt(c,"subtype",sub);
        PLogI(name+".base",GetBaseItemType(it));PLogI(name+".stack",mode==2?GetItemCharges(it):GetItemStackSize(it));
    }
    DelayCommand(10.0+IntToFloat(n)*0.2,Start(c));DelayCommand(112.0,Report(c));
}
void main()
{
    object m=GetModule();SetLocalString(m,"item1","nw_it_sparscr104");SetLocalString(m,"item2","nw_wmgwn004");SetLocalString(m,"item3","nw_it_mpotion001");
    int i;string c;
    for(i=0;i<5;i++){c=IntToString(i);Setup("scroll"+c,i,"nw_elfmage015",66+i,1);Setup("wand"+c,7+i,"nw_elfmage015",66+i,2);}
    Setup("potion",5,"nw_elfmage015",66,3);
    Setup("cheat1000",14,"nw_elfmage015",0,0,5);Setup("cheat2000",15,"nw_elfmage015",0,0,6);Setup("cheat5000",16,"nw_elfmage015",0,0,7);
    Setup("haste1000",17,"nw_elfmage015",0,0,5,TRUE,TRUE);
    Setup("prep1000",18,"qc_wiz",0,0,5,FALSE);Setup("prep5000",19,"qc_wiz",0,0,7,FALSE);Setup("hasteprep1000",20,"qc_wiz",0,0,5,FALSE,TRUE);
    DelayCommand(113.0,PDone());
}

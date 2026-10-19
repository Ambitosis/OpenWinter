// ActionEquipMostDamagingMelee / Ranged against different targets: non-physical
// damage bonuses count only against a valid non-plot creature; the search is repeated without a target only when a melee
// weapon is in hand, a target was given and nothing beat the weapon in hand; ammunition replaces the first (or loaded)
// stack only by a strictly higher score. Fighters (Strength 10, no event scripts); inventory order is logged.
#include "parity_inc"
object Area(){return GetObjectByTag("PARITY_AREA");}
location L(float x,float y){return Location(Area(),Vector(x,y,0.0),0.0);}
object Actor(string k)
{
    int n=GetLocalInt(GetModule(),"NACT");SetLocalInt(GetModule(),"NACT",n+1);
    object o=CreateObject(OBJECT_TYPE_CREATURE,"er_fighter",L(4.0+5.0*IntToFloat(n%12),4.0+5.0*IntToFloat(n/12)),FALSE,"A_"+k);
    int e;for(e=5000;e<=5012;e++)SetEventScript(o,e,"");ChangeToStandardFaction(o,STANDARD_FACTION_COMMONER);SetAILevel(o,AI_LEVEL_NORMAL);
    SetLocalString(GetModule(),"CASE"+IntToString(n),k);return o;
}
object Dummy(string sTag,string sRes,float x,int bPlot)
{
    object o=CreateObject(OBJECT_TYPE_CREATURE,sRes,L(x,70.0),FALSE,sTag);
    int e;for(e=5000;e<=5012;e++)SetEventScript(o,e,"");ChangeToStandardFaction(o,STANDARD_FACTION_MERCHANT);
    if(bPlot)SetPlotFlag(o,TRUE);return o;
}
object Item(object a,string sRes,string sTag,int nStack=1){object i=CreateItemOnObject(sRes,a,nStack,sTag);SetIdentified(i,TRUE);return i;}
void Prop(object i,itemproperty p){AddItemProperty(DURATION_TYPE_PERMANENT,p,i);}
string T(object o){return GetIsObjectValid(o)?GetTag(o):"none";}
void Melee(object a,object v){SetLocalInt(a,"KIND",1);SetLocalObject(a,"VS",v);}
void Ranged(object a,object v){SetLocalInt(a,"KIND",2);SetLocalObject(a,"VS",v);}
object Fire2d6(object a){object i=Item(a,"nw_wswls001","LS_FIRE");Prop(i,ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE,IP_CONST_DAMAGEBONUS_2d6));return i;}
object Undead2d6(object a){object i=Item(a,"nw_wswls001","LS_UND");Prop(i,ItemPropertyDamageBonusVsRace(IP_CONST_RACIALTYPE_UNDEAD,IP_CONST_DAMAGETYPE_SLASHING,IP_CONST_DAMAGEBONUS_2d6));return i;}
void Setup()
{
    object vHuman=Dummy("D_HUMAN","nw_bandit001",10.0,FALSE),vPlot=Dummy("D_PLOT","nw_bandit001",15.0,TRUE);
    object vFireImm=Dummy("D_FIREIMM","nw_bandit001",20.0,FALSE);ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDamageImmunityIncrease(DAMAGE_TYPE_FIRE,100),vFireImm);
    object vUndead=Dummy("D_UNDEAD","nw_zombie01",25.0,FALSE);
    object vChest=CreateObject(OBJECT_TYPE_PLACEABLE,"plc_chest1",L(30.0,70.0),FALSE,"D_CHEST");
    object a,i;
    // Fire 2d6 on a longsword (8 + 12) against a greataxe (12).
    a=Actor("FIRE_NP");Fire2d6(a);Item(a,"nw_waxgr001","GAXE");Melee(a,vHuman);
    a=Actor("FIRE_PLOT");Fire2d6(a);Item(a,"nw_waxgr001","GAXE");Melee(a,vPlot);
    a=Actor("FIRE_NONE");Fire2d6(a);Item(a,"nw_waxgr001","GAXE");Melee(a,OBJECT_INVALID);
    a=Actor("FIRE_CHEST");Fire2d6(a);Item(a,"nw_waxgr001","GAXE");Melee(a,vChest);
    a=Actor("FIRE_IMM");Fire2d6(a);Item(a,"nw_waxgr001","GAXE");Melee(a,vFireImm);
    // The repeat without a target: a longsword with 2d6 slashing versus undead in hand, a greataxe in the pack.
    a=Actor("REP_UND");i=Undead2d6(a);AssignCommand(a,ActionEquipItem(i,INVENTORY_SLOT_RIGHTHAND));Item(a,"nw_waxgr001","GAXE");Melee(a,vUndead);
    a=Actor("REP_UNARMED");Undead2d6(a);Item(a,"nw_waxgr001","GAXE");Melee(a,vUndead);
    a=Actor("REP_NONE");i=Undead2d6(a);AssignCommand(a,ActionEquipItem(i,INVENTORY_SLOT_RIGHTHAND));Item(a,"nw_waxgr001","GAXE");Melee(a,OBJECT_INVALID);
    a=Actor("REP_HUMAN");i=Undead2d6(a);AssignCommand(a,ActionEquipItem(i,INVENTORY_SLOT_RIGHTHAND));Item(a,"nw_waxgr001","GAXE");Melee(a,vHuman);
    // A +1 longsword in hand (9) against a plot target, a greataxe (12) in the pack.
    a=Actor("KEEP_PLOT");i=Item(a,"nw_wswls001","LS_P1");Prop(i,ItemPropertyEnhancementBonus(1));AssignCommand(a,ActionEquipItem(i,INVENTORY_SLOT_RIGHTHAND));Item(a,"nw_waxgr001","GAXE");Melee(a,vPlot);
    // Ammunition: plain bolts made first (second in inventory order), then fire 1d6 or +5 piercing bolts.
    a=Actor("AM_FIRE_NP");Item(a,"nw_wbwxl001","XL");Item(a,"nw_wambo001","BOLTS",20);Prop(Item(a,"nw_wambo001","BOLTS_FIRE",20),ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE,IP_CONST_DAMAGEBONUS_1d6));Ranged(a,vHuman);
    a=Actor("AM_FIRE_NP2");Item(a,"nw_wbwxl001","XL");Prop(Item(a,"nw_wambo001","BOLTS_FIRE",20),ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE,IP_CONST_DAMAGEBONUS_1d6));Item(a,"nw_wambo001","BOLTS",20);Ranged(a,vHuman);
    a=Actor("AM_FIRE_PLOT");Item(a,"nw_wbwxl001","XL");Prop(Item(a,"nw_wambo001","BOLTS_FIRE",20),ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE,IP_CONST_DAMAGEBONUS_1d6));Item(a,"nw_wambo001","BOLTS",20);Ranged(a,vPlot);
    a=Actor("AM_PIERCE");Item(a,"nw_wbwxl001","XL");Prop(Item(a,"nw_wambo001","BOLTS_5",20),ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_PIERCING,IP_CONST_DAMAGEBONUS_5));Item(a,"nw_wambo001","BOLTS",20);Ranged(a,OBJECT_INVALID);
    a=Actor("AM_PIERCE_NP");Item(a,"nw_wbwxl001","XL");Prop(Item(a,"nw_wambo001","BOLTS_5",20),ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_PIERCING,IP_CONST_DAMAGEBONUS_5));Item(a,"nw_wambo001","BOLTS",20);Ranged(a,vHuman);
    a=Actor("AM_ENH");Item(a,"nw_wbwxl001","XL");Prop(Item(a,"nw_wambo001","BOLTS_E3",20),ItemPropertyEnhancementBonus(3));Item(a,"nw_wambo001","BOLTS",20);Ranged(a,OBJECT_INVALID);
    a=Actor("AM_LOADED");Item(a,"nw_wbwxl001","XL");i=Item(a,"nw_wambo001","BOLTS",20);AssignCommand(a,ActionEquipItem(i,INVENTORY_SLOT_BOLTS));
    Prop(Item(a,"nw_wambo001","BOLTS_FIRE",20),ItemPropertyDamageBonus(IP_CONST_DAMAGETYPE_FIRE,IP_CONST_DAMAGEBONUS_1d6));Ranged(a,vHuman);
}
void Go()
{
    int i,n=GetLocalInt(GetModule(),"NACT");
    for(i=0;i<n;i++)
    {
        string k=GetLocalString(GetModule(),"CASE"+IntToString(i));object a=GetObjectByTag("A_"+k);string s="";
        object it=GetFirstItemInInventory(a);while(GetIsObjectValid(it)){s+=GetTag(it)+" ";it=GetNextItemInInventory(a);}
        SetLocalString(a,"INV",s);PLog(k+".before",T(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,a))+" "+T(GetItemInSlot(INVENTORY_SLOT_BOLTS,a)));
        if(GetLocalInt(a,"KIND")==1)AssignCommand(a,ActionEquipMostDamagingMelee(GetLocalObject(a,"VS")));
        else AssignCommand(a,ActionEquipMostDamagingRanged(GetLocalObject(a,"VS")));
    }
}
void Read()
{
    int i,n=GetLocalInt(GetModule(),"NACT");
    for(i=0;i<n;i++)
    {
        string k=GetLocalString(GetModule(),"CASE"+IntToString(i));object a=GetObjectByTag("A_"+k);
        PLog(k+".order",GetLocalString(a,"INV"));PLog(k+".right",T(GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,a)));PLog(k+".ammo",T(GetItemInSlot(INVENTORY_SLOT_BOLTS,a)));
    }
    PLogI("undead.race",GetRacialType(GetObjectByTag("D_UNDEAD")));PLogI("plot.flag",GetPlotFlag(GetObjectByTag("D_PLOT")));
}
void main(){DelayCommand(1.0,Setup());DelayCommand(4.0,Go());DelayCommand(10.0,Read());DelayCommand(11.0,PDone());}

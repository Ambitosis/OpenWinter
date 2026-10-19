// When a cast provokes an attack of opportunity (expected: only a cast that
// starts a new combat round for the caster; defensive casting set during a running round waits for the next attack
// round; an NPC loses its combat mode when a round ends). Per case: caster C (stripped bandit with Concentration 40,
// so Defensive Casting's check passes, hostile faction, immortal) stands between two stripped commoner bandits, V1 1.5 m west facing it and V2 1.5 m east facing it. A
// prodder P 6 m north cheat-casts the stock Magic Missile at V1 at 5 s and at V2 at 10 s and 16 s, so V1 is in combat
// from about 7 s and V2 from about 12 s. V1 is destroyed at 12.5 s. C's own casts are cheat Ray of Frost (an observer
// script: no damage, a harmful spell-cast-at event), at P unless noted. Attacks on C are logged with the attacker, and the attacker's
// order is cleared at once.
//   single:  C casts at 8.                       queued: C queues two casts at 8 (the second waits for the round).
//   gap7:    C casts at 8 and 15.                melee:  C attacks V1 at 7; at 10 ClearAllActions and a cast.
//   defidle: defensive casting set at 6 (idle), casts at 8 and 16.
//   defmid:  cast at 8, defensive casting set at 9 (round running), cast at 16.
//   selfat/selfp: C itself puts V1 in combat (cast at it at 5), then casts at 11.5 at V1 / at P.
//   patv: cast at V1 at 10.   sbmm/sbray: casts at V1 at 5 and 12 (stock Magic Missile / the observer).
//   singleua: as single, C unarmed.
// GetActionMode(C, ACTION_MODE_DEFENSIVE_CAST) is read at 7, 9.5, 13, 15 and 15.9 s. The Prelude faction table is
// needed: with the generated module's own factions no victim ever made an attack of opportunity.
#include "parity_inc"
#include "parity_time"
object Area(){return GetObjectByTag("PARITY_AREA");}
object Mk(string tag,float x,float y,float facing,int faction,string res="nw_bandit001")
{
    object o=CreateObject(OBJECT_TYPE_CREATURE,res,Location(Area(),Vector(x,y,0.0),facing),FALSE,tag);
    int e;for(e=5000;e<=5012;e++)SetEventScript(o,e,"");SetAILevel(o,AI_LEVEL_NORMAL);ChangeToStandardFaction(o,faction);SetImmortal(o,TRUE);
    return o;
}
void Cast(object c,object t){AssignCommand(c,ActionCastSpellAtObject(SPELL_RAY_OF_FROST,t,METAMAGIC_ANY,TRUE));}
void Prod(object p,object t){AssignCommand(p,ActionCastSpellAtObject(SPELL_MAGIC_MISSILE,t,METAMAGIC_ANY,TRUE));}
void Mode(object c,int on){SetActionMode(c,ACTION_MODE_DEFENSIVE_CAST,on);}
void ReadMode(object c,string at){PLogI(GetTag(c)+".mode"+at,GetActionMode(c,ACTION_MODE_DEFENSIVE_CAST));}
void Melee(object c,object v){AssignCommand(c,ActionAttack(v));}
void ClearCast(object c,object t){AssignCommand(c,ClearAllActions());Cast(c,t);}
void Probe(object c,object v,string k){PLogI(GetTag(c)+"."+k+".combat",GetIsInCombat(v));PLogI(GetTag(c)+"."+k+".sees",GetObjectSeen(c,v));PLogI(GetTag(c)+"."+k+".enemy",GetIsEnemy(c,v));}
void Report(object c)
{
    string k=GetTag(c);
    WriteTimestampedLogEntry("RAW|"+k+".atk|"+GetLocalString(c,"atk"));
    PLogSeq(k+".casts",GetLocalString(c,"casts"),0.35);
    // melee is a race (an attack of opportunity in one nwserver run of three, OpenWinter always): kept as a note.
    if(k=="melee"){WriteTimestampedLogEntry("RAW|melee.v1|"+IntToString(GetLocalInt(c,k+"V1")));return;}
    PLogI(k+".v1",GetLocalInt(c,k+"V1"));PLogI(k+".v2",GetLocalInt(c,k+"V2"));
    // The original's attack of opportunity comes 0.3-1.4 s after the cast starts (OpenWinter: at once), seen up to 1.7 s.
    PLogSeq(k+".atk",GetLocalString(c,"atk"),2.0);
}
void Case(string k,int n)
{
    float x=12.0+IntToFloat(n%3)*25.0,y=12.0+IntToFloat(n/3)*25.0;
    object c=Mk(k,x,y,0.0,STANDARD_FACTION_HOSTILE,"cp_caster");SetEventScript(c,EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED,"cp_atk");
    // Immune to the attacks of opportunity's damage, so a cast that drew one isn't lost to a Concentration check.
    int d;for(d=0;d<3;d++)ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectDamageImmunityIncrease(d==0?DAMAGE_TYPE_BLUDGEONING:(d==1?DAMAGE_TYPE_PIERCING:DAMAGE_TYPE_SLASHING),100),c);
    object v1=Mk(k+"V1",x-1.5,y,0.0,STANDARD_FACTION_COMMONER);
    object v2=Mk(k+"V2",x+1.5,y,180.0,STANDARD_FACTION_COMMONER);
    object p=Mk(k+"P",x,y+6.0,270.0,STANDARD_FACTION_HOSTILE);
    DelayCommand(5.0,Prod(p,v1));DelayCommand(10.0,Prod(p,v2));DelayCommand(16.0,Prod(p,v2));
    DelayCommand(7.9,Probe(c,v1,"v1"));DelayCommand(13.9,Probe(c,v2,"v2"));
    if(GetStringLeft(k,2)!="sb")DelayCommand(12.5,DestroyObject(v1));
    DelayCommand(7.0,ReadMode(c,"7"));DelayCommand(9.5,ReadMode(c,"9_5"));DelayCommand(13.0,ReadMode(c,"13"));
    DelayCommand(15.0,ReadMode(c,"15"));DelayCommand(15.9,ReadMode(c,"15_9"));
    if(k=="single")DelayCommand(8.0,Cast(c,p));
    if(k=="queued"){DelayCommand(8.0,Cast(c,p));DelayCommand(8.0,Cast(c,p));}
    if(k=="gap7"){DelayCommand(8.0,Cast(c,p));DelayCommand(15.0,Cast(c,p));}
    if(k=="melee"){DelayCommand(7.0,Melee(c,v1));DelayCommand(10.0,ClearCast(c,p));}
    if(k=="defidle"){DelayCommand(6.0,Mode(c,TRUE));DelayCommand(8.0,Cast(c,p));DelayCommand(16.0,Cast(c,p));}
    if(k=="defmid"){DelayCommand(8.0,Cast(c,p));DelayCommand(9.0,Mode(c,TRUE));DelayCommand(16.0,Cast(c,p));}
    if(k=="selfat"){DelayCommand(5.0,Cast(c,v1));DelayCommand(11.5,Cast(c,v1));}
    if(k=="selfp"){DelayCommand(5.0,Cast(c,v1));DelayCommand(11.5,Cast(c,p));}
    if(k=="sbmm"){DelayCommand(5.0,Prod(c,v1));DelayCommand(12.0,Prod(c,v1));}
    if(k=="sbray"){DelayCommand(5.0,Cast(c,v1));DelayCommand(12.0,Cast(c,v1));}
    if(GetStringRight(k,2)=="ua"){object w=GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,c);if(GetIsObjectValid(w))DestroyObject(w);}
    if(k=="singleua"){DelayCommand(8.0,Cast(c,p));}
    if(k=="patv"){DelayCommand(10.0,Cast(c,v1));}
    DelayCommand(24.0,Report(c));
}
void main()
{
    PStart();
    Case("single",0);Case("queued",1);Case("gap7",2);Case("melee",3);Case("defidle",4);Case("defmid",5);
    Case("selfat",6);Case("selfp",7);Case("patv",8);Case("sbmm",9);Case("sbray",10);Case("singleua",11);
    DelayCommand(25.0,PDone());
}

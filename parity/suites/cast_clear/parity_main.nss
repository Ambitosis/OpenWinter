#include "parity_inc"
#include "parity_time"
void Old(object o){SetLocalInt(o,"old",1);}
void Next(object o){SetLocalInt(o,"next",1);SetLocalFloat(o,"nextAt",PNow());}
void Clear(object o,int mode)
{
 PLogI(GetTag(o)+".readyAtClear",GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,1,0));
 AssignCommand(o,ClearAllActions(TRUE));
 if(mode==2)AssignCommand(o,ActionWait(0.2));
 if(mode==3)AssignCommand(o,ActionMoveToLocation(Location(GetArea(o),GetPosition(o)+Vector(1.0,0.0,0.0),0.0),FALSE));
 if(mode)AssignCommand(o,ActionDoCommand(Next(o)));
}
void Read(object o,string phase)
{
 string k=GetTag(o)+"."+phase;float commit=GetLocalFloat(o,"commit");
 PLogI(k+".impacts",GetLocalInt(o,"impacts"));
 PLogI(k+".old",GetLocalInt(o,"old"));PLogI(k+".next",GetLocalInt(o,"next"));
 PLogI(k+".action",GetCurrentAction(o));
 PLogI(k+".ready",GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,1,0));
 if(GetLocalInt(o,"impacts"))PLogF(k+".impactAfterCommit",GetLocalFloat(o,"impactAt")-commit,0.10);
 // Queue processing in native NPC AI can add a tick; this tolerance is below the
 // >0.5-1.4s premature continuation exposed by the previous cancellation path.
 if(GetLocalInt(o,"next"))PLogF(k+".nextAfterCommit",GetLocalFloat(o,"nextAt")-commit,0.25);
 if(phase=="during")PLogI(k+".moved",GetDistanceBetweenLocations(GetLocation(o),GetLocalLocation(o,"origin"))>0.1);
}
void Committed(object o,int index,int poll)
{
 if(GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,1,0))
 {
  if(poll<200){DelayCommand(0.02,Committed(o,index,poll+1));return;}
  PLogI(GetTag(o)+".committed",0);return;
 }
 PLogI(GetTag(o)+".committed",1);SetLocalFloat(o,"commit",PNow());
 float delay=0.1;int mode=1;
 if(index==2)mode=2;if(index==3)mode=3;if(index==6)mode=0;
 if(index==4)delay=0.65;if(index==5)delay=1.0;
 if(index)DelayCommand(delay,Clear(o,mode));
 DelayCommand(0.70,Read(o,"during"));DelayCommand(3.5,Read(o,"done"));
}
void Start(object o,int index)
{
 AssignCommand(o,ActionCastSpellAtObject(SPELL_MAGE_ARMOR,o,METAMAGIC_NONE,FALSE));
 AssignCommand(o,ActionDoCommand(Old(o)));
 DelayCommand(0.2,Committed(o,index,0));
}
void Prepare(object o)
{
 AssignCommand(o,ClearAllActions(TRUE));int l;int s;
 for(l=0;l<10;l++)for(s=0;s<GetMemorizedSpellCountByLevel(o,CLASS_TYPE_WIZARD,l);s++)SetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,l,s,FALSE);
 SetMemorizedSpell(o,CLASS_TYPE_WIZARD,1,0,SPELL_MAGE_ARMOR,TRUE,METAMAGIC_NONE,FALSE);
 PLogI(GetTag(o)+".class",GetLevelByClass(CLASS_TYPE_WIZARD,o));
 PLogI(GetTag(o)+".prepared",GetMemorizedSpellId(o,CLASS_TYPE_WIZARD,1,0));
 PLogI(GetTag(o)+".preparedReady",GetMemorizedSpellReady(o,CLASS_TYPE_WIZARD,1,0));
}
void Run()
{
 int i;for(i=0;i<7;i++)
 {
  object o=CreateObject(OBJECT_TYPE_CREATURE,"nw_drowmage001",PCentre(IntToFloat(i%4)*5.0,IntToFloat(i/4)*5.0));
  SetTag(o,IntToString(i));SetPlotFlag(o,FALSE);SetAILevel(o,AI_LEVEL_NORMAL);SetLocalLocation(o,"origin",GetLocation(o));
  SetEventScript(o,EVENT_SCRIPT_CREATURE_ON_HEARTBEAT,"");
  object item=GetFirstItemInInventory(o);while(GetIsObjectValid(item)){object next=GetNextItemInInventory(o);DestroyObject(item);item=next;}
  DelayCommand(0.3,Prepare(o));DelayCommand(1.0+IntToFloat(i)*0.5,Start(o,i));
 }
 DelayCommand(17.0,PDone());
}
void main(){DelayCommand(0.2,Run());}

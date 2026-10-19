#include "ec_inc"
void Dispatch()
{
 object c=GetObjectByTag("CRIT"),p=GetObjectByTag("PLC"),d=GetObjectByTag("DOOR"),a=GetArea(c);
 PLogI("gate.crit",GetIsObjectValid(c)); PLogI("gate.plc",GetIsObjectValid(p)); PLogI("gate.door",GetIsObjectValid(d));
 // Public setters isolate routing from the separate GFF loading question.
 PLogI("gate.plc.dialog",SetEventScript(p,EVENT_SCRIPT_PLACEABLE_ON_DIALOGUE,"ec_event"));
 PLogI("gate.door.dialog",SetEventScript(d,EVENT_SCRIPT_DOOR_ON_DIALOGUE,"ec_event"));
 SignalEvent(GetModule(),EventUserDefined(11)); SignalEvent(a,EventUserDefined(12));
 SignalEvent(c,EventUserDefined(13)); SignalEvent(p,EventUserDefined(14)); SignalEvent(d,EventUserDefined(15));
 SignalEvent(c,EventSpellCastAt(p,601,FALSE)); SignalEvent(p,EventSpellCastAt(c,601,FALSE)); SignalEvent(d,EventSpellCastAt(c,601,FALSE));
 SignalEvent(c,EventConversation()); SignalEvent(p,EventConversation()); SignalEvent(d,EventConversation());
 SignalEvent(GetModule(),EventActivateItem(c,GetLocation(p),p));
}
void Finish()
{
 PLogI("count.crit",GetLocalInt(GetObjectByTag("CRIT"),"count"));
 PLogI("count.plc",GetLocalInt(GetObjectByTag("PLC"),"count"));
 PLogI("count.door",GetLocalInt(GetObjectByTag("DOOR"),"count"));
 PDone();
}
void main()
{
 Snapshot("load"); SetLocalString(GetModule(),"key","load"); ExecuteScript("ec_nested",OBJECT_SELF); Snapshot("load.after");
 DelayCommand(0.1,Snapshot("load.delay")); AssignCommand(OBJECT_SELF,Snapshot("load.assign"));
 DelayCommand(1.0,Dispatch()); DelayCommand(3.0,Finish());
}

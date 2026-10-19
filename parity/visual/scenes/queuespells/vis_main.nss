#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Key(int vk){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(vk)+"|tap");}
void Click(string xy){WriteTimestampedLogEntry("VISUAL|click|"+xy);}
void Cast(int vk){Key(vk);DelayCommand(.22,Click("0.973,0.07"));}
void Read(object pc,string label){Note(label+".slots."+IntToString(GetMemorizedSpellReady(pc,CLASS_TYPE_CLERIC,1,0))+IntToString(GetMemorizedSpellReady(pc,CLASS_TYPE_CLERIC,1,1))+IntToString(GetMemorizedSpellReady(pc,CLASS_TYPE_CLERIC,1,2))+".action."+IntToString(GetCurrentAction(pc)));}
void Prep(object pc){SetMemorizedSpell(pc,CLASS_TYPE_CLERIC,1,0,SPELL_ENDURE_ELEMENTS,TRUE);SetMemorizedSpell(pc,CLASS_TYPE_CLERIC,1,1,SPELL_BLESS,TRUE);SetMemorizedSpell(pc,CLASS_TYPE_CLERIC,1,2,SPELL_ENDURE_ELEMENTS,TRUE);Read(pc,"prepared");}
void Ready(object pc){Note("character-fixture|"+GetName(pc)+"|pc="+IntToString(GetIsPC(pc)));Note("class."+IntToString(GetLevelByClass(CLASS_TYPE_CLERIC,pc))+".haste."+IntToString(GetHasSpellEffect(SPELL_HASTE,pc)));Prep(pc);}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetPlotFlag(pc,TRUE);DelayCommand(.5,Ready(pc));DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));DelayCommand(2.0,VShot("before"));
 DelayCommand(3.0,Cast(112));DelayCommand(3.4,Cast(113));DelayCommand(3.8,Cast(112));DelayCommand(4.3,VShot("three_queued"));DelayCommand(7.5,VShot("waiting_first"));DelayCommand(13.5,VShot("waiting_second"));DelayCommand(20.0,Read(pc,"three_finished"));
 DelayCommand(21.0,Prep(pc));DelayCommand(23.0,Cast(112));DelayCommand(23.4,Cast(113));DelayCommand(23.8,Click("0.017,0.027"));DelayCommand(24.2,VShot("head_cancel_early"));DelayCommand(31.0,Read(pc,"early_finished"));
 DelayCommand(32.0,Prep(pc));DelayCommand(34.0,Cast(112));DelayCommand(34.4,Cast(113));DelayCommand(35.9,Click("0.017,0.027"));DelayCommand(36.3,VShot("head_cancel_committed"));DelayCommand(46.0,Read(pc,"committed_finished"));DelayCommand(47.0,VShot("done"));DelayCommand(48.0,VDone());}

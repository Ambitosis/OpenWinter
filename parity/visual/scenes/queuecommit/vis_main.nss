#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Click(string xy){WriteTimestampedLogEntry("VISUAL|click|"+xy);}
void Cast(int key){WriteTimestampedLogEntry("VISUAL|key|"+IntToString(key)+"|tap");DelayCommand(.22,Click("0.973,0.07"));}
void Read(object pc,string name){Note(name+".slots."+IntToString(GetMemorizedSpellReady(pc,CLASS_TYPE_CLERIC,1,0))+IntToString(GetMemorizedSpellReady(pc,CLASS_TYPE_CLERIC,1,1))+".action."+IntToString(GetCurrentAction(pc)));}
void Poll(object pc,int n)
{
    if(!GetMemorizedSpellReady(pc,CLASS_TYPE_CLERIC,1,0))
    {
        Read(pc,"committed_before_click");VShot("before_cancel");
        DelayCommand(.1,Click("0.017,0.027"));
        DelayCommand(.4,Read(pc,"after_click"));DelayCommand(.5,VShot("after_cancel"));
        return;
    }
    if(n<100)DelayCommand(.05,Poll(pc,n+1));else Note("commit_gate_failed");
}
void main()
{
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetPlotFlag(pc,TRUE);
    Note("character-fixture|"+GetName(pc)+"|pc=1");
    SetMemorizedSpell(pc,CLASS_TYPE_CLERIC,1,0,SPELL_ENDURE_ELEMENTS,TRUE);
    SetMemorizedSpell(pc,CLASS_TYPE_CLERIC,1,1,SPELL_BLESS,TRUE);
    DelayCommand(1.0,VCamera(pc,90.0,5.0,65.0));
    DelayCommand(3.0,Cast(112));DelayCommand(3.4,Cast(113));DelayCommand(4.0,Poll(pc,0));
    DelayCommand(18.0,Read(pc,"finished"));DelayCommand(19.0,VDone());
}

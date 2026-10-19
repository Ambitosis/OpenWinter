#include "visual_inc"
object Actor(object pc,float x){object o=VSpawn(pc,"nw_bandit001",x,2.0,270.0);int i;for(i=5000;i<=5012;i++)SetEventScript(o,i,"");AssignCommand(o,ClearAllActions(TRUE));return o;}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);VHidePC(pc);
object a=Actor(pc,-3.0);object b=Actor(pc,-1.0);object c=Actor(pc,1.0);object d=Actor(pc,3.0);
ReplaceObjectAnimation(b,"castarea","sitcross");
ReplaceObjectAnimation(c,"castarealp","sitcross");
ReplaceObjectAnimation(d,"conjure1","sitcross");
DelayCommand(1.0,VCamera(pc,90.0,10.0,60.0));
DelayCommand(4.0,AssignCommand(a,ActionCastSpellAtObject(6,a,METAMAGIC_NONE,TRUE)));
DelayCommand(4.0,AssignCommand(b,ActionCastSpellAtObject(6,b,METAMAGIC_NONE,TRUE)));
DelayCommand(4.0,AssignCommand(c,ActionCastSpellAtObject(6,c,METAMAGIC_NONE,TRUE)));
DelayCommand(4.0,AssignCommand(d,ActionCastSpellAtObject(6,d,METAMAGIC_NONE,TRUE)));
DelayCommand(4.6,VShot("phase_06"));
DelayCommand(5.7,VShot("phase_17"));
DelayCommand(6.1,VShot("phase_21"));
DelayCommand(6.5,VShot("phase_25"));
DelayCommand(7.1,VShot("phase_31"));
DelayCommand(7.8,VShot("phase_38"));
DelayCommand(10.0,VDone());}

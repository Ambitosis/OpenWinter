#include "parity_inc"
#include "parity_time"
void State(string key) {
 PLogI(key+".A",GetIsOpen(GetObjectByTag("A")));
 PLogI(key+".B",GetIsOpen(GetObjectByTag("B")));
 PLogI(key+".lockedB",GetLocked(GetObjectByTag("B")));
}
void OpenA(){ object d=GetObjectByTag("A");AssignCommand(d,ActionOpenDoor(d)); }
void CloseA(){ object d=GetObjectByTag("A");AssignCommand(d,ActionCloseDoor(d)); }
void CloseB(){ object d=GetObjectByTag("B");AssignCommand(d,ActionCloseDoor(d)); }
void main(){PStart();State("initial");DelayCommand(1.0,OpenA());DelayCommand(2.5,State("openA"));
 DelayCommand(3.0,CloseB());DelayCommand(4.5,State("closeB"));DelayCommand(5.0,CloseA());
 DelayCommand(6.5,State("closeA"));DelayCommand(7.0,OpenA());DelayCommand(8.5,State("reopenA"));
 DelayCommand(9.0,CloseA());DelayCommand(10.5,State("linkedCloseA"));DelayCommand(12.0,PDone());}

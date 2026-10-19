#include "parity_inc"
#include "parity_time"
void Read(object c,object item,string tag){PLogI(GetTag(c)+"."+tag+".valid",GetIsObjectValid(item));PLogI(GetTag(c)+"."+tag+".stack",GetItemStackSize(item));}
void main(){object c=OBJECT_SELF;object p=GetSpellCastItem();PLogF(GetTag(c)+".impact",PNow()-GetLocalFloat(c,"start"),0.15);Read(c,p,"impact");DelayCommand(0.1,Read(c,p,"point1"));DelayCommand(0.3,Read(c,p,"point3"));DelayCommand(0.45,Read(c,p,"point45"));DelayCommand(0.6,Read(c,p,"point6"));DelayCommand(0.9,Read(c,p,"point9"));}

#include "sf_inc"
void main()
{
    Pair("d1",-10.0,40.0,1.0);Pair("d1p5",15.0,40.0,1.5);
    Pair("d2",0.0,0.0,2.0);Pair("d6",20.0,0.0,6.0);
    Pair("d10",0.0,30.0,10.0);Pair("d20",30.0,20.0,20.0);
    DelayCommand(22.0,PDone());
}

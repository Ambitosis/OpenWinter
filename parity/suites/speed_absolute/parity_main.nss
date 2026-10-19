#include "parity_inc"
location L(float x,float y) { return Location(GetObjectByTag("PARITY_AREA"),Vector(x,y,0.0),0.0); }
void Mark(object o) { SetLocalFloat(o,"start",GetPosition(o).x); }
float Median(float a,float b,float c) { if(a>b) { float t=a;a=b;b=t; } if(b>c)b=c; return a>b?a:b; }
void Report(object o,int input) {
 string k="speed."+IntToString(input); object m=GetModule(); int n=GetLocalInt(m,k)+1; SetLocalInt(m,k,n);
 float v=(GetPosition(o).x-GetLocalFloat(o,"start"))/2.0; SetLocalFloat(m,k+IntToString(n),v);
 if(n==3) SetLocalFloat(m,k+".median",Median(GetLocalFloat(m,k+"1"),GetLocalFloat(m,k+"2"),v));
}
void Go(int input,int lane) {
 float y=3.0+IntToFloat(lane)*3.0; object o=CreateObject(OBJECT_TYPE_CREATURE,"p_mover",L(2.0,y)); SetAILevel(o,AI_LEVEL_NORMAL);
 ApplyEffectToObject(DURATION_TYPE_PERMANENT,EffectMovementSpeedIncrease(input),o);
 AssignCommand(o,ActionMoveToLocation(L(80.0,y),TRUE)); DelayCommand(0.5,Mark(o)); DelayCommand(2.5,Report(o,input)); DelayCommand(3.0,DestroyObject(o));
}
void Batch() { Go(-150,0);Go(-100,1);Go(-10,2);Go(0,3);Go(50,4);Go(99,5);Go(100,6);Go(110,7);Go(150,8);Go(200,9); }
void Ratio(int input) {
 object m=GetModule(); string k="speed."+IntToString(input);
 PLogI(k+".samples",GetLocalInt(m,k));
 // Compare against this engine's unmodified lane to remove native sampling quantization.
 PLogF(k+".ratio",GetLocalFloat(m,k+".median")/GetLocalFloat(m,"speed.0.median"),0.025);
}
void Finish() { Ratio(-150);Ratio(-100);Ratio(-10);Ratio(0);Ratio(50);Ratio(99);Ratio(100);Ratio(110);Ratio(150);Ratio(200);PDone(); }
void main() { Batch();DelayCommand(4.0,Batch());DelayCommand(8.0,Batch());DelayCommand(12.0,Finish()); }

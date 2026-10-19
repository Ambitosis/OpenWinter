#include "parity_inc"
void Check(int n) {
 object c=GetObjectByTag("PROBE"), a=GetObjectByTag("WALL");
 int found=FALSE; object o=GetFirstInPersistentObject(a);
 while(GetIsObjectValid(o)) { if(o==c) found=TRUE; o=GetNextInPersistentObject(a); }
 PLogI("point."+IntToString(n),found);
}
void Place(int n,float x,float y) {
 object c=GetObjectByTag("PROBE");
 AssignCommand(c,JumpToLocation(PCentre(x,y)));
 DelayCommand(0.15,Check(n));
}
void Wall(float facing) {
 object old=GetObjectByTag("WALL"); if(GetIsObjectValid(old)) {SetTag(old,"OLD_WALL");DestroyObject(old);}
 ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY,EffectAreaOfEffect(AOE_PER_WALLBLADE,"wall_noop","wall_noop","wall_noop"),Location(GetObjectByTag("PARITY_AREA"),Vector(20.0,20.0,0.0),facing),10.0);
 object a=GetNearestObjectToLocation(OBJECT_TYPE_AREA_OF_EFFECT,PCentre());
 if(a==old)a=GetNearestObjectToLocation(OBJECT_TYPE_AREA_OF_EFFECT,PCentre(),2);SetTag(a,"WALL");
}
void Run() {
 object c=CreateObject(OBJECT_TYPE_CREATURE,"nw_bandit001",PCentre(-10.0,-10.0));SetTag(c,"PROBE");
 int i; for(i=5000;i<=5012;i++)SetEventScript(c,i,"");SetAILevel(c,AI_LEVEL_VERY_LOW);SetPlotFlag(c,TRUE);
 Wall(0.0);
 DelayCommand(0.3,Place(0,0.0,0.0));DelayCommand(0.6,Place(1,0.4,0.0));DelayCommand(0.9,Place(2,0.6,0.0));
 DelayCommand(1.2,Place(3,0.0,4.9));DelayCommand(1.5,Place(4,0.0,5.1));
 DelayCommand(1.8,Place(5,-0.4,-4.9));DelayCommand(2.1,Place(6,-0.6,0.0));DelayCommand(2.4,Place(7,0.0,-5.1));
 DelayCommand(3.0,Wall(90.0));
 DelayCommand(3.3,Place(8,0.0,0.0));DelayCommand(3.6,Place(9,0.0,0.4));DelayCommand(3.9,Place(10,0.0,0.6));
 DelayCommand(4.2,Place(11,4.9,0.0));DelayCommand(4.5,Place(12,5.1,0.0));
 DelayCommand(4.8,Place(13,-4.9,-0.4));DelayCommand(5.1,Place(14,0.0,-0.6));DelayCommand(5.4,Place(15,-5.1,0.0));
 DelayCommand(6.0,PDone());
}
void main(){DelayCommand(1.0,Run());}

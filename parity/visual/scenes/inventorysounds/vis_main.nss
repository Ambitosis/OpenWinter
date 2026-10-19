#include "visual_inc"
void ClearPack(object pc) {
 object it=GetFirstItemInInventory(pc);
 while(GetIsObjectValid(it)) { DestroyObject(it); it=GetNextItemInInventory(pc); }
}
void CreateArmor(object pc,int index) {
 string res="nw_aarcl00"+IntToString(index);
 if(index==10) res="nw_cloth001";
 if(index==11) res="nw_aarcl002";
 object it=CreateItemOnObject(res,pc);SetLocalObject(pc,"probe_item",it);
 if(index==11) AddItemProperty(DURATION_TYPE_PERMANENT,ItemPropertyACBonus(5),it);
 WriteTimestampedLogEntry("VISUAL|note|armor|"+res+"|ac="+IntToString(GetItemACValue(it))+"|name="+GetName(it));
}
void SpawnArmor(object pc,int index) { ClearPack(pc); DelayCommand(0.2,CreateArmor(pc,index)); }
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 ClearPack(pc);
 DelayCommand(2.0,VCamera(pc,90.0,8.0,60.0));
 DelayCommand(4.7,ExportSingleCharacter(pc));
 DelayCommand(3.0,WriteTimestampedLogEntry("VISUAL|key|73|tap"));
 int i;
 for(i=0;i<11;i++) {
  float t=4.0+IntToFloat(i)*3.0;
  DelayCommand(t,SpawnArmor(pc,i+1));
  DelayCommand(t+1.0,WriteTimestampedLogEntry("VISUAL|click|0.025,0.535"));
  if(i==0||i==9) DelayCommand(t+1.5,VShot("carry_"+IntToString(i)));
  DelayCommand(t+2.0,WriteTimestampedLogEntry("VISUAL|click|0.120,0.535"));
  DelayCommand(t+2.0,WriteTimestampedLogEntry("VISUAL|note|placement|"+IntToString(i+1)));

 }
 DelayCommand(37.0,ExportSingleCharacter(pc));
 DelayCommand(38.0,VDone());
}

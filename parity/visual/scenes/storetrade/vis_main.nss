#include "visual_inc"
void Click(float x, float y) {
    string xy = FloatToString(x / 1280.0,0,6) + "," + FloatToString(y / 720.0,0,6);
    WriteTimestampedLogEntry("VISUAL|mouse|" + xy + "|up");
    DelayCommand(0.25, WriteTimestampedLogEntry("VISUAL|mouse|" + xy + "|down"));
    DelayCommand(0.4, WriteTimestampedLogEntry("VISUAL|mouse|" + xy + "|up"));
}
void Note(object pc, string label) {
 WriteTimestampedLogEntry("VISUAL|note|"+label+"|gold="+IntToString(GetGold(pc)));
 object i=GetFirstItemInInventory(pc); while(GetIsObjectValid(i)) {
 WriteTimestampedLogEntry("VISUAL|note|"+label+"|item="+GetResRef(i)+"|stack="+IntToString(GetItemStackSize(i)));
 i=GetNextItemInInventory(pc); }
}
void Give(object pc, string resref, int n=1) { CreateItemOnObject(resref,pc,n); }
void Reset(object pc, object store) {
 object i=GetFirstItemInInventory(pc); while(GetIsObjectValid(i)) { DestroyObject(i); i=GetNextItemInInventory(pc); }
 DelayCommand(0.3, Give(pc,"nw_wswdg001"));
 DelayCommand(0.3, Give(pc,"nw_it_mpotion001",3));
 OpenStore(store,pc);
}
void main() {
 object pc=GetEnteringObject(); if(!GetIsPC(pc))return;
 SetPlotFlag(pc,TRUE);GiveGoldToCreature(pc,10000);
 object store=CreateObject(OBJECT_TYPE_STORE,"st_empty",GetLocation(pc));
 object armor=CreateItemOnObject("nw_aarcl001",store); SetInfiniteFlag(armor,TRUE);
 DelayCommand(2.0,VCamera(pc,90.0,12.0,60.0));
 DelayCommand(4.0,Reset(pc,store));
 DelayCommand(6.0,Click(48.0,369.0));
 DelayCommand(8.0,VShot("plain_buy"));
 DelayCommand(8.0,Note(pc,"plain_buy"));
 DelayCommand(10.0,Click(695.0,418.0));
 DelayCommand(11.0,AssignCommand(pc,ClearAllActions(TRUE)));
 DelayCommand(15.0,Reset(pc,store));
 DelayCommand(16.0,WriteTimestampedLogEntry("VISUAL|key|17|down"));
 DelayCommand(17.0,Click(48.0,369.0));
 DelayCommand(19.0,VShot("ctrl_buy"));
 DelayCommand(19.0,Note(pc,"ctrl_buy"));
 DelayCommand(20.0,WriteTimestampedLogEntry("VISUAL|key|17|up"));
 DelayCommand(21.0,Click(695.0,418.0));
 DelayCommand(22.0,AssignCommand(pc,ClearAllActions(TRUE)));
 DelayCommand(26.0,Reset(pc,store));
 DelayCommand(27.0,WriteTimestampedLogEntry("VISUAL|key|16|down"));
 DelayCommand(28.0,Click(48.0,369.0));
 DelayCommand(30.0,VShot("shift_buy"));
 DelayCommand(30.0,Note(pc,"shift_buy"));
 DelayCommand(31.0,WriteTimestampedLogEntry("VISUAL|key|16|up"));
 DelayCommand(32.0,Click(695.0,418.0));
 DelayCommand(33.0,AssignCommand(pc,ClearAllActions(TRUE)));
 DelayCommand(37.0,Reset(pc,store));
 DelayCommand(38.0,WriteTimestampedLogEntry("VISUAL|key|16|down"));
 DelayCommand(38.0,WriteTimestampedLogEntry("VISUAL|key|17|down"));
 DelayCommand(39.0,Click(48.0,369.0));
 DelayCommand(41.0,VShot("both_buy"));
 DelayCommand(41.0,Note(pc,"both_buy"));
 DelayCommand(42.0,WriteTimestampedLogEntry("VISUAL|key|16|up"));
 DelayCommand(42.0,WriteTimestampedLogEntry("VISUAL|key|17|up"));
 DelayCommand(43.0,Click(695.0,418.0));
 DelayCommand(44.0,AssignCommand(pc,ClearAllActions(TRUE)));
 DelayCommand(48.0,Reset(pc,store));
 DelayCommand(50.0,Click(892.0,400.0));
 DelayCommand(52.0,VShot("plain_sell"));
 DelayCommand(52.0,Note(pc,"plain_sell"));
 DelayCommand(54.0,Click(695.0,418.0));
 DelayCommand(55.0,AssignCommand(pc,ClearAllActions(TRUE)));
 DelayCommand(59.0,Reset(pc,store));
 DelayCommand(60.0,WriteTimestampedLogEntry("VISUAL|key|17|down"));
 DelayCommand(61.0,Click(892.0,400.0));
 DelayCommand(63.0,VShot("ctrl_sell"));
 DelayCommand(63.0,Note(pc,"ctrl_sell"));
 DelayCommand(64.0,WriteTimestampedLogEntry("VISUAL|key|17|up"));
 DelayCommand(65.0,Click(695.0,418.0));
 DelayCommand(66.0,AssignCommand(pc,ClearAllActions(TRUE)));
 DelayCommand(70.0,Reset(pc,store));
 DelayCommand(71.0,WriteTimestampedLogEntry("VISUAL|key|16|down"));
 DelayCommand(72.0,Click(892.0,400.0));
 DelayCommand(74.0,VShot("shift_sell"));
 DelayCommand(74.0,Note(pc,"shift_sell"));
 DelayCommand(75.0,WriteTimestampedLogEntry("VISUAL|key|16|up"));
 DelayCommand(76.0,Click(695.0,418.0));
 DelayCommand(77.0,AssignCommand(pc,ClearAllActions(TRUE)));
 DelayCommand(81.0,Reset(pc,store));
 DelayCommand(82.0,WriteTimestampedLogEntry("VISUAL|key|16|down"));
 DelayCommand(82.0,WriteTimestampedLogEntry("VISUAL|key|17|down"));
 DelayCommand(83.0,Click(892.0,400.0));
 DelayCommand(85.0,VShot("both_sell"));
 DelayCommand(85.0,Note(pc,"both_sell"));
 DelayCommand(86.0,WriteTimestampedLogEntry("VISUAL|key|16|up"));
 DelayCommand(86.0,WriteTimestampedLogEntry("VISUAL|key|17|up"));
 DelayCommand(87.0,Click(695.0,418.0));
 DelayCommand(88.0,AssignCommand(pc,ClearAllActions(TRUE)));
 DelayCommand(92.0,VDone());
}

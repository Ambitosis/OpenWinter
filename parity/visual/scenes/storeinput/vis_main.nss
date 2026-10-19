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
void Hover(float x,float y) { WriteTimestampedLogEntry("VISUAL|mouse|"+FloatToString(x/1280.0,0,6)+","+FloatToString(y/720.0,0,6)+"|up"); }
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetPlotFlag(pc,TRUE);GiveGoldToCreature(pc,10000);
 object s=CreateObject(OBJECT_TYPE_STORE,"st_empty",GetLocation(pc));
 object a=CreateItemOnObject("nw_aarcl001",s);SetInfiniteFlag(a,TRUE);
 object d=GetFirstItemInInventory(pc);while(GetIsObjectValid(d)){DestroyObject(d);d=GetNextItemInInventory(pc);}
 DelayCommand(1.0,Give(pc,"nw_aarcl001"));
 DelayCommand(1.5,Give(pc,"nw_wswdg001"));
 DelayCommand(2.0,VCamera(pc,90.0,12.0,60.0));
 DelayCommand(3.0,OpenStore(s,pc));
 DelayCommand(5.0,Hover(50.0,365.0));DelayCommand(7.0,VShot("a_store_unusable"));
 DelayCommand(9.0,Hover(886.0,365.0));DelayCommand(11.0,VShot("b_pack_unusable"));
 DelayCommand(13.0,Hover(933.0,399.0));DelayCommand(15.0,VShot("c_pack_usable"));
 DelayCommand(17.0,Click(933.0,399.0)); DelayCommand(19.0,Click(165.0,250.0));DelayCommand(21.0,VShot("d_sell_drop"));DelayCommand(21.0,Note(pc,"sell_pending"));
 DelayCommand(23.0,Click(580.0,418.0)); DelayCommand(25.0,Note(pc,"sold"));DelayCommand(25.0,VShot("e_sold"));
 DelayCommand(27.0,Click(48.0,369.0)); DelayCommand(29.0,Click(580.0,418.0)); DelayCommand(31.0,Note(pc,"bought"));DelayCommand(31.0,VShot("f_bought"));
 DelayCommand(33.0,WriteTimestampedLogEntry("VISUAL|key|18|down"));DelayCommand(34.0,Click(48.0,369.0));DelayCommand(36.0,VShot("g_alt_buy"));DelayCommand(36.0,Note(pc,"alt_buy"));DelayCommand(37.0,WriteTimestampedLogEntry("VISUAL|key|18|up"));
 DelayCommand(39.0,Click(695.0,418.0));
 DelayCommand(41.0,Hover(48.0,369.0));
 DelayCommand(42.0,WriteTimestampedLogEntry("VISUAL|mouse|0.037500,0.512500|down"));
 DelayCommand(43.0,WriteTimestampedLogEntry("VISUAL|mouse|0.755000,0.350000|down"));
 DelayCommand(44.0,WriteTimestampedLogEntry("VISUAL|mouse|0.755000,0.350000|up"));
 DelayCommand(46.0,VShot("h_buy_drag"));DelayCommand(46.0,Note(pc,"drag_buy"));
 DelayCommand(48.0,Click(695.0,418.0));
 DelayCommand(50.0,WriteTimestampedLogEntry("VISUAL|key|16|down"));
 DelayCommand(51.0,Hover(48.0,369.0));
 DelayCommand(52.0,WriteTimestampedLogEntry("VISUAL|mouse|0.037500,0.512500|down"));
 DelayCommand(53.0,WriteTimestampedLogEntry("VISUAL|mouse|0.780000,0.350000|down"));
 DelayCommand(54.0,WriteTimestampedLogEntry("VISUAL|mouse|0.780000,0.350000|up"));
 DelayCommand(56.0,VShot("i_shift_buy_drag"));DelayCommand(56.0,Note(pc,"shift_drag_buy"));
 DelayCommand(57.0,WriteTimestampedLogEntry("VISUAL|key|16|up"));
 DelayCommand(59.0,VDone());
}

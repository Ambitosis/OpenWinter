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
void Mark(string label) { WriteTimestampedLogEntry("VISUAL|note|store_audio|"+label); }
void MarkClick(string label,float x,float y) { Mark(label); Click(x,y); }
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetPlotFlag(pc,TRUE);GiveGoldToCreature(pc,10000);
 object area=GetArea(pc); MusicBackgroundStop(area); MusicBattleStop(area); AmbientSoundSetDayVolume(area,0); AmbientSoundSetNightVolume(area,0);
 object s=CreateObject(OBJECT_TYPE_STORE,"st_empty",GetLocation(pc));
 object a=CreateItemOnObject("nw_aarcl001",s);SetInfiniteFlag(a,TRUE);
 object w=CreateItemOnObject("nw_wswdg001",s);SetInfiniteFlag(w,TRUE);
 object d=GetFirstItemInInventory(pc);while(GetIsObjectValid(d)){DestroyObject(d);d=GetNextItemInInventory(pc);}
 DelayCommand(2.0,VCamera(pc,90.0,12.0,60.0));DelayCommand(3.0,OpenStore(s,pc));
 DelayCommand(6.0,MarkClick("cancel_prompt",48.0,369.0));DelayCommand(8.0,VShot("a_confirmation"));
 DelayCommand(10.0,MarkClick("cancel",695.0,418.0));
 DelayCommand(12.0,VShot("a2_cancelled"));
 DelayCommand(16.0,VShot("a3_armor_confirm"));DelayCommand(19.0,VShot("a4_armor_bought"));
 DelayCommand(14.0,MarkClick("armor_prompt",48.0,369.0));
 DelayCommand(17.0,MarkClick("armor_buy",580.0,418.0));DelayCommand(19.0,Note(pc,"armor_bought"));
 DelayCommand(20.0,Click(112.0,44.0));
 DelayCommand(22.0,MarkClick("dagger_prompt",30.0,400.0));
 DelayCommand(25.0,MarkClick("dagger_buy",580.0,418.0));DelayCommand(27.0,Note(pc,"dagger_bought"));
 DelayCommand(28.0,Hover(30.0,400.0));DelayCommand(29.0,Mark("drag_pickup"));
 DelayCommand(29.0,WriteTimestampedLogEntry("VISUAL|mouse|0.023438,0.555556|down"));
 DelayCommand(30.0,WriteTimestampedLogEntry("VISUAL|mouse|0.755000,0.350000|down"));
 DelayCommand(32.0,Mark("drag_buy"));DelayCommand(32.0,WriteTimestampedLogEntry("VISUAL|mouse|0.755000,0.350000|up"));DelayCommand(33.0,Note(pc,"drag_bought"));
 DelayCommand(35.0,WriteTimestampedLogEntry("VISUAL|key|16|down"));
 DelayCommand(36.0,MarkClick("shift_buy",30.0,400.0));
 DelayCommand(38.0,Note(pc,"shift_bought"));DelayCommand(38.0,VShot("b_shift_qol"));
 DelayCommand(39.0,WriteTimestampedLogEntry("VISUAL|key|16|up"));
 DelayCommand(41.0,VDone());
}

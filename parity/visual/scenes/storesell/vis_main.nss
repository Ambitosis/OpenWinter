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
void Stocks(object s) {int n=0;object i=GetFirstItemInInventory(s);while(GetIsObjectValid(i)){n++;i=GetNextItemInInventory(s);}WriteTimestampedLogEntry("VISUAL|note|stock_count|"+IntToString(n));}
void main(){object pc=GetEnteringObject();if(!GetIsPC(pc))return;SetPlotFlag(pc,TRUE);
object i=GetFirstItemInInventory(pc);while(GetIsObjectValid(i)){DestroyObject(i);i=GetNextItemInInventory(pc);}
object s=CreateObject(OBJECT_TYPE_STORE,"st_empty",GetLocation(pc));object a=CreateItemOnObject("nw_aarcl001",s);SetInfiniteFlag(a,TRUE);
DelayCommand(1.0,Give(pc,"nw_aarcl001"));DelayCommand(1.5,Give(pc,"nw_wswdg001"));
DelayCommand(2.0,VCamera(pc,90.0,12.0,60.0));DelayCommand(3.0,OpenStore(s,pc));
DelayCommand(5.0,Click(878.0,365.0));DelayCommand(7.0,Click(160.0,250.0));DelayCommand(9.0,Note(pc,"sold_infinite"));DelayCommand(9.0,Stocks(s));DelayCommand(9.0,VShot("a_sold_infinite"));
DelayCommand(11.0,Hover(930.0,400.0));
DelayCommand(12.0,WriteTimestampedLogEntry("VISUAL|mouse|0.726563,0.555556|down"));
DelayCommand(12.05,WriteTimestampedLogEntry("VISUAL|mouse|0.726563,0.555556|up"));
DelayCommand(12.15,WriteTimestampedLogEntry("VISUAL|mouse|0.726563,0.555556|down"));
DelayCommand(12.2,WriteTimestampedLogEntry("VISUAL|mouse|0.726563,0.555556|up"));
DelayCommand(14.0,VShot("b_double_sell"));DelayCommand(14.0,Note(pc,"double_sell"));DelayCommand(18.0,VDone());}

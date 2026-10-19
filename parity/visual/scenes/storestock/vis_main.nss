#include "visual_inc"
void Note(string name, object i, object s) { WriteTimestampedLogEntry("VISUAL|note|"+name+"|valid="+IntToString(GetIsObjectValid(i))+"|owner="+IntToString(GetItemPossessor(i)==s)+"|stack="+IntToString(GetItemStackSize(i))); }
void Create(object s, string rr, string name, string tag="") { object i=CreateItemOnObject(rr,s,1,tag); Note(name,i,s); }
void main() {
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;
 object s=CreateObject(OBJECT_TYPE_STORE,"st_empty",GetLocation(pc));
 object a=CreateItemOnObject("nw_aarcl001",s);Note("first",a,s);SetInfiniteFlag(a,TRUE);
 DelayCommand(1.0,Create(s,"nw_aarcl001","duplicate_infinite"));
 DelayCommand(2.0,Create(s,"nw_aarcl001","retagged_infinite","different_tag"));
 DelayCommand(3.0,Create(s,"nw_aarcl002","different_armor"));
 DelayCommand(4.0,Create(s,"nw_wswdg001","new_finite"));
 DelayCommand(5.0,Create(s,"nw_wswdg001","duplicate_finite"));
 DelayCommand(6.0,Create(s,"nw_it_mpotion001","potion"));
 DelayCommand(7.0,Create(s,"nw_it_mpotion001","potion_again"));
 DelayCommand(8.0,OpenStore(s,pc)); DelayCommand(10.0,VShot("stock"));
 DelayCommand(12.0,VDone());
}

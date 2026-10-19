// Shared by the transitfollow scene's scripts: a tenth-second clock and readings of who is where.
string Vec(vector v) { return FloatToString(v.x,0,2)+","+FloatToString(v.y,0,2); }
string Clock() { return FloatToString(IntToFloat(GetLocalInt(GetModule(),"TF_TICK"))/10.0,0,1); }
string AreaTag(object o) { object a=GetArea(o); return GetIsObjectValid(a) ? GetTag(a) : "none"; }
string Where(object o) {
    if(!GetIsObjectValid(o)) return "gone";
    return AreaTag(o)+"@"+Vec(GetPosition(o))+"/a"+IntToString(GetCurrentAction(o));
}
void TFNote(string s) { WriteTimestampedLogEntry("VISUAL|note|t="+Clock()+"|"+s); }

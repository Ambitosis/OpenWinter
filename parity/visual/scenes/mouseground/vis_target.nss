void main() {
    vector v=GetTargetingModeSelectedPosition(); vector p=GetPosition(GetFirstPC());
    WriteTimestampedLogEntry("VISUAL|note|"+GetLocalString(GetModule(),"phase")+"|"+FloatToString(v.x,0,5)+","+FloatToString(v.y,0,5)
      +"|distance="+FloatToString(VectorMagnitude(v-p),0,5));
}

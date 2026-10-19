void main(){
 if(GetSpellId()!=SPELL_GREASE)return;
 SetLocalInt(OBJECT_SELF,"ground_cast",GetLocalInt(OBJECT_SELF,"ground_cast")+1);
 SetLocalLocation(OBJECT_SELF,"ground_cast_location",GetSpellTargetLocation());
 vector v=GetPositionFromLocation(GetSpellTargetLocation());
 WriteTimestampedLogEntry("VISUAL|note|ground_cast|"+FloatToString(v.x)+","+FloatToString(v.y)+","+FloatToString(v.z));
}

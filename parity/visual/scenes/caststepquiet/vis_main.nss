#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Walk(object pc){
    Note("walk.start");
    vector v=GetPosition(pc);
    AssignCommand(pc,ActionMoveToLocation(Location(GetArea(pc),Vector(v.x,v.y+4.0,v.z),90.0),FALSE));
}
void Stop(object pc){AssignCommand(pc,ClearAllActions(TRUE));Note("walk.stop");}
void IdleMarker(object pc){
    Note("idle.start");
    ReplaceObjectAnimation(pc,"pause1","cwalkf");
    AssignCommand(pc,ActionPlayAnimation(ANIMATION_LOOPING_PAUSE,1.0,2.5));
}
void Reset(object pc,string clip){ReplaceObjectAnimation(pc,clip,"");Note("reset."+clip);}
void CastMarker(object pc,string clip,string marker){
    Note("cast.start."+clip);
    ReplaceObjectAnimation(pc,clip,marker);
    AssignCommand(pc,ActionCastSpellAtObject(SPELL_MAGE_ARMOR,pc,METAMAGIC_NONE,TRUE));
    AssignCommand(pc,ActionDoCommand(Note("cast.done."+clip)));
}
void main(){
    object pc=GetEnteringObject();if(!GetIsPC(pc))return;VBegin(pc);
    Note("pc."+GetName(pc));SetFootstepType(0,pc);
    DelayCommand(1.0,VCamera(pc,90.0,6.0,65.0));
    DelayCommand(2.0,IdleMarker(pc));DelayCommand(5.0,Reset(pc,"pause1"));
    DelayCommand(6.0,CastMarker(pc,"castselflp","walk"));DelayCommand(10.0,Reset(pc,"castselflp"));
    DelayCommand(14.0,CastMarker(pc,"castself","cwalkf"));DelayCommand(18.0,Reset(pc,"castself"));
    DelayCommand(3.0,VShot("idle_marker"));DelayCommand(8.3,VShot("loop_marker"));
    DelayCommand(15.8,VShot("release_marker"));DelayCommand(20.0,VDone());
}

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
    DelayCommand(2.0,Walk(pc));DelayCommand(5.5,Stop(pc));
    DelayCommand(6.0,IdleMarker(pc));DelayCommand(9.0,Reset(pc,"pause1"));
    DelayCommand(10.0,CastMarker(pc,"conjure1","walk"));DelayCommand(14.0,Reset(pc,"conjure1"));
    DelayCommand(18.0,CastMarker(pc,"castselflp","walk"));DelayCommand(22.0,Reset(pc,"castselflp"));
    DelayCommand(26.0,CastMarker(pc,"castself","cwalkf"));DelayCommand(30.0,Reset(pc,"castself"));
    DelayCommand(3.0,VShot("actual_walk"));DelayCommand(7.0,VShot("idle_marker"));
    DelayCommand(11.0,VShot("conjure_marker"));DelayCommand(20.3,VShot("loop_marker"));
    DelayCommand(27.8,VShot("release_marker"));DelayCommand(32.0,VDone());
}

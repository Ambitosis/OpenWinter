#include "visual_inc"
void Note(string s){WriteTimestampedLogEntry("VISUAL|note|"+s);}
void Mouse(string xy,string state){WriteTimestampedLogEntry("VISUAL|mouse|"+xy+"|"+state);}
void Hover(string xy){WriteTimestampedLogEntry("VISUAL|hover|"+xy);}
void Observe(object pc,object chest,object shield,object club,string phase)
{
 Note(phase+".shield-chest."+IntToString(GetItemPossessor(shield)==chest)+".club-chest."+IntToString(GetItemPossessor(club)==chest));
}
void Setup(object pc)
{
 object chest=VPlaceable(pc,"plc_chest1",1.2,0.0,180.0);SetTag(chest,"placement_chest");SetLocked(chest,FALSE);SetEventScript(chest,EVENT_SCRIPT_PLACEABLE_ON_OPEN,"");
 object shield=CreateItemOnObject("nw_ashsw001",pc,1,"placement_shield");
 object club=CreateItemOnObject("nw_wblcl001",pc,1,"placement_club");
 Note("fixture.shield."+IntToString(GetIsObjectValid(shield))+".club."+IntToString(GetIsObjectValid(club)));
 DelayCommand(1.5,AssignCommand(pc,ActionInteractObject(chest)));
 DelayCommand(4.0,VShot("a_before"));
 DelayCommand(5.0,Mouse("0.03,0.54","down"));
 DelayCommand(5.3,Hover("0.030,0.095"));
 DelayCommand(6.2,VShot("b_edge_preview"));
 DelayCommand(6.8,Mouse("0.030,0.095","up"));
 DelayCommand(7.5,VShot("c_shield_placed"));
 DelayCommand(8.0,Observe(pc,chest,shield,club,"shield"));
 DelayCommand(9.0,Mouse("0.068,0.54","down"));
 DelayCommand(9.3,Hover("0.030,0.095"));
 DelayCommand(10.2,VShot("d_overlap_preview"));
 DelayCommand(10.8,Mouse("0.030,0.095","up"));
 DelayCommand(11.5,VShot("e_club_placed"));
 DelayCommand(12.0,Observe(pc,chest,shield,club,"club"));
 DelayCommand(17.0,VDone());
}
void main()
{
 object pc=GetEnteringObject();if(!GetIsPC(pc))return;GiveGoldToCreature(pc,123);
 object i=GetFirstItemInInventory(pc);while(GetIsObjectValid(i)){DestroyObject(i);i=GetNextItemInInventory(pc);}
 DelayCommand(.5,Setup(pc));DelayCommand(1.0,VCamera(pc,90.0,6.0,60.0));
}

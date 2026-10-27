// Chat commands for the test bed: "possess" creates a goblin beside the speaker and possesses it, "unpossess"
// returns to the character (the possessed creature speaks then), "dodgering" equips a ring that grants Dodge (a bonus feat from an item).
#include "nwnx_player"

void main()
{
    object oPC = GetPCChatSpeaker();
    string sLine = GetPCChatMessage();
    if (sLine == "possess")
    {
        object oGoblin = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", GetLocation(oPC));
        SetLocalObject(GetModule(), "BR_POSSESSED", oGoblin);
        NWNX_Player_PossessCreature(oPC, oGoblin);
    }
    else if (sLine == "unpossess")
    {
        UnpossessFamiliar(GetLocalObject(GetModule(), "BR_POSSESSED"));
    }
    else if (sLine == "dodgering")
    {
        object oRing = CreateItemOnObject("nw_it_mring001", oPC);
        AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyBonusFeat(IP_CONST_FEAT_DODGE), oRing);
        AssignCommand(oPC, ActionEquipItem(oRing, INVENTORY_SLOT_LEFTRING));
    }
}

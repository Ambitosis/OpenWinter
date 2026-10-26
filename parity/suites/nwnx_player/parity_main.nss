// NWNX_Player calls given objects that aren't players (nwserver has no players here): what they return.
#include "parity_inc"
#include "nwnx_player"

void main()
{
    object oC = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre());
    PLogI("game.object.npc", NWNX_Player_GetGameObject(oC) == OBJECT_INVALID);
    PLogI("game.object.invalid", NWNX_Player_GetGameObject(OBJECT_INVALID) == OBJECT_INVALID);
    PLogI("quest.npc", NWNX_Player_GetQuestCompleted(oC, "q"));
    PLogI("store.npc", NWNX_Player_GetOpenStore(oC) == OBJECT_INVALID);
    PLogI("turd.npc", NWNX_Player_GetTURD(oC) == OBJECT_INVALID);
    PLog("bic.npc", NWNX_Player_GetBicFileName(oC));
    PLogI("platform.npc", NWNX_Player_GetPlatformId(oC));
    PLogI("language.npc", NWNX_Player_GetLanguage(oC));
    PLog("explore.npc", NWNX_Player_GetAreaExplorationState(oC, GetArea(oC)));
    struct NWNX_Player_JournalEntry j = NWNX_Player_GetJournalEntry(oC, "q");
    PLogI("journal.npc", j.nUpdated);
    struct NWNX_Player_JournalEntry e;
    e.sName = "N"; e.sText = "T"; e.sTag = "q";
    PLogI("journal.add.npc", NWNX_Player_AddCustomJournalEntry(oC, e));
    struct NWNX_Player_QuickBarSlot s = NWNX_Player_GetQuickBarSlot(oC, 0);
    PLogI("qb.npc", s.nObjectType);
    PLogI("possess.npc", NWNX_Player_PossessCreature(oC, oC));
    PDone();
}

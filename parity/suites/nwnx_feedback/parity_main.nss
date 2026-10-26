// NWNX_Feedback: global and personal hidden states as the getters report them (no players: personal on an NPC).
#include "parity_inc"
#include "nwnx_feedback"

void main()
{
    object oC = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre());
    PLogI("fb.default", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL));
    NWNX_Feedback_SetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL, TRUE);
    PLogI("fb.set", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL));
    PLogI("fb.other", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_CONTAINER_FULL));
    PLogI("cl.same.id", NWNX_Feedback_GetCombatLogMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL));
    NWNX_Feedback_SetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL, FALSE);
    PLogI("fb.cleared", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL));
    NWNX_Feedback_SetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL, 5);
    PLogI("fb.set5", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL));
    NWNX_Feedback_SetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL, FALSE);

    PLogI("cl.default", NWNX_Feedback_GetCombatLogMessageHidden(NWNX_FEEDBACK_COMBATLOG_COMPLEX_ATTACK));
    NWNX_Feedback_SetCombatLogMessageHidden(NWNX_FEEDBACK_COMBATLOG_COMPLEX_ATTACK, TRUE);
    PLogI("cl.set", NWNX_Feedback_GetCombatLogMessageHidden(NWNX_FEEDBACK_COMBATLOG_COMPLEX_ATTACK));
    PLogI("cl.fb.same.id", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_COMBATLOG_COMPLEX_ATTACK));

    PLogI("jr.default", NWNX_Feedback_GetJournalUpdatedMessageHidden());
    NWNX_Feedback_SetJournalUpdatedMessageHidden(TRUE);
    PLogI("jr.set", NWNX_Feedback_GetJournalUpdatedMessageHidden());

    PLogI("npc.default", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL, oC));
    NWNX_Feedback_SetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL, TRUE, oC);
    PLogI("npc.set", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL, oC));
    PLogI("npc.global", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_INVENTORY_FULL));
    PLogI("npc.cl", NWNX_Feedback_GetCombatLogMessageHidden(NWNX_FEEDBACK_COMBATLOG_COMPLEX_ATTACK, oC));

    NWNX_Feedback_SetFeedbackMessageMode(TRUE);
    PLogI("wl.default", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_CONTAINER_FULL));
    NWNX_Feedback_SetFeedbackMessageHidden(NWNX_FEEDBACK_CONTAINER_FULL, TRUE);
    PLogI("wl.set", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_CONTAINER_FULL));
    NWNX_Feedback_SetFeedbackMessageMode(FALSE);
    PLogI("wl.off", NWNX_Feedback_GetFeedbackMessageHidden(NWNX_FEEDBACK_CONTAINER_FULL));
    PDone();
}

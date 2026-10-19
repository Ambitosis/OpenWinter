// Reputation: run on a commoner. Objects come from module locals.
void main()
{
    object m = GetModule();
    object npc = OBJECT_SELF;
    object orc = GetLocalObject(m, "orc");
    object pc1 = GetLocalObject(m, "pc1");
    object pc2 = GetLocalObject(m, "pc2");

    SetLocalInt(m, "r_orc", GetReputation(npc, orc));
    SetLocalInt(m, "enemy_orc", GetIsEnemy(orc));
    SetIsTemporaryFriend(orc, npc);
    SetLocalInt(m, "friend_orc", GetIsFriend(orc));
    SetLocalInt(m, "react_orc", GetIsReactionTypeFriendly(orc));
    ClearPersonalReputation(orc, npc);
    SetLocalInt(m, "enemy_orc2", GetIsEnemy(orc));

    AdjustReputation(pc1, npc, -100);
    SetLocalInt(m, "enemy_pc1", GetIsEnemy(pc1));
    SetLocalInt(m, "pc1_view", GetReputation(pc1, npc));
    SetLocalInt(m, "std_pc1", GetStandardFactionReputation(STANDARD_FACTION_COMMONER, pc1));
    SetLocalInt(m, "enemy_pc2", GetIsEnemy(pc2));
    AdjustReputation(npc, pc1, -100); // a PC as the source changes nothing
    SetLocalInt(m, "neutral_npc_by_pc", GetIsNeutral(npc, pc2));

    SetIsTemporaryEnemy(pc2, npc, TRUE, 10.0);
    SetLocalInt(m, "temp_enemy", GetIsEnemy(pc2));

    SetStandardFactionReputation(STANDARD_FACTION_HOSTILE, 100, npc);
    SetLocalInt(m, "std_hostile", GetStandardFactionReputation(STANDARD_FACTION_HOSTILE, npc));
    SetLocalInt(m, "orc_friend", GetIsFriend(npc, orc));
    SetLocalInt(m, "avg_rep", GetFactionAverageReputation(npc, pc1));
    SetLocalInt(m, "bad", GetReputation(npc, OBJECT_INVALID));
}

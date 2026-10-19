// Run on the observer: every CREATURE_TYPE_* filter.
void main()
{
    object m = GetModule();
    SetLocalObject(m, "pc", GetNearestCreature(CREATURE_TYPE_PLAYER_CHAR, PLAYER_CHAR_IS_PC));
    SetLocalObject(m, "npc1", GetNearestCreature(CREATURE_TYPE_PLAYER_CHAR, PLAYER_CHAR_NOT_PC));
    SetLocalObject(m, "npc2", GetNearestCreature(CREATURE_TYPE_PLAYER_CHAR, PLAYER_CHAR_NOT_PC, OBJECT_SELF, 2));
    SetLocalObject(m, "enemy", GetNearestCreature(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY));
    SetLocalObject(m, "friend", GetNearestCreature(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_FRIEND));
    SetLocalObject(m, "neutral", GetNearestCreature(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_NEUTRAL));
    SetLocalObject(m, "elf", GetNearestCreature(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_ELF));
    SetLocalObject(m, "wizard", GetNearestCreature(CREATURE_TYPE_CLASS, CLASS_TYPE_WIZARD));
    SetLocalObject(m, "dead", GetNearestCreature(CREATURE_TYPE_IS_ALIVE, FALSE));
    SetLocalObject(m, "blessed", GetNearestCreature(CREATURE_TYPE_HAS_SPELL_EFFECT, SPELL_BLESS));
    SetLocalObject(m, "unblessed_enemy", GetNearestCreature(CREATURE_TYPE_DOES_NOT_HAVE_SPELL_EFFECT, SPELL_BLESS, OBJECT_SELF, 1,
                                                           CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY));
    SetLocalObject(m, "seen", GetNearestCreature(CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN));
    SetLocalObject(m, "notseen", GetNearestCreature(CREATURE_TYPE_PERCEPTION, PERCEPTION_NOT_SEEN));
    SetLocalObject(m, "three", GetNearestCreature(CREATURE_TYPE_IS_ALIVE, TRUE, OBJECT_SELF, 1,
                                                 CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_HUMAN, CREATURE_TYPE_CLASS, CLASS_TYPE_FIGHTER));
    SetLocalObject(m, "loc", GetNearestCreatureToLocation(CREATURE_TYPE_PLAYER_CHAR, PLAYER_CHAR_NOT_PC,
                                                          Location(GetArea(OBJECT_SELF), Vector(30.0, 30.0, 0.0), 0.0)));
    SetLocalObject(m, "none", GetNearestCreature(CREATURE_TYPE_RACIAL_TYPE, RACIAL_TYPE_DRAGON));
}

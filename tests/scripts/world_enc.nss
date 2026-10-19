// Run on an encounter: reports its settings.
void main()
{
    object m = GetModule();
    SetLocalInt(m, "enc_active", GetEncounterActive());
    SetLocalInt(m, "enc_difficulty", GetEncounterDifficulty());
    SetLocalInt(m, "enc_spawns", GetEncounterSpawnsCurrent());
    SetLocalInt(m, "enc_max", GetEncounterSpawnsMax());
}

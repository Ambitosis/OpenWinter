// The server settings a module sees at load: difficulty, play options, passwords, name and level limits.
#include "parity_inc"
#include "nwnx_admin"

void main()
{
    PLogI("difficulty", GetGameDifficulty());
    int i;
    for (i = 10; i <= 28; i++) PLogI("opt." + IntToString(i), NWNX_Administration_GetPlayOption(i));
    PLog("pw.player", NWNX_Administration_GetPlayerPassword());
    PLog("pw.dm", NWNX_Administration_GetDMPassword());
    PLog("server.name", NWNX_Administration_GetServerName());
    PLogI("level.min", NWNX_Administration_GetMinLevel());
    PLogI("level.max", NWNX_Administration_GetMaxLevel());
    PDone();
}

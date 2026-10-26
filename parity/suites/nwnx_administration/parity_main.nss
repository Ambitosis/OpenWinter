// NWNX_Administration getters and setters: passwords, server name, bans, play options, debug values, levels.
#include "parity_inc"
#include "nwnx_admin"
#include "nwnx_util"

// The ban list spans lines; one reading per list.
string Flat(string s)
{
    string r = "";
    int n = GetStringLength(s), i;
    for (i = 0; i < n; i++)
    {
        string c = GetSubString(s, i, 1);
        int a = FindSubString(NWNX_Util_GetAsciiTableString(), c, 1);
        r += a < 32 ? "<" + IntToString(a) + ">" : c;
    }
    return r;
}

void main()
{
    PLog("pw.player.default", NWNX_Administration_GetPlayerPassword());
    NWNX_Administration_SetPlayerPassword("secret");
    PLog("pw.player.set", NWNX_Administration_GetPlayerPassword());
    NWNX_Administration_ClearPlayerPassword();
    PLog("pw.player.cleared", NWNX_Administration_GetPlayerPassword());

    PLog("pw.dm.default", NWNX_Administration_GetDMPassword());
    NWNX_Administration_SetDMPassword("dmpass");
    PLog("pw.dm.set", NWNX_Administration_GetDMPassword());
    NWNX_Administration_SetDMPassword("");
    PLog("pw.dm.empty", NWNX_Administration_GetDMPassword());

    NWNX_Administration_SetServerName("Parity Server");
    PLog("server.name", NWNX_Administration_GetServerName());
    NWNX_Administration_SetModuleName("Parity Module");
    PLog("module.name", GetModuleName());

    PLog("ban.empty", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_AddBannedIP("10.1.2.3");
    PLog("ban.ip", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_AddBannedCDKey("ABCDEFGH");
    PLog("ban.key", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_AddBannedPlayerName("Griefer");
    PLog("ban.name", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_AddBannedIP("10.1.2.4");
    NWNX_Administration_AddBannedIP("10.1.2.3");
    PLog("ban.ip2", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_RemoveBannedIP("10.1.2.3");
    PLog("ban.unip", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_RemoveBannedCDKey("ABCDEFGH");
    NWNX_Administration_RemoveBannedPlayerName("Griefer");
    NWNX_Administration_RemoveBannedIP("10.1.2.4");
    PLog("ban.cleared", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_AddBannedPlayerName("griefer");
    NWNX_Administration_RemoveBannedPlayerName("GRIEFER");
    PLog("ban.case", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_RemoveBannedPlayerName("griefer");
    NWNX_Administration_AddBannedCDKey("abcdefgh");
    NWNX_Administration_AddBannedIP("");
    PLog("ban.lower", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_RemoveBannedCDKey("ABCDEFGH");
    PLog("ban.lower.remove", Flat(NWNX_Administration_GetBannedList()));
    NWNX_Administration_RemoveBannedCDKey("abcdefgh");
    NWNX_Administration_RemoveBannedIP("");

    int i;
    for (i = 10; i <= 29; i++)
        if (i != 12 && i != 13) PLogI("opt.default." + IntToString(i), NWNX_Administration_GetPlayOption(i));
    for (i = 0; i <= 28; i++)
    {
        int nOld = NWNX_Administration_GetPlayOption(i);
        NWNX_Administration_SetPlayOption(i, i == 10 ? 2 : (i <= 9 ? 7 : 1));
        PLogI("opt.set." + IntToString(i), NWNX_Administration_GetPlayOption(i));
        NWNX_Administration_SetPlayOption(i, 0);
        PLogI("opt.clear." + IntToString(i), NWNX_Administration_GetPlayOption(i));
        NWNX_Administration_SetPlayOption(i, nOld);
    }
    NWNX_Administration_SetPlayOption(10, 5);
    PLogI("opt.pvp5", NWNX_Administration_GetPlayOption(10));
    NWNX_Administration_SetPlayOption(10, 1);
    NWNX_Administration_SetPlayOption(11, 3);
    PLogI("opt.pause3", NWNX_Administration_GetPlayOption(11));
    NWNX_Administration_SetPlayOption(11, 0);

    for (i = 0; i <= 4; i++)
    {
        PLogI("debug.default." + IntToString(i), NWNX_Administration_GetDebugValue(i));
        NWNX_Administration_SetDebugValue(i, TRUE);
        PLogI("debug.set." + IntToString(i), NWNX_Administration_GetDebugValue(i));
        NWNX_Administration_SetDebugValue(i, FALSE);
        PLogI("debug.clear." + IntToString(i), NWNX_Administration_GetDebugValue(i));
    }

    PLogI("level.min", NWNX_Administration_GetMinLevel());
    PLogI("level.max", NWNX_Administration_GetMaxLevel());
    NWNX_Administration_SetMinLevel(3);
    NWNX_Administration_SetMaxLevel(20);
    PLogI("level.min3", NWNX_Administration_GetMinLevel());
    PLogI("level.max20", NWNX_Administration_GetMaxLevel());
    NWNX_Administration_SetMinLevel(0);
    PLogI("level.min0", NWNX_Administration_GetMinLevel());
    NWNX_Administration_SetMaxLevel(41);
    PLogI("level.max41", NWNX_Administration_GetMaxLevel());
    NWNX_Administration_SetMinLevel(30);
    PLogI("level.min30", NWNX_Administration_GetMinLevel());
    NWNX_Administration_SetMaxLevel(255);
    PLogI("level.max255", NWNX_Administration_GetMaxLevel());
    NWNX_Administration_SetMaxLevel(41);
    NWNX_Administration_SetMinLevel(50);
    PLogI("level.min50.max41", NWNX_Administration_GetMinLevel());
    NWNX_Administration_SetMinLevel(1);
    NWNX_Administration_SetMaxLevel(0);
    PLogI("level.max0", NWNX_Administration_GetMaxLevel());
    NWNX_Administration_SetMaxLevel(256);
    PLogI("level.max256", NWNX_Administration_GetMaxLevel());
    NWNX_Administration_SetMinLevel(256);
    PLogI("level.min256", NWNX_Administration_GetMinLevel());
    NWNX_Administration_SetMinLevel(-1);
    PLogI("level.minneg", NWNX_Administration_GetMinLevel());
    NWNX_Administration_SetMaxLevel(10);
    NWNX_Administration_SetMinLevel(20);
    PLogI("level.min20.max10", NWNX_Administration_GetMinLevel());
    PLogI("level.max10", NWNX_Administration_GetMaxLevel());
    NWNX_Administration_SetMinLevel(1);
    NWNX_Administration_SetMaxLevel(40);

    PLogI("turd.none", NWNX_Administration_DeleteTURD("nobody", "No One"));
    NWNX_Administration_ReloadRules();
    PLogI("rules.reloaded", Get2DAString("classes", "HitDie", 0) != "");
    PDone();
}

// NWNX_SQL settings: the type name as given, another database file, and UTF-8 conversion of text.
#include "parity_inc"
#include "nwnx_sql"

void main()
{
    PLog("type", NWNX_SQL_GetDatabaseType());
    NWNX_SQL_PrepareQuery("SELECT hex(?), length(?), ?");
    NWNX_SQL_PreparedString(0, "caf" + "\xE9");
    NWNX_SQL_PreparedString(1, "caf" + "\xE9");
    NWNX_SQL_PreparedString(2, "\xE9" + "\x80");
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    PLog("hex", NWNX_SQL_ReadDataInActiveRow(0));
    PLog("len", NWNX_SQL_ReadDataInActiveRow(1));
    PLogI("roundtrip", NWNX_SQL_ReadDataInActiveRow(2) == "\xE9" + "\x80");
    NWNX_SQL_ExecuteQuery("SELECT char(233), char(8364), char(1046), x'E9'");
    NWNX_SQL_ReadNextRow();
    PLogI("e9", NWNX_SQL_ReadDataInActiveRow(0) == "\xE9");
    PLogI("euro", NWNX_SQL_ReadDataInActiveRow(1) == "\x80");
    PLogI("euro.q", NWNX_SQL_ReadDataInActiveRow(1) == "?");
    PLog("cyrillic", NWNX_SQL_ReadDataInActiveRow(2));
    PLogI("cyrillic.len", GetStringLength(NWNX_SQL_ReadDataInActiveRow(2)));
    PLogI("rawbyte.len", GetStringLength(NWNX_SQL_ReadDataInActiveRow(3)));
    // An invalid UTF-8 byte reads back as one character (which one is not measured).
    NWNX_SQL_PrepareQuery("SELECT hex(?)");
    NWNX_SQL_PreparedString(0, "\x80" + "\x9F");
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    PLog("c1.hex", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_ExecuteQuery("SELEC 'caf" + "\xE9" + "'");
    PLog("errtext", NWNX_SQL_GetLastError());
    NWNX_SQL_ExecuteQuery("SELECT 'caf" + "\xE9" + "', hex('caf" + "\xE9" + "')");
    NWNX_SQL_ReadNextRow();
    PLogI("literal", NWNX_SQL_ReadDataInActiveRow(0) == "caf" + "\xE9");
    PLog("literal.hex", NWNX_SQL_ReadDataInActiveRow(1));
    PDone();
}

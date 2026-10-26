// NWNX_Core_PluginExists: plugin names as NWNXCall takes them.
#include "parity_inc"
#include "nwnx_core"
#include "nwnx_nwsqliteext"

void Check(string sName)
{
    PLogI("exists." + sName, NWNX_Core_PluginExists(sName));
}

void main()
{
    Check("NWNX_Creature");
    Check("NWNX_SQL");
    Check("NWNX_Events");
    Check("NWNX_Tweaks");
    Check("NWNX_Core");
    Check("Creature");
    Check("nwnx_creature");
    Check("NWNX_CREATURE");
    Check("NWNX_NoSuchPlugin");
    Check("");
    Check("NWNX_Redis");
    Check("NWNX_Experimental");
    Check("NWNX_Optimizations");
    // NWSQLiteExtensions without its settings: no 2DA tables.
    PLogI("sqliteext.disabled", NWNX_NWSQLiteExtensions_CreateVirtual2DATable("effecticons"));
    PDone();
}

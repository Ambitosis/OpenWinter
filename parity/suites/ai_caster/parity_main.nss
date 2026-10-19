// Stock caster AI (docs/STOCK_AI.md): spellcasters with their own nw_c2_default* scripts against fighters, one fight
// per area (Prelude factions, 8 m apart):
// - wiz (areas 0-5): nw_bandit005, a 5th-level wizard (memorised Magic Missile, Sleep, Web, Fireball, Mage Armor,
//   Invisibility...), against two nw_bandit001 defenders (wizf).
// - sorc (areas 6-10): nw_goblinboss, a 9th-level sorcerer with an electricity aura and Summon Creature I/II, against
//   three nw_bandit001 defenders (sorcf).
// - clr (areas 11-15): nw_bandit004, a 3rd-level cleric, against one nw_bandit001 defender (clrf).
// Spells are read from the OnSpellCastAt events of their targets (spell scripts signal their target, self buffs the
// caster). Summoned creatures are logged too (role "summ").
#include "pai_inc"

object Ar(int i) { return GetObjectByTag("AI" + (i < 10 ? "0" : "") + IntToString(i)); }

void Setup()
{
    int i;
    for (i = 0; i < 16; i++)
    {
        object a = Ar(i);
        if (i < 6)
        {
            PaiStart(PaiSpawn("nw_bandit005", a, 11.0, 15.0, 0.0, "wiz", STANDARD_FACTION_HOSTILE));
            PaiStart(PaiSpawn("nw_bandit001", a, 19.0, 13.8, 180.0, "wizf", STANDARD_FACTION_DEFENDER));
            PaiStart(PaiSpawn("nw_bandit001", a, 19.0, 16.2, 180.0, "wizf", STANDARD_FACTION_DEFENDER));
        }
        else if (i < 11)
        {
            PaiStart(PaiSpawn("nw_goblinboss", a, 11.0, 15.0, 0.0, "sorc", STANDARD_FACTION_HOSTILE));
            PaiStart(PaiSpawn("nw_bandit001", a, 19.0, 12.6, 180.0, "sorcf", STANDARD_FACTION_DEFENDER));
            PaiStart(PaiSpawn("nw_bandit001", a, 19.0, 15.0, 180.0, "sorcf", STANDARD_FACTION_DEFENDER));
            PaiStart(PaiSpawn("nw_bandit001", a, 19.0, 17.4, 180.0, "sorcf", STANDARD_FACTION_DEFENDER));
        }
        else
        {
            PaiStart(PaiSpawn("nw_bandit004", a, 11.0, 15.0, 0.0, "clr", STANDARD_FACTION_HOSTILE));
            PaiStart(PaiSpawn("nw_bandit001", a, 19.0, 15.0, 180.0, "clrf", STANDARD_FACTION_DEFENDER));
        }
    }
}

// Summoned creatures join the log as they appear.
void Summons()
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, o);
        if (GetIsObjectValid(s) && !GetLocalInt(s, "PAI_ON"))
        {
            SetLocalString(s, "PAI_SUMMONER", PaiRole(o));
            PaiWrap(s, "summ");
            PaiStart(s);
            PaiTally("summ.resref", GetResRef(s));
            SetLocalFloat(s, "PAI_SUMMONED", PaiT(o));
            PaiAdd("summ." + PaiRole(o));
        }
    }
    DelayCommand(0.5, Summons());
}

void Report()
{
    PaiReportRole("wiz", 3);
    PaiReportRole("wizf");
    PaiReportRole("sorc", 3);
    PaiReportRole("sorcf");
    PaiReportRole("clr", 3);
    PaiReportRole("clrf");
    PaiReportTally("summ.resref", PaiGet("sorc.count"));
    PaiMean("summ.by.sorc", IntToFloat(PaiGet("summ.sorc")), PaiGet("sorc.count"), 0.7, 0.3);
    PaiMean("summ.by.wiz", IntToFloat(PaiGet("summ.wiz")), PaiGet("wiz.count"), 0.7, 0.3);
    PaiMean("summ.by.clr", IntToFloat(PaiGet("summ.clr")), PaiGet("clr.count"), 0.7, 0.3);
    PaiDumpRaw();
    PDone();
}

void main()
{
    PStart();
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    DelayCommand(1.0, Setup());
    DelayCommand(1.1, PaiPollLoop(90.0));
    DelayCommand(2.0, Summons());
    DelayCommand(93.0, Report());
}

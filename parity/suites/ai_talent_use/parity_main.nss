// What ActionUseTalentOnObject / ActionUseTalentAtLocation do with the talents GetCreatureTalentBest finds (docs/
// STOCK_AI.md): the action and how long it lasts, the spells it signals, the uses it spends, a potion drunk, a feat's
// special attack, a summoned creature, an aura's area of effect. Each caster (event scripts removed) faces a hostile
// goblin 6 m away; the cases stand 12 m apart.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }

object Mk(string sRes, float x, float y, string sTag, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(Area(), Vector(x, y, 0.0), 0.0), FALSE, sTag);
    int i;
    for (i = 5000; i <= 5012; i++) SetEventScript(o, i, "");
    SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_SPELLCASTAT, "tu_spell");
    SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "tu_atk");
    AssignCommand(o, ClearAllActions(TRUE));
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

string TS(talent t) { return GetIsTalentValid(t) ? IntToString(GetTypeFromTalent(t)) + ":" + IntToString(GetIdFromTalent(t)) : "-"; }

// Case n: caster at (6 + 12 * (n % 6), 6 + 14 * (n / 6)), target 6 m east.
object Caster(int n) { return GetObjectByTag("TU_C" + IntToString(n)); }
object Target(int n) { return GetObjectByTag("TU_T" + IntToString(n)); }

void Pair(int n, string sRes)
{
    float x = 6.0 + 12.0 * IntToFloat(n % 6), y = 6.0 + 14.0 * IntToFloat(n / 6);
    object c = Mk(sRes, x, y, "TU_C" + IntToString(n), STANDARD_FACTION_DEFENDER);
    object t = Mk("nw_goblina", x + 6.0, y, "TU_T" + IntToString(n), STANDARD_FACTION_HOSTILE);
    SetImmortal(t, TRUE);
    SetImmortal(c, TRUE);
}

void Poll(int n)
{
    object c = Caster(n);
    int a = GetCurrentAction(c);
    string s = a == ACTION_INVALID ? "none" : IntToString(a);
    if (s != GetLocalString(c, "TU_LAST"))
    {
        SetLocalString(c, "TU_LAST", s);
        SetLocalString(c, "TU_ACTS", GetLocalString(c, "TU_ACTS") + s + "@" + FloatToString(PSince() - GetLocalFloat(c, "TU_T0"), 0, 1) + " ");
    }
}

void PollAll(float fLeft)
{
    int n;
    for (n = 0; n < 15; n++) Poll(n);
    if (fLeft > 0.0) DelayCommand(0.1, PollAll(fLeft - 0.1));
}

int CountItems(object o)
{
    int n = 0;
    object i = GetFirstItemInInventory(o);
    while (GetIsObjectValid(i)) { n += GetItemStackSize(i); i = GetNextItemInInventory(o); }
    return n;
}

void Use(int n, int nCat, int nCR, int nWhere)
{
    object c = Caster(n), t = Target(n);
    talent tUse = nCat < 0 ? TalentFeat(-nCat) : GetCreatureTalentBest(nCat, nCR, c);
    SetLocalFloat(c, "TU_T0", PSince());
    SetLocalString(c, "TU_TALENT", TS(tUse));
    int nId = GetIdFromTalent(tUse);
    SetLocalInt(c, "TU_ID", nId);
    SetLocalInt(c, "TU_HAS0", GetHasSpell(nId, c));
    SetLocalInt(c, "TU_ITEMS0", CountItems(c));
    if (nWhere == 0) AssignCommand(c, ActionUseTalentOnObject(tUse, c));
    else if (nWhere == 1) AssignCommand(c, ActionUseTalentOnObject(tUse, t));
    else
    {
        vector v = (GetPosition(c) + GetPosition(t)) * 0.5;
        AssignCommand(c, ActionUseTalentAtLocation(tUse, Location(Area(), v, 0.0)));
    }
}

void Report(int n, string sName)
{
    object c = Caster(n), t = Target(n);
    string k = "use." + sName;
    PLog(k + ".talent", GetLocalString(c, "TU_TALENT"));
    PLogSeq(k + ".acts", GetLocalString(c, "TU_ACTS"), 0.35);
    PLogSeq(k + ".spells", GetLocalString(c, "TU_LOG") + GetLocalString(t, "TU_LOG"), 0.35);
    int nId = GetLocalInt(c, "TU_ID");
    PLog(k + ".has", IntToString(GetLocalInt(c, "TU_HAS0")) + ">" + IntToString(GetHasSpell(nId, c)));
    PLog(k + ".items", IntToString(GetLocalInt(c, "TU_ITEMS0")) + ">" + IntToString(CountItems(c)));
    object s = GetAssociate(ASSOCIATE_TYPE_SUMMONED, c);
    PLog(k + ".summon", GetIsObjectValid(s) ? GetResRef(s) : "-");
    object aoe = GetNearestObject(OBJECT_TYPE_AREA_OF_EFFECT, c);
    PLog(k + ".aoe", GetIsObjectValid(aoe) && GetDistanceBetween(aoe, c) < 1.0 ? GetTag(aoe) : "-");
}

void Setup()
{
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    Pair(0, "nw_goblinboss");  // aura (16) on self
    Pair(1, "nw_goblinboss");  // protection (13) on self
    Pair(2, "nw_goblinboss");  // summon (15) at a location
    Pair(3, "nw_goblinboss");  // harmful ranged (2) at the target
    Pair(4, "nw_goblinboss");  // enhancement (9) on self
    Pair(5, "nw_bandit005");   // indiscriminate area (1) at the target
    Pair(6, "nw_bandit005");   // discriminate area (11) at the target
    Pair(7, "nw_bandit004");   // healing touch (5) on self
    Pair(8, "nw_bandit004");   // enhancement single (9) on self
    Pair(9, "nw_orcboss");     // healing potion (17) on self
    Pair(10, "nw_dwarfmerc005"); // melee feat (22) at the target
    Pair(11, "nw_drgred001");  // breath (19) at the target
    Pair(12, "nw_dwarfmerc005"); // Knockdown by TalentFeat at the target
    Pair(13, "nw_goblinboss"); // harmful ranged (2) at the target, best at CR 3
    Pair(14, "nw_goblinboss"); // aura (16) on self, then the target walks into it
}

void Go()
{
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(10), Caster(7));
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDamage(60), Caster(9));
    Use(0, 16, 20, 0);
    Use(1, 13, 20, 0);
    Use(2, 15, 20, 2);
    Use(3, 2, 20, 1);
    Use(4, 9, 20, 0);
    Use(5, 1, 20, 1);
    Use(6, 11, 20, 1);
    Use(7, 5, 20, 0);
    Use(8, 9, 20, 0);
    Use(9, 17, 20, 0);
    Use(10, 22, 20, 1);
    Use(11, 19, 20, 1);
    Use(12, -23, 20, 1);
    Use(13, 2, 3, 1);
    Use(14, 16, 20, 0);
    DelayCommand(4.0, AssignCommand(Target(14), ActionMoveToObject(Caster(14), TRUE, 1.0)));
}

void ReportAll()
{
    Report(0, "sorc.aura"); Report(1, "sorc.protect"); Report(2, "sorc.summon"); Report(3, "sorc.ranged");
    Report(4, "sorc.enhance"); Report(5, "wiz.area1"); Report(6, "wiz.area11"); Report(7, "clr.heal");
    Report(8, "clr.enhance"); Report(9, "orc.potion"); Report(10, "ftr.feat"); Report(11, "drag.breath");
    Report(12, "ftr.knockdown"); Report(13, "sorc.ranged3"); Report(14, "sorc.auraenter");
    PLogI("use.sorc.auraenter.hurt", GetCurrentHitPoints(Target(14)) < GetMaxHitPoints(Target(14)));
    PLogI("use.sorc.aura.haseffect", GetHasSpellEffect(197, Caster(0)));
    PLogI("use.sorc.aura.haseffect14", GetHasSpellEffect(197, Caster(14)));
    PLogI("use.sorc.aura.talentagain", GetIsTalentValid(GetCreatureTalentBest(16, 20, Caster(0))));
    PDone();
}

void main()
{
    PStart();
    DelayCommand(1.0, Setup());
    // After the casters have perceived their targets (first perception about 4 s after they appear).
    DelayCommand(7.0, Go());
    DelayCommand(7.0, PollAll(14.0));
    DelayCommand(22.0, ReportAll());
}

// NWNX_Risenholm effect tags: which effects an empty or exact tag matches, and how removals are counted.
#include "parity_inc"
#include "nwnx_risenholm"
string List(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { s += IntToString(GetEffectType(e)) + ":" + GetEffectTag(e) + ";"; e = GetNextEffect(o); }
    return s;
}
object Gob(int i, string sRes = "nw_goblina")
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, PCentre(IntToFloat(i) * 2.0 - 10.0));
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    return o;
}
// An empty tag matches every effect, the engine's own hidden ones too (every creature has one or more that
// GetFirstEffect does not list), so the count it reports is not compared.
void Case(string k, object o, string sTag)
{
    PLogI(k + ".has", NWNX_Risenholm_GetHasEffectByTag(o, sTag));
    int n = NWNX_Risenholm_RemoveEffectsByTag(o, sTag);
    if (sTag != "") PLogI(k + ".n", n);
    PLog(k + ".left", List(o));
}
void main()
{
    object a = Gob(0); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(1), "X"), a); Case("a", a, "");
    object b = Gob(1); ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(1), b); Case("b", b, "");
    object c = Gob(2); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectLinkEffects(EffectACIncrease(1), EffectLinkEffects(EffectAttackIncrease(1), EffectSkillIncrease(1, 1))), "L"), c); PLog("c.list", List(c)); Case("c", c, "L");
    object d = Gob(3); ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectLinkEffects(EffectACIncrease(1), EffectAttackIncrease(1)), d); Case("d", d, "");
    object e2 = Gob(4); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(1), "TAG_A"), e2); Case("e", e2, "TAG");
    object f = Gob(5); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(1), "X"), f); ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2), f); Case("f", f, "");
    object g = Gob(6); ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2), g); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(1), "X"), g); Case("g", g, "");
    object h = Gob(7); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(1), "X"), h); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(2), "X"), h); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(3), "X"), h); Case("h", h, "X");
    object i = Gob(8); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(1), "X"), i); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectLinkEffects(EffectACIncrease(1), EffectAttackIncrease(1)), "X"), i); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(3), "X"), i); Case("i", i, "X");
    object j = Gob(9); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectLinkEffects(EffectACIncrease(1), EffectAttackIncrease(1)), "X"), j); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(3), "X"), j); Case("j", j, "X");
    object k = Gob(10, "nw_bandit001"); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(1), "X"), k); Case("k", k, "");
    object l = Gob(11, "nw_elfmage015"); ApplyEffectToObject(DURATION_TYPE_PERMANENT, TagEffect(EffectACIncrease(1), "X"), l); Case("l", l, "");
    PDone();
}

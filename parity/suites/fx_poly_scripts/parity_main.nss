// The stock polymorph scripts run on NPCs (cheat casts): Wild Shape (nw_s2_wildshape), Elemental Shape
// (nw_s2_elemshape), Greater Wildshape and Undead Shape (x2_s2_gwildshp, with its x2_inc_shifter use limits
// and a drider-darkness use), Polymorph Self (nw_s0_polyself), Shapechange (nw_s0_shapechg, which polymorphs
// half a second later) and Tenser's Transformation (nw_s0_tenstrans, which refuses non-players).
// The scripts merge the creature's own item properties into the form's hide and weapon
// (IPWildShapeCopyItemProperties, by polymorph.2da MergeW/MergeI/MergeA): what lands where is logged.
#include "parity_fx"
#include "parity_items"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

void Strip(object it)
{
    itemproperty ip = GetFirstItemProperty(it);
    while (GetIsItemPropertyValid(ip)) { RemoveItemProperty(it, ip); ip = GetNextItemProperty(it); }
}

void Give(object o, string sRes, itemproperty ip, int nSlot)
{
    object it = CreateItemOnObject(sRes, o);
    Strip(it);
    AddItemProperty(DURATION_TYPE_PERMANENT, ip, it);
    AssignCommand(o, ActionEquipItem(it, nSlot));
}

string Slots(object o)
{
    string s = "";
    int n;
    for (n = 0; n < 18; n++)
    {
        object it = GetItemInSlot(n, o);
        if (GetIsObjectValid(it)) s += " " + IntToString(n) + "=" + GetResRef(it) + "{" + PIpList(it) + "}";
    }
    return s;
}

string Stats(object o)
{
    return "ac=" + IntToString(GetAC(o)) + " str=" + IntToString(GetAbilityScore(o, 0)) + " dex=" + IntToString(GetAbilityScore(o, 1))
        + " con=" + IntToString(GetAbilityScore(o, 2)) + " wis=" + IntToString(GetAbilityScore(o, 4)) + " cha=" + IntToString(GetAbilityScore(o, 5))
        + " spot=" + IntToString(GetSkillRank(SKILL_SPOT, o)) + " listen=" + IntToString(GetSkillRank(SKILL_LISTEN, o))
        + " disc=" + IntToString(GetSkillRank(SKILL_DISCIPLINE, o)) + " app=" + IntToString(GetAppearanceType(o))
        + " hp=" + IntToString(GetCurrentHitPoints(o)) + "/" + IntToString(GetMaxHitPoints(o));
}

string Durations(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        s += " " + IntToString(GetEffectType(e)) + ":" + IntToString(GetEffectDurationType(e)) + ":" + IntToString(GetEffectSubType(e))
            + ":" + IntToString(GetEffectSpellId(e)) + ":" + IntToString(GetEffectDuration(e));
        e = GetNextEffect(o);
    }
    return s == "" ? "-" : s;
}

void Watch(string k, object o)
{
    PLog(k + ".stats", Stats(o));
    PLog(k + ".slots", Slots(o));
    PLog(k + ".fx", Durations(o));
}

// Every equipped item gets one property the merge would carry: armour, helm and shield (MergeA), ring,
// amulet, boots, cloak and belt (MergeI), and the weapon (MergeW).
object Dress(string sRes)
{
    object o = PFxSpawn(NextCell(), sRes);
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    object armor = GetItemInSlot(1, o);
    if (GetIsObjectValid(armor)) { Strip(armor); AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertyAbilityBonus(IP_CONST_ABILITY_INT, 2), armor); }
    object sword = GetItemInSlot(4, o);
    if (GetIsObjectValid(sword)) { Strip(sword); AddItemProperty(DURATION_TYPE_PERMANENT, ItemPropertySkillBonus(SKILL_DISCIPLINE, 5), sword); }
    Give(o, "nw_arhe001", ItemPropertyAbilityBonus(IP_CONST_ABILITY_WIS, 2), INVENTORY_SLOT_HEAD);
    Give(o, "nw_ashsw001", ItemPropertyACBonus(2), INVENTORY_SLOT_LEFTHAND);
    Give(o, "nw_it_mring001", ItemPropertyAbilityBonus(IP_CONST_ABILITY_CHA, 2), INVENTORY_SLOT_LEFTRING);
    Give(o, "nw_it_mneck001", ItemPropertySkillBonus(SKILL_SPOT, 3), INVENTORY_SLOT_NECK);
    Give(o, "nw_it_mboots001", ItemPropertySkillBonus(SKILL_LISTEN, 4), INVENTORY_SLOT_BOOTS);
    return o;
}

void RemoveAll(object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { RemoveEffect(o, e); e = GetNextEffect(o); }
}

void Cast(object o, int nSpell)
{
    AssignCommand(o, ActionCastSpellAtObject(nSpell, o, METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE));
}

// A dressed creature casts a polymorph spell at itself; before, at 1 s and when the form ends at 8 s.
void Shape(string k, string sRes, int nSpell)
{
    object o = Dress(sRes);
    DelayCommand(1.5, Watch(k + ".before", o));
    DelayCommand(2.0, Cast(o, nSpell));
    DelayCommand(3.0, Watch(k + ".during", o));
    DelayCommand(8.0, RemoveAll(o));
    DelayCommand(10.0, Watch(k + ".after", o));
}

void Limit(string k, object o)
{
    PLog(k, "688=" + IntToString(GetLocalInt(o, "X2_GWILDSHP_LIMIT_688")) + " 687=" + IntToString(GetLocalInt(o, "X2_GWILDSHP_LIMIT_687"))
        + " aoe=" + IntToString(GetIsObjectValid(GetNearestObject(OBJECT_TYPE_AREA_OF_EFFECT, o))));
}

// Greater Wildshape (drow): the use limit it sets, then three drider-darkness uses (the third is refused).
void Drow()
{
    string k = "gws.drow";
    object o = Dress("ps_shift");
    DelayCommand(1.5, Watch(k + ".before", o));
    DelayCommand(2.0, Cast(o, 682));
    DelayCommand(3.0, Watch(k + ".during", o));
    DelayCommand(3.1, Limit(k + ".limit0", o));
    DelayCommand(3.5, AssignCommand(o, ActionCastSpellAtLocation(688, GetLocation(o), METAMAGIC_ANY, TRUE, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(4.5, Limit(k + ".limit1", o));
    DelayCommand(5.0, AssignCommand(o, ActionCastSpellAtLocation(688, GetLocation(o), METAMAGIC_ANY, TRUE, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(6.0, AssignCommand(o, ActionCastSpellAtLocation(688, GetLocation(o), METAMAGIC_ANY, TRUE, PROJECTILE_PATH_TYPE_DEFAULT, TRUE)));
    DelayCommand(7.5, Limit(k + ".limit3", o));
    DelayCommand(8.0, RemoveAll(o));
    DelayCommand(10.0, Watch(k + ".after", o));
}

// Shapechange polymorphs 0.5 s after the script runs.
void Shapechange()
{
    string k = "shapechange";
    object o = Dress("ps_wiz");
    DelayCommand(1.5, Watch(k + ".before", o));
    DelayCommand(2.0, Cast(o, 392));
    DelayCommand(2.3, PLog(k + ".t03", "app=" + IntToString(GetAppearanceType(o))));
    DelayCommand(3.0, Watch(k + ".during", o));
    DelayCommand(8.0, RemoveAll(o));
    DelayCommand(10.0, Watch(k + ".after", o));
}

void Run()
{
    Shape("wild.bear", "ps_dru", 401);
    Shape("wild.wolf20", "ps_dru20", 403);
    Shape("elem.water", "ps_dru", 398);
    Shape("elem.fire20", "ps_dru20", 397);
    Drow();
    Shape("undead.spectre", "ps_shift", 706);
    Shape("gws.basilisk", "ps_shift", 670);
    Shape("polyself.troll", "ps_wiz", 388);
    Shapechange();
    Shape("tenser", "ps_wiz", SPELL_TENSERS_TRANSFORMATION);
    DelayCommand(12.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}

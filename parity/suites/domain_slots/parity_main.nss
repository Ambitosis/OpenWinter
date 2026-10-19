#include "parity_inc"
void Cast(object c, int spell, int meta, int request)
{
    AssignCommand(c, ActionCastSpellAtObject(spell, c, meta, FALSE, request,
        PROJECTILE_PATH_TYPE_DEFAULT, TRUE, CLASS_TYPE_CLERIC));
}
void Report(string key, object c, int spell, int level)
{
    PLogI(key + ".after", GetMemorizedSpellReady(c, CLASS_TYPE_CLERIC, level, 0));
    PLogI(key + ".effect", GetHasSpellEffect(spell, c));
    PLogI(key + ".casts", GetLocalInt(c, "CAST_COUNT"));
    PLogI(key + ".castlevel", GetLocalInt(c, "CAST_LEVEL"));
    PLogI(key + ".castclass", GetLocalInt(c, "CAST_CLASS"));
    PLogI(key + ".castmeta", GetLocalInt(c, "CAST_META"));
    PLogI(key + ".castdc", GetLocalInt(c, "CAST_DC"));
    PLogI(key + ".casterlevel", GetLocalInt(c, "CAST_CL"));
}
void Case(int index, string profile, int spell, int meta, int level, int domain, int request)
{
    string key = IntToString(index);
    object c = CreateObject(OBJECT_TYPE_CREATURE, profile,
        PCentre(IntToFloat(index % 6)*27.0, IntToFloat(index / 6)*27.0), FALSE, "domain" + key);
    int l; int n;
    for (l=0; l<=9; l++) for (n=0; n<GetMemorizedSpellCountByLevel(c, CLASS_TYPE_CLERIC, l); n++)
        SetMemorizedSpellReady(c, CLASS_TYPE_CLERIC, l, n, FALSE);
    SetMemorizedSpell(c, CLASS_TYPE_CLERIC, level, 0, spell, TRUE, meta, domain);
    PLogI(key + ".classlevel", GetLevelByClass(CLASS_TYPE_CLERIC, c));
    PLogI(key + ".domain1", GetDomain(c, 1, CLASS_TYPE_CLERIC));
    PLogI(key + ".domain2", GetDomain(c, 2, CLASS_TYPE_CLERIC));
    PLogI(key + ".stored", GetMemorizedSpellId(c, CLASS_TYPE_CLERIC, level, 0));
    PLogI(key + ".meta", GetMemorizedSpellMetaMagic(c, CLASS_TYPE_CLERIC, level, 0));
    PLogI(key + ".domainflag", GetMemorizedSpellIsDomainSpell(c, CLASS_TYPE_CLERIC, level, 0));
    PLogI(key + ".ready", GetMemorizedSpellReady(c, CLASS_TYPE_CLERIC, level, 0));
    DelayCommand(4.0, Cast(c, spell, meta, request));
    DelayCommand(12.0, Report(key, c, spell, level));
}
void main()
{
    SetLocalString(GetModule(), "X2_S_UD_SPELLSCRIPT", "domain_hook");
    // Stock x2_inc_spellhook skips NPC observers unless this area flag is set.
    SetLocalInt(GetObjectByTag("PARITY_AREA"), "X2_L_WILD_MAGIC", TRUE);
    Case(0,"dom_magic",102,0,1,1,1);
    Case(1,"dom_magic",102,0,2,1,2);
    Case(2,"dom_magic",102,0,1,1,0);
    Case(3,"dom_magic",102,0,1,0,0);
    Case(4,"dom_other",102,0,1,1,1);
    Case(5,"dom_reverse",102,0,1,1,1);
    Case(6,"dom_airanimal",102,0,1,1,1);
    Case(7,"dom_magic",102,METAMAGIC_EXTEND,2,1,2);
    Case(8,"dom_magic",102,METAMAGIC_EXTEND,1,1,1);
    Case(9,"dom_magic",102,METAMAGIC_EXTEND,3,1,3);
    Case(10,"dom_magic",102,METAMAGIC_EXTEND,2,1,0);
    Case(11,"dom_magic",102,METAMAGIC_EXTEND,2,0,0);
    Case(12,"dom_magic",6,0,1,1,1);
    Case(13,"dom_other",6,0,1,1,1);
    Case(14,"dom_airanimal",6,0,1,1,1);
    Case(15,"dom_magic",6,0,2,1,2);
    Case(16,"dom_magic",6,METAMAGIC_EXTEND,2,1,2);
    Case(17,"dom_magic",6,METAMAGIC_EXTEND,1,1,1);
    Case(18,"dom_magic",172,0,4,1,4);
    Case(19,"dom_magic",172,0,5,1,5);
    Case(20,"dom_magic",172,METAMAGIC_EXTEND,5,1,5);
    Case(21,"dom_magic",172,0,4,0,0);
    Case(22,"dom_travel",62,0,3,1,3);
    Case(23,"dom_travel",62,0,4,1,4);
    Case(24,"dom_other",62,0,3,1,3);
    Case(25,"dom_other",62,0,4,1,4);
    Case(26,"dom_travel",62,METAMAGIC_EXTEND,4,1,4);
    Case(27,"dom_travel",62,METAMAGIC_EXTEND,5,1,5);
    Case(28,"dom_travel",62,0,3,0,0);
    Case(29,"dom_travel",62,0,4,0,0);
    Case(30,"dom_magic",102,0,1,1,2);
    Case(31,"dom_magic",102,0,1,0,1);
    Case(32,"dom_magic",102,METAMAGIC_EXTEND,2,1,1);
    Case(33,"dom_magic",172,METAMAGIC_EXTEND,5,1,4);
    Case(34,"dom_travel",62,METAMAGIC_EXTEND,4,1,3);
    Case(35,"dom_travel",62,METAMAGIC_EXTEND,5,1,4);
    Case(36,"dom_magic",6,0,1,1,0);
    Case(37,"dom_travel",62,0,4,1,0);
    Case(38,"dom_travel",62,0,3,1,0);
    Case(39,"dom_magic",102,METAMAGIC_EXTEND,2,0,1);
    DelayCommand(13.0,PDone());
}

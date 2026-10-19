// Effect lists: iteration order, links, RemoveEffect (also while iterating), and what the
// getters report for effects made by a creature or with spell ids, tags and caster levels.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

effect Nth(int i)
{
    switch (i)
    {
        case 0: return EffectSavingThrowIncrease(SAVING_THROW_ALL, 1);
        case 1: return EffectACIncrease(1);
        case 2: return EffectAbilityIncrease(ABILITY_STRENGTH, 1);
        case 3: return EffectSkillIncrease(SKILL_SPOT, 1);
        case 4: return EffectSpellResistanceIncrease(10);
        case 5: return EffectHaste();
        case 6: return EffectConcealment(10);
        case 7: return EffectImmunity(IMMUNITY_TYPE_POISON);
        case 8: return EffectTemporaryHitpoints(3);
        case 9: return EffectAttackIncrease(1);
        case 10: return EffectVisualEffect(VFX_DUR_GLOW_RED);
        case 11: return EffectDamageResistance(DAMAGE_TYPE_FIRE, 5);
        case 12: return EffectMovementSpeedIncrease(10);
        case 13: return EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE);
        case 14: return EffectRegenerate(1, 6.0);
        case 15: return EffectUltravision();
        case 16: return EffectACDecrease(1);
        case 17: return EffectAbilityDecrease(ABILITY_CHARISMA, 1);
        case 18: return EffectDamageIncrease(1);
        case 19: return EffectCutsceneGhost();
        case 20: return EffectSlow();
        case 21: return EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 10);
        case 22: return EffectBonusFeat(FEAT_DODGE);
        case 23: return EffectIcon(3);
    }
    return EffectHeal(-1);
}

const int NTH = 24;

void Order()
{
    object a = PFxSpawn(NextCell());
    object b = PFxSpawn(NextCell());
    int i;
    for (i = 0; i < NTH; i++) ApplyEffectToObject(DURATION_TYPE_PERMANENT, Nth(i), a);
    for (i = NTH - 1; i >= 0; i--) ApplyEffectToObject(DURATION_TYPE_PERMANENT, Nth(i), b);
    PLog("order.forward", PFxTypes(a));
    PLog("order.reverse", PFxTypes(b));

    // One type, several durations and values.
    object c = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(1), c, 60.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2), c);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(3), c, 30.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(4, AC_NATURAL_BONUS), c);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(5, AC_DEFLECTION_BONUS), c, 90.0);
    PLog("order.sametype", PFxList(c));

    // Different subtypes and spell ids.
    object d = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSkillIncrease(SKILL_HIDE, 1)), d);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, ExtraordinaryEffect(EffectSkillIncrease(SKILL_HIDE, 2)), d);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, SetEffectSpellId(EffectSkillIncrease(SKILL_HIDE, 3), 40), d);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, SetEffectSpellId(EffectSkillIncrease(SKILL_HIDE, 4), 10), d);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_HIDE, 5), d);
    PLog("order.subtypes", PFxList(d));
}

void Links()
{
    // A link of three, its order and link ids.
    object o = PFxSpawn(NextCell());
    effect l = EffectLinkEffects(EffectSkillIncrease(SKILL_SPOT, 1), EffectACIncrease(1));
    l = EffectLinkEffects(EffectSavingThrowIncrease(SAVING_THROW_ALL, 1), l);
    PLog("link.pre", PFxDescribe(l));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_WISDOM, 1), o);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, l, o, 50.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_INTELLIGENCE, 1), o);
    PLog("link.list", PFxList(o));
    string sIds = "";
    effect e = GetFirstEffect(o);
    string sFirst = "";
    while (GetIsEffectValid(e))
    {
        string sId = GetEffectLinkId(e);
        if (sFirst == "") sFirst = sId;
        sIds += (sId == "" ? "empty" : (sId == sFirst ? "A" : "other")) + " ";
        PLog("link.durrem." + IntToString(GetEffectType(e)), IntToString(GetEffectDuration(e)) + "/" + IntToString(GetEffectDurationRemaining(e)));
        e = GetNextEffect(o);
    }
    PLog("link.sameids", sIds);

    // Removing one part of the link.
    e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_AC_INCREASE) RemoveEffect(o, e);
        e = GetNextEffect(o);
    }
    PLog("link.afterremove", PFxTypes(o));

    // Link with a visual effect and an instant effect inside.
    object p = PFxSpawn(NextCell());
    PFxSnap(p);
    effect m = EffectLinkEffects(EffectVisualEffect(VFX_DUR_GLOW_RED), EffectDamage(3));
    m = EffectLinkEffects(EffectACIncrease(2), m);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, m, p, 20.0);
    PLog("link.mixed.list", PFxList(p));
    PLog("link.mixed.delta", PFxDiff(p));

    // Linking the same effect twice, and linking links.
    object q = PFxSpawn(NextCell());
    effect s = EffectSkillIncrease(SKILL_HIDE, 2);
    effect s2 = EffectLinkEffects(s, s);
    effect s4 = EffectLinkEffects(s2, s2);
    PFxSnap(q);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, s4, q);
    PLog("link.dup.list", PFxTypes(q));
    PLog("link.dup.delta", PFxDiff(q));

    // A link where the target is immune to one part.
    object r = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_PARALYSIS), r);
    effect n = EffectLinkEffects(EffectParalyze(), EffectSkillIncrease(SKILL_HIDE, 2));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, n, r);
    PLog("link.immune.list", PFxTypes(r));
    object r2 = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_PARALYSIS), r2);
    effect n2 = EffectLinkEffects(EffectParalyze(), EffectVisualEffect(VFX_DUR_GLOW_RED));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, n2, r2);
    PLog("link.immune.vfx.list", PFxTypes(r2));
}

void Removal()
{
    // Remove every effect while iterating.
    object o = PFxSpawn(NextCell());
    int i;
    for (i = 0; i < 6; i++) ApplyEffectToObject(DURATION_TYPE_PERMANENT, Nth(i), o);
    int n = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        RemoveEffect(o, e);
        n++;
        e = GetNextEffect(o);
    }
    PLog("remove.iterated", IntToString(n));
    PLog("remove.left", PFxTypes(o));

    // Remove with a stored copy after more effects were added.
    object p = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2), p);
    effect kept = GetFirstEffect(p);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2), p);
    RemoveEffect(p, kept);
    PLog("remove.copy", PFxList(p));

    // Remove an effect value that was never applied.
    object q = PFxSpawn(NextCell());
    effect never = EffectACIncrease(2);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, never, q);
    RemoveEffect(q, never);
    PLog("remove.unapplied", PFxList(q));

    // The same value applied twice, then removed once.
    object q2 = PFxSpawn(NextCell());
    effect twice = EffectACIncrease(3);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, twice, q2);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SPOT, 1), q2);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, twice, q2);
    RemoveEffect(q2, twice);
    PLog("remove.twice", PFxTypes(q2));
    // A modified copy of an applied value.
    object q3 = PFxSpawn(NextCell());
    effect base = EffectACIncrease(3);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, base, q3);
    RemoveEffect(q3, SupernaturalEffect(base));
    PLog("remove.modifiedcopy", PFxTypes(q3));
    // A link value removed by the link itself, and by a fresh link of the same parts.
    object q4 = PFxSpawn(NextCell());
    effect lk = EffectLinkEffects(EffectACIncrease(1), EffectSkillIncrease(SKILL_SPOT, 1));
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, lk, q4);
    RemoveEffect(q4, lk);
    PLog("remove.linkvalue", PFxTypes(q4));

    // Remove from the wrong object.
    object r = PFxSpawn(NextCell());
    object r2 = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2), r);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2), r2);
    RemoveEffect(r2, GetFirstEffect(r));
    PLog("remove.wrongobject", PFxTypes(r) + " / " + PFxTypes(r2));

    // Nested iteration on two objects.
    object s = PFxSpawn(NextCell());
    object t = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Nth(0), s);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Nth(1), s);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Nth(2), t);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Nth(3), t);
    string sOut = "";
    effect x = GetFirstEffect(s);
    while (GetIsEffectValid(x))
    {
        sOut += IntToString(GetEffectType(x)) + "(";
        effect y = GetFirstEffect(t);
        while (GetIsEffectValid(y)) { sOut += IntToString(GetEffectType(y)) + " "; y = GetNextEffect(t); }
        sOut += ")";
        x = GetNextEffect(s);
    }
    PLog("iterate.nested", sOut);

    // Nested iteration on the same object (bounded: the cursor is shared).
    sOut = "";
    int nGuard = 0;
    x = GetFirstEffect(s);
    while (GetIsEffectValid(x) && nGuard++ < 4)
    {
        sOut += IntToString(GetEffectType(x)) + "(";
        effect y = GetFirstEffect(s);
        while (GetIsEffectValid(y)) { sOut += IntToString(GetEffectType(y)) + " "; y = GetNextEffect(s); }
        sOut += ")";
        x = GetNextEffect(s);
    }
    PLog("iterate.samenested", sOut);
    PLog("iterate.nextwithoutfirst", IntToString(GetEffectType(GetNextEffect(t))));
}

void Made(object oTarget)
{
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(1), oTarget, 12.0);
    PLog("creator.inassign", PFxDescribe(GetFirstEffect(oTarget)));
}

void Creators()
{
    object caster = PFxSpawn(NextCell());
    SetTag(caster, "CASTER");
    object o = PFxSpawn(NextCell());
    AssignCommand(caster, Made(o));
    object p = PFxSpawn(NextCell());
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, SetEffectCreator(SetEffectCasterLevel(SetEffectSpellId(TagEffect(EffectACIncrease(1), "T1"), 12), 7), caster), p);
    PLog("creator.set", PFxDescribe(GetFirstEffect(p)));
    object q = PFxSpawn(NextCell());
    effect l = TagEffect(EffectLinkEffects(EffectACIncrease(1), EffectSkillIncrease(SKILL_SPOT, 1)), "LINKTAG");
    l = SetEffectSpellId(l, 99);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, l, q);
    effect e = GetFirstEffect(q);
    while (GetIsEffectValid(e)) { PLog("creator.linktag", PFxDescribe(e)); e = GetNextEffect(q); }
    PLog("hasspell.99", IntToString(GetHasSpellEffect(99, q)));
    PLog("hasspell.98", IntToString(GetHasSpellEffect(98, q)));
    PLog("hasspell.minus1", IntToString(GetHasSpellEffect(-1, q)));
    // The creator destroyed after applying.
    object r = PFxSpawn(NextCell());
    object maker = PFxSpawn(NextCell());
    AssignCommand(maker, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(1), r));
    DelayCommand(0.5, DestroyObject(maker));
    DelayCommand(1.5, PLog("creator.destroyed", PFxDescribe(GetFirstEffect(r))));
}

void All(object o, int nFrom, int nTo)
{
    int i;
    for (i = nFrom; i <= nTo; i++)
    {
        effect e;
        switch (i)
        {
            case 0: e = EffectHaste(); break;
            case 1: e = EffectDamageResistance(DAMAGE_TYPE_ACID, 5); break;
            case 2: e = EffectSlow(); break;
            case 3: e = EffectRegenerate(1, 6.0); break;
            case 4: e = EffectParalyze(); break;
            case 5: e = EffectAttackIncrease(1); break;
            case 6: e = EffectAttackDecrease(1); break;
            case 7: e = EffectDamageReduction(2, DAMAGE_POWER_PLUS_ONE); break;
            case 8: e = EffectDamageIncrease(1); break;
            case 9: e = EffectDamageDecrease(1); break;
            case 10: e = EffectTemporaryHitpoints(1); break;
            case 11: e = EffectDamageImmunityIncrease(DAMAGE_TYPE_ACID, 5); break;
            case 12: e = EffectDamageImmunityDecrease(DAMAGE_TYPE_ACID, 5); break;
            case 13: e = EffectEntangle(); break;
            case 14: e = EffectKnockdown(); break;
            case 15: e = EffectDeaf(); break;
            case 16: e = EffectImmunity(IMMUNITY_TYPE_FEAR); break;
            case 17: e = EffectEnemyAttackBonus(1); break;
            case 18: e = EffectSpellFailure(10, SPELL_SCHOOL_GENERAL, SPELL_FAILURE_TYPE_ARCANE); break;
            case 19: e = EffectSavingThrowIncrease(SAVING_THROW_ALL, 1); break;
            case 20: e = EffectSavingThrowDecrease(SAVING_THROW_ALL, 1); break;
            case 21: e = EffectMovementSpeedIncrease(10); break;
            case 22: e = EffectMovementSpeedDecrease(10); break;
            case 23: e = EffectVisualEffect(VFX_DUR_GLOW_RED); break;
            case 24: e = EffectAreaOfEffect(AOE_PER_FOGACID, "****", "****", "****"); break;
            case 25: e = EffectBeam(VFX_BEAM_LIGHTNING, GetModule(), BODY_NODE_CHEST); break;
            case 26: e = EffectSpellResistanceIncrease(5); break;
            case 27: e = EffectSpellResistanceDecrease(1); break;
            case 28: e = EffectAbilityIncrease(ABILITY_WISDOM, 1); break;
            case 29: e = EffectAbilityDecrease(ABILITY_WISDOM, 1); break;
            case 30: e = EffectModifyAttacks(1); break;
            case 31: e = EffectCurse(1, 1, 1, 1, 1, 1); break;
            case 32: e = EffectSilence(); break;
            case 33: e = EffectInvisibility(INVISIBILITY_TYPE_NORMAL); break;
            case 34: e = EffectACIncrease(1); break;
            case 35: e = EffectACDecrease(1); break;
            case 36: e = EffectSpellImmunity(SPELL_FIREBALL); break;
            case 37: e = EffectSkillIncrease(SKILL_SPOT, 1); break;
            case 38: e = EffectSkillDecrease(SKILL_SPOT, 1); break;
            case 39: e = EffectDamageShield(1, 0, DAMAGE_TYPE_ACID); break;
            case 40: e = EffectSanctuary(10); break;
            case 41: e = EffectSpellLevelAbsorption(1, 1); break;
            case 42: e = EffectIcon(3); break;
            case 43: e = EffectSeeInvisible(); break;
            case 44: e = EffectUltravision(); break;
            case 45: e = EffectTrueSeeing(); break;
            case 46: e = EffectBlindness(); break;
            case 47: e = EffectDarkness(); break;
            case 48: e = EffectMissChance(10); break;
            case 49: e = EffectConcealment(10); break;
            case 50: e = EffectTurnResistanceIncrease(1); break;
            case 51: e = EffectTurnResistanceDecrease(1); break;
            case 52: e = EffectNegativeLevel(1); break;
            case 53: e = EffectBonusFeat(FEAT_DODGE); break;
            case 54: e = EffectCutsceneParalyze(); break;
            case 55: e = EffectEthereal(); break;
            case 56: e = EffectCutsceneGhost(); break;
            case 57: e = EffectCutsceneImmobilize(); break;
            case 58: e = EffectRunScript(); break;
            case 59: e = EffectPacified(); break;
            case 60: e = EffectTimeStopImmunity(); break;
            case 61: e = EffectForceWalk(); break;
            case 62: e = EffectCutsceneDominated(); break;
            case 63: e = EffectStunned(); break;
            case 64: e = EffectTurned(); break;
            case 65: e = EffectSpellFailure(10); break;
            case 66: e = EffectDisease(DISEASE_FILTH_FEVER); break;
            case 67: e = EffectPoison(POISON_NIGHTSHADE); break;
            case 68: e = EffectPolymorph(POLYMORPH_TYPE_BROWN_BEAR); break;
        }
        ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
    }
}

void TypeOrder()
{
    // Every lasting type at once, applied last to first.
    object o = PFxSpawn(NextCell(), "nw_bandit006");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_POISON), o);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_DISEASE), o);
    int i;
    for (i = 68; i >= 0; i -= 4) All(o, i - 3 < 0 ? 0 : i - 3, i);
    PLog("typeorder.all", PFxTypes(o));
}

void Run()
{
    Order();
    DelayCommand(0.1, Links());
    DelayCommand(0.2, Removal());
    DelayCommand(0.3, Creators());
    DelayCommand(0.4, TypeOrder());
    DelayCommand(3.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}

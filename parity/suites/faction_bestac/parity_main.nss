// GetFactionBestAC / GetFactionWorstAC: which AC is compared (Dexterity, dodge, deflection, natural,
// versus-race bonuses, Uncanny Dodge, Defensive Awareness), ties in membership order, the 0 / 999 starting points,
// bMustBeVisible as a walkmesh line of sight (tile 121's walls; invisibility plays no part), dead members and
// members in another area. Each phase makes fresh creatures in the Merchant faction and destroys them after.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x, y, 0.0), 0.0); }

object Make(string sRes, string sTag, float x, float y, object oArea = OBJECT_INVALID)
{
    if (!GetIsObjectValid(oArea)) oArea = Area();
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(oArea, Vector(x, y, 0.0), 0.0), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetLocalInt(GetModule(), "N_" + sTag, 1);
    return o;
}

void Join(string sTag) { ChangeToStandardFaction(GetObjectByTag(sTag), STANDARD_FACTION_MERCHANT); }

string T(object o) { return GetIsObjectValid(o) ? GetTag(o) : "invalid"; }

// Best and worst from one member, visible-only and not.
void Read(string k, string sRef)
{
    object r = GetObjectByTag(sRef);
    PLog(k + ".best", T(GetFactionBestAC(r)));
    PLog(k + ".worst", T(GetFactionWorstAC(r)));
    PLog(k + ".best.all", T(GetFactionBestAC(r, FALSE)));
    PLog(k + ".worst.all", T(GetFactionWorstAC(r, FALSE)));
}

void AC(string k, string sTag) { PLogI(k + ".ac." + sTag, GetAC(GetObjectByTag(sTag))); }

void Clear(string sTags)
{
    while (sTags != "")
    {
        int p = FindSubString(sTags, " ");
        string s = p < 0 ? sTags : GetStringLeft(sTags, p);
        sTags = p < 0 ? "" : GetSubString(sTags, p + 1, 1000);
        object o = GetObjectByTag(s);
        SetIsDestroyable(TRUE, FALSE, FALSE);
        AssignCommand(o, SetIsDestroyable(TRUE, FALSE, FALSE));
        DestroyObject(o);
    }
}

// 1: plate (18), Dexterity 30 (sheet 20, counted 10), Dexterity 30 with Uncanny Dodge (20).
void P1()
{
    Make("fa_plate", "A1", 2.0, 2.0); Make("fa_dex30", "B1", 3.5, 2.0); Make("fa_uncanny", "C1", 5.0, 2.0);
    Join("A1"); Join("B1"); Join("C1");
}
void R1()
{
    AC("p1", "A1"); AC("p1", "B1"); AC("p1", "C1");
    Read("p1", "A1"); Read("p1.fromB", "B1");
    Clear("A1 B1 C1");
}

// 2: the same with the Uncanny Dodge member beyond a tile wall.
void P2()
{
    Make("fa_plate", "A2", 2.0, 2.0); Make("fa_dex30", "B2", 3.5, 2.0); Make("fa_uncanny", "C2", 13.0, 3.0);
    Join("A2"); Join("B2"); Join("C2");
}
void R2()
{
    PLogI("p2.los.AC", LineOfSightObject(GetObjectByTag("A2"), GetObjectByTag("C2")));
    Read("p2", "A2"); Read("p2.fromC", "C2");
    Clear("A2 B2 C2");
}

// 3: ties: F joins before E.
void P3()
{
    Make("fa_plain", "E3", 2.0, 2.0); Make("fa_plain", "F3", 3.5, 2.0);
    Join("F3"); Join("E3");
}
void R3() { Read("p3", "E3"); Clear("E3 F3"); }

// 4: AC effects: dodge +9, deflection +3, natural +4, deflection +8 versus humans only, armour enchantment +1.
void P4()
{
    object g = Make("fa_plain", "G4", 2.0, 2.0), h = Make("fa_plain", "H4", 3.5, 2.0), i = Make("fa_plain", "I4", 5.0, 2.0);
    object j = Make("fa_plain", "J4", 2.0, 4.0), k = Make("fa_plain", "K4", 3.5, 4.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(9, AC_DODGE_BONUS), g);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(3, AC_DEFLECTION_BONUS), h);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(4, AC_NATURAL_BONUS), i);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusRacialTypeEffect(EffectACIncrease(8, AC_DEFLECTION_BONUS), RACIAL_TYPE_HUMAN), j);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(1, AC_ARMOUR_ENCHANTMENT_BONUS), k);
    Join("G4"); Join("H4"); Join("I4"); Join("J4"); Join("K4");
}
void R4()
{
    AC("p4", "G4"); AC("p4", "H4"); AC("p4", "I4"); AC("p4", "J4"); AC("p4", "K4");
    Read("p4", "H4");
    Clear("G4 H4 I4 J4 K4");
}

// 5: a lone member at AC 0 or below; then one at 999 or above.
void P5()
{
    object l = Make("fa_plain", "L5", 2.0, 2.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACDecrease(20, AC_DEFLECTION_BONUS), l);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACDecrease(20, AC_NATURAL_BONUS), l);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACDecrease(20, AC_ARMOUR_ENCHANTMENT_BONUS), l);
    Join("L5");
}
void R5() { AC("p5", "L5"); Read("p5", "L5"); Clear("L5"); }

// 6: Dexterity 3 (-4) against a plain member that joined first.
void P6()
{
    Make("fa_plain", "O6", 2.0, 2.0); Make("fa_dex3", "N6", 3.5, 2.0);
    Join("O6"); Join("N6");
}
void R6() { AC("p6", "N6"); AC("p6", "O6"); Read("p6", "O6"); Clear("N6 O6"); }

// 7: an invisible plate member, a dead plate member, a plate member in another area.
void P7()
{
    object p = Make("fa_plate", "P7", 2.0, 2.0); Make("fa_plain", "Q7", 3.5, 2.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectInvisibility(INVISIBILITY_TYPE_NORMAL), p);
    Join("Q7"); Join("P7");
}
void R7() { Read("p7.invis", "Q7"); Clear("P7 Q7"); }

void P8()
{
    object r = Make("fa_plate", "R8", 2.0, 2.0); Make("fa_plain", "S8", 3.5, 2.0);
    AssignCommand(r, SetIsDestroyable(FALSE, FALSE, FALSE));
    Join("S8"); Join("R8");
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), r);
}
void R8() { PLogI("p8.dead", GetIsDead(GetObjectByTag("R8"))); Read("p8.dead", "S8"); Clear("R8 S8"); }

void P9()
{
    Make("fa_plate", "T9", 5.0, 5.0, GetObjectByTag("FA_OTHER")); Make("fa_plain", "U9", 2.0, 2.0);
    Join("U9"); Join("T9");
}
void R9() { Read("p9.area", "U9"); Read("p9.fromT", "T9"); Clear("T9 U9"); }

// 10: Defensive Awareness 1 with Dexterity 30 against plate.
void P10()
{
    Make("fa_plate", "V10", 2.0, 2.0); Make("fa_aware", "W10", 3.5, 2.0);
    Join("V10"); Join("W10");
}
void R10() { AC("p10", "W10"); Read("p10", "V10"); Clear("V10 W10"); }

// 11: the reference alone with a member beyond the wall in the same room as nobody, plain AC both.
void P11()
{
    Make("fa_plain", "X11", 2.0, 2.0); Make("fa_plate", "Y11", 13.0, 3.0); Make("fa_plate", "Z11", 3.0, 13.0);
    Join("X11"); Join("Y11"); Join("Z11");
}
void R11()
{
    PLogI("p11.los.XY", LineOfSightObject(GetObjectByTag("X11"), GetObjectByTag("Y11")));
    PLogI("p11.los.XZ", LineOfSightObject(GetObjectByTag("X11"), GetObjectByTag("Z11")));
    Read("p11", "X11");
    Clear("X11 Y11 Z11");
}

// 12: a small creature (halfling appearance) against a medium one that joined first.
void P12()
{
    Make("fa_plain", "M12", 2.0, 2.0); Make("fa_small", "S12", 3.5, 2.0);
    Join("M12"); Join("S12");
}
void R12() { AC("p12", "S12"); PLogI("p12.size", GetCreatureSize(GetObjectByTag("S12"))); Read("p12", "M12"); Clear("M12 S12"); }

void main()
{
    float t = 1.0;
    DelayCommand(t, P1()); DelayCommand(t + 2.0, R1()); t += 4.0;
    DelayCommand(t, P2()); DelayCommand(t + 2.0, R2()); t += 4.0;
    DelayCommand(t, P3()); DelayCommand(t + 2.0, R3()); t += 4.0;
    DelayCommand(t, P4()); DelayCommand(t + 2.0, R4()); t += 4.0;
    DelayCommand(t, P5()); DelayCommand(t + 2.0, R5()); t += 4.0;
    DelayCommand(t, P6()); DelayCommand(t + 2.0, R6()); t += 4.0;
    DelayCommand(t, P7()); DelayCommand(t + 2.0, R7()); t += 4.0;
    DelayCommand(t, P8()); DelayCommand(t + 3.0, R8()); t += 5.0;
    DelayCommand(t, P9()); DelayCommand(t + 2.0, R9()); t += 4.0;
    DelayCommand(t, P10()); DelayCommand(t + 2.0, R10()); t += 4.0;
    DelayCommand(t, P11()); DelayCommand(t + 2.0, R11()); t += 4.0;
    DelayCommand(t, P12()); DelayCommand(t + 2.0, R12()); t += 4.0;
    DelayCommand(t, PDone());
}

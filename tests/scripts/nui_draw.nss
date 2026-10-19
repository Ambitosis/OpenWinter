// NUI renderer test: draw lists (every primitive, before/after order, scissor,
// array binds) and a window loaded from a .jui resource (nui_test_jui).
#include "nw_inc_nui"

json Vec(float x, float y) { return NuiVec(x, y); }

void main()
{
    object oPC = OBJECT_SELF;
    json jTrue = JsonBool(TRUE), jFalse = JsonBool(FALSE);

    json jList = JsonArray();
    // Behind the widget: a filled backdrop.
    jList = JsonArrayInsert(jList, NuiDrawListRect(jTrue, NuiColor(30, 40, 70), jTrue, JsonFloat(1.0), NuiRect(0.0, 0.0, 420.0, 300.0), NUI_DRAW_LIST_ITEM_ORDER_BEFORE));
    jList = JsonArrayInsert(jList, NuiDrawListRect(jTrue, NuiColor(255, 200, 80), jFalse, JsonFloat(2.0), NuiRect(10.0, 10.0, 120.0, 70.0)));
    jList = JsonArrayInsert(jList, NuiDrawListLine(jTrue, NuiColor(255, 80, 80), JsonFloat(3.0), Vec(10.0, 100.0), Vec(130.0, 160.0)));
    jList = JsonArrayInsert(jList, NuiDrawListPolyLine(jTrue, NuiColor(80, 255, 120), jFalse, JsonFloat(2.0), JsonParse("[150, 20, 190, 80, 230, 20, 270, 80]")));
    jList = JsonArrayInsert(jList, NuiDrawListPolyLine(jTrue, NuiColor(80, 200, 255, 160), jTrue, JsonFloat(1.0), JsonParse("[290, 20, 340, 20, 315, 80]")));
    jList = JsonArrayInsert(jList, NuiDrawListCurve(jTrue, NuiColor(255, 255, 255), JsonFloat(2.0), Vec(150.0, 160.0), Vec(330.0, 160.0), Vec(190.0, 90.0), Vec(290.0, 230.0)));
    jList = JsonArrayInsert(jList, NuiDrawListCircle(jTrue, NuiColor(200, 120, 255), jTrue, JsonFloat(1.0), NuiRect(20.0, 180.0, 60.0, 60.0)));
    jList = JsonArrayInsert(jList, NuiDrawListCircle(jTrue, NuiColor(255, 255, 255), jFalse, JsonFloat(2.0), NuiRect(90.0, 180.0, 90.0, 60.0)));
    jList = JsonArrayInsert(jList, NuiDrawListArc(jTrue, NuiColor(255, 140, 40), jTrue, JsonFloat(1.0), Vec(240.0, 230.0), JsonFloat(40.0), JsonFloat(0.0), JsonFloat(4.0)));
    jList = JsonArrayInsert(jList, NuiDrawListArc(jTrue, NuiColor(40, 220, 220), jFalse, JsonFloat(3.0), Vec(330.0, 230.0), JsonFloat(30.0), JsonFloat(3.14), JsonFloat(6.28)));
    jList = JsonArrayInsert(jList, NuiDrawListText(jTrue, NuiColor(255, 255, 255), NuiRect(16.0, 30.0, 110.0, 40.0), JsonString("Draw list text")));
    jList = JsonArrayInsert(jList, NuiDrawListImage(jTrue, JsonString("is_fireball"), NuiRect(360.0, 100.0, 48.0, 48.0), JsonInt(NUI_ASPECT_FIT), JsonInt(NUI_HALIGN_CENTER), JsonInt(NUI_VALIGN_MIDDLE)));
    // Only while hovering.
    jList = JsonArrayInsert(jList, NuiDrawListText(jTrue, NuiColor(255, 255, 0), NuiRect(250.0, 270.0, 160.0, 20.0), JsonString("hovering!"), NUI_DRAW_LIST_ITEM_ORDER_AFTER, NUI_DRAW_LIST_ITEM_RENDER_MOUSE_HOVER));
    // Array binds: one rect per element.
    jList = JsonArrayInsert(jList, NuiDrawListRect(jTrue, NuiBind("dots_color"), jTrue, JsonFloat(1.0), NuiBind("dots"), NUI_DRAW_LIST_ITEM_ORDER_AFTER, NUI_DRAW_LIST_ITEM_RENDER_ALWAYS, TRUE));
    // Partly outside the widget; clipped by the scissor.
    jList = JsonArrayInsert(jList, NuiDrawListCircle(jTrue, NuiColor(255, 60, 60, 200), jTrue, JsonFloat(1.0), NuiRect(380.0, 250.0, 80.0, 80.0)));

    json jCanvas = NuiId(NuiDrawList(NuiSpacer(), jTrue, jList), "canvas");
    json jCol = JsonArray();
    jCol = JsonArrayInsert(jCol, NuiHeight(jCanvas, 300.0));
    jCol = JsonArrayInsert(jCol, NuiHeight(NuiLabel(NuiBind("status"), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_MIDDLE)), 24.0));

    json jWin = NuiWindow(NuiCol(jCol), JsonString("NUI Draw List"), NuiRect(40.0, 60.0, 440.0, 380.0), JsonBool(FALSE), JsonBool(FALSE), JsonBool(TRUE), JsonBool(FALSE), JsonBool(TRUE));
    int nToken = NuiCreate(oPC, jWin, "draw");

    json jDots = JsonArray(), jColors = JsonArray();
    int i;
    for (i = 0; i < 8; i++)
    {
        jDots = JsonArrayInsert(jDots, NuiRect(150.0 + IntToFloat(i) * 22.0, 110.0, 14.0, 14.0));
        jColors = JsonArrayInsert(jColors, NuiColor(40 + i * 28, 255 - i * 25, 120));
    }
    NuiSetBind(oPC, nToken, "dots", jDots);
    NuiSetBind(oPC, nToken, "dots_color", jColors);
    NuiSetBind(oPC, nToken, "status", JsonString("Every draw list primitive; the red circle is scissored."));

    // A window defined in a .jui resource (with a bound label), transparent with no title bar.
    int nJui = NuiCreateFromResRef(oPC, "nui_test_jui", "jui");
    NuiSetBind(oPC, nJui, "message", JsonString("Loaded from nui_test_jui.jui"));
}

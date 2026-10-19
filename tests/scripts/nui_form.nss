// NUI renderer test: a form with every widget type, bound values and events.
// Run with /script nui_form (dev commands). Events go to nui_form_ev.
#include "nw_inc_nui"

json Strings3(string a, string b, string c)
{
    return JsonArrayInsert(JsonArrayInsert(JsonArrayInsert(JsonArray(), JsonString(a)), JsonString(b)), JsonString(c));
}

void main()
{
    object oPC = OBJECT_SELF;

    json jName = JsonArray();
    jName = JsonArrayInsert(jName, NuiWidth(NuiLabel(JsonString("Name:"), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_MIDDLE)), 70.0));
    jName = JsonArrayInsert(jName, NuiTextEdit(JsonString("Enter a name"), NuiBind("name"), 32, FALSE));
    jName = NuiHeight(NuiRow(jName), 30.0);

    json jChecks = JsonArray();
    jChecks = JsonArrayInsert(jChecks, NuiCheck(JsonString("Enabled"), NuiBind("chk")));
    jChecks = JsonArrayInsert(jChecks, NuiId(NuiButtonSelect(JsonString("Select me"), NuiBind("sel")), "btn_sel"));
    jChecks = JsonArrayInsert(jChecks, NuiTooltip(NuiCombo(NuiBind("combo_entries"), NuiBind("combo")), JsonString("A combo box")));
    jChecks = NuiHeight(NuiRow(jChecks), 30.0);

    json jChoices = JsonArray();
    jChoices = JsonArrayInsert(jChoices, NuiOptions(NUI_DIRECTION_HORIZONTAL, Strings3("Red", "Green", "Blue"), NuiBind("opt")));
    jChoices = JsonArrayInsert(jChoices, NuiToggles(NUI_DIRECTION_HORIZONTAL, Strings3("Stats", "Skills", "Feats"), NuiBind("tab")));
    jChoices = NuiHeight(NuiRow(jChoices), 30.0);

    json jSliders = JsonArray();
    jSliders = JsonArrayInsert(jSliders, NuiSlider(NuiBind("slider"), JsonInt(0), JsonInt(100), JsonInt(1)));
    jSliders = JsonArrayInsert(jSliders, NuiWidth(NuiLabel(NuiBind("slider"), JsonInt(NUI_HALIGN_CENTER), JsonInt(NUI_VALIGN_MIDDLE)), 40.0));
    jSliders = JsonArrayInsert(jSliders, NuiSliderFloat(NuiBind("sliderf"), JsonFloat(0.0), JsonFloat(1.0), JsonFloat(0.01)));
    jSliders = JsonArrayInsert(jSliders, NuiWidth(NuiLabel(NuiBind("sliderf", 0, 2), JsonInt(NUI_HALIGN_CENTER), JsonInt(NUI_VALIGN_MIDDLE)), 40.0));
    jSliders = NuiHeight(NuiRow(jSliders), 26.0);

    json jProgress = JsonArray();
    jProgress = JsonArrayInsert(jProgress, NuiProgress(NuiBind("sliderf")));
    jProgress = JsonArrayInsert(jProgress, NuiStyleForegroundColor(NuiProgress(JsonFloat(0.35)), NuiColor(200, 40, 40)));
    jProgress = NuiHeight(NuiRow(jProgress), 22.0);

    json jImages = JsonArray();
    jImages = JsonArrayInsert(jImages, NuiWidth(NuiImage(JsonString("po_hu_m_01_m"), JsonInt(NUI_ASPECT_FIT), JsonInt(NUI_HALIGN_CENTER), JsonInt(NUI_VALIGN_MIDDLE)), 64.0));
    jImages = JsonArrayInsert(jImages, NuiId(NuiWidth(NuiButtonImage(JsonString("is_fireball")), 48.0), "btn_fireball"));
    jImages = JsonArrayInsert(jImages, NuiWidth(NuiButtonImage(JsonString("ir_attack")), 48.0));
    jImages = JsonArrayInsert(jImages, NuiWidth(NuiColorPicker(NuiBind("color")), 130.0));
    json jSlots = JsonArray();
    jSlots = JsonArrayInsert(jSlots, NuiChartSlot(NUI_CHART_TYPE_COLUMN, JsonString("Gold"), NuiColor(220, 170, 60), NuiBind("chart_a")));
    jSlots = JsonArrayInsert(jSlots, NuiChartSlot(NUI_CHART_TYPE_LINES, JsonString("XP"), NuiColor(90, 160, 255), NuiBind("chart_b")));
    jImages = JsonArrayInsert(jImages, NuiChart(jSlots));
    jImages = NuiHeight(NuiRow(jImages), 90.0);

    json jText = NuiHeight(NuiText(NuiBind("text"), TRUE, NUI_SCROLLBARS_AUTO), 64.0);
    json jEdit = NuiHeight(NuiTextEdit(JsonString("Multi-line notes"), NuiBind("notes"), 500, TRUE), 52.0);

    json jButtons = JsonArray();
    jButtons = JsonArrayInsert(jButtons, NuiSpacer());
    jButtons = JsonArrayInsert(jButtons, NuiId(NuiWidth(NuiEncouraged(NuiButton(JsonString("Hello")), JsonBool(TRUE)), 110.0), "btn_hello"));
    jButtons = JsonArrayInsert(jButtons, NuiWidth(NuiDisabledTooltip(NuiEnabled(NuiButton(JsonString("Disabled")), JsonBool(FALSE)), JsonString("Not now")), 110.0));
    jButtons = JsonArrayInsert(jButtons, NuiId(NuiWidth(NuiButton(JsonString("Close")), 110.0), "btn_close"));
    jButtons = JsonArrayInsert(jButtons, NuiSpacer());
    jButtons = NuiHeight(NuiRow(jButtons), 32.0);

    json jStatus = NuiHeight(NuiStyleForegroundColor(NuiLabel(NuiBind("status"), JsonInt(NUI_HALIGN_LEFT), JsonInt(NUI_VALIGN_MIDDLE)), NuiColor(120, 230, 120)), 22.0);

    json jCol = JsonArray();
    jCol = JsonArrayInsert(jCol, jName);
    jCol = JsonArrayInsert(jCol, jChecks);
    jCol = JsonArrayInsert(jCol, jChoices);
    jCol = JsonArrayInsert(jCol, jSliders);
    jCol = JsonArrayInsert(jCol, jProgress);
    jCol = JsonArrayInsert(jCol, jImages);
    jCol = JsonArrayInsert(jCol, jText);
    jCol = JsonArrayInsert(jCol, jEdit);
    jCol = JsonArrayInsert(jCol, jButtons);
    jCol = JsonArrayInsert(jCol, jStatus);

    json jWin = NuiWindow(NuiCol(jCol), JsonString("NUI Widgets"), NuiBind("geometry"), JsonBool(TRUE), JsonNull(), JsonBool(TRUE), JsonBool(FALSE), JsonBool(TRUE));
    int nToken = NuiCreate(oPC, jWin, "form", "nui_form_ev");

    NuiSetBind(oPC, nToken, "geometry", NuiRect(-1.0, 60.0, 620.0, 560.0)); // x centred: the client writes the real rect back
    NuiSetBind(oPC, nToken, "name", JsonString("Freddie"));
    NuiSetBind(oPC, nToken, "chk", JsonBool(TRUE));
    NuiSetBind(oPC, nToken, "sel", JsonBool(FALSE));
    json jEntries = JsonArray();
    jEntries = JsonArrayInsert(jEntries, NuiComboEntry("Fighter", 4));
    jEntries = JsonArrayInsert(jEntries, NuiComboEntry("Wizard", 10));
    jEntries = JsonArrayInsert(jEntries, NuiComboEntry("Rogue", 8));
    NuiSetBind(oPC, nToken, "combo_entries", jEntries);
    NuiSetBind(oPC, nToken, "combo", JsonInt(10));
    NuiSetBind(oPC, nToken, "opt", JsonInt(1));
    NuiSetBind(oPC, nToken, "tab", JsonInt(0));
    NuiSetBind(oPC, nToken, "slider", JsonInt(42));
    NuiSetBind(oPC, nToken, "sliderf", JsonFloat(0.65));
    NuiSetBind(oPC, nToken, "color", NuiColor(80, 140, 220));
    NuiSetBind(oPC, nToken, "chart_a", JsonParse("[3, 5, 2, 8, 6, 9]"));
    NuiSetBind(oPC, nToken, "chart_b", JsonParse("[1, 4, 3, 6, 9, 7]"));
    NuiSetBind(oPC, nToken, "text", JsonString("NuiText is a read-only, word-wrapped text field inside an implied group. It scrolls when the text is longer than the box, which this paragraph should be once it keeps going for a few more lines of filler text to prove it."));
    NuiSetBind(oPC, nToken, "notes", JsonString("Line one, then more notes that wrap inside the multi-line edit box."));
    NuiSetBind(oPC, nToken, "status", JsonString("Ready."));
    NuiSetBindWatch(oPC, nToken, "slider", TRUE);
    NuiSetBindWatch(oPC, nToken, "chk", TRUE);
    NuiSetBindWatch(oPC, nToken, "geometry", TRUE);
}

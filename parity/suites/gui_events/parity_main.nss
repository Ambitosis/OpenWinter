// GUI event functions with no player: what GetLastGuiEvent* return outside an OnPlayerGuiEvent, and that
// SetGuiPanelDisabled, PopUpGUIPanel and PopUpDeathGUIPanel on a non-player do nothing visible to scripts.
#include "parity_inc"

void Last(string k)
{
    vector v = GetLastGuiEventVector();
    PLogI(k + ".type", GetLastGuiEventType());
    PLogI(k + ".integer", GetLastGuiEventInteger());
    PLogI(k + ".object_valid", GetIsObjectValid(GetLastGuiEventObject()));
    PLogI(k + ".player_valid", GetIsObjectValid(GetLastGuiEventPlayer()));
    PLogF(k + ".vector", v.x + v.y + v.z);
}

void main()
{
    Last("load");
    object oArea = GetObjectByTag("PARITY_AREA");
    object oNpc = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(10.0, 10.0, 0.0), 0.0));
    SetGuiPanelDisabled(oNpc, GUI_PANEL_INVENTORY, TRUE);
    SetGuiPanelDisabled(OBJECT_INVALID, GUI_PANEL_CHARACTERSHEET, TRUE, oNpc);
    PopUpGUIPanel(oNpc, GUI_PANEL_JOURNAL);
    PopUpDeathGUIPanel(oNpc);
    PLogI("npc.valid", GetIsObjectValid(oNpc));
    Last("after");
    DelayCommand(1.0, Last("later"));
    DelayCommand(1.5, PDone());
}

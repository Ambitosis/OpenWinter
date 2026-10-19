// Event handler for nui_form: reports clicks and watched binds in the status label.
void main()
{
    object oPC = NuiGetEventPlayer();
    int nToken = NuiGetEventWindow();
    string sType = NuiGetEventType();
    string sElem = NuiGetEventElement();
    if (sType == "click" && sElem == "btn_hello")
        NuiSetBind(oPC, nToken, "status", JsonString("Hello clicked! (sel=" + JsonDump(NuiGetBind(oPC, nToken, "sel")) + ")"));
    else if (sType == "click" && sElem == "btn_close")
        NuiDestroy(oPC, nToken);
    else if (sType == "click" && sElem == "btn_sel")
        NuiSetBind(oPC, nToken, "status", JsonString("Select toggled: " + JsonDump(NuiGetBind(oPC, nToken, "sel"))));
    else if (sType == "watch" && sElem == "slider")
        NuiSetBind(oPC, nToken, "status", JsonString("Slider now " + JsonDump(NuiGetBind(oPC, nToken, "slider"))));
    else if (sType == "watch" && sElem == "geometry")
        NuiSetBind(oPC, nToken, "status", JsonString("Geometry from client: " + JsonDump(NuiGetBind(oPC, nToken, "geometry"))));
    else if (sType == "watch" && sElem == "chk")
        NuiSetBind(oPC, nToken, "status", JsonString("Check now " + JsonDump(NuiGetBind(oPC, nToken, "chk"))));
}

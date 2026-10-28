// Logs each event of the test windows, with the geometry the original client reports.
void main()
{
    object oPC = NuiGetEventPlayer();
    int nToken = NuiGetEventWindow();
    string sLine = "VISUAL|note|nui|" + NuiGetWindowId(oPC, nToken) + "|" + NuiGetEventType() + "|" + NuiGetEventElement();
    if (NuiGetEventType() == "watch") sLine += "|value=" + JsonDump(NuiGetBind(oPC, nToken, NuiGetEventElement()));
    WriteTimestampedLogEntry(sLine);
}

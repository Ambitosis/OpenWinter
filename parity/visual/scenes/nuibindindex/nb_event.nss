// Logs every event of the test window: type, element, array index and, for a watch, the bind's value.
void main()
{
    object oPC = NuiGetEventPlayer();
    int nToken = NuiGetEventWindow();
    string sType = NuiGetEventType();
    string sElement = NuiGetEventElement();
    string sLine = "VISUAL|note|nui|" + sType + "|" + sElement + "|index=" + IntToString(NuiGetEventArrayIndex());
    if (sType == "watch") sLine += "|value=" + JsonDump(NuiGetBind(oPC, nToken, sElement));
    WriteTimestampedLogEntry(sLine);
}

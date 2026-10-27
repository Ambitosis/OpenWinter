// Reports every NUI event of the bind test window: its type, element, array index and the bind's value now.
void main()
{
    object oPC = NuiGetEventPlayer();
    int nToken = NuiGetEventWindow();
    string sType = NuiGetEventType();
    string sElement = NuiGetEventElement();
    string sLine = "BINDTEST|" + sType + "|" + sElement + "|index=" + IntToString(NuiGetEventArrayIndex());
    if (sType == "watch") sLine += "|value=" + JsonDump(NuiGetBind(oPC, nToken, sElement));
    WriteTimestampedLogEntry(sLine);
    if (sType == "watch") SendMessageToPC(oPC, sLine);
}

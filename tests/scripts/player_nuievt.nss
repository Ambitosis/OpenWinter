// NUI event script used by player_nui: records each event in module locals.
void main()
{
    object m = GetModule();
    int n = GetLocalInt(m, "events") + 1;
    SetLocalInt(m, "events", n);
    string s = NuiGetEventType() + ":" + NuiGetEventElement() + ":" + IntToString(NuiGetEventArrayIndex());
    s = s + ":" + IntToString(NuiGetEventWindow() == GetLocalInt(m, "token"));
    s = s + ":" + IntToString(NuiGetEventPlayer() == GetLocalObject(m, "pc"));
    s = s + ":" + JsonDump(NuiGetEventPayload());
    if (NuiGetEventType() == "watch") s = s + ":" + JsonGetString(NuiGetBind(NuiGetEventPlayer(), NuiGetEventWindow(), NuiGetEventElement()));
    SetLocalString(m, "event" + IntToString(n), s);
}

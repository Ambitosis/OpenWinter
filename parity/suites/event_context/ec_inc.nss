#include "parity_inc"
void Snapshot(string key)
{
 PLogI(key+".inherited",GetCurrentlyRunningEvent());
 PLogI(key+".direct",GetCurrentlyRunningEvent(FALSE));
}

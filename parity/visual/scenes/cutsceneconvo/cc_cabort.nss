#include "cc_inc"
// The module's OnPlayerCancelCutscene: logs only, so cutscene mode stays as it was.
void main() { SetLocalInt(GetModule(),"aborts",GetLocalInt(GetModule(),"aborts")+1); CcNote("cutscene_abort"); }

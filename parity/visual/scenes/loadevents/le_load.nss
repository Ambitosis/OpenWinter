#include "le_inc"
// Module OnModuleLoad (set by vis_main with SetEventScript, so that the save carries it).
void main() { LeNote("ev_module_load|pcs=" + IntToString(GetIsObjectValid(GetFirstPC()))); }

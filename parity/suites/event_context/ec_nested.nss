#include "ec_inc"
void main()
{
 string key=GetLocalString(GetModule(),"key");
 Snapshot(key+".nested");
 ExecuteScriptChunk("SetLocalInt(OBJECT_SELF,\"chunkInherited\",GetCurrentlyRunningEvent());SetLocalInt(OBJECT_SELF,\"chunkDirect\",GetCurrentlyRunningEvent(FALSE));",OBJECT_SELF);
 PLogI(key+".chunk.inherited",GetLocalInt(OBJECT_SELF,"chunkInherited"));
 PLogI(key+".chunk.direct",GetLocalInt(OBJECT_SELF,"chunkDirect"));
}

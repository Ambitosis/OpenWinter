// Execution: ExecuteScript(Chunk), script parameters, DelayCommand/AssignCommand capture and order,
// the instruction limit (TMI) and recursion.
#include "parity_inc"

int nGlobal = 1;

void Report(string k, int n) { PLogI(k, n); }
void ReportS(string k, string s) { PLog(k, s); }

void Capture()
{
    object m = GetModule();
    int i = 1;
    DelayCommand(0.1, Report("cap.local", i));
    i = 2;
    SetLocalInt(m, "v", 1);
    DelayCommand(0.1, Report("cap.call", GetLocalInt(m, "v")));
    SetLocalInt(m, "v", 5);
    nGlobal = 10;
    DelayCommand(0.1, Report("cap.global", nGlobal));
    nGlobal = 20;
    int j;
    for (j = 0; j < 3; j++) DelayCommand(0.1, Report("cap.loop", j));
    string s = "a";
    DelayCommand(0.1, ReportS("cap.string", s + "b"));
    s = "z";
    // Order: assigned commands, zero delays and nested delays.
    object w = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(), FALSE, "orderobj");
    DelayCommand(0.0, ReportS("order", "delay0.a"));
    AssignCommand(w, ReportS("order", "assign.a"));
    DelayCommand(0.0, ReportS("order", "delay0.b"));
    AssignCommand(w, ReportS("order", "assign.b"));
    DelayCommand(-1.0, ReportS("order", "delay-1"));
    AssignCommand(w, DelayCommand(0.0, ReportS("order", "assign.delay0")));
    DelayCommand(0.0, DelayCommand(0.0, ReportS("order", "delay0.delay0")));
    ReportS("order", "inline");
    // Commands owned by an object that is destroyed first.
    object d = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(2.0, 0.0), FALSE, "doomed");
    AssignCommand(d, DelayCommand(0.3, ReportS("doomed.delayed", "ran")));
    AssignCommand(d, ReportS("doomed.assigned", "ran"));
    DestroyObject(d, 0.1);
    AssignCommand(OBJECT_INVALID, ReportS("assign.invalid", "ran"));
}

void Params()
{
    SetScriptParam("a", "1");
    SetScriptParam("b", "two");
    ExecuteScript("ex_params");
    PLog("param.after", "[" + GetScriptParam("a") + "]");
    ExecuteScript("ex_params");
    SetScriptParam("a", "x");
    SetScriptParam("a", "y");
    ExecuteScript("ex_params");
    SetScriptParam("a", "");
    ExecuteScript("ex_params");
    SetScriptParam("nested", "1");
    ExecuteScript("ex_nest");
    SetScriptParam("a", "unused");
    DelayCommand(0.1, ExecuteScript("ex_params"));
}

void Exec()
{
    object m = GetModule();
    ExecuteScript("does_not_exist");
    PLog("exec.missing", "continues");
    ExecuteScript("ex_cond");
    PLogI("exec.cond.ran", GetLocalInt(m, "condran"));
    PLogI("rec.level.main", GetScriptRecursionLevel());
    PLog("rec.name.main", GetScriptName());
    PLog("rec.name.0", GetScriptName(0));
    PLog("rec.name.5", "[" + GetScriptName(5) + "]");
    SetLocalInt(m, "depth", 0);
    ExecuteScript("ex_recurse");
    PLogI("rec.maxdepth", GetLocalInt(m, "depth"));
    PLogI("rec.maxlevel", GetLocalInt(m, "maxlevel"));
    // Script-function recursion until the stack gives out.
    SetLocalInt(m, "fdepth", 0);
    ExecuteScript("ex_frecurse");
    PLogI("frec.depth", GetLocalInt(m, "fdepth"));
    PLog("frec.after", "continues");
}

void Tmi()
{
    object m = GetModule();
    PLogI("tmi.remaining.start", GetScriptInstructionsRemaining());
    ExecuteScript("ex_tmi");
    // Never reached: TMI in a script aborts the scripts that ExecuteScript-ed it too.
    PLog("tmi.parent", "continues");
}

void TmiAfter()
{
    object m = GetModule();
    PLogI("tmi.first.remaining", GetLocalInt(m, "tmi.first"));
    PLogI("tmi.loops", GetLocalInt(m, "tmi.loops"));
    ExecuteScript("ex_cost");
}

void Chunks()
{
    object m = GetModule();
    PLog("chunk.ok", "[" + ExecuteScriptChunk("SetLocalInt(GetModule(), \"chunk\", 7);") + "]");
    PLogI("chunk.ok.ran", GetLocalInt(m, "chunk"));
    PLog("chunk.syntax", "[" + ExecuteScriptChunk("SetLocalInt(GetModule(), \"chunk\", 8)") + "]");
    PLog("chunk.undefined", "[" + ExecuteScriptChunk("NoSuchFunction();") + "]");
    PLog("chunk.nomain", "[" + ExecuteScriptChunk("int x = 1;", OBJECT_SELF, FALSE) + "]");
    PLog("chunk.withmain", "[" + ExecuteScriptChunk("void main() { SetLocalInt(GetModule(), \"chunk2\", 3); }", OBJECT_SELF, FALSE) + "]");
    PLogI("chunk.withmain.ran", GetLocalInt(m, "chunk2"));
    PLog("chunk.empty", "[" + ExecuteScriptChunk("") + "]");
    object w = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-2.0, 0.0), FALSE, "chunkself");
    PLog("chunk.self", "[" + ExecuteScriptChunk("SetLocalString(GetModule(), \"chunkself\", GetTag(OBJECT_SELF));", w) + "]");
    PLog("chunk.self.tag", GetLocalString(m, "chunkself"));
    PLog("chunk.include", "[" + ExecuteScriptChunk("#include \"parity_inc\"\nvoid main() { PLog(\"chunk.inner\", GetScriptName() + \"/\" + IntToString(GetScriptRecursionLevel())); }", OBJECT_SELF, FALSE) + "]");
    PLog("chunk.divzero", "[" + ExecuteScriptChunk("int x = 0; x = 1 / x;") + "]");
    PLog("chunk.after", "continues");
    // Names, recursion, functions and structs inside chunks; delayed commands stored by a chunk.
    ExecuteScriptChunk("SetLocalString(GetModule(), \"cn\", GetScriptName() + \"/\" + IntToString(GetScriptRecursionLevel()) + \"/\" + GetScriptChunk());");
    PLog("chunk.name", GetLocalString(m, "cn"));
    PLog("chunk.funcs", "[" + ExecuteScriptChunk("struct S { int a; }; int Sq(int n) { return n * n; } void main() { struct S s; s.a = Sq(6); SetLocalInt(GetModule(), \"sq\", s.a); }", OBJECT_SELF, FALSE) + "]");
    PLogI("chunk.funcs.ran", GetLocalInt(m, "sq"));
    PLog("chunk.cond", "[" + ExecuteScriptChunk("int StartingConditional() { SetLocalInt(GetModule(), \"sc\", 5); return 1; }", OBJECT_SELF, FALSE) + "]");
    PLogI("chunk.cond.ran", GetLocalInt(m, "sc"));
    ExecuteScriptChunk("int n = 21; DelayCommand(0.1, SetLocalInt(GetModule(), \"delayed\", n * 2));");
    DelayCommand(0.3, PLogI("chunk.delayed", GetLocalInt(m, "delayed")));
    SetScriptParam("p", "param!");
    ExecuteScriptChunk("SetLocalString(GetModule(), \"cp\", GetScriptParam(\"p\"));");
    PLog("chunk.param", GetLocalString(m, "cp"));
    PLog("chunk.invalidtarget", "[" + ExecuteScriptChunk("SetLocalInt(GetModule(), \"it\", GetIsObjectValid(OBJECT_SELF) + 10);", OBJECT_INVALID) + "]");
    PLogI("chunk.invalidtarget.ran", GetLocalInt(m, "it"));
    PLog("chunk.mismatch", "[" + ExecuteScriptChunk("int x = \"a\";") + "]");
    PLog("chunk.nested", "[" + ExecuteScriptChunk("ExecuteScriptChunk(\"SetLocalInt(GetModule(), \\\"nest\\\", GetScriptRecursionLevel());\");") + "]");
    PLogI("chunk.nested.level", GetLocalInt(m, "nest"));
    PLog("compile.ok", "[" + CompileScript("cmp_new", "SetLocalInt(GetModule(), \"compiled\", 77);", TRUE) + "]");
    ExecuteScript("cmp_new");
    PLogI("compile.ran", GetLocalInt(m, "compiled"));
}

void main()
{
    DelayCommand(1.0, Capture());
    DelayCommand(1.5, Params());
    DelayCommand(2.0, Exec());
    DelayCommand(2.5, Tmi());
    DelayCommand(2.6, TmiAfter());
    DelayCommand(3.0, Chunks());
    DelayCommand(3.1, ExecuteScript("ex_consts"));
    DelayCommand(4.0, PDone());
}

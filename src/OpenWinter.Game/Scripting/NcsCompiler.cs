using System.Reflection;
using System.Reflection.Emit;

namespace OpenWinter.Game.Scripting;

/// <summary>
/// Compiles an NCS program to a .NET dynamic method that RyuJIT turns into
/// native code. The compiled method keeps the interpreter's stack model and
/// calls the same ScriptThread operations, but removes all decoding and
/// dispatch: operands are constants, NCS jumps are IL branches, RETN maps the
/// code offset JSR pushed to a table of return sites, and instruction counting
/// happens once per basic block.
///
/// Signature: (ScriptThread thread, int entryOffset). Entry offsets are 0 and
/// every STORE_STATE resume point, so stored actions run compiled too.
/// </summary>
public static class NcsCompiler
{
    static readonly Type T = typeof(ScriptThread);
    static MethodInfo M(string name) => T.GetMethod(name) ?? throw new MissingMethodException(T.Name, name);
    static readonly FieldInfo ReturnsField = T.GetField(nameof(ScriptThread.Returns))!;
    static readonly MethodInfo StackCount = typeof(Stack<int>).GetProperty("Count")!.GetGetMethod()!;
    static readonly ConstructorInfo ScriptExceptionCtor = typeof(ScriptException).GetConstructor(new[] { typeof(string) })!;

    public static Action<ScriptThread, int> Compile(NcsProgram program)
    {
        var code = program.Instructions;
        var method = new DynamicMethod("ncs_" + program.Name, typeof(void), new[] { T, typeof(int) }, typeof(NcsCompiler).Module, skipVisibility: true);
        var il = method.GetILGenerator();
        var labels = new Label[code.Length];
        for (int i = 0; i < code.Length; i++) labels[i] = il.DefineLabel();

        // Block leaders: entry points, jump targets, return sites, and instructions after control transfers and
        // after instructions with effects or errors.
        var leaders = new bool[code.Length + 1];
        var entries = new List<int> { 0 };
        var returnSites = new List<int>();
        if (code.Length > 0) leaders[0] = true;
        for (int i = 0; i < code.Length; i++)
        {
            var ins = code[i];
            switch (ins.Op)
            {
                case 0x1D:
                case 0x1F:
                case 0x25:
                    leaders[program.IndexAt(ins.A)] = true;
                    leaders[i + 1] = true;
                    break;
                case 0x1E:
                    leaders[program.IndexAt(ins.A)] = true;
                    leaders[i + 1] = true;
                    if (i + 1 < code.Length) returnSites.Add(i + 1);
                    break;
                case 0x20:
                    leaders[i + 1] = true;
                    break;
                case 0x05:
                case 0x17:
                case 0x18:
                    // Engine calls, divisions and modulos end their block: the block's count is charged up front,
                    // so this keeps the count an engine call reads (GetScriptInstructionsRemaining), the count after
                    // an abort or error, and whether TOO MANY INSTRUCTIONS or DIVIDE BY ZERO comes first the same
                    // as in the interpreter.
                    leaders[i + 1] = true;
                    break;
                case 0x2C:
                    if (TryIndex(program, ins.C, out int resume)) { leaders[resume] = true; entries.Add(ins.C); }
                    break;
            }
        }

        // Entry dispatch.
        foreach (var entry in entries.Distinct())
        {
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4, entry);
            il.Emit(OpCodes.Beq, labels[program.IndexAt(entry)]);
        }
        il.Emit(OpCodes.Ldstr, $"{program.Name}: bad entry point");
        il.Emit(OpCodes.Newobj, ScriptExceptionCtor);
        il.Emit(OpCodes.Throw);

        for (int i = 0; i < code.Length; i++)
        {
            var ins = code[i];
            il.MarkLabel(labels[i]);
            if (leaders[i])
            {
                int len = 1;
                while (i + len < code.Length && !leaders[i + len]) len++;
                Call(il, "Tick", len);
            }
            switch (ins.Op)
            {
                case 0x01: Call(il, "CpDownSp", ins.A, ins.B); break;
                case 0x02: Call(il, "RsAdd", ins.Type); break;
                case 0x03: Call(il, "CpTopSp", ins.A, ins.B); break;
                case 0x04:
                    switch (ins.Type)
                    {
                        case 3: Call(il, "ConstInt", ins.A); break;
                        case 4: Call(il, "ConstFloatBits", ins.A); break;
                        case 6: Call(il, "ConstObject", ins.A); break;
                        case 5:
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldstr, ins.S!);
                            il.Emit(OpCodes.Call, M("ConstString"));
                            break;
                        default:
                            il.Emit(OpCodes.Ldarg_0);
                            il.Emit(OpCodes.Ldc_I4, (int)ins.Type);
                            il.Emit(OpCodes.Ldc_I4, ins.A);
                            if (ins.S != null) il.Emit(OpCodes.Ldstr, ins.S);
                            else il.Emit(OpCodes.Ldnull);
                            il.Emit(OpCodes.Call, M("ConstEngine"));
                            break;
                    }
                    break;
                case 0x05:
                    Call(il, "Action", ins.A, ins.B, ins.Offset);
                    Call(il, "CheckControlCompiled");
                    break;
                case 0x06: Call(il, "LogAnd"); break;
                case 0x07: Call(il, "LogOr"); break;
                case 0x08: Call(il, "IncOr"); break;
                case 0x09: Call(il, "ExcOr"); break;
                case 0x0A: Call(il, "BoolAnd"); break;
                case 0x0B:
                case 0x0C:
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, (int)ins.Type);
                    il.Emit(OpCodes.Ldc_I4, ins.A);
                    il.Emit(ins.Op == 0x0C ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Call, M("Equal"));
                    break;
                case 0x0D: Call(il, "Compare", ins.Type, 0); break;
                case 0x0E: Call(il, "Compare", ins.Type, 1); break;
                case 0x0F: Call(il, "Compare", ins.Type, 2); break;
                case 0x10: Call(il, "Compare", ins.Type, 3); break;
                case 0x11: Call(il, "ShLeft"); break;
                case 0x12: Call(il, "ShRight"); break;
                case 0x13: Call(il, "UShRight"); break;
                case >= 0x14 and <= 0x17:
                    if (ins.Type == 0x20) Call(il, "ArithII", ins.Op - 0x14);
                    else Call(il, "Arith", ins.Type, ins.Op - 0x14);
                    break;
                case 0x18: Call(il, "Mod"); break;
                case 0x19: Call(il, "Neg", ins.Type); break;
                case 0x1A: Call(il, "Comp"); break;
                case 0x1B: Call(il, "MovSp", ins.A); break;
                case 0x1D:
                    il.Emit(OpCodes.Br, labels[program.IndexAt(ins.A)]);
                    break;
                case 0x1E:
                    // Push the code offset, as the interpreter does, so backtraces read the same in both tiers.
                    Call(il, "PushReturn", ins.Next);
                    il.Emit(OpCodes.Br, labels[program.IndexAt(ins.A)]);
                    break;
                case 0x1F:
                    Call(il, "PopInt");
                    il.Emit(OpCodes.Brfalse, labels[program.IndexAt(ins.A)]);
                    break;
                case 0x25:
                    Call(il, "PopInt");
                    il.Emit(OpCodes.Brtrue, labels[program.IndexAt(ins.A)]);
                    break;
                case 0x20:
                {
                    var notOuter = il.DefineLabel();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, ReturnsField);
                    il.Emit(OpCodes.Callvirt, StackCount);
                    il.Emit(OpCodes.Brtrue, notOuter);
                    Call(il, "Finish");
                    il.Emit(OpCodes.Ret);
                    il.MarkLabel(notOuter);
                    Call(il, "PopReturnSite");
                    il.Emit(OpCodes.Switch, returnSites.Select(r => labels[r]).ToArray());
                    il.Emit(OpCodes.Ldstr, $"{program.Name}: bad return address");
                    il.Emit(OpCodes.Newobj, ScriptExceptionCtor);
                    il.Emit(OpCodes.Throw);
                    break;
                }
                case 0x21: Call(il, "Destruct", ins.A, ins.B, ins.C); break;
                case 0x22: Call(il, "NotI"); break;
                case 0x23: Call(il, "DecISp", ins.A); break;
                case 0x24: Call(il, "IncISp", ins.A); break;
                case 0x26: Call(il, "CpDownBp", ins.A, ins.B); break;
                case 0x27: Call(il, "CpTopBp", ins.A, ins.B); break;
                case 0x28: Call(il, "DecIBp", ins.A); break;
                case 0x29: Call(il, "IncIBp", ins.A); break;
                case 0x2A: Call(il, "SaveBp"); break;
                case 0x2B: Call(il, "RestoreBp"); break;
                case 0x2C: Call(il, "StoreState", ins.A, ins.B, ins.C); break;
                case 0x2D: case 0x1C: case 0x42: break;
                default:
                    il.Emit(OpCodes.Ldstr, $"unknown opcode 0x{ins.Op:X2} at {ins.Offset}");
                    il.Emit(OpCodes.Newobj, ScriptExceptionCtor);
                    il.Emit(OpCodes.Throw);
                    break;
            }
        }
        program.ReturnSites = returnSites.Select(r => code[r].Offset).ToArray();
        // Errors read as the interpreter's do.
        il.Emit(OpCodes.Ldstr, "ran off the end of the code");
        il.Emit(OpCodes.Newobj, ScriptExceptionCtor);
        il.Emit(OpCodes.Throw);
        return method.CreateDelegate<Action<ScriptThread, int>>();
    }

    static bool TryIndex(NcsProgram p, int offset, out int index)
    {
        try { index = p.IndexAt(offset); return true; }
        catch (ScriptException) { index = -1; return false; }
    }

    static void Call(ILGenerator il, string name, params int[] args)
    {
        il.Emit(OpCodes.Ldarg_0);
        foreach (var a in args) il.Emit(OpCodes.Ldc_I4, a);
        il.Emit(OpCodes.Call, M(name));
    }
}

namespace OpenWinter.Game.Scripting.Compiler;

/// <summary>
/// The in-process NWScript compiler behind ExecuteScriptChunk and CompileScript (an independent
/// implementation of the language, producing NCS the VM runs).
/// </summary>
public static class NssCompiler
{
    /// <summary>
    /// Compiles <paramref name="source"/>; returns the NCS file, or null with <paramref name="error"/> worded like the
    /// original compiler ("name(line): ERROR: MESSAGE"). Includes are read through <paramref name="include"/>.
    /// </summary>
    /// <param name="mainOnly">Only void main() counts as an entry point (chunks); otherwise StartingConditional does too.</param>
    public static byte[]? Compile(string source, string name, NssDefinitions defs, Func<string, string?> include, out string error, bool mainOnly = false)
    {
        error = "";
        try
        {
            var decls = NssParser.Parse(source, name, defs.EngineStructures, include);
            return NssCodeGen.Generate(defs, decls, name, mainOnly);
        }
        catch (NssCompileException e)
        {
            error = e.Line > 0 ? $"{e.File}({e.Line}): ERROR: {e.Message}" : $"{e.File}: ERROR: {e.Message}";
            return null;
        }
    }
}

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Ruby: OpenWinter runs no Ruby interpreter; Evaluate returns "".</summary>
static class RubyPlugin
{
    [NwnxFunc("Ruby", "Evaluate", UnsupportedReason = "OpenWinter has no Ruby interpreter; the code is not run and the result is empty")]
    static void Evaluate(NwnxCall n)
    {
        n.PopString();
        n.Push("");
    }
}

using OpenWinter.Game.Conversations;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Dialog: the node whose condition or action script is running, read from the conversation's event context.
/// Outside a conversation the node type and id are -1, the script type and index 0 (parity suite nwnx_dialog).
/// </summary>
static class DialogPlugin
{
    const string P = "Dialog";

    static ConversationSession? Session(NwnxCall n) =>
        n.Host?.CurrentEvent.Values.TryGetValue(ConversationService.NodeSessionKey, out var v) == true ? v as ConversationSession : null;

    static int Value(NwnxCall n, string key, int outside) =>
        n.Host?.CurrentEvent.Values.TryGetValue(key, out var v) == true && v is int i ? i : outside;

    [NwnxFunc(P, "GetCurrentNodeType")] static void NodeType(NwnxCall n) => n.Push(Value(n, ConversationService.NodeTypeKey, -1));
    [NwnxFunc(P, "GetCurrentScriptType")] static void ScriptType(NwnxCall n) => n.Push(Value(n, ConversationService.ScriptTypeKey, 0));
    [NwnxFunc(P, "GetCurrentNodeID")] static void NodeId(NwnxCall n) => n.Push(Value(n, ConversationService.NodeIdKey, -1));
    [NwnxFunc(P, "GetCurrentNodeIndex")] static void NodeIndex(NwnxCall n) => n.Push(Value(n, ConversationService.NodeIndexKey, 0));

    [NwnxFunc(P, "GetCurrentNodeText")]
    static void NodeText(NwnxCall n)
    {
        int language = n.PopInt(); int gender = n.PopInt();
        n.Push(Session(n) is { } s ? n.World.System<ConversationService>().CurrentNodeText(s, language, gender) : "");
    }

    /// <summary>Replaces the current node's text for the rest of the conversation (only a condition's change is seen
    /// before the line shows, as the header says).</summary>
    [NwnxFunc(P, "SetCurrentNodeText")]
    static void SetNodeText(NwnxCall n)
    {
        var text = n.PopString(); n.PopInt(); n.PopInt();
        if (Session(n) is { } s) ConversationService.SetNodeText(s, text);
    }

    [NwnxFunc(P, "End")]
    static void End(NwnxCall n)
    {
        if (n.PopObject() is { } o) n.World.System<ConversationService>().Abort(o);
    }
}

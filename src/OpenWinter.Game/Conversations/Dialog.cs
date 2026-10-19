using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.Conversations;

/// <summary>A link from one dialogue node to another, gated by a conditional script.</summary>
public sealed class DialogLink
{
    public int Index;
    /// <summary>StartingConditional script; empty means always available.</summary>
    public string Active = "";
    public bool IsChild;
    public readonly Dictionary<string, string> ConditionParams = new();
}

/// <summary>An NPC entry or a PC reply.</summary>
public sealed class DialogNode
{
    public LocString Text = new();
    /// <summary>Tag of the speaking object (entries only; empty = the conversation owner).</summary>
    public string Speaker = "";
    public string Script = "";
    public readonly Dictionary<string, string> ActionParams = new();
    public int Animation;
    public bool AnimLoop;
    public string Sound = "";
    public string Quest = "";
    public int QuestEntry = -1;
    public float Delay = -1;
    public readonly List<DialogLink> Links = new();
}

/// <summary>A DLG conversation tree.</summary>
public sealed class Dialog
{
    public readonly List<DialogNode> Entries = new();
    public readonly List<DialogNode> Replies = new();
    public readonly List<DialogLink> Starts = new();
    public string EndScript = "", AbortScript = "";
    public bool PreventZoomIn;
    public int DelayEntry, DelayReply;

    public static Dialog Load(GffStruct g)
    {
        var d = new Dialog
        {
            EndScript = g.GetString("EndConversation"),
            AbortScript = g.GetString("EndConverAbort"),
            PreventZoomIn = g.GetInt("PreventZoomIn") != 0,
            DelayEntry = (int)g.GetInt("DelayEntry"),
            DelayReply = (int)g.GetInt("DelayReply"),
        };
        foreach (var e in g.GetList("EntryList")) d.Entries.Add(Node(e, "RepliesList"));
        foreach (var r in g.GetList("ReplyList")) d.Replies.Add(Node(r, "EntriesList"));
        foreach (var s in g.GetList("StartingList")) d.Starts.Add(Link(s));
        return d;
    }

    static DialogNode Node(GffStruct s, string linkList)
    {
        var n = new DialogNode
        {
            Text = s.GetLocString("Text"),
            Speaker = s.GetString("Speaker"),
            Script = s.GetString("Script"),
            Animation = (int)s.GetInt("Animation"),
            AnimLoop = s.GetInt("AnimLoop", 1) != 0,
            Sound = s.GetString("Sound"),
            Quest = s.GetString("Quest"),
            QuestEntry = s.Has("QuestEntry") ? (int)s.GetInt("QuestEntry") : -1,
            Delay = s.GetInt("Delay") is var delay && delay == 0xFFFFFFFF ? -1 : (float)delay,
        };
        foreach (var p in s.GetList("ActionParams")) n.ActionParams[p.GetString("Key")] = p.GetString("Value");
        foreach (var l in s.GetList(linkList)) n.Links.Add(Link(l));
        return n;
    }

    static DialogLink Link(GffStruct s)
    {
        var l = new DialogLink { Index = (int)s.GetInt("Index"), Active = s.GetString("Active"), IsChild = s.GetInt("IsChild") != 0 };
        foreach (var p in s.GetList("ConditionParams")) l.ConditionParams[p.GetString("Key")] = p.GetString("Value");
        return l;
    }
}

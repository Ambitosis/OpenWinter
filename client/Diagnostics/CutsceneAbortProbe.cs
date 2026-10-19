using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Diagnostics;

/// <summary>Real-Godot headless input/connection regression; no assets or audio.</summary>
public partial class CutsceneAbortProbe : Node
{
    int _checks;
    void Check(bool passed,string name) { if(!passed)throw new InvalidOperationException(name); _checks++; }
    static void KeyEvent(Key key,bool pressed=true,bool echo=false)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode=key,PhysicalKeycode=key,Pressed=pressed,Echo=echo });
        Input.FlushBufferedEvents();
    }
    public override async void _Ready()
    {
        try
        {
            var (client,server)=LoopbackConnection.CreatePair();
            var session=new ClientSession { Connection=client };
            bool cutscene=false,dialog=false;
            var input=new CutsceneAbortInput { Session=session,CanRequest=()=>cutscene&&!dialog };
            AddChild(input);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            KeyEvent(Key.Escape);
            Check(!server.TryReceive(out _),"ordinary Escape sends no cutscene request");
            cutscene=true;
            KeyEvent(Key.Escape);
            Check(server.TryReceive(out var packet),"cutscene Escape reaches connection");
            var message=ClientRequestMessage.Read(new MessageReader(packet));
            Check(message.Kind=="cutscene-abort","request identity");
            Check(message.Json=="","request carries no chosen player");
            KeyEvent(Key.Escape,false); Check(!server.TryReceive(out _),"release ignored");
            KeyEvent(Key.Escape,true,true); Check(!server.TryReceive(out _),"repeat ignored");
            KeyEvent(Key.Space); Check(!server.TryReceive(out _),"other keys preserved");
            KeyEvent(Key.Escape); Check(server.TryReceive(out _),"separate Escape can signal again");
            dialog=true; KeyEvent(Key.Escape); Check(!server.TryReceive(out _),"visible dialog retains existing Escape behavior");
            dialog=false; cutscene=false; KeyEvent(Key.Escape); Check(!server.TryReceive(out _),"mode exit stops requests");
            session.Free();
            GD.Print($"[cutscene-abort] {_checks} checks passed"); GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}

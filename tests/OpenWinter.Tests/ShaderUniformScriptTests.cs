using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>SetShaderUniform* and SetMaterialShaderUniform* from scripts, as the player's client receives them.</summary>
public sealed class ShaderUniformScriptTests
{
    sealed class Fixture : IDisposable
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly LoopbackConnection Client;

        public Fixture()
        {
            var resources = new ResMan(TestData.Paths); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            var area = new Area { ResRef = "shaders", Width = 1, Height = 1 };
            World.Register(area); World.Areas.Add(area);
            Server = new GameServer(World, new ServerOptions { StartArea = "shaders", Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair(); Client = pair.Client; Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Shader tester" }.Write()); Server.Tick(.01f);
            for (int i = 0; i < 10; i++) Server.Tick(.05f);
            Drain();
        }

        public List<(string Kind, uint Subject, JsonElement Data)> Run(string body)
        {
            var code = NssCompiler.Compile("void main(){object oPC = GetFirstPC();" + body + "}", "shd_test", World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(code != null, error);
            World.Scripts.Register("shd_test", new NcsProgram("shd_test", code!));
            World.Scripts.Run("shd_test", World.Module.Id);
            for (int i = 0; i < 3; i++) Server.Tick(.05f);
            return Drain();
        }

        public List<(string, uint, JsonElement)> Drain()
        {
            var notes = new List<(string, uint, JsonElement)>();
            while (Client.TryReceive(out var data))
                if ((MessageType)data[0] == MessageType.Notify && NotifyMessage.Read(new MessageReader(data)) is { } n && n.Kind.Contains("uniform"))
                    notes.Add((n.Kind, n.Subject, JsonDocument.Parse(n.Json).RootElement.Clone()));
            return notes;
        }

        public void Dispose()
        {
            World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose();
            foreach (var source in World.Resources.Sources.OfType<IDisposable>().Distinct()) source.Dispose();
        }
    }

    [DataFact]
    public void TheFloatIntAndVecBanksArriveApartAndIntsExactly()
    {
        using var f = new Fixture();
        var notes = f.Run("SetShaderUniformFloat(oPC, SHADER_UNIFORM_11, 0.5);" +
            "SetShaderUniformInt(oPC, SHADER_UNIFORM_11, 16777217);" +
            "SetShaderUniformVec(oPC, SHADER_UNIFORM_16, 1.0, 2.0, 3.0, 4.0);" +
            "SetShaderUniformInt(oPC, 16, 5);"); // outside the 16 of a bank: ignored
        Assert.Equal(3, notes.Count);
        Assert.All(notes, n => Assert.Equal("shader-uniform", n.Kind));
        Assert.Equal(("float", 10), (notes[0].Data.GetProperty("type").GetString(), notes[0].Data.GetProperty("shader").GetInt32()));
        Assert.Equal(("int", 10, 16777217), (notes[1].Data.GetProperty("type").GetString(), notes[1].Data.GetProperty("shader").GetInt32(), notes[1].Data.GetProperty("int").GetInt32()));
        Assert.Equal(("vec4", 15), (notes[2].Data.GetProperty("type").GetString(), notes[2].Data.GetProperty("shader").GetInt32()));
    }

    [DataFact]
    public void MaterialUniformsStayOnTheObjectUntilReset()
    {
        using var f = new Fixture();
        var notes = f.Run("SetMaterialShaderUniformVec4(oPC, \"pfa0_head001\", \"faceSmile\", 0.75);" +
            "SetMaterialShaderUniformInt(oPC, \"pfa0_head001\", \"mode\", 16777217);");
        Assert.Equal(2, notes.Count);
        Assert.Equal("faceSmile", notes[0].Data.GetProperty("param").GetString());
        Assert.Equal(16777217, notes[1].Data.GetProperty("int").GetInt32());
        var pc = f.World.AllObjects.OfType<OpenWinter.Game.Objects.Creature>().Single(c => c.IsPC);
        Assert.Equal(2, pc.MaterialUniforms!.Count);
        notes = f.Run("ResetMaterialShaderUniforms(oPC, \"PFA0_HEAD001\", \"mode\");");
        Assert.Equal("material-uniform-reset", Assert.Single(notes).Kind);
        Assert.Equal("faceSmile", Assert.Single(pc.MaterialUniforms).Key.Param);
    }
}

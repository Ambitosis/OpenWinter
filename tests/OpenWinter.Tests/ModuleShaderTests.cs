using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Aurora.Shaders;
using OpenWinter.Game.Distribution;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Module shaders (docs/SHADERS.md): MTR materials, the shader preprocessor and translator (on shaders written
/// for these tests), and the script uniform banks through the bridge.
/// </summary>
public class ModuleShaderTests
{
    /// <summary>
    /// A small stand-in for the installed game's shader library, written for these tests: the same file names and
    /// a few of the names shaders use, including functions OpenWinter replaces (lighting, fog).
    /// </summary>
    static readonly Dictionary<string, string> Installed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["inc_standard"] = """
            #ifndef INC_STANDARD
            #define INC_STANDARD
            #ifndef LIGHTING
            #define LIGHTING 0
            #endif
            uniform mat4 m_mv;
            uniform mat4 m_proj;
            uniform mat4 m_view_inv;
            uniform int texture0Bound;
            uniform sampler2D texUnit0;
            varying vec3 vPosView;
            varying vec3 vVertexNormal;
            varying vec2 vVertexTexCoords;
            #if SHADER_TYPE == 1
            attribute vec4 vPos;
            attribute vec3 vNormal;
            attribute vec2 vTcIn;
            void ApplyStandardShader()
            {
                vPosView = (m_mv * vPos).xyz;
                vVertexNormal = mat3(m_mv) * vNormal;
                vVertexTexCoords = vTcIn;
                gl_Position = m_proj * vec4(vPosView, 1.0);
            }
            #else
            vec4 FragmentColor;
            vec3 vFragmentNormal;
            float fRoughness = 0.5;
            void ComputeLighting(inout vec4 ColorIO, vec3 vNormalThis) { ColorIO.rgb *= 0.25; }
            void ApplyFog(inout vec4 ColorIO) { ColorIO = vec4(1.0, 0.0, 1.0, 1.0); }
            void SetupStandardShaderInputs()
            {
                vFragmentNormal = normalize(vVertexNormal);
                if (texture0Bound != 0) FragmentColor *= texture2D(texUnit0, vVertexTexCoords);
            }
            void ApplyStandardShader()
            {
                ComputeLighting(FragmentColor, vFragmentNormal);
                ApplyFog(FragmentColor);
            }
            #endif
            #endif
            """,
        ["inc_scriptable"] = string.Concat(new[] { ("float", "Float"), ("int", "Int"), ("vec4", "Vec") }
            .SelectMany(k => Enumerable.Range(1, 16).Select(i => $"uniform {k.Item1} scriptable{k.Item2}{i};\n"))),
        ["vslit"] = "#define SHADER_TYPE 1\n#define LIGHTING 1\n#include \"inc_standard\"\nvoid main() { ApplyStandardShader(); }",
        ["fslit"] = "#define SHADER_TYPE 2\n#define LIGHTING 1\n#include \"inc_standard\"\nvoid main() { FragmentColor = vec4(1.0); SetupStandardShaderInputs(); ApplyStandardShader(); gl_FragColor = FragmentColor; }",
    };

    static ShaderTranslator With(params (string Name, string Source)[] shaders)
    {
        var map = shaders.ToDictionary(s => s.Name, s => s.Source, StringComparer.OrdinalIgnoreCase);
        return new ShaderTranslator { ModuleSource = n => map.GetValueOrDefault(n), InstalledSource = n => Installed.GetValueOrDefault(n) };
    }

    const string Tint = """
        // A fragment shader that tints the surface by a script uniform.
        #define SHADER_TYPE 2
        #define FOG 1
        #define LIGHTING 1
        #include "inc_standard"
        #include "inc_scriptable"
        uniform float strength;
        vec3 Tinted(vec3 c, float vPos)   // a parameter named like an engine input stays a parameter
        {
            return mix(c, scriptableVec3.rgb, strength * vPos);
        }
        void main()
        {
            FragmentColor = vec4(1.0);
            SetupStandardShaderInputs();
            FragmentColor.rgb = Tinted(FragmentColor.rgb, float(scriptableInt2 & 1) * scriptableFloat2);
            ApplyStandardShader();
            gl_FragColor = FragmentColor;
        }
        """;

    [Fact]
    public void MaterialFilesGiveShadersTexturesAndParameters()
    {
        var m = Mtr.Parse("""
            // comment
            customshaderVS vsworn
            customshaderFS fsrobe   // trailing comment
            renderhint NormalAndSpecMapped
            transparency=1
            texture0 tno01_grass
            texture2 null
            parameter float wetDarken 0.20
            parameter int mode 16777217
            parameter float tint 1.0 0.5 0.25 1.0
            """, "tno01_grass");
        Assert.Equal("vsworn", m.VertexShader);
        Assert.Equal("fsrobe", m.FragmentShader);
        Assert.Equal("NormalAndSpecMapped", m.RenderHint);
        Assert.True(m.Transparency);
        Assert.Equal("tno01_grass", m.Textures[0]);
        Assert.Equal("", m.Textures[2]);
        Assert.Equal(0.2f, m.Parameter("wetDarken")!.Values[0], 4);
        Assert.Equal(16777217, m.Parameter("mode")!.IntValue); // exact, not through a float
        Assert.Equal([1f, 0.5f, 0.25f, 1f], m.Parameter("tint")!.Values);
        Assert.True(m.HasCustomShader);
        Assert.False(Mtr.Parse("texture0 a").HasCustomShader);
    }

    [Fact]
    public void ThePreprocessorEvaluatesConditionsMacrosAndIncludes()
    {
        var pp = new ShaderPreprocessor { Resolve = n => n == "inc_a" ? "#ifndef INC_A\n#define INC_A\n#define TWICE(x) ((x) * 2)\nint fromA;\n#endif\n" : null };
        pp.Define("QUALITY", "2");
        string text = pp.Process("""
            #include "inc_a"
            #include "inc_a"
            #define ON 1
            #if ON == 1 && QUALITY > 1 && defined(INC_A)
            float a = TWICE(3.0);
            #elif ON
            float wrong;
            #else
            float wrong2;
            #endif
            #ifdef MISSING
            float wrong3;
            #endif
            #if UNDEFINED_NAME == 0 /* an unknown name is 0 */
            float b = 0x10;
            #endif
            #include "inc_missing"
            """);
        var compact = string.Join(" ", text.Split((char[])[' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("int fromA; float a = ((3.0) * 2); float b = 0x10;", compact);
        Assert.Contains(pp.Problems, p => p.Contains("inc_missing"));
    }

    [Fact]
    public void AFragmentShaderBecomesAGodotShaderWithStateUniformsAndBanks()
    {
        var t = With(("ow_tint", Tint)).Translate("vslit", "ow_tint");
        Assert.True(t.Ok, t.Reason);
        Assert.True(t.Lit);
        Assert.Contains("shader_type spatial;", t.Code);
        Assert.Contains("skip_vertex_transform", t.Code);
        // The banks are separate globals of their own types.
        Assert.Contains("global uniform vec4 scriptableVec3;", t.Code);
        Assert.Contains("global uniform int scriptableInt2;", t.Code);
        Assert.Contains("global uniform float scriptableFloat2;", t.Code);
        Assert.Contains(t.Globals, g => g == "scriptableInt2");
        // The module's own uniform is a material parameter; engine material values have their defaults.
        Assert.Contains(t.Parameters, p => p is { Name: "strength", Type: "float", Engine: false });
        Assert.Contains("uniform sampler2D texUnit0", t.Code);
        // Functions take the stage's state; globals live in it; GLSL 1 names are Godot's.
        Assert.Contains("vec3 fs_Tinted(inout OW_FS ow, vec3 c, float vPos)", t.Code);
        Assert.Contains("mix(c, scriptableVec3.rgb, strength * vPos)", t.Code);
        Assert.Contains("ow.FragmentColor.rgb = fs_Tinted(ow, ow.FragmentColor.rgb", t.Code);
        Assert.Contains("fs_main(ow);", t.Code);
        Assert.DoesNotContain("texture2D", t.Code);
        Assert.DoesNotContain("gl_FragColor =", t.Code.Replace("ow.gl_FragColor =", ""));
        // The colour goes to Godot's lighting with the shader's normal and roughness.
        Assert.Contains("ALBEDO =", t.Code);
        Assert.Contains("NORMAL = normalize(ow.vFragmentNormal);", t.Code);
        Assert.Contains("ROUGHNESS =", t.Code);
        // The installed library's lighting and fog are replaced (Godot lights and fogs); the rest is used as installed.
        Assert.Contains("void fs_ComputeLighting(inout OW_FS ow, inout vec4 ColorIO, vec3 vNormalThis)", t.Code);
        Assert.DoesNotContain("*= 0.25", t.Code);
        Assert.DoesNotContain("vec4(1.0, 0.0, 1.0, 1.0)", t.Code);
        Assert.Contains("texture(texUnit0", t.Code);
        // No highlights unless the module's own code sets the surface's material values.
        Assert.Contains("specular_disabled", t.Code);
    }

    [Fact]
    public void AModulesOwnFunctionsAreNeverReplaced()
    {
        var t = With(("ow_fs", "#define SHADER_TYPE 2\n#define LIGHTING 1\n#include \"inc_standard\"\nvoid ApplyFog(inout vec3 ColorIO) { ColorIO = vec3(0.5); }\n" +
            "void main() { FragmentColor = vec4(1.0); ApplyFog(FragmentColor.rgb); fRoughness = 0.1; gl_FragColor = FragmentColor; }")).Translate("vslit", "ow_fs");
        Assert.True(t.Ok, t.Reason);
        Assert.Contains("ColorIO = vec3(0.5);", t.Code);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(t.Code, @"void fs_ApplyFog\(inout OW_FS ow, inout vec3 ColorIO\)"));
        Assert.Contains("specular_schlick_ggx", t.Code); // it sets fRoughness itself
    }

    [Fact]
    public void VaryingsAPairOfStagesSharePassThroughGodotVaryings()
    {
        var t = With(
            ("ow_vs", "#define SHADER_TYPE 1\n#include \"inc_standard\"\nvarying vec3 vMine;\nvarying float vUnused;\nvoid main() { ApplyStandardShader(); vMine = vPos.xyz; vUnused = 1.0; }"),
            ("ow_fs", "#define SHADER_TYPE 2\n#define LIGHTING 0\n#include \"inc_standard\"\nvarying vec3 vMine;\nvoid main() { FragmentColor = vec4(vMine, 1.0); SetupStandardShaderInputs(); ApplyStandardShader(); gl_FragColor = FragmentColor; }"))
            .Translate("ow_vs", "ow_fs");
        Assert.True(t.Ok, t.Reason);
        Assert.False(t.Lit);
        Assert.Contains("unshaded", t.Code);
        Assert.Contains("varying vec3 ow_v_vMine;", t.Code);
        Assert.Contains("ow_v_vMine = ow.vMine;", t.Code);
        Assert.Contains("ow.vMine = ow_v_vMine;", t.Code);
        // Only what the fragment stage reads is passed on.
        Assert.DoesNotContain("ow_v_vUnused", t.Code);
        // The library places the vertex in the model's space; the module's own code reads it in its mesh's space.
        Assert.Contains("ow.vPos = vec4(VERTEX, 1.0);", t.Code);
        Assert.Contains("ow.vMine = ow." + EngineNames.MeshPosition + ".xyz;", t.Code);
        Assert.True(t.MeshSpace);
    }

    [Fact]
    public void AModuleThatMovesTheVertexKeepsTheModelsSpace()
    {
        var t = With(("ow_vs", "#define SHADER_TYPE 1\n#include \"inc_standard\"\nuniform float lift;\nvoid main() { vPos.z += lift; ApplyStandardShader(); }"))
            .Translate("ow_vs", "fslit");
        Assert.True(t.Ok, t.Reason);
        Assert.False(t.MeshSpace);
        Assert.Contains("ow.vPos.z += lift;", t.Code);
        Assert.Contains(t.Notes, n => n.Contains("writes vPos"));
    }

    [Fact]
    public void AModuleThatPlacesTheVertexItselfIsMarkedForMovingMeshes()
    {
        // gl_Position from m_mvp * vPos: mesh-space values are the rest pose, so meshes that move within their model
        // give such a shader none of their mesh-space data (ModelBuilder) and it reads the model's space.
        var t = With(("ow_vs", "#define SHADER_TYPE 1\n#include \"inc_standard\"\nuniform mat4 m_mvp;\nvarying vec3 vMine;\n" +
                "void main() { ApplyStandardShader(); vMine = vPos.xyz; gl_Position = m_mvp * vPos; }"),
            ("ow_fs", "#define SHADER_TYPE 2\n#define LIGHTING 0\n#include \"inc_standard\"\nvarying vec3 vMine;\nvoid main() { gl_FragColor = vec4(vMine, 1.0); }"))
            .Translate("ow_vs", "ow_fs");
        Assert.True(t.Ok, t.Reason);
        Assert.True(t.MeshSpace);
        Assert.True(t.PlacesVertex);
        Assert.Contains("ow.gl_Position = ow." + EngineNames.MeshModelViewProjection + " * ow." + EngineNames.MeshPosition + ";", t.Code);
        Assert.Contains("POSITION = " + EngineNames.GodotFromGlClip + " * ow.gl_Position;", t.Code);
        Assert.Contains("(CUSTOM0.w > 0.5 ? vec4(CUSTOM0.xyz, 1.0) : vec4(VERTEX, 1.0))", t.Code);
        Assert.Contains("(dot(CUSTOM1, CUSTOM1) == 0.0 ? mat4(1.0)", t.Code);
    }

    [Fact]
    public void MeshSpaceProjectionIsGlsClipSpace()
    {
        Assert.StartsWith(EngineNames.GlFromGodotClip + " * PROJECTION_MATRIX * MODELVIEW_MATRIX", EngineNames.Find(EngineNames.MeshModelViewProjection)!.Vertex);
        var t = With(("ow_vs", "#define SHADER_TYPE 1\n#include \"inc_standard\"\nuniform mat4 m_mvp;\nvarying vec4 vClip;\n" +
                "void main() { ApplyStandardShader(); vClip = m_mvp * vPos; }"),
            ("ow_fs", "#define SHADER_TYPE 2\n#define LIGHTING 0\n#include \"inc_standard\"\nvarying vec4 vClip;\nvoid main() { gl_FragColor = vClip; }"))
            .Translate("ow_vs", "ow_fs");
        Assert.True(t.Ok, t.Reason);
        Assert.True(t.MeshSpace);
        Assert.Contains("ow.vClip = ow." + EngineNames.MeshModelViewProjection + " * ow." + EngineNames.MeshPosition + ";", t.Code);
    }

    [Fact]
    public void BoundingBoxesAreMaterialValues()
    {
        var t = With(("ow_vs", "#define SHADER_TYPE 1\n#include \"inc_standard\"\nuniform vec3 bboxMin;\nuniform vec3 bboxMax;\nvarying vec3 vRel;\n" +
                "void main() { ApplyStandardShader(); vRel = (vPos.xyz - bboxMin) / (bboxMax - bboxMin); }"),
            ("ow_fs", "#define SHADER_TYPE 2\n#define LIGHTING 0\n#include \"inc_standard\"\nvarying vec3 vRel;\nvoid main() { gl_FragColor = vec4(vRel, 1.0); }"))
            .Translate("ow_vs", "ow_fs");
        Assert.True(t.Ok, t.Reason);
        Assert.True(t.Bounds);
        Assert.True(t.MeshSpace);
        Assert.Contains("uniform vec3 bboxMin = vec3(0.0);", t.Code);
    }

    [Fact]
    public void TheMainTextureIsSampledDecodedAndHandedOverEncoded()
    {
        var t = With().Translate("vslit", "fslit");
        Assert.True(t.Ok, t.Reason);
        Assert.Contains("uniform sampler2D texUnit0 : source_color,", t.Code);
        Assert.Contains("ow_encoded(texture(texUnit0, ", t.Code);
        // A shader that hands the texture on to a function of its own samples it raw.
        var passed = With(("ow_fs", "#define SHADER_TYPE 2\n#define LIGHTING 0\n#include \"inc_standard\"\nvec4 Fetch(sampler2D s) { return texture2D(s, vec2(0.5)); }\n" +
            "void main() { gl_FragColor = Fetch(texUnit0); }")).Translate("vslit", "ow_fs");
        Assert.True(passed.Ok, passed.Reason);
        Assert.DoesNotContain("source_color", passed.Code);
        Assert.DoesNotContain("ow_encoded", passed.Code);
    }

    [Fact]
    public void ShadersThatCantBeUsedSayWhy()
    {
        var missing = With(("ow_fs", "#define SHADER_TYPE 2\n#include \"inc_nowhere\"\nvoid main() {}")).Translate("vslit", "ow_fs");
        Assert.False(missing.Ok);
        Assert.Contains("inc_nowhere", missing.Reason);
        Assert.False(With().Translate("vslit", "no_such_shader").Ok);
        var noMain = With(("ow_fs", "#define SHADER_TYPE 2\nfloat f() { return 1.0; }")).Translate("vslit", "ow_fs");
        Assert.Contains("no main()", noMain.Reason);
        var error = With(("ow_fs", "#error not for this engine\nvoid main() {}")).Translate("vslit", "ow_fs");
        Assert.Contains("#error", error.Reason);
    }

    [Fact]
    public void ModuleIncludesComeBeforeTheLibrary()
    {
        var t = With(("inc_scriptable", "uniform float myOwn;"),
                ("ow_fs", "#define SHADER_TYPE 2\n#include \"inc_scriptable\"\nvoid main() { gl_FragColor = vec4(myOwn); }"))
            .Translate("vslit", "ow_fs");
        Assert.True(t.Ok, t.Reason);
        Assert.Contains("uniform float myOwn;", t.Code);
        Assert.DoesNotContain("scriptableFloat1", t.Code);
    }

    [DataFact]
    public void TheInstalledGamesStandardProgramsTranslate()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame(includeUserOverride: false);
        var translator = new ShaderTranslator { ModuleSource = ShaderSources.Module(rm), InstalledSource = ShaderSources.Installed(rm) };
        foreach (var (vs, fs) in new[] { ("vslit", "fslit"), ("vslit_nm", "fslit_nm"), ("vsvt", "fst"), ("vsvc", "fsc"), ("vslit_sk", "fslit"), ("vslit_sm", "fslit_sm") })
        {
            var t = translator.Translate(vs, fs);
            Assert.True(t.Ok, $"{vs} + {fs}: {t.Reason}");
            Assert.DoesNotContain("lightPosition", t.Code);
        }
    }

    // ---- sources that must not hang or crash the client ----

    static void Refused(string source, string expect)
    {
        var t = With(("ow_fs", source)).Translate("vslit", "ow_fs");
        Assert.False(t.Ok);
        Assert.Contains(expect, t.Reason);
    }

    [Fact]
    public void IncludesThatFanOutAreRefused() =>
        Refused("#include \"ow_fs\"\n#include \"ow_fs\"\nvoid main() {}", "includes");

    [Fact]
    public void MacrosThatExplodeAreRefused() =>
        Refused(string.Concat(Enumerable.Range(1, 40).Select(i => $"#define A{i} A{i - 1} A{i - 1}\n")) + "#define A0 x\nfloat f = A40;\nvoid main() {}", "macros");

    [Fact]
    public void LongMacroChainsAreRefused() =>
        Refused(string.Concat(Enumerable.Range(1, 30000).Select(i => $"#define M{i} M{i - 1}\n")) + "#define M0 1\nint i = M30000;\nvoid main() {}", "macros");

    [Fact]
    public void DeeplyNestedConditionsAreRefused() =>
        Refused("#if " + new string('(', 20000) + "1" + new string(')', 20000) + "\n#endif\nvoid main() {}", "#if");

    [Fact]
    public void OversizedNumbersInConditionsAreRefused() =>
        Refused("#if 99999999999999999999\n#endif\nvoid main() {}", "#if");

    [Theory]
    [InlineData("#define SHADER_TYPE 2\nstruct")]
    [InlineData("void main() {")]
    [InlineData("void main( { }")]
    [InlineData("float f(float x { return x; }\nvoid main() {}")]
    [InlineData("uniform float [3];\nvoid main() {}")]
    [InlineData("#if\n#elif\n#else\n#else\n#endif\n#endif\nvoid main() {}")]
    [InlineData("#define F(a, b) a ## b ## #a\nfloat F(x, ) = F(;\nvoid main() { }")]
    public void BrokenSourcesFallBackWithoutThrowing(string source)
    {
        var t = With(("ow_fs", source)).Translate("vslit", "ow_fs");
        if (!t.Ok) Assert.NotEmpty(t.Reason);
    }

    [Fact]
    public void ScriptUniformBanksAreNamedPerKind()
    {
        Assert.Equal("scriptableFloat1", EngineNames.ScriptUniform("float", 0));
        Assert.Equal("scriptableInt16", EngineNames.ScriptUniform("int", 15));
        Assert.Equal("scriptableVec11", EngineNames.ScriptUniform("vec4", 10));
        Assert.Null(EngineNames.ScriptUniform("int", 16));
        Assert.Equal(48, EngineNames.Globals.Count(g => g.Name.StartsWith("scriptable", StringComparison.Ordinal)));
    }

    [Fact]
    public void ShadersAndMaterialsGoToClientsWithTheModulesPresentation()
    {
        Assert.True(ModuleClientResources.IsClientResource(ResType.Shd));
        Assert.True(ModuleClientResources.IsClientResource(ResType.Mtr));
    }

    sealed class Data : INwnBridgeData
    {
        public int TwoDARowCount(string table) => 0;
        public string TwoDA(string table, int row, string column) => "";
        public string Text(uint strRef, byte gender) => "";
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    [Fact]
    public void TheBridgeGivesNwserversShaderUniformsTheSameBanks()
    {
        var output = new List<byte[]>();
        var data = new Data();
        var presentation = new NwnPresentation(data, new NwnWorldMirror(data, output.Add), output.Add);
        Assert.True(presentation.ToClient(new SetShaderUniformMessages.SetShaderUniformFloat { Index = 10, Value = 0.25f }));
        Assert.True(presentation.ToClient(new SetShaderUniformMessages.SetShaderUniformInt { Index = 10, Value = 0x1E00FF01 }));
        Assert.True(presentation.ToClient(new SetShaderUniformMessages.SetShaderUniformVec { Index = 0, X = 1, Y = 2, Z = 3, W = 4 }));
        var notes = output.Select(b => NotifyMessage.Read(new MessageReader(b))).ToList();
        Assert.All(notes, n => Assert.Equal("shader-uniform", n.Kind));
        var json = notes.Select(n => JsonDocument.Parse(n.Json).RootElement).ToList();
        Assert.Equal(("float", 10, 0.25f), (json[0].GetProperty("type").GetString(), json[0].GetProperty("shader").GetInt32(), json[0].GetProperty("value")[0].GetSingle()));
        Assert.Equal(("int", 10, 0x1E00FF01), (json[1].GetProperty("type").GetString(), json[1].GetProperty("shader").GetInt32(), json[1].GetProperty("int").GetInt32()));
        Assert.Equal("vec4", json[2].GetProperty("type").GetString());
        Assert.Equal([1f, 2f, 3f, 4f], json[2].GetProperty("value").EnumerateArray().Select(v => v.GetSingle()));
    }

    const string PostVs = "#define SHADER_TYPE 1\nattribute vec4 vPos;\nattribute vec2 vTcIn;\nvarying vec2 vVertexTexCoords;\nvoid main() { gl_Position = vPos; vVertexTexCoords = vTcIn; }";

    const string PostFs = """
        #define SHADER_TYPE 2
        uniform sampler2D texFBColor;
        uniform sampler2D texFBDepth;
        uniform mat4 m_view_inv;
        varying vec2 vVertexTexCoords;
        void main()
        {
            vec4 c = texture2D(texFBColor, vVertexTexCoords);
            float d = texture2D(texFBDepth, vVertexTexCoords).x;
            vec3 eye = m_view_inv[3].xyz;
            gl_FragColor = vec4(c.rgb * d + eye * 0.0, 1.0);
        }
        """;

    [Fact]
    public void ThePostProcessPassIsACanvasItemReadingTheViewAtGlCoordinates()
    {
        var t = With(("vsfbpostpr", PostVs), ("fsfbpostpr", PostFs)).Translate("vsfbpostpr", "fsfbpostpr", new ShaderOptions { PostProcess = true });
        Assert.True(t.Ok, t.Reason);
        Assert.Contains("shader_type canvas_item;", t.Code);
        Assert.Contains("render_mode unshaded, blend_disabled;", t.Code);
        Assert.DoesNotContain("ALBEDO", t.Code);
        Assert.Contains("COLOR = vec4(clamp(ow.gl_FragColor.rgb", t.Code);
        Assert.True(t.ReadsColor && t.ReadsDepth);
        // Reads of the framebuffers go through helpers that flip to GL's origin and turn depth into GL's.
        Assert.Contains("ow_fb_color(ow.vVertexTexCoords)", t.Code);
        Assert.Contains("ow_fb_depth(ow.vVertexTexCoords).x", t.Code);
        Assert.Contains("hint_screen_texture", t.Code);
        Assert.Contains("1.0 - textureLod(ow_fb_depth_tex, vec2(uv.x, 1.0 - uv.y), 0.0).r", t.Code);
        // The quad covers the screen with GL texture coordinates; the camera comes from the pass's globals.
        Assert.Contains("ow.vTcIn = vec2(UV.x, 1.0 - UV.y);", t.Code);
        Assert.Contains("ow.m_view_inv = ow_pp_view_inv;", t.Code);
        Assert.Contains("global uniform mat4 ow_pp_view_inv;", t.Code);
        Assert.DoesNotContain("VERTEX =", t.Code);
    }

    [Fact]
    public void ProjectionsAreGlsAndAModulesOwnPlacementIsUsed()
    {
        const string vs = "#define SHADER_TYPE 1\nuniform mat4 m_mvp;\nattribute vec4 vPos;\nvoid main() { gl_Position = m_mvp * vPos; gl_Position.y += 0.1; }";
        var t = With(("ow_place", vs)).Translate("ow_place", "fslit");
        Assert.True(t.Ok, t.Reason);
        // The module's own code reads the mesh's matrices, in GL's clip space too.
        Assert.Contains("ow." + EngineNames.MeshModelViewProjection + " = " + EngineNames.GlFromGodotClip + " * PROJECTION_MATRIX", t.Code);
        Assert.Contains("POSITION = " + EngineNames.GodotFromGlClip + " * ow.gl_Position;", t.Code);
        // The installed standard programs keep the renderer's own placement.
        var plain = With().Translate("vslit", "fslit");
        Assert.DoesNotContain("POSITION =", plain.Code);
    }

    [Fact]
    public void GlslOnlyFormsAreRewrittenForGodot()
    {
        const string fs = """
            #define SHADER_TYPE 2
            mat4 Turn(float a)
            {
                return mat4(cos(a), sin(a), 0.0, 0.0, -sin(a), cos(a), 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0);
            }
            void main()
            {
                int n;
                float f = 0.0;
                vec4 v4 = vec4(1.0);
                vec3 v3 = vec3(v4);
                const vec3 k = vec3(vec4(2.0));
                for (n = 1; n < 4; n++) f += float(n);
                gl_FragColor = Turn(f) * vec4(1.0);
            }
            """;
        var t = With(("ow_forms", fs)).Translate("vslit", "ow_forms");
        Assert.True(t.Ok, t.Reason);
        Assert.Contains("mat4(vec4(cos(a), sin(a), 0.0, 0.0), vec4(", t.Code);
        Assert.Contains("vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0))", t.Code);
        Assert.Contains("n = 1;", t.Code);
        // GLSL's vec3 of a vec4 keeps its first components; a constant's initializer is left as written.
        Assert.Contains("vec3 v3 = ow_vec3(v4);", t.Code);
        Assert.Contains("vec3 ow_vec3(vec4 v) { return v.xyz; }", t.Code);
        Assert.Contains("const vec3 k = vec3(vec4(2.0));", t.Code);
        Assert.Contains("for(int ow_for0 = 0; n < 4; n++)", t.Code.Replace("for (", "for("));
    }

    [Fact]
    public void SingleChannelDdsTexturesDecodeAsGrey()
    {
        // A 4x4 BC4 ("ATI1") texture: endpoints 255 and 0, the first pixel index 0 (255), the second index 1 (0),
        // the third index 2 (between, 6/7 of the way to the first endpoint).
        var dds = new byte[128 + 8];
        "DDS "u8.CopyTo(dds);
        BitConverter.GetBytes(124).CopyTo(dds, 4);
        BitConverter.GetBytes(4).CopyTo(dds, 12);
        BitConverter.GetBytes(4).CopyTo(dds, 16);
        BitConverter.GetBytes(1).CopyTo(dds, 28);
        BitConverter.GetBytes(4).CopyTo(dds, 80);
        "ATI1"u8.CopyTo(dds.AsSpan(84));
        dds[128] = 255;
        dds[129] = 0;
        dds[130] = 0b10_001_000;
        var t = OpenWinter.Aurora.Textures.TextureDecoder.DecodeDds(dds);
        Assert.NotNull(t);
        Assert.Equal((4, 4), (t!.Width, t.Height));
        Assert.Equal(new byte[] { 255, 255, 255, 255, 0, 0, 0, 255 }, t.Data.Take(8));
        Assert.Equal(255 * 6 / 7, t.Data[8]);
        // The same block behind the extended (DX10) header, as DXGI_FORMAT_BC4_UNORM.
        var dx10 = new byte[148 + 8];
        dds.AsSpan(0, 128).CopyTo(dx10);
        "DX10"u8.CopyTo(dx10.AsSpan(84));
        BitConverter.GetBytes(80).CopyTo(dx10, 128);
        dds.AsSpan(128, 8).CopyTo(dx10.AsSpan(148));
        Assert.Equal(t.Data, OpenWinter.Aurora.Textures.TextureDecoder.DecodeDds(dx10)!.Data);
    }
}

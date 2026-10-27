using System.Text;

namespace OpenWinter.Aurora.Shaders;

/// <summary>How the material using a translated shader is drawn (from the mesh, its texture and its MTR).</summary>
public sealed record ShaderOptions
{
    /// <summary>Blends with what is behind it: the shader's alpha is used.</summary>
    public bool Transparent { get; init; }
    public bool Additive { get; init; }
    public bool CullDisabled { get; init; }
    public bool DepthDrawAlways { get; init; }
    public bool DepthDrawNever { get; init; }
    /// <summary>No renderer fog whatever the shader says (the sky, which is drawn at infinity).</summary>
    public bool FogDisabled { get; init; }
    /// <summary>Extra declarations at the top level of the generated shader (the client's keyhole cut).</summary>
    public string Declarations { get; init; } = "";
    /// <summary>Code run at the end of the fragment stage, after ALBEDO (and ALPHA) are set.</summary>
    public string FragmentEnd { get; init; } = "";
}

/// <summary>A material parameter of a translated shader (set by the client from the mesh, its texture and its MTR).</summary>
public sealed record ShaderParameter(string Name, string Type, int ArraySize, bool Engine);

public sealed class ShaderTranslation
{
    public required string VertexShader { get; init; }
    public required string FragmentShader { get; init; }
    public string Code = "";
    /// <summary>Why the shader can't be used; empty when it translated.</summary>
    public readonly List<string> Problems = new();
    /// <summary>Things approximated or ignored (the shader still works).</summary>
    public readonly List<string> Notes = new();
    public readonly List<ShaderParameter> Parameters = new();
    public readonly SortedSet<string> Globals = new(StringComparer.Ordinal);
    public readonly List<string> Includes = new();
    public bool Ok => Problems.Count == 0;
    /// <summary>Lit by the renderer (LIGHTING 1); otherwise drawn unshaded.</summary>
    public bool Lit;
    public bool Fog;
    public bool Keyholing;
    public string Reason => string.Join("; ", Problems);
}

/// <summary>
/// Translates a vertex and fragment shader written for the original's shader system (GLSL with #include of its
/// library and the engine's uniform, attribute and varying names) into one Godot spatial shader.
/// <list type="bullet">
/// <item>Sources are preprocessed (<see cref="ShaderPreprocessor"/>); shaders and includes come from the module
/// first, then the player's installed game (its own shader library, read at run time). The few library functions
/// that do the renderer's own work (lighting from the engine's light list, fog, keyholing, the targeting
/// highlight) are replaced by <see cref="ShaderOverrides"/>, because Godot does that work itself.</item>
/// <item>Godot has no mutable globals and no built-ins outside the stage functions, so every global variable,
/// attribute, varying and engine input of a stage lives in one state struct passed (inout) to every function.</item>
/// <item>Each stage's main() becomes a function called from vertex() / fragment(); the vertex stage hands Godot the
/// view-space position and normal, the fragment stage the colour (ALBEDO), normal, roughness and alpha, and Godot
/// does the lighting and fog.</item>
/// <item>Engine uniforms become global parameters (world state, script uniforms) or material parameters
/// (textures, material colours); a module's own uniforms are material parameters its MTR sets.</item>
/// </list>
/// Anything it can't translate is reported in <see cref="ShaderTranslation.Problems"/>; the caller then keeps the
/// mesh's normal material.
/// </summary>
public sealed class ShaderTranslator
{
    /// <summary>A module's own shader source by name (module and hak resources), or null.</summary>
    public Func<string, string?> ModuleSource { get; init; } = _ => null;

    /// <summary>A shader of the installed game by name (its shader library and standard programs), or null.</summary>
    public Func<string, string?> InstalledSource { get; init; } = _ => null;

    const int MaxFunctions = 4000;

    /// <summary>Values the engine defines for every shader (its quality settings).</summary>
    public static readonly IReadOnlyDictionary<string, string> Predefined = new Dictionary<string, string>
    {
        ["OPENWINTER"] = "1",
        ["SHADER_QUALITY_MODE"] = "2",
        ["FRAGMENT_LIGHTING"] = "1",
        ["GAMMA_CORRECTION"] = "1",
        ["KEYHOLING_ENABLED"] = "1",
        ["MAX_NUM_LIGHTS"] = "8",
        ["MAX_NUM_BONES"] = "64",
        ["SHADER_DEBUG_MODE"] = "0",
    };

    readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string? Text, bool Installed)> _sources = new(StringComparer.OrdinalIgnoreCase);

    (string? Text, bool Installed) Source(string name) => _sources.GetOrAdd(name, n =>
        ModuleSource(n) is { } own ? (own, false) : InstalledSource(n) is { } installed ? (installed, true) : (null, false));

    public string? Resolve(string name) => Source(name).Text;

    /// <summary>Whether a shader of this name comes from the installed game (no module or hak has one).</summary>
    public bool IsInstalled(string name) => Source(name).Installed;

    /// <summary>The name a shader's uniform has in the translated code (a few names are Godot's own).</summary>
    public static string UniformName(string name) => Reserved.Contains(name) ? "ow_u_" + name : name;

    public ShaderTranslation Translate(string vertexShader, string fragmentShader, ShaderOptions? options = null)
    {
        options ??= new ShaderOptions();
        var result = new ShaderTranslation { VertexShader = vertexShader, FragmentShader = fragmentShader };
        try
        {
            var vs = Unit.Load(this, vertexShader, 1, result);
            var fs = Unit.Load(this, fragmentShader, 2, result);
            if (vs == null || fs == null || !result.Ok) return result;
            result.Code = new Emitter(vs, fs, options, result).Emit();
        }
        catch (TranslationException e)
        {
            result.Problems.Add(e.Message);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Whatever the source, translating it must never take the caller down: the mesh keeps its material.
            result.Problems.Add($"can't be translated ({e.GetType().Name})");
        }
        if (!result.Ok) result.Code = "";
        return result;
    }

    internal sealed class TranslationException(string message) : Exception(message);

    // ---- tokens ----

    internal enum Kind { Ident, Number, Punct }

    /// <summary>A token, with the file it came from (the main shader or an include).</summary>
    internal readonly record struct Tok(string Text, Kind Kind, string File = "")
    {
        public override string ToString() => Text;
    }

    internal static List<Tok> Tokens(string text, string file = "")
    {
        var list = new List<Tok>();
        foreach (var t in ShaderPreprocessor.Lex(text))
        {
            if (char.IsWhiteSpace(t[0])) continue;
            var kind = char.IsAsciiLetter(t[0]) || t[0] == '_' ? Kind.Ident : char.IsAsciiDigit(t[0]) || (t[0] == '.' && t.Length > 1) ? Kind.Number : Kind.Punct;
            list.Add(new Tok(t, kind, file));
        }
        return list;
    }

    /// <summary>Preprocessed text with file markers, as tokens that know their file.</summary>
    internal static List<Tok> MarkedTokens(string text)
    {
        var list = new List<Tok>();
        string file = "";
        var chunk = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith(ShaderPreprocessor.Marker, StringComparison.Ordinal))
            {
                list.AddRange(Tokens(chunk.ToString(), file));
                chunk.Clear();
                file = line[ShaderPreprocessor.Marker.Length..].Trim();
                continue;
            }
            chunk.Append(line).Append('\n');
        }
        list.AddRange(Tokens(chunk.ToString(), file));
        return list;
    }

    // ---- language tables ----

    internal static readonly HashSet<string> BuiltinTypes = new(StringComparer.Ordinal)
    {
        "void", "bool", "int", "uint", "float", "double", "vec2", "vec3", "vec4", "ivec2", "ivec3", "ivec4", "uvec2", "uvec3", "uvec4",
        "bvec2", "bvec3", "bvec4", "mat2", "mat3", "mat4", "mat2x2", "mat2x3", "mat2x4", "mat3x2", "mat3x3", "mat3x4", "mat4x2",
        "mat4x3", "mat4x4", "sampler2D", "samplerCube", "sampler3D", "sampler2DArray", "isampler2D", "usampler2D", "sampler2DShadow",
    };

    internal static readonly HashSet<string> Precision = new(StringComparer.Ordinal) { "highp", "mediump", "lowp" };

    static readonly HashSet<string> StorageQualifiers = new(StringComparer.Ordinal)
    {
        "uniform", "attribute", "varying", "const", "in", "out", "inout", "flat", "smooth", "noperspective", "centroid", "invariant",
        "highp", "mediump", "lowp",
    };

    /// <summary>Names GLSL allows but Godot's shader language keeps for itself.</summary>
    internal static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "ow", "global", "instance", "render_mode", "shader_type", "stencil_mode", "group_uniforms", "source_color", "uint",
        "uvec2", "uvec3", "uvec4", "switch", "case", "default", "PI", "TAU", "E", "TIME", "VERTEX", "NORMAL", "TANGENT", "BINORMAL",
        "UV", "UV2", "COLOR", "ALBEDO", "ALPHA", "EMISSION", "ROUGHNESS", "METALLIC", "SPECULAR", "POSITION", "MODEL_MATRIX",
        "VIEW_MATRIX", "PROJECTION_MATRIX", "INV_VIEW_MATRIX", "INV_PROJECTION_MATRIX", "VIEWPORT_SIZE", "FRAGCOORD", "SCREEN_UV",
        "FRONT_FACING", "POINT_SIZE", "LIGHT", "ATTENUATION", "DEPTH", "AO", "RIM", "CLEARCOAT", "ANISOTROPY", "SSS_STRENGTH",
        "BACKLIGHT", "OUTPUT_IS_SRGB", "CAMERA_POSITION_WORLD", "NODE_POSITION_WORLD", "VERTEX_ID", "INSTANCE_ID", "DIFFUSE_LIGHT",
        "SPECULAR_LIGHT", "MODELVIEW_MATRIX", "NORMAL_MAP", "NORMAL_MAP_DEPTH",
    };

    /// <summary>GLSL 1.x built-in functions under their Godot names.</summary>
    internal static readonly Dictionary<string, string> BuiltinRenames = new(StringComparer.Ordinal)
    {
        ["texture2D"] = "texture",
        ["texture2DLod"] = "textureLod",
        ["texture2DProj"] = "textureProj",
        ["texture2DProjLod"] = "textureProjLod",
        ["texture2DGradEXT"] = "textureGrad",
        ["textureCube"] = "texture",
        ["textureCubeLod"] = "textureLod",
        ["texture2DLodEXT"] = "textureLod",
        ["textureCubeLodEXT"] = "textureLod",
    };

    /// <summary>The fixed GL variables shaders read and write, as state fields.</summary>
    internal static readonly Dictionary<string, string> GlVariables = new(StringComparer.Ordinal)
    {
        ["gl_Position"] = "vec4",
        ["gl_PointSize"] = "float",
        ["gl_FragColor"] = "vec4",
        ["gl_FragDepth"] = "float",
        ["gl_FragCoord"] = "vec4",
        ["gl_FrontFacing"] = "bool",
    };

    // ---- one stage ----

    internal sealed record Variable(string Name, string Type, string Array, List<Tok>? Init, string Storage, string Interpolation);

    internal sealed record Param(string Qualifier, string Type, string Name, string Array);

    internal sealed record Function(string ReturnType, string Name, List<Param> Params, List<Tok> Body);

    /// <summary>One preprocessed and parsed shader file (a vertex or fragment stage).</summary>
    internal sealed class Unit
    {
        public required string Name;
        public required int Stage;
        /// <summary>Whether a file comes from the installed game (its library functions may be replaced).</summary>
        public required Func<string, bool> Installed;
        /// <summary>Installed library functions replaced by OpenWinter's, by name.</summary>
        readonly HashSet<string> _replaced = new(StringComparer.Ordinal);
        public string Prefix => Stage == 1 ? "vs_" : "fs_";
        public readonly Dictionary<string, string> Macros = new();
        public readonly List<string> Structs = new();
        public readonly HashSet<string> StructNames = new(StringComparer.Ordinal);
        public readonly List<Variable> Uniforms = new();
        public readonly List<Variable> Consts = new();
        /// <summary>Mutable globals, attributes and varyings, in declaration order.</summary>
        public readonly List<Variable> State = new();
        public readonly List<Function> Functions = new();
        public readonly HashSet<string> FunctionNames = new(StringComparer.Ordinal);
        /// <summary>Fragment outputs declared "out vec4 name": written as gl_FragColor.</summary>
        public readonly HashSet<string> FragmentOutputs = new(StringComparer.Ordinal);

        public int Switch(string name) => Macros.TryGetValue(name, out var v) && int.TryParse(v, out var n) ? n : 0;

        public static Unit? Load(ShaderTranslator t, string name, int stage, ShaderTranslation result)
        {
            if (name.Length == 0) { result.Problems.Add($"no {(stage == 1 ? "vertex" : "fragment")} shader named"); return null; }
            var source = t.Resolve(name);
            if (source == null) { result.Problems.Add($"shader {name} not found"); return null; }
            var pp = new ShaderPreprocessor { Resolve = t.Resolve, FileMarkers = true };
            foreach (var (k, v) in Predefined) pp.Define(k, v);
            pp.Define("SHADER_TYPE", stage.ToString());
            string text = pp.Process(source, name);
            foreach (var p in pp.Problems)
                if (p.Contains("unknown directive")) result.Notes.Add(p);
                else result.Problems.Add(p);
            foreach (var inc in pp.Included)
                if (!result.Includes.Contains(inc, StringComparer.OrdinalIgnoreCase)) result.Includes.Add(inc);
            if (!result.Ok) return null;
            var unit = new Unit { Name = name, Stage = stage, Installed = t.IsInstalled };
            foreach (var (k, m) in pp.Macros)
                if (m.Parameters == null) unit.Macros[k] = string.Concat(m.Body);
            try
            {
                var tokens = MarkedTokens(text);
                unit.Parse(tokens);
                unit.AddOverrides(pp);
                unit.SetsMaterial = tokens.Any(x => x.Text is "fSpecularity" or "fRoughness" or "fMetallicness" or "SpecularColor"
                    && x.File.Length > 0 && !t.IsInstalled(x.File));
                if (unit.Functions.Count > MaxFunctions) throw new TranslationException($"more than {MaxFunctions} functions");
            }
            catch (TranslationException e)
            {
                result.Problems.Add($"{name}: {e.Message}");
                return null;
            }
            if (!unit.FunctionNames.Contains("main")) { result.Problems.Add($"{name}: no main()"); return null; }
            return unit;
        }

        void Parse(List<Tok> tokens)
        {
            int i = 0;
            while (i < tokens.Count)
            {
                var t = tokens[i];
                if (t.Text == ";") { i++; continue; }
                if (t.Text == "precision") { i = Skip(tokens, i, ";") + 1; continue; }
                if (t.Text == "layout") { i = Close(tokens, i + 1) + 1; continue; }
                if (t.Text == "struct") { i = ParseStruct(tokens, i); continue; }
                int depth = 0, j = i;
                for (; j < tokens.Count; j++)
                {
                    string s = tokens[j].Text;
                    if (s is "(" or "[") depth++;
                    else if (s is ")" or "]") depth--;
                    else if (depth == 0 && s is ";" or "{") break;
                }
                if (j >= tokens.Count) throw new TranslationException($"unexpected end after '{t.Text}'");
                if (tokens[j].Text == "{")
                {
                    int end = Close(tokens, j);
                    ParseFunction(tokens.GetRange(i, j - i), tokens.GetRange(j + 1, end - j - 1));
                    i = end + 1;
                }
                else
                {
                    ParseDeclaration(tokens.GetRange(i, j - i));
                    i = j + 1;
                }
            }
        }

        static int Skip(List<Tok> tokens, int i, string until)
        {
            while (i < tokens.Count && tokens[i].Text != until) i++;
            return i;
        }

        /// <summary>The index of the bracket closing the one at <paramref name="open"/>.</summary>
        static int Close(List<Tok> tokens, int open)
        {
            string o = tokens[open].Text, c = o switch { "(" => ")", "[" => "]", _ => "}" };
            int depth = 0;
            for (int i = open; i < tokens.Count; i++)
            {
                if (tokens[i].Text == o) depth++;
                else if (tokens[i].Text == c && --depth == 0) return i;
            }
            throw new TranslationException($"unclosed '{o}'");
        }

        int ParseStruct(List<Tok> tokens, int i)
        {
            if (i + 1 >= tokens.Count) throw new TranslationException("struct without a name");
            string name = tokens[i + 1].Text;
            int open = i + 2;
            if (open >= tokens.Count || tokens[open].Text != "{") throw new TranslationException("struct without a body");
            int close = Close(tokens, open);
            var members = new StringBuilder();
            foreach (var m in Split(tokens.GetRange(open + 1, close - open - 1), ";"))
            {
                var parts = m.Where(x => !Precision.Contains(x.Text)).ToList();
                if (parts.Count > 0) members.Append('\t').Append(Join(parts)).Append(";\n");
            }
            StructNames.Add(name);
            Structs.Add($"struct {name} {{\n{members}}};");
            int end = Skip(tokens, close, ";");
            if (end > close + 1) ParseDeclaration([new Tok(name, Kind.Ident), .. tokens.GetRange(close + 1, end - close - 1)]);
            return end + 1;
        }

        void ParseDeclaration(List<Tok> decl)
        {
            var qualifiers = new HashSet<string>(StringComparer.Ordinal);
            int k = 0;
            while (k < decl.Count && StorageQualifiers.Contains(decl[k].Text)) qualifiers.Add(decl[k++].Text);
            if (k >= decl.Count) return;
            string type = decl[k++].Text;
            while (k < decl.Count && Precision.Contains(decl[k].Text)) k++;
            var rest = decl.GetRange(k, decl.Count - k);
            if (rest.Count >= 2 && rest[0].Kind == Kind.Ident && rest[1].Text == "(") return; // a function prototype
            string storage = qualifiers.Contains("uniform") ? "uniform"
                : qualifiers.Contains("attribute") || (qualifiers.Contains("in") && Stage == 1) ? "attribute"
                : qualifiers.Contains("varying") || (qualifiers.Contains("out") && Stage == 1) || (qualifiers.Contains("in") && Stage == 2) ? "varying"
                : qualifiers.Contains("out") ? "output"
                : qualifiers.Contains("const") ? "const" : "global";
            string interpolation = qualifiers.Contains("flat") ? "flat" : "";
            foreach (var d in Split(rest, ","))
            {
                if (d.Count == 0 || d[0].Kind != Kind.Ident) throw new TranslationException($"can't read the declaration of {type}");
                string name = d[0].Text;
                int at = 1;
                string array = "";
                while (at < d.Count && d[at].Text == "[")
                {
                    int close = Close(d, at);
                    array += Join(d.GetRange(at, close - at + 1));
                    at = close + 1;
                }
                List<Tok>? init = null;
                if (at < d.Count && d[at].Text == "=") init = d.GetRange(at + 1, d.Count - at - 1);
                var v = new Variable(name, type, array, init, storage, interpolation);
                switch (storage)
                {
                    case "uniform": Uniforms.Add(v); break;
                    case "const": Consts.Add(v); break;
                    case "output": FragmentOutputs.Add(name); break;
                    default:
                        // A name declared twice (a varying in two includes): the first wins.
                        if (!State.Any(s => s.Name == name)) State.Add(v);
                        break;
                }
            }
        }

        void ParseFunction(List<Tok> header, List<Tok> body)
        {
            int open = header.FindIndex(x => x.Text == "(");
            if (open < 1) throw new TranslationException($"can't read the function '{Join(header)}'");
            int close = Close(header, open);
            string name = header[open - 1].Text;
            string ret = Join(header.GetRange(0, open - 1).Where(x => !Precision.Contains(x.Text) && x.Text is not ("inline" or "const")));
            var parameters = new List<Param>();
            var inner = header.GetRange(open + 1, close - open - 1);
            if (!(inner.Count == 0 || (inner.Count == 1 && inner[0].Text == "void")))
                foreach (var p in Split(inner, ","))
                {
                    var parts = p.Where(x => !Precision.Contains(x.Text) && x.Text != "const").ToList();
                    string qualifier = parts.Count > 0 && parts[0].Text is "in" or "out" or "inout" ? parts[0].Text : "";
                    if (qualifier.Length > 0) parts.RemoveAt(0);
                    if (parts.Count < 1) throw new TranslationException($"{name}: can't read a parameter");
                    string ptype = parts[0].Text;
                    string pname = parts.Count > 1 ? parts[1].Text : "ow_unnamed" + parameters.Count;
                    string parray = parts.Count > 2 ? Join(parts.Skip(2)) : "";
                    parameters.Add(new Param(qualifier, ptype, pname, parray));
                }
            if (ShaderOverrides.Functions.ContainsKey(name) && header[0].File.Length > 0 && Installed(header[0].File) && !_overriding)
            {
                _replaced.Add(name);
                return;
            }
            FunctionNames.Add(name);
            Functions.Add(new Function(ret, name, parameters, body));
        }

        bool _overriding;

        /// <summary>OpenWinter's versions of the installed library functions this stage defined.</summary>
        public void AddOverrides(ShaderPreprocessor pp)
        {
            if (_replaced.Count == 0) return;
            var own = new ShaderPreprocessor { Resolve = _ => null };
            foreach (var (k, m) in pp.Macros) own.Macros[k] = m;
            string text = own.Process(string.Join("\n", _replaced.Select(n => ShaderOverrides.Functions[n])), "OpenWinter");
            if (own.Problems.Count > 0) throw new TranslationException(string.Join("; ", own.Problems));
            int before = Functions.Count;
            _overriding = true;
            Parse(Tokens(text, "OpenWinter"));
            _overriding = false;
            // An overload the module itself defines stays the module's.
            static string Signature(Function f) => string.Join(",", f.Params.Select(p => p.Type + p.Array));
            for (int i = Functions.Count - 1; i >= before; i--)
                if (Functions.Take(before).Any(g => g.Name == Functions[i].Name && Signature(g) == Signature(Functions[i]))) Functions.RemoveAt(i);
        }

        /// <summary>Whether code outside the installed game sets the surface's material values (so it wants highlights).</summary>
        public bool SetsMaterial;

        public static List<List<Tok>> Split(List<Tok> tokens, string separator)
        {
            var parts = new List<List<Tok>> { new() };
            int depth = 0;
            foreach (var t in tokens)
            {
                if (t.Text is "(" or "[" or "{") depth++;
                else if (t.Text is ")" or "]" or "}") depth--;
                else if (depth == 0 && t.Text == separator) { parts.Add(new()); continue; }
                parts[^1].Add(t);
            }
            if (parts[^1].Count == 0) parts.RemoveAt(parts.Count - 1);
            return parts;
        }
    }

    internal static string Join(IEnumerable<Tok> tokens)
    {
        var b = new StringBuilder();
        Tok? prev = null;
        foreach (var t in tokens)
        {
            if (prev is { } p && NeedsSpace(p, t)) b.Append(' ');
            b.Append(t.Text);
            prev = t;
        }
        return b.ToString();
    }

    static bool NeedsSpace(Tok a, Tok b)
    {
        if (b.Text is "." or "," or ";" or ")" or "]") return false;
        if (a.Text is "." or "(" or "[" or "!" or "~") return false;
        if (b.Text is "(" or "[" or "++" or "--" && a.Kind != Kind.Punct) return false;
        return true;
    }

    /// <summary>A function body, one statement per line.</summary>
    internal static string Body(List<Tok> tokens)
    {
        var b = new StringBuilder("\t");
        int paren = 0, indent = 1;
        Tok? prev = null;
        foreach (var t in tokens)
        {
            if (t.Text == "}") indent = Math.Max(1, indent - 1);
            if (prev is { } p)
            {
                bool lineBreak = (p.Text == ";" && paren == 0) || p.Text is "{" or "}" || t.Text == "}";
                if (lineBreak) b.Append('\n').Append('\t', indent);
                else if (NeedsSpace(p, t)) b.Append(' ');
            }
            b.Append(t.Text);
            if (t.Text == "(") paren++;
            else if (t.Text == ")") paren--;
            else if (t.Text == "{") indent++;
            prev = t;
        }
        return b.ToString();
    }

    // ---- output ----

    /// <summary>Puts both stages together into one Godot shader.</summary>
    sealed class Emitter(Unit vs, Unit fs, ShaderOptions options, ShaderTranslation result)
    {
        readonly Dictionary<string, (string Type, string Array, string Interpolation)> _varyings = new(StringComparer.Ordinal);
        readonly Dictionary<string, Variable> _uniforms = new(StringComparer.Ordinal);
        /// <summary>Varyings the fragment stage reads, and uniforms the code uses (only these are declared).</summary>
        readonly HashSet<string> _used = new(StringComparer.Ordinal);
        readonly HashSet<string> _usedUniforms = new(StringComparer.Ordinal);
        /// <summary>Every name at the top level of the output, which local names must not reuse.</summary>
        readonly HashSet<string> _topLevel = new(StringComparer.Ordinal);

        public string Emit()
        {
            result.Lit = fs.Switch("LIGHTING") == 1;
            result.Fog = fs.Switch("FOG") == 1 && !options.FogDisabled;
            result.Keyholing = fs.Switch("KEYHOLING") == 1;
            foreach (var u in vs.Uniforms.Concat(fs.Uniforms)) AddUniform(u);
            foreach (var unit in new[] { vs, fs })
                foreach (var v in unit.State.Where(s => s.Storage == "varying"))
                {
                    if (_varyings.TryGetValue(v.Name, out var had))
                    {
                        if (had.Type != v.Type) throw new TranslationException($"varying {v.Name} is {had.Type} in one stage and {v.Type} in the other");
                        continue;
                    }
                    bool integer = v.Type.StartsWith('i') || v.Type.StartsWith('u') || v.Type == "int";
                    _varyings[v.Name] = (v.Type, v.Array, integer ? "flat" : v.Interpolation);
                }
            foreach (var u in _uniforms.Keys) _topLevel.Add(u);
            // The fragment stage first: the vertex stage only passes on the varyings it reads.
            var fsStage = new Stage(this, fs, options, result);
            var fsCode = fsStage.Build();
            foreach (var name in _varyings.Keys.Where(fsStage.HasField).ToList()) _used.Add(name);
            var vsCode = new Stage(this, vs, options, result).Build();

            var b = new StringBuilder();
            b.Append("// Translated by OpenWinter from ").Append(vs.Name).Append(" and ").Append(fs.Name).Append(".\n");
            b.Append("shader_type spatial;\n");
            var modes = new List<string> { "skip_vertex_transform" };
            modes.Add(options.Additive ? "blend_add" : "blend_mix");
            modes.Add(options.DepthDrawNever ? "depth_draw_never" : options.DepthDrawAlways ? "depth_draw_always" : "depth_draw_opaque");
            modes.Add(options.CullDisabled ? "cull_disabled" : "cull_back");
            // Highlights only where the module's own code sets the surface's material values (wet surfaces);
            // otherwise the surface is lit as OpenWinter lights plain meshes, without highlights.
            if (result.Lit) { modes.Add("diffuse_lambert"); modes.Add(fs.SetsMaterial ? "specular_schlick_ggx" : "specular_disabled"); }
            else modes.Add("unshaded");
            if (!result.Fog) modes.Add("fog_disabled");
            b.Append("render_mode ").Append(string.Join(", ", modes)).Append(";\n\n");
            b.Append("uniform float ow_ok;\n");
            b.Append("uniform vec3 ow_self_illumination = vec3(0.0);\n");
            var structs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in vs.Structs.Concat(fs.Structs))
                if (structs.Add(s)) b.Append(s).Append('\n');
            foreach (var unit in new[] { vs, fs })
                foreach (var c in unit.Consts)
                    b.Append("const ").Append(c.Type).Append(' ').Append(unit.Prefix).Append(c.Name).Append(ConstArray(unit, c.Array))
                        .Append(c.Init != null ? " = " + Join(RenameConsts(unit, c.Init)) : "").Append(";\n");
            foreach (var u in _uniforms.Values.Where(u => _usedUniforms.Contains(u.Name)))
            {
                var engine = EngineNames.Find(u.Name);
                if (engine?.Source == EngineSource.Global)
                {
                    b.Append("global uniform ").Append(engine.Type).Append(' ').Append(u.Name).Append(";\n");
                    result.Globals.Add(u.Name);
                    continue;
                }
                string type = engine?.Type ?? u.Type;
                string array = u.Array.Length > 0 ? ConstArray(vs.Uniforms.Contains(u) ? vs : fs, u.Array) : "";
                b.Append("uniform ").Append(type).Append(' ').Append(UniformName(u.Name)).Append(array);
                if (engine?.Hint is { } hint) b.Append(" : ").Append(hint);
                else if (type is "sampler2D") b.Append(" : filter_linear_mipmap, repeat_enable");
                if (engine?.Default is { } def && array.Length == 0) b.Append(" = ").Append(def);
                b.Append(";\n");
                int size = array.Length > 2 && int.TryParse(array[1..^1], out var n) ? n : 0;
                result.Parameters.Add(new ShaderParameter(UniformName(u.Name), type, size, engine != null));
            }
            foreach (var (name, (type, array, interpolation)) in _varyings.Where(v => _used.Contains(v.Key)))
                b.Append("varying ").Append(interpolation.Length > 0 ? interpolation + " " : "").Append(type).Append(" ow_v_").Append(name).Append(array).Append(";\n");
            if (options.Declarations.Length > 0) b.Append('\n').Append(options.Declarations).Append('\n');
            b.Append('\n').Append(vsCode).Append('\n').Append(fsCode);
            return b.ToString();
        }

        void AddUniform(Variable u)
        {
            if (EngineNames.Find(u.Name) is { Source: EngineSource.Builtin }) return;
            if (_uniforms.TryGetValue(u.Name, out var had))
            {
                var engine = EngineNames.Find(u.Name);
                if (engine == null && had.Type != u.Type) throw new TranslationException($"uniform {u.Name} is {had.Type} in one stage and {u.Type} in the other");
                return;
            }
            _uniforms[u.Name] = u;
        }

        public bool UseUniform(string name)
        {
            if (!_uniforms.ContainsKey(name)) return false;
            _usedUniforms.Add(name);
            return true;
        }
        public bool IsTopLevel(string name) => _topLevel.Contains(name);
        public IReadOnlyDictionary<string, (string Type, string Array, string Interpolation)> Varyings => _varyings;
        /// <summary>The varyings passed from the vertex to the fragment stage.</summary>
        public IEnumerable<KeyValuePair<string, (string Type, string Array, string Interpolation)>> Passed => _varyings.Where(v => _used.Contains(v.Key));

        public static string UniformName(string name) => ShaderTranslator.UniformName(name);

        /// <summary>An array size that names a constant is given the constant's prefixed name.</summary>
        static string ConstArray(Unit unit, string array) =>
            array.Length == 0 ? "" : Join(RenameConsts(unit, Tokens(array)));

        static List<Tok> RenameConsts(Unit unit, List<Tok> tokens) =>
            tokens.Select(t => t.Kind == Kind.Ident && unit.Consts.Any(c => c.Name == t.Text) ? t with { Text = unit.Prefix + t.Text } : t).ToList();
    }

    /// <summary>One stage's state struct, functions and Godot stage function.</summary>
    sealed class Stage(Emitter emitter, Unit unit, ShaderOptions options, ShaderTranslation result)
    {
        string StateType => unit.Stage == 1 ? "OW_VS" : "OW_FS";
        readonly Dictionary<string, (string Type, string Array)> _fields = new(StringComparer.Ordinal);
        readonly Dictionary<string, Variable> _state = new(StringComparer.Ordinal);
        readonly HashSet<string> _consts = new(StringComparer.Ordinal);
        readonly HashSet<string> _builtinUniforms = new(StringComparer.Ordinal);

        public string Build()
        {
            foreach (var v in unit.State) _state[v.Name] = v;
            foreach (var c in unit.Consts) _consts.Add(c.Name);
            foreach (var u in unit.Uniforms)
                if (EngineNames.Find(u.Name) is { Source: EngineSource.Builtin }) _builtinUniforms.Add(u.Name);

            var functions = new StringBuilder();
            foreach (var f in Ordered())
            {
                var scope = new Scopes();
                scope.Push();
                var ps = new List<string> { $"inout {StateType} ow" };
                foreach (var p in f.Params)
                {
                    string local = scope.Declare(p.Name, this);
                    ps.Add($"{(p.Qualifier.Length > 0 ? p.Qualifier + " " : "")}{p.Type} {local}{p.Array}");
                }
                string ret = f.ReturnType.Length > 0 ? f.ReturnType : "void";
                functions.Append(ret).Append(' ').Append(unit.Prefix).Append(f.Name).Append('(').Append(string.Join(", ", ps)).Append(")\n{\n");
                functions.Append(Body(Rewrite(f.Body, scope))).Append("\n}\n\n");
            }
            var inits = new StringBuilder();
            foreach (var v in unit.State.Where(v => v.Init != null && v.Storage == "global"))
            {
                var scope = new Scopes();
                scope.Push();
                inits.Append("\tow.").Append(v.Name).Append(" = ").Append(Join(Rewrite(v.Init!, scope))).Append(";\n");
                Field(v.Name, v.Type, v.Array);
            }

            var stage = new StringBuilder();
            stage.Append(unit.Stage == 1 ? "void vertex()\n{\n" : "void fragment()\n{\n");
            stage.Append('\t').Append(StateType).Append(" ow;\n");
            foreach (var (name, _) in _fields.ToList())
            {
                if (EngineNames.Find(name) is { Source: EngineSource.Builtin } e)
                {
                    string? expr = unit.Stage == 1 ? e.Vertex : e.Fragment;
                    if (expr != null) stage.Append("\tow.").Append(name).Append(" = ").Append(expr).Append(";\n");
                    else result.Notes.Add($"{unit.Name}: {name} is not available in this stage");
                }
            }
            if (unit.Stage == 1)
            {
                // What the standard transform would give, for shaders that set only some of it.
                if (_fields.ContainsKey("vPosView")) stage.Append("\tow.vPosView = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;\n");
                if (_fields.ContainsKey("vVertexNormal")) stage.Append("\tow.vVertexNormal = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);\n");
                if (_fields.ContainsKey("vVertexTexCoords")) stage.Append("\tow.vVertexTexCoords = UV;\n");
            }
            else
            {
                foreach (var (name, _) in emitter.Varyings)
                    if (_fields.ContainsKey(name)) stage.Append("\tow.").Append(name).Append(" = ow_v_").Append(name).Append(";\n");
                if (_fields.ContainsKey("gl_FragColor")) stage.Append("\tow.gl_FragColor = vec4(1.0);\n");
            }
            stage.Append(inits);
            stage.Append('\t').Append(unit.Prefix).Append("main(ow);\n");
            if (unit.Stage == 1)
            {
                stage.Append(_fields.ContainsKey("vPosView") ? "\tVERTEX = ow.vPosView;\n" : "\tVERTEX = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;\n");
                stage.Append(_fields.ContainsKey("vVertexNormal") ? "\tNORMAL = normalize(ow.vVertexNormal);\n" : "\tNORMAL = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);\n");
                foreach (var (name, (type, array, _)) in emitter.Passed)
                {
                    if (_fields.ContainsKey(name)) stage.Append("\tow_v_").Append(name).Append(" = ow.").Append(name).Append(";\n");
                    else if (array.Length == 0 && Zero(type) is { } zero) stage.Append("\tow_v_").Append(name).Append(" = ").Append(zero).Append(";\n");
                }
            }
            else
            {
                stage.Append(_fields.ContainsKey("gl_FragColor") ? "\tvec4 ow_color = ow.gl_FragColor;\n" : "\tvec4 ow_color = vec4(1.0);\n");
                // The shader's colour is gamma-encoded; the renderer shades linear colour.
                stage.Append("\tvec3 ow_srgb = max(ow_color.rgb, vec3(0.0));\n");
                stage.Append("\tALBEDO = mix(pow((ow_srgb + vec3(0.055)) / vec3(1.055), vec3(2.4)), ow_srgb / vec3(12.92), lessThan(ow_srgb, vec3(0.04045)));\n");
                if (result.Lit)
                {
                    if (_fields.ContainsKey("vFragmentNormal")) stage.Append("\tNORMAL = normalize(ow.vFragmentNormal);\n");
                    if (_fields.ContainsKey("fRoughness")) stage.Append("\tROUGHNESS = clamp(ow.fRoughness, 0.0, 1.0);\n");
                    // The surface's reflectance at normal incidence: Godot's SPECULAR 0.5 is 4%.
                    if (unit.SetsMaterial && _fields.ContainsKey("fSpecularity")) stage.Append("\tSPECULAR = sqrt(clamp(ow.fSpecularity / 0.16, 0.0, 1.0));\n");
                    if (_fields.ContainsKey("fMetallicness")) stage.Append("\tMETALLIC = clamp(ow.fMetallicness, 0.0, 1.0);\n");
                    stage.Append("\tEMISSION = ALBEDO * ow_self_illumination;\n");
                }
                if (options.Transparent || options.Additive) stage.Append("\tALPHA = clamp(ow_color.a, 0.0, 1.0);\n");
                if (options.FragmentEnd.Length > 0) stage.Append(options.FragmentEnd).Append('\n');
            }
            stage.Append("}\n");

            var b = new StringBuilder();
            b.Append("struct ").Append(StateType).Append(" {\n");
            if (_fields.Count == 0) b.Append("\tfloat ow_unused;\n");
            foreach (var (name, (type, array)) in _fields) b.Append('\t').Append(type).Append(' ').Append(name).Append(array).Append(";\n");
            b.Append("};\n\n");
            b.Append(functions).Append(stage);
            return b.ToString();
        }

        static string? Zero(string type) => type switch
        {
            "float" => "0.0",
            "int" => "0",
            "bool" => "false",
            "ivec2" or "ivec3" or "ivec4" => type + "(0)",
            "vec2" or "vec3" or "vec4" or "mat2" or "mat3" or "mat4" => type + "(0.0)",
            _ => null,
        };

        void Field(string name, string type, string array) => _fields.TryAdd(name, (type, array));

        public bool HasField(string name) => _fields.ContainsKey(name);

        /// <summary>A local of this name would clash with a top-level name of the output.</summary>
        public bool Collides(string name) => emitter.IsTopLevel(name) || unit.FunctionNames.Contains(name);

        /// <summary>Functions in an order where each comes after those it calls (Godot needs no prototypes then).</summary>
        IEnumerable<Function> Ordered()
        {
            var byName = unit.Functions.GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.ToList());
            var done = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var order = new List<Function>();
            void Visit(string name)
            {
                if (done.Contains(name) || !visiting.Add(name)) return;
                foreach (var f in byName[name])
                    for (int i = 0; i + 1 < f.Body.Count; i++)
                        if (f.Body[i].Kind == Kind.Ident && f.Body[i + 1].Text == "(" && byName.ContainsKey(f.Body[i].Text) && (i == 0 || f.Body[i - 1].Text != "."))
                            Visit(f.Body[i].Text);
                visiting.Remove(name);
                done.Add(name);
                order.AddRange(byName[name]);
            }
            // Only what main() and the global initializers reach is needed.
            foreach (var v in unit.State.Where(v => v.Init != null && v.Storage == "global"))
                for (int i = 0; i + 1 < v.Init!.Count; i++)
                    if (v.Init[i].Kind == Kind.Ident && v.Init[i + 1].Text == "(" && byName.ContainsKey(v.Init[i].Text)) Visit(v.Init[i].Text);
            Visit("main");
            return order;
        }

        sealed class Scopes
        {
            readonly List<Dictionary<string, string>> _stack = new();
            public void Push() => _stack.Add(new(StringComparer.Ordinal));
            public void Pop() { if (_stack.Count > 1) _stack.RemoveAt(_stack.Count - 1); }
            public string? Find(string name)
            {
                for (int i = _stack.Count - 1; i >= 0; i--)
                    if (_stack[i].TryGetValue(name, out var local)) return local;
                return null;
            }
            public string Declare(string name, Stage stage)
            {
                string local = Reserved.Contains(name) || stage.Collides(name) || name.StartsWith("ow_", StringComparison.Ordinal)
                    ? "ow_l_" + name : name;
                _stack[^1][name] = local;
                return local;
            }
        }

        bool IsType(string name) => BuiltinTypes.Contains(name) || unit.StructNames.Contains(name);

        /// <summary>A body (or initializer) with globals turned into state fields and calls given the state.</summary>
        List<Tok> Rewrite(List<Tok> body, Scopes scope)
        {
            var output = new List<Tok>(body.Count + 16);
            int paren = 0;
            int declarationDepth = -1;
            bool declareNext = false;
            for (int i = 0; i < body.Count; i++)
            {
                var t = body[i];
                string? next = i + 1 < body.Count ? body[i + 1].Text : null;
                string? prev = i > 0 ? body[i - 1].Text : null;
                switch (t.Text)
                {
                    case "{": scope.Push(); output.Add(t); continue;
                    case "}": scope.Pop(); output.Add(t); continue;
                    case "(": paren++; output.Add(t); continue;
                    case ")":
                        paren--;
                        if (declarationDepth > paren) declarationDepth = -1;
                        output.Add(t);
                        continue;
                    case ";": declarationDepth = -1; declareNext = false; output.Add(t); continue;
                    case ",":
                        if (declarationDepth == paren) declareNext = true;
                        output.Add(t);
                        continue;
                }
                if (t.Kind != Kind.Ident) { output.Add(t); continue; }
                if (prev == ".") { output.Add(t); continue; }
                if (IsType(t.Text) && next != null && i + 1 < body.Count && body[i + 1].Kind == Kind.Ident && !Precision.Contains(next))
                {
                    output.Add(t);
                    declareNext = true;
                    declarationDepth = paren;
                    continue;
                }
                if (Precision.Contains(t.Text) || t.Text == "const") { if (t.Text == "const") output.Add(t); continue; }
                if (declareNext)
                {
                    declareNext = false;
                    output.Add(t with { Text = scope.Declare(t.Text, this) });
                    continue;
                }
                if (scope.Find(t.Text) is { } local) { output.Add(t with { Text = local }); continue; }
                if (next == "(" && unit.FunctionNames.Contains(t.Text))
                {
                    output.Add(t with { Text = unit.Prefix + t.Text });
                    output.Add(new Tok("(", Kind.Punct));
                    output.Add(new Tok("ow", Kind.Ident));
                    if (i + 2 < body.Count && body[i + 2].Text != ")") output.Add(new Tok(",", Kind.Punct));
                    paren++;
                    i++;
                    continue;
                }
                if (StateName(t.Text) is { } field)
                {
                    output.Add(new Tok("ow", Kind.Ident));
                    output.Add(new Tok(".", Kind.Punct));
                    output.Add(t with { Text = field });
                    continue;
                }
                if (t.Text == "gl_FragData" && next == "[")
                {
                    int close = i + 1;
                    while (close < body.Count && body[close].Text != "]") close++;
                    Field("gl_FragColor", "vec4", "");
                    output.Add(new Tok("ow", Kind.Ident));
                    output.Add(new Tok(".", Kind.Punct));
                    output.Add(new Tok("gl_FragColor", Kind.Ident));
                    i = close;
                    continue;
                }
                if (_consts.Contains(t.Text)) { output.Add(t with { Text = unit.Prefix + t.Text }); continue; }
                if (emitter.UseUniform(t.Text)) { output.Add(t with { Text = Emitter.UniformName(t.Text) }); continue; }
                if (BuiltinRenames.TryGetValue(t.Text, out var renamed)) { output.Add(t with { Text = renamed }); continue; }
                if (t.Text.StartsWith("gl_", StringComparison.Ordinal))
                    throw new TranslationException($"{unit.Name}: {t.Text} is not supported");
                output.Add(t);
            }
            return output;
        }

        /// <summary>The state field a global name lives in, or null when it isn't one (adding the field on first use).</summary>
        string? StateName(string name)
        {
            if (_fields.ContainsKey(name)) return name;
            if (_state.TryGetValue(name, out var v))
            {
                if (v.Storage == "attribute" && EngineNames.Find(name) is not { Source: EngineSource.Builtin })
                    result.Notes.Add($"{unit.Name}: attribute {name} is not provided (zero)");
                Field(name, v.Type, v.Array);
                return name;
            }
            if (_builtinUniforms.Contains(name))
            {
                Field(name, EngineNames.Find(name)!.Type, "");
                return name;
            }
            if (unit.FragmentOutputs.Contains(name) || name == "gl_FragColor")
            {
                Field("gl_FragColor", "vec4", "");
                return "gl_FragColor";
            }
            if (GlVariables.TryGetValue(name, out var type))
            {
                Field(name, type, "");
                return name;
            }
            return null;
        }
    }
}

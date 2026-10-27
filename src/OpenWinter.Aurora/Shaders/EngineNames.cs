namespace OpenWinter.Aurora.Shaders;

/// <summary>Where a name a shader reads from the engine comes from in OpenWinter's renderer.</summary>
public enum EngineSource
{
    /// <summary>A global shader parameter the client keeps up to date (world time, player position, script uniforms).</summary>
    Global,
    /// <summary>A per-material parameter the client sets when it builds the material (textures, material colours).</summary>
    Material,
    /// <summary>Read from the renderer's built-in values when the stage starts (vertex attributes, matrices).</summary>
    Builtin,
}

public sealed record EngineName(string Name, EngineSource Source, string Type, string? Default = null, string? Hint = null,
    string? Vertex = null, string? Fragment = null, int ArraySize = 0);

/// <summary>
/// The engine-provided names module shaders use, and how OpenWinter provides each. Positions given to shaders are in
/// Aurora coordinates (Z up): the client's world is turned into Godot's Y-up space by a fixed rotation, which the
/// view matrices given to shaders undo.
/// </summary>
public static class EngineNames
{
    /// <summary>Aurora world space to Godot world space (x, y, z) to (x, z, -y), and back.</summary>
    public const string AuroraToGodot = "mat4(vec4(1.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, -1.0, 0.0), vec4(0.0, 1.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0))";
    public const string GodotToAurora = "mat4(vec4(1.0, 0.0, 0.0, 0.0), vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, -1.0, 0.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0))";

    public static readonly IReadOnlyDictionary<string, EngineName> All = Build();

    /// <summary>The global parameters every translated shader may use; the client registers them all at start-up.</summary>
    public static IEnumerable<EngineName> Globals => All.Values.Where(n => n.Source == EngineSource.Global);

    public static EngineName? Find(string name) => All.TryGetValue(name, out var n) ? n : null;

    /// <summary>The script uniform for SetShaderUniform* bank <paramref name="type"/> ("float", "int", "vec4") and index 0-15.</summary>
    public static string? ScriptUniform(string type, int index) => index is < 0 or > 15 ? null : type switch
    {
        "float" => $"scriptableFloat{index + 1}",
        "int" => $"scriptableInt{index + 1}",
        "vec4" or "vec" => $"scriptableVec{index + 1}",
        _ => null,
    };

    static Dictionary<string, EngineName> Build()
    {
        var d = new Dictionary<string, EngineName>(StringComparer.Ordinal);
        void G(string type, params string[] names)
        {
            foreach (var n in names) d[n] = new EngineName(n, EngineSource.Global, type);
        }
        void M(string type, string? def, string? hint, params string[] names)
        {
            foreach (var n in names) d[n] = new EngineName(n, EngineSource.Material, type, def, hint);
        }
        void B(string name, string type, string? vertex, string? fragment) => d[name] = new EngineName(name, EngineSource.Builtin, type, Vertex: vertex, Fragment: fragment);

        for (int i = 1; i <= 16; i++)
        {
            G("float", $"scriptableFloat{i}");
            G("int", $"scriptableInt{i}");
            G("vec4", $"scriptableVec{i}");
        }
        G("int", "sceneCurrentFrame", "worldtimerTimeOfDay", "moduleYear", "moduleMonth", "moduleDay", "moduleHour", "moduleDawnHour",
            "moduleDuskHour", "moduleMinutesPerHour", "playerInCutscene", "cameraDialogState", "windPointSourcesCount", "areaFlags",
            "areaWeatherType", "targetObjectType", "targetShape", "targetShapeFeatId", "targetShapeSpellId", "targetShapeHarmsAllies",
            "targetShapeHarmsEnemies", "targetShapeHelpsAllies", "targetShapeIgnoresSelf", "targetShapeOriginOnSelf");
        G("float", "playerCameraDist", "cameraPitch", "cameraYaw", "cameraViewAngle", "areaWeatherDensity", "keyholeDiameterMin",
            "keyholeDiameterMax", "keyholeMinZOffset", "targetShapeSizeX", "targetShapeSizeY", "targetShapeSpellRange");
        G("vec3", "playerPosition", "cameraPosition", "cameraFocus", "areaGlobalWind", "lightAreaAmbient", "lightAreaDiffuse",
            "lightAreaDiffuseDirection", "clipParams", "targetWorldCoords");
        G("vec4", "fogColor", "fogParams", "targetShapeColour");
        G("ivec2", "userinputMousePosition");

        const string colour = "hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable";
        for (int i = 0; i <= 5; i++)
        {
            M("sampler2D", null, colour, $"texUnit{i}");
            M("int", "0", null, $"texture{i}Bound");
        }
        M("sampler2D", null, "hint_default_black, filter_linear_mipmap, repeat_enable", "texUnitEnv");
        M("samplerCube", null, "hint_default_black", "texUnitEnvCube");
        M("sampler2D", null, "hint_default_black, filter_linear, repeat_enable", "texUnitNoise");
        M("int", "0", null, "texEnvBound", "texEnvCubeBound", "envMapCube", "texNoiseBound", "numLights", "staticLighting", "skinmesh",
            "subvariant", "keyholeCanDissolve");
        M("vec4", "vec4(1.0)", null, "materialFrontAmbient", "materialFrontDiffuse", "globalColor");
        M("vec4", "vec4(0.0, 0.0, 0.0, 1.0)", null, "materialFrontEmissive");
        M("vec4", "vec4(0.0)", null, "frontLightModelProductSceneColor", "CustomSpecularColor");
        M("mat4", "mat4(1.0)", null, "m_texture");
        M("float", "-1.0", null, "fAlphaDiscardValue");
        M("float", "0.0", null, "Metallicness", "Roughness", "Specularity", "DisplacementMultiplier", "DisplacementOffset",
            "lightFalloffFactor", "lightMaxIntensityInv");
        M("vec3", "vec3(0.0)", null, "bboxMin", "bboxMax");

        B("vPos", "vec4", "vec4(VERTEX, 1.0)", null);
        B("vNormal", "vec3", "NORMAL", null);
        B("vTcIn", "vec2", "UV", null);
        B("vColor", "vec4", "COLOR", null);
        B("vTangent", "vec3", "TANGENT", null);
        B("fHandedness", "float", "1.0", null);
        B("vIndex", "vec4", "vec4(0.0)", null);
        B("vWeight", "vec4", "vec4(1.0, 0.0, 0.0, 0.0)", null);
        B("vStaticLightDir", "vec3", "vec3(0.0, 0.0, 1.0)", null);
        B("m_mv", "mat4", "MODELVIEW_MATRIX", "VIEW_MATRIX * MODEL_MATRIX");
        B("m_mvp", "mat4", "PROJECTION_MATRIX * MODELVIEW_MATRIX", "PROJECTION_MATRIX * VIEW_MATRIX * MODEL_MATRIX");
        B("m_view", "mat4", "VIEW_MATRIX * " + AuroraToGodot, "VIEW_MATRIX * " + AuroraToGodot);
        B("m_view_inv", "mat4", GodotToAurora + " * INV_VIEW_MATRIX", GodotToAurora + " * INV_VIEW_MATRIX");
        B("m_proj", "mat4", "PROJECTION_MATRIX", "PROJECTION_MATRIX");
        B("m_proj_inv", "mat4", "INV_PROJECTION_MATRIX", "INV_PROJECTION_MATRIX");
        B("m_vp_inv", "mat4", GodotToAurora + " * INV_VIEW_MATRIX * INV_PROJECTION_MATRIX", GodotToAurora + " * INV_VIEW_MATRIX * INV_PROJECTION_MATRIX");
        B("ow_normal_matrix", "mat3", "MODELVIEW_NORMAL_MATRIX", "mat3(VIEW_MATRIX * MODEL_MATRIX)");
        B("screenParams", "vec2", "VIEWPORT_SIZE", "VIEWPORT_SIZE");
        B("pixelParams", "vec3", "vec3(1.0 / VIEWPORT_SIZE, VIEWPORT_SIZE.x / VIEWPORT_SIZE.y)", "vec3(1.0 / VIEWPORT_SIZE, VIEWPORT_SIZE.x / VIEWPORT_SIZE.y)");
        // GL's window origin is the bottom left corner and its depth grows away from the camera (Godot's is reversed).
        B("gl_FragCoord", "vec4", null, "vec4(FRAGCOORD.x, VIEWPORT_SIZE.y - FRAGCOORD.y, 1.0 - FRAGCOORD.z, FRAGCOORD.w)");
        B("gl_FrontFacing", "bool", null, "FRONT_FACING");
        return d;
    }
}

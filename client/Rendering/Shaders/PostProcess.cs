using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.Rendering.Shaders;

/// <summary>
/// A module's post-process pass (fsfbpostpr, see <see cref="ModuleShaders.PostProcess"/>): a full-screen canvas item
/// drawn over the finished 3D view and under the interface, reading the view's colour (the screen texture) and
/// depth (copied from the 3D render by <see cref="DepthCopy"/>). Without a module pass nothing is added and the view
/// is shown as it always is.
/// </summary>
public partial class PostProcess : CanvasLayer
{
    public required ClientSession Session;
    public required Camera3D Camera;

    ColorRect? _rect;
    ShaderMaterial? _material;
    DepthCopy? _depth;
    Texture2Drd? _depthTexture;
    bool _depthBound;

    /// <summary>Metadata on canvas layers that are part of the 3D picture (visual captures keep them).</summary>
    public const string SceneLayerMeta = "ow_scene_layer";

    public override void _Ready()
    {
        // Below the interface (layer 0 and up), above the 3D view.
        Layer = -1;
        Name = "PostProcess";
        SetMeta(SceneLayerMeta, true);
        Session.ContentChanged += Rebuild;
        Session.ModuleCleared += Clear;
    }

    public override void _ExitTree()
    {
        Session.ContentChanged -= Rebuild;
        Session.ModuleCleared -= Clear;
        Clear();
    }

    void Rebuild()
    {
        Clear();
        _material = Session.Content?.Materials.Shaders?.PostProcess();
        if (_material == null) return;
        _rect = new ColorRect { Name = "Pass", Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
        if (_material.GetMeta("ow_reads_depth", false).AsBool())
        {
            _depthTexture = new Texture2Drd();
            _depth = new DepthCopy { Target = _depthTexture };
            // Until the first copy, depth reads as the far plane.
            var far = Image.CreateEmpty(1, 1, false, Image.Format.Rf);
            far.SetPixel(0, 0, new Color(0, 0, 0));
            _material.SetShaderParameter("ow_fb_depth_tex", ImageTexture.CreateFromImage(far));
            _depthBound = false;
            Camera.Compositor = new Compositor { CompositorEffects = new Godot.Collections.Array<CompositorEffect> { _depth } };
        }
        GD.Print("[shaders] post-process pass: the module's fsfbpostpr" + (_depth != null ? " (with depth)" : ""));
    }

    void Clear()
    {
        _rect?.QueueFree();
        _rect = null;
        if (_depth != null && GodotObject.IsInstanceValid(Camera)) Camera.Compositor = null;
        _depth?.Release();
        _depth = null;
        _depthTexture = null;
        _material = null;
    }

    public override void _Process(double delta)
    {
        if (_rect == null) return;
        // Only over a loaded area (not the loading screen's empty view).
        _rect.Visible = Session.Area != null && GodotObject.IsInstanceValid(Session.Area);
        if (_depth != null && !_depthBound && _depth.Ready && _material != null)
        {
            _material.SetShaderParameter("ow_fb_depth_tex", _depthTexture);
            _depthBound = true;
        }
    }
}

/// <summary>
/// Copies the 3D view's depth (after transparent surfaces, resolved from multisampling) into a texture the
/// post-process pass reads. The copy is Godot's own reversed depth; the pass turns it into GL's.
/// </summary>
[GlobalClass]
public partial class DepthCopy : CompositorEffect
{
    public Texture2Drd? Target;
    /// <summary>The copy has been made at least once (the target texture exists).</summary>
    public bool Ready => _ready;

    volatile bool _ready, _released;
    RenderingDevice? _rd;
    Rid _shader, _pipeline, _sampler, _output;
    Vector2I _size;

    const string Code = """
        #version 450
        layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
        layout(set = 0, binding = 0) uniform sampler2D depth_in;
        layout(r32f, set = 0, binding = 1) uniform restrict writeonly image2D depth_out;
        void main() {
            ivec2 p = ivec2(gl_GlobalInvocationID.xy);
            ivec2 size = imageSize(depth_out);
            if (p.x >= size.x || p.y >= size.y) return;
            imageStore(depth_out, p, vec4(texelFetch(depth_in, p, 0).r, 0.0, 0.0, 1.0));
        }
        """;

    public DepthCopy()
    {
        EffectCallbackType = EffectCallbackTypeEnum.PostTransparent;
        AccessResolvedDepth = true;
    }

    bool Init()
    {
        if (_rd != null) return _pipeline.IsValid;
        _rd = RenderingServer.GetRenderingDevice();
        if (_rd == null) return false;
        var source = new RDShaderSource { Language = RenderingDevice.ShaderLanguage.Glsl, SourceCompute = Code };
        var spirv = _rd.ShaderCompileSpirVFromSource(source);
        if (spirv.CompileErrorCompute.Length > 0)
        {
            GD.PushWarning("[shaders] depth copy: " + spirv.CompileErrorCompute);
            return false;
        }
        _shader = _rd.ShaderCreateFromSpirV(spirv);
        _pipeline = _rd.ComputePipelineCreate(_shader);
        _sampler = _rd.SamplerCreate(new RDSamplerState { MinFilter = RenderingDevice.SamplerFilter.Nearest, MagFilter = RenderingDevice.SamplerFilter.Nearest });
        return _pipeline.IsValid;
    }

    public override void _RenderCallback(int effectCallbackType, RenderData renderData)
    {
        if (_released || Target == null || !Init() || renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD buffers) return;
        var size = buffers.GetInternalSize();
        if (size.X <= 0 || size.Y <= 0) return;
        if (size != _size || !_output.IsValid)
        {
            var old = _output;
            var format = new RDTextureFormat
            {
                Format = RenderingDevice.DataFormat.R32Sfloat, Width = (uint)size.X, Height = (uint)size.Y,
                UsageBits = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.StorageBit,
            };
            _output = _rd!.TextureCreate(format, new RDTextureView(), new Godot.Collections.Array<byte[]>());
            _size = size;
            Target.TextureRdRid = _output;
            if (old.IsValid) _rd.FreeRid(old);
        }
        var depth = buffers.GetDepthLayer(0);
        if (!depth.IsValid) return;
        var input = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = 0 };
        input.AddId(_sampler);
        input.AddId(depth);
        var output = new RDUniform { UniformType = RenderingDevice.UniformType.Image, Binding = 1 };
        output.AddId(_output);
        var set = UniformSetCacheRD.GetCache(_shader, 0, new Godot.Collections.Array<RDUniform> { input, output });
        long list = _rd!.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(list, _pipeline);
        _rd.ComputeListBindUniformSet(list, set, 0);
        _rd.ComputeListDispatch(list, (uint)((size.X + 7) / 8), (uint)((size.Y + 7) / 8), 1);
        _rd.ComputeListEnd();
        _ready = true;
    }

    /// <summary>Frees the device resources (on the render thread).</summary>
    public void Release()
    {
        _released = true;
        Enabled = false;
        var rd = _rd;
        if (rd == null) return;
        if (Target != null && _output.IsValid) Target.TextureRdRid = new Rid();
        var rids = new[] { _output, _pipeline, _sampler, _shader };
        _rd = null;
        RenderingServer.CallOnRenderThread(Callable.From(() =>
        {
            foreach (var rid in rids)
                if (rid.IsValid) rd.FreeRid(rid);
        }));
    }
}

using Godot;

namespace OpenWinter.Client.Rendering.Emitters;

/// <summary>
/// The particle simulation (a particles shader) and the particle drawing shaders (one per blend), shared by every
/// emitter: each emitter only owns a ShaderMaterial with its own parameters, and draw materials are shared by
/// emitters with the same texture, grid, blend and render mode.
/// </summary>
public static class EmitterShaders
{
    /// <summary>Process modes: how the simulation sets each particle's basis.</summary>
    public const int ProcessSize = 0, ProcessLocalPlane = 3, ProcessAlignedPlane = 4, ProcessStreak = 5, ProcessTumble = 6;
    /// <summary>Draw modes: how the quad is oriented on screen.</summary>
    public const int DrawCamera = 0, DrawUpright = 1, DrawWorldFlat = 2, DrawBasis = 3, DrawStreak = 4;

    static Shader? _process, _singleProcess;
    static readonly Dictionary<string, Shader> Draw = new();

    public static Shader Process => _process ??= new Shader { Code = ProcessCode };

    // A native Single keeps its one particle and holds the final ramp value.
    // Godot still cycles the one-slot pool; preserve its simulation state across
    // those cycles instead of recreating the particle and replaying its fade.
    public static Shader SingleProcess => _singleProcess ??= new Shader
    {
        Code = ProcessCode.Replace("render_mode disable_velocity;", "render_mode disable_velocity, keep_data;")
            .Replace("const bool single_particle = false;", "const bool single_particle = true;")
    };

    /// <summary>
    /// The draw shader for a blend: "add" (Lighten), "mix" (Normal), "cut" (Punch-Through); <paramref name="lit"/>
    /// for m_isTinted particles, which the scene's lights shade (parity/visual, scene emitcal: a white tinted
    /// particle is moonlit blue at night and full white by day, an untinted one always white).
    /// </summary>
    public static Shader DrawShader(string blend, bool lit = false, bool twoSided = true)
    {
        string key = blend + (lit ? "|lit" : "") + (twoSided ? "" : "|one-sided");
        if (Draw.TryGetValue(key, out var s)) return s;
        string modes = blend switch
        {
            "add" => "blend_add, depth_draw_never, fog_disabled",
            "cut" => "blend_mix, depth_draw_opaque",
            _ => "blend_mix, depth_draw_never",
        };
        modes += lit ? ", diffuse_lambert, specular_disabled" : ", unshaded";
        string cut = blend == "cut" ? "\tALPHA_SCISSOR_THRESHOLD = 0.5;\n" : "";
        // Tinted particles take each light's full colour, whichever way they face (a moonlit particle is as blue
        // edge-on as face-on).
        string light = lit ? "void light() {\n\tDIFFUSE_LIGHT += ATTENUATION * LIGHT_COLOR;\n}\n" : "";
        // Lighten uses the texture's authored display values. Sampling as sRGB
        // and then raising to 1/2.2 is not an inverse: it amplifies dark texels.
        s = new Shader
        {
            Code = DrawCode.Replace("/*SOURCE_COLOR*/", blend == "add" ? "" : "source_color,")
            .Replace("/*MODES*/", modes).Replace("/*CULL*/", twoSided ? "cull_disabled" : "cull_back")
            .Replace("/*CUT*/", cut).Replace("/*LIGHT*/", light)
        };
        Draw[key] = s;
        return s;
    }

    const string ProcessCode = """
shader_type particles;
render_mode disable_velocity;

const bool single_particle = false;
uniform int mode = 0;
uniform bool manual_trail = false;
uniform float life = 1.0;
uniform float velocity = 0.0;
uniform float randvel = 0.0;
uniform float spread = 0.0;
uniform vec2 area = vec2(0.0);
uniform float gravity = 0.0;
uniform vec3 down = vec3(0.0, -1.0, 0.0);
uniform float drag = 0.0;
uniform vec4 col_start = vec4(1.0);
uniform vec4 col_mid = vec4(1.0);
uniform vec4 col_end = vec4(1.0);
uniform vec2 size_start = vec2(1.0);
uniform vec2 size_mid = vec2(1.0);
uniform vec2 size_end = vec2(1.0);
uniform vec3 percent = vec3(0.0, 0.5, 1.0);
uniform bool has_mid = false;
uniform float rot_speed = 0.0;
uniform float frame_start = 0.0;
uniform float frame_end = 0.0;
uniform float fps = 0.0;
uniform bool random_frame = false;
uniform float blur = 0.2;
uniform bool bounce = false;
uniform float bounce_co = 0.0;
uniform float floor_h = -1e9;
uniform int p2p = 0;
uniform vec3 target = vec3(0.0);
uniform float bezier2 = 0.0;
uniform float bezier3 = 0.0;
uniform float grav = 0.0;

uint hash_u(uint x) {
	x ^= x >> 16u;
	x *= 2146121005u;
	x ^= x >> 15u;
	x *= 2221713035u;
	x ^= x >> 16u;
	return x;
}

float rnd(inout uint s) {
	s = hash_u(s);
	return float(s & 16777215u) / 16777215.0;
}

float ramp1(float t, float a, float m, float b) {
	if (!has_mid) return mix(a, b, clamp((t - percent.x) / max(percent.z - percent.x, 1e-4), 0.0, 1.0));
	if (t < percent.y) return mix(a, m, clamp((t - percent.x) / max(percent.y - percent.x, 1e-4), 0.0, 1.0));
	return mix(m, b, clamp((t - percent.y) / max(percent.z - percent.y, 1e-4), 0.0, 1.0));
}

vec4 ramp4(float t, vec4 a, vec4 m, vec4 b) {
	return vec4(ramp1(t, a.x, m.x, b.x), ramp1(t, a.y, m.y, b.y), ramp1(t, a.z, m.z, b.z), ramp1(t, a.w, m.w, b.w));
}

void start() {
	// CUSTOM.w is initialized once on the first birth. Only the Single shader
	// retains it on a pool restart; ordinary fountains/explosions still reset.
	if (!single_particle || CUSTOM.w <= 0.0) {
		uint s = hash_u(NUMBER * 747796405u + RANDOM_SEED);
		mat4 birth_transform = manual_trail ? TRANSFORM : EMISSION_TRANSFORM;
		float birth_age = manual_trail ? CUSTOM.y : 0.0;
		// Godot exposes AmountRatio to custom process shaders; it does not gate
		// their births automatically. Only reject new births, so changes in a
		// keyed rate leave particles already in flight alive.
		uint birth_seed = hash_u(s ^ 2246822519u);
		if (AMOUNT_RATIO < 1.0 && rnd(birth_seed) >= AMOUNT_RATIO) {
			ACTIVE = false;
		}
		if (RESTART_POSITION || manual_trail) {
			vec3 local = vec3((rnd(s) * 2.0 - 1.0) * area.x, (rnd(s) * 2.0 - 1.0) * area.y, 0.0);
			TRANSFORM = mat4(1.0);
			TRANSFORM[3] = birth_transform * vec4(local, 1.0);
			USERDATA1 = vec4(TRANSFORM[3].xyz, 0.0);
		}
		if (RESTART_VELOCITY) {
			// A cone of the spread's full angle around the emitter's Z axis, uniform over the cap.
			float c = mix(cos(min(spread, 6.2831853) * 0.5), 1.0, rnd(s));
			float sn = sqrt(max(0.0, 1.0 - c * c));
			float phi = rnd(s) * 6.2831853;
			vec3 dir = vec3(sn * cos(phi), sn * sin(phi), c);
			float speed = velocity + (rnd(s) * 2.0 - 1.0) * randvel;
			VELOCITY = normalize(mat3(birth_transform) * dir) * speed;
		}
		if (mode == 4) {
			// Native aligned cards use the emitter X/Z plane at birth. Keeping
			// those axes prevents old trail particles rotating with the emitter.
			USERDATA2 = vec4(normalize(birth_transform[0].xyz), 0.0);
			USERDATA3 = vec4(normalize(birth_transform[2].xyz), 0.0);
		}
		float frames = max(frame_end - frame_start + 1.0, 1.0);
		float f0 = random_frame ? frame_start + floor(rnd(s) * frames * 0.9999) : frame_start;
		USERDATA1.w = f0;
		CUSTOM = vec4(0.0, birth_age, f0, life > 0.0 ? life : 1e9);
		COLOR = col_start;
	}
}

void process() {
	CUSTOM.y += DELTA;
	if (!single_particle && CUSTOM.y > CUSTOM.w) {
		ACTIVE = false;
	}
	float t = clamp(CUSTOM.y / CUSTOM.w, 0.0, 1.0);
	vec3 ez = normalize(EMISSION_TRANSFORM[2].xyz);
	if (p2p == 1) {
		// Bezier from where the particle was born to the target, over its life.
		vec3 p0 = USERDATA1.xyz;
		vec3 p1 = p0 + ez * bezier2;
		vec3 p2 = target + ez * bezier3;
		float u = 1.0 - t;
		vec3 pos = u * u * u * p0 + 3.0 * u * u * t * p1 + 3.0 * u * t * t * p2 + t * t * t * target;
		VELOCITY = (pos - TRANSFORM[3].xyz) / max(DELTA, 1e-4);
		TRANSFORM[3].xyz = pos;
	} else {
		VELOCITY += down * gravity * DELTA;
		if (p2p == 2) VELOCITY += normalize(target - TRANSFORM[3].xyz + vec3(1e-5)) * grav * DELTA;
		VELOCITY /= 1.0 + drag * DELTA;
		TRANSFORM[3].xyz += VELOCITY * DELTA;
		if (bounce) {
			vec3 up = -down;
			float h = dot(TRANSFORM[3].xyz, up);
			float vn = dot(VELOCITY, up);
			if (h < floor_h && vn < 0.0) {
				TRANSFORM[3].xyz += up * (floor_h - h);
				VELOCITY -= up * vn * (1.0 + clamp(bounce_co, 0.0, 1.0));
			}
		}
	}
	vec4 c = ramp4(t, col_start, col_mid, col_end);
	COLOR = vec4(c.rgb, clamp(c.a, 0.0, 1.0));
	vec2 sz = max(vec2(ramp1(t, size_start.x, size_mid.x, size_end.x), ramp1(t, size_start.y, size_mid.y, size_end.y)), vec2(0.0));
	if (fps > 0.0) {
		float frames = max(frame_end - frame_start + 1.0, 1.0);
		// Keep the initial random frame: CUSTOM.z already contains the previous
		// displayed frame, so adding total age to it would accelerate every update.
		float first = random_frame ? USERDATA1.w : frame_start;
		CUSTOM.z = frame_start + mod(first - frame_start + floor(CUSTOM.y * fps), frames);
	}
	CUSTOM.x += rot_speed * DELTA;
	float ca = cos(CUSTOM.x), sa = sin(CUSTOM.x);
	vec3 bx = vec3(sz.x, 0.0, 0.0), by = vec3(0.0, sz.y, 0.0), bz = vec3(0.0, 0.0, 1.0);
	if (mode == 3) {
		// In the emitter's own XY plane.
		vec3 ex = normalize(EMISSION_TRANSFORM[0].xyz), ey = normalize(EMISSION_TRANSFORM[1].xyz);
		bx = (ex * ca + ey * sa) * sz.x;
		by = (ey * ca - ex * sa) * sz.y;
		bz = ez;
	} else if (mode == 4) {
		// Fixed-axis and moving-parent controls distinguish this from a
		// plane normal to velocity or a current-emitter orientation.
		vec3 rx = USERDATA2.xyz, ry = USERDATA3.xyz;
		bx = (rx * ca + ry * sa) * sz.x;
		by = (ry * ca - rx * sa) * sz.y;
		bz = normalize(cross(rx, ry));
	} else if (mode == 5) {
		// A streak along the velocity, as long as the distance covered in the blur time.
		float speed = length(VELOCITY);
		by = (speed > 1e-4 ? VELOCITY / speed : vec3(0.0, 1.0, 0.0)) * (sz.y + speed * blur);
	} else if (mode == 6) {
		uint s = hash_u(NUMBER * 2654435761u + 7u);
		vec3 axis = normalize(vec3(rnd(s), rnd(s), rnd(s)) * 2.0 - 1.0 + vec3(1e-3));
		float ang = CUSTOM.x + rnd(s) * 6.2831853;
		float cs = cos(ang), sn = sin(ang), ic = 1.0 - cs;
		mat3 r = mat3(
			vec3(cs + axis.x * axis.x * ic, axis.y * axis.x * ic + axis.z * sn, axis.z * axis.x * ic - axis.y * sn),
			vec3(axis.x * axis.y * ic - axis.z * sn, cs + axis.y * axis.y * ic, axis.z * axis.y * ic + axis.x * sn),
			vec3(axis.x * axis.z * ic + axis.y * sn, axis.y * axis.z * ic - axis.x * sn, cs + axis.z * axis.z * ic));
		bx = r[0] * sz.x;
		by = r[1] * sz.x;
		bz = r[2] * sz.x;
	}
	TRANSFORM[0] = vec4(bx, 0.0);
	TRANSFORM[1] = vec4(by, 0.0);
	TRANSFORM[2] = vec4(bz, 0.0);
}
""";

    const string DrawCode = """
shader_type spatial;
render_mode /*CULL*/, skip_vertex_transform, /*MODES*/;

uniform sampler2D tex : /*SOURCE_COLOR*/ filter_linear_mipmap, repeat_disable;
uniform bool has_tex = true;
uniform vec2 grid = vec2(1.0);
uniform int mode = 0;
uniform bool flip_v = false;
uniform float tex_gamma = 1.0;
uniform float pull = 1.0;
uniform float alpha_gamma = 1.0;

void vertex() {
	vec3 center = MODEL_MATRIX[3].xyz;
	vec3 ax = MODEL_MATRIX[0].xyz;
	vec3 ay = MODEL_MATRIX[1].xyz;
	float sx = length(ax), sy = length(ay);
	float ca = cos(INSTANCE_CUSTOM.x), sa = sin(INSTANCE_CUSTOM.x);
	vec2 v = vec2(VERTEX.x * sx, VERTEX.y * sy);
	vec3 world;
	if (mode == 0) {
		v = vec2(v.x * ca - v.y * sa, v.x * sa + v.y * ca);
		// Camera-facing particles are drawn half their size nearer the camera, so a flame shows over the rim of
		// the brazier it burns in rather than being cut by it (as the original draws them).
		vec3 to_cam = CAMERA_POSITION_WORLD - center;
		float dist = length(to_cam);
		center += to_cam / max(dist, 1e-4) * min(0.5 * min(sx, sy), dist * 0.5) * pull;
		world = center + INV_VIEW_MATRIX[0].xyz * v.x + INV_VIEW_MATRIX[1].xyz * v.y;
	} else if (mode == 1 || mode == 4) {
		vec3 up = mode == 1 ? vec3(0.0, 1.0, 0.0) : (sy > 1e-6 ? ay / sy : vec3(0.0, 1.0, 0.0));
		vec3 right = cross(up, CAMERA_POSITION_WORLD - center);
		right = length(right) > 1e-5 ? normalize(right) : INV_VIEW_MATRIX[0].xyz;
		world = center + right * v.x + up * v.y;
	} else if (mode == 2) {
		// Flat on the ground: the quad's up is world north (Godot -Z), its normal world up.
		v = vec2(v.x * ca - v.y * sa, v.x * sa + v.y * ca);
		world = center + vec3(v.x, 0.0, -v.y);
	} else {
		world = center + ax * VERTEX.x + ay * VERTEX.y;
	}
	VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
	// Lit (tinted) particles face the camera, or the sky when they lie flat.
	NORMAL = mode == 2 ? (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz : normalize((VIEW_MATRIX * vec4(CAMERA_POSITION_WORLD - center, 0.0)).xyz);
	float frame = INSTANCE_CUSTOM.z;
	vec2 cell = vec2(mod(frame, grid.x), mod(floor(frame / grid.x), grid.y));
	UV = (cell + UV) / grid;
	if (flip_v) UV.y = 1.0 - UV.y;
}

void fragment() {
	vec4 t = has_tex ? texture(tex, UV) : vec4(1.0);
	ALBEDO = pow(t.rgb, vec3(tex_gamma)) * COLOR.rgb;
	ALPHA = pow(t.a * COLOR.a, alpha_gamma);
/*CUT*/}
/*LIGHT*/
""";
}

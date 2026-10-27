# Compiles translated shaders with Godot (used by `openwinter-tools shader-check --compile`).
# check.json lists the engine's global shader parameters and the shaders to compile; each result is logged as
# OWBEGIN|name ... OWEND|name|ok (or |failed), with Godot's own error lines in between.
extends SceneTree

const TYPES = {
	"float": RenderingServer.GLOBAL_VAR_TYPE_FLOAT,
	"int": RenderingServer.GLOBAL_VAR_TYPE_INT,
	"vec2": RenderingServer.GLOBAL_VAR_TYPE_VEC2,
	"vec3": RenderingServer.GLOBAL_VAR_TYPE_VEC3,
	"vec4": RenderingServer.GLOBAL_VAR_TYPE_VEC4,
	"ivec2": RenderingServer.GLOBAL_VAR_TYPE_IVEC2,
	"mat4": RenderingServer.GLOBAL_VAR_TYPE_MAT4,
}

const DEFAULTS = {
	"float": 0.0,
	"int": 0,
	"vec2": Vector2.ZERO,
	"vec3": Vector3.ZERO,
	"vec4": Vector4.ZERO,
	"ivec2": Vector2i.ZERO,
	"mat4": Projection.IDENTITY,
}

func _initialize():
	var check = JSON.parse_string(FileAccess.get_file_as_string("res://check.json"))
	for g in check.globals:
		RenderingServer.global_shader_parameter_add(g.name, TYPES[g.type], DEFAULTS[g.type])
	for name in check.shaders:
		print("OWBEGIN|" + name)
		var shader = Shader.new()
		shader.code = FileAccess.get_file_as_string("res://" + name + ".gdshader")
		var ok = false
		for u in shader.get_shader_uniform_list():
			if u.name == "ow_ok":
				ok = true
		print("OWEND|" + name + "|" + ("ok" if ok else "failed"))
	quit()

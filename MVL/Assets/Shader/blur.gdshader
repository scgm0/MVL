shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap_anisotropic;

uniform float lod: hint_range(0.0, 10) = 0.0;
uniform float blend: hint_range(0.0, 1.0) = 0.5;

void fragment() {
	vec4 color = texture(screen_texture, SCREEN_UV, lod);
	COLOR.rgb = mix(COLOR.rgb, color.rgb, blend);
	COLOR.a *= step(0.0, lod);
}
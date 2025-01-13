shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

uniform float lod: hint_range(0.0, 5) = 0.0;

void fragment(){
	vec4 color = texture(screen_texture, SCREEN_UV, lod);
	COLOR.rgb = color.rgb * COLOR.a;
	COLOR.a = 1.0;
}
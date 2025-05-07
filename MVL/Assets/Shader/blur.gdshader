shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

uniform float lod: hint_range(0.0, 10) = 0.0;
uniform float blend: hint_range(0.0, 1.0) = 0.5;

const int dither_matrix[64] = int[](
	 0, 32,  8, 40,  2, 34, 10, 42,
	48, 16, 56, 24, 50, 18, 58, 26,
	12, 44,  4, 36, 14, 46,  6, 38,
	60, 28, 52, 20, 62, 30, 54, 22,
	 3, 35, 11, 43,  1, 33,  9, 41,
	51, 19, 59, 27, 49, 17, 57, 25,
	15, 47,  7, 39, 13, 45,  5, 37,
	63, 31, 55, 23, 61, 29, 53, 21
);

const float weights[9] = float[](
	0.0625, 0.125,  0.0625,
	0.125,  0.25,   0.125,
	0.0625, 0.125,  0.0625
);

const vec2 offsets[9] = vec2[](
	vec2(-1.0, -1.0), vec2(0.0, -1.0), vec2(1.0, -1.0),
	vec2(-1.0,  0.0), vec2(0.0,  0.0), vec2(1.0,  0.0),
	vec2(-1.0,  1.0), vec2(0.0,  1.0), vec2(1.0,  1.0)
);

float dither(ivec2 pos) {
	int index = (pos.x & 7) + ((pos.y & 7) << 3);
	return float(dither_matrix[index]) / 64.0 - 0.5;
}

vec4 gaussian_mip_blur(sampler2D tex, vec2 uv, vec2 pixel_size, float target_mip_level, float blur_spread_factor) {
	vec4 sum = vec4(0.0);
	for (int i = 0; i < 9; i++) {
		vec2 sample_uv = uv + offsets[i] * pixel_size * blur_spread_factor;
		sum += textureLod(tex, sample_uv, target_mip_level) * weights[i];
	}
	return sum;
}


void fragment() {
	if (lod > 0.0001) {
		float actual_mip_level = lod * 0.75;
		float gaussian_kernel_spread = lod / 0.75;

		vec4 blurred_color = gaussian_mip_blur(screen_texture, SCREEN_UV, SCREEN_PIXEL_SIZE, actual_mip_level, gaussian_kernel_spread);

		vec3 blended_color_rgb = mix(COLOR.rgb, blurred_color.rgb, blend);

		ivec2 pos = ivec2(FRAGCOORD.xy);
		float dither_amount = dither(pos) * 0.00425 * lod;
		blended_color_rgb += dither_amount;
		
		COLOR.rgb = blended_color_rgb;
	} else {
		COLOR = texture(screen_texture, SCREEN_UV);
	}
}
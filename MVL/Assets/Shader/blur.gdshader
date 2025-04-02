shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;

uniform float lod: hint_range(0.0, 10) = 0.0;
uniform float blend: hint_range(0.0, 1.0) = 0.5;

// 8x8 Bayer矩阵，用于有序抖动
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

float dither(ivec2 pos) {
	int index = (pos.x & 7) + ((pos.y & 7) << 3);
	return float(dither_matrix[index]) / 64.0 - 0.5; // 将值映射到-0.5到0.5之间
}

vec4 blurMipmap(sampler2D tex, vec2 uv, vec2 size, float angleOffset) {
	const int samples = 8;
	vec4 sum = vec4(0.0);
	highp float radius = lod;
#if CURRENT_RENDERER == RENDERER_FORWARD_PLUS
	radius *= 0.75;
#endif
	for (int i = 0; i < samples; i++) {
		float angle = (float(i) / float(samples)) * 6.2832 + angleOffset;
		vec2 offset = vec2(cos(angle), sin(angle)) * radius * size;
		sum += textureLod(tex, uv + offset, radius);
	}
	return sum / float(samples);
}

void fragment() {
	if (lod > 0.0) {
		vec4 color = blurMipmap(screen_texture, SCREEN_UV, SCREEN_PIXEL_SIZE, 0.0);
		color += blurMipmap(screen_texture, SCREEN_UV, SCREEN_PIXEL_SIZE, 1.5708);
		color *= 0.5;
		vec3 blended_color = mix(COLOR.rgb, color.rgb, blend);

		// 计算当前像素位置并应用抖动
		ivec2 pos = ivec2(FRAGCOORD.xy);
		float dither_amount = dither(pos) * 0.00425 * lod; // 抖动强度随LOD增加
		blended_color += dither_amount;

		COLOR.rgb = blended_color;
		COLOR.a *= step(0.0, lod);
	} else {
		COLOR = texture(screen_texture, SCREEN_UV);
	}
}
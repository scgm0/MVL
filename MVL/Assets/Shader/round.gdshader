shader_type canvas_item;

uniform vec2 window_size = vec2(1162, 658);
uniform float edge_softness = 1.5; // 边缘柔化
uniform vec4 corner_radius = vec4(12.0, 12.0, 12.0, 12.0); // 圆角半径

// 边框设置
uniform float border_thickness = 1.5; // 边框厚度
uniform float border_softness = 2.0;  // 边框柔化

// 阴影设置
uniform float shadow_size = 5.0;
uniform vec2 shadow_offset = vec2(0.0, 0.0); // 阴影偏移

// 颜色设置
uniform vec4 color_bg: source_color = vec4(0.0, 0.0, 0.0, 0.0); // 背景颜色
uniform vec4 color_border: source_color = vec4(1.0, 1.0, 1.0, 0.3); // 边框颜色
uniform vec4 color_shadow: source_color = vec4(0.0, 0.0, 0.0, 1.0); // 阴影颜色

varying vec4 MODULATE;
// 计算圆角矩形的SDF (Signed Distance Field)
float roundedBoxSDF(vec2 position, vec2 size, vec4 radius) {
    radius.xy = (position.x > 0.0) ? radius.xy : radius.zw;
    radius.x  = (position.y > 0.0) ? radius.x  : radius.y;

    vec2 q = abs(position) - size + radius.x;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius.x;
}

void vertex() {
	MODULATE = COLOR;
}

void fragment() {
	vec2 rectCenter = window_size * 0.5 - shadow_offset;
	vec2 frag_coord = (UV * (1.0 / TEXTURE_PIXEL_SIZE));
	vec4 colorRect = texture(TEXTURE, UV);
    vec2 halfSize = window_size * 0.5 - shadow_size ; // 矩形的半尺寸

    // 计算与圆角矩形的距离
    float distance = roundedBoxSDF(frag_coord - rectCenter, halfSize - 1.0, corner_radius - 1.0);

    // 平滑处理边缘
    float smoothedAlpha = 1.0 - smoothstep(0.0, edge_softness, distance);

	const float jitter = 0.5f;
    // 计算边框透明度
    float borderAlpha = 0.0;
	for (int i = -1; i <= 1; i++)
	for (int j = -1; j <= 1; j++) {
		float sdf = roundedBoxSDF(frag_coord - rectCenter + vec2(float(i) * jitter, float(j) * jitter), halfSize, corner_radius);
		float w = 1.0 - smoothstep(border_thickness - border_softness, border_thickness, abs(sdf));
		borderAlpha += w;
	}
	borderAlpha /= 9.0;

    // 计算阴影透明度
    float shadowDistance = roundedBoxSDF(frag_coord - rectCenter - shadow_offset, halfSize, corner_radius);
    float shadowAlpha = 1.0 - smoothstep(-shadow_size, shadow_size, shadowDistance);

    // 混合背景和阴影
    vec4 res_shadow_color = mix(color_bg, color_shadow, shadowAlpha);

    // 混合背景+阴影与矩形
    vec4 res_shadow_with_rect_color = mix(
        res_shadow_color,
        colorRect,
        min(colorRect.a, smoothedAlpha)
    );

    // 混合背景+阴影+矩形与边框
    vec4 res_shadow_with_rect_with_border = mix(
        res_shadow_with_rect_color,
        color_border,
        min(color_border.a, min(borderAlpha, smoothedAlpha))
    );

    // 输出最终颜色
    COLOR = res_shadow_with_rect_with_border * MODULATE;
}

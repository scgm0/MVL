shader_type canvas_item;

uniform vec2 windowSize = vec2(1162, 658);
uniform float edgeSoftness = 1.0; // 边缘柔化
uniform vec4 cornerRadiuses = vec4(12.0, 12.0, 12.0, 12.0); // 圆角半径

// 边框设置
uniform float borderThickness = 2.0; // 边框厚度
uniform float borderSoftness = 1.0;  // 边框柔化

// 阴影设置
uniform float shadowThickness = 5.0;
uniform vec2 shadowOffset = vec2(0.0, 0.0); // 阴影偏移

// 颜色设置
uniform vec4 colorBg: source_color = vec4(0.0, 0.0, 0.0, 0.0); // 背景颜色
uniform vec4 colorBorder: source_color = vec4(0.7, 0.25, 0.55, 1.0); // 边框颜色
uniform vec4 colorShadow: source_color = vec4(0.4, 0.4, 0.4, 1.0); // 阴影颜色

// 计算圆角矩形的SDF (Signed Distance Field)
float roundedBoxSDF(vec2 position, vec2 size, vec4 radius) {
    radius.xy = (position.x > 0.0) ? radius.xy : radius.zw;
    radius.x  = (position.y > 0.0) ? radius.x  : radius.y;

    vec2 q = abs(position) - size + radius.x;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius.x;
}

void fragment() {
	vec2 rectCenter = windowSize * 0.5 - shadowOffset;
	vec2 frag_coord = (UV * (1.0 / TEXTURE_PIXEL_SIZE));
	vec4 colorRect = texture(TEXTURE, UV);
    vec2 halfSize = (windowSize - shadowThickness * 2.0) * 0.5; // 矩形的半尺寸

    // 计算与圆角矩形的距离
    float distance = roundedBoxSDF(frag_coord - rectCenter, halfSize - 1.0, cornerRadiuses - 1.0);

    // 平滑处理边缘
    float smoothedAlpha = 1.0 - smoothstep(0.0, edgeSoftness, distance);

	const float jitter = 0.5f;
    // 计算边框透明度
    float borderAlpha = 0.0;
	for (int i = -1; i <= 1; i++)
	for (int j = -1; j <= 1; j++) {
		float sdf = roundedBoxSDF(frag_coord - rectCenter + vec2(float(i) * jitter, float(j) * jitter), halfSize, cornerRadiuses);
		float w = 1.0 - smoothstep(borderThickness - borderSoftness, borderThickness, abs(sdf));
		borderAlpha += w;
	}
	borderAlpha /= 9.0;

    // 计算阴影透明度
    float shadowDistance = roundedBoxSDF(frag_coord - rectCenter - shadowOffset, halfSize, cornerRadiuses);
    float shadowAlpha = 1.0 - smoothstep(-shadowThickness, shadowThickness, shadowDistance);

    // 混合背景和阴影
    vec4 res_shadow_color = mix(colorBg, colorShadow, shadowAlpha);

    // 混合背景+阴影与矩形
    vec4 res_shadow_with_rect_color = mix(
        res_shadow_color,
        colorRect,
        min(colorRect.a, smoothedAlpha)
    );

    // 混合背景+阴影+矩形与边框
    vec4 res_shadow_with_rect_with_border = mix(
        res_shadow_with_rect_color,
        colorBorder,
        min(colorBorder.a, min(borderAlpha, smoothedAlpha))
    );

    // 输出最终颜色
    COLOR = res_shadow_with_rect_with_border;
}

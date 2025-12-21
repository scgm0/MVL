shader_type canvas_item;

uniform float radius = 10.0;
uniform vec2 windowTopLeft = vec2(5);
uniform vec2 windowExpandedSize = vec2(1162, 658);

uniform vec4 shadowColor: source_color = vec4(0.0, 0.0, 0.0, 1.0);

uniform vec4  outlineColor: source_color = vec4(1, 1, 1, 0.5);
uniform float outlineThickness = 0.0;
uniform vec4  secondOutlineColor: source_color = vec4(1, 1, 1, 0.5);
uniform float secondOutlineThickness = 0.0;
uniform vec4  outerOutlineColor: source_color = vec4(1, 1, 1, 0.5);
uniform float outerOutlineThickness = 1.0;

varying vec2 windowSize;
varying vec2 halfSize;
varying vec2 rectCenter;
varying vec2 fragCoord;
varying vec2 uv2;
varying vec4 modulate;

vec2 tex_to_pixel(vec2 texcoord) {
	return vec2(texcoord.x * windowExpandedSize.x - windowTopLeft.x, (1.0-texcoord.y)* windowExpandedSize.y - windowTopLeft.y);
}

vec2 pixel_to_tex(vec2 pixelcoord) {
	return vec2((pixelcoord.x + windowTopLeft.x) / windowExpandedSize.x,  1.0-(pixelcoord.y + windowTopLeft.y) / windowExpandedSize.y);
}

bool hasExpandedSize() { return windowSize != windowExpandedSize; }
bool hasPrimaryOutline() { return outlineColor.a > 0.0 && outlineThickness > 0.0; }
bool hasSecondOutline() { return secondOutlineColor.a > 0.0 && secondOutlineThickness > 0.0; }
bool hasOuterOutline() { return hasExpandedSize() && outerOutlineColor.a > 0.0 && outerOutlineThickness > 0.0; }

bool is_within(float point, float a, float b) { return (point >= min(a, b) && point <= max(a, b)); }
bool is_within(vec2 point, vec2 corner_a, vec2 corner_b) {
	return is_within(point.x, corner_a.x, corner_b.x) && is_within(point.y, corner_a.y, corner_b.y);
}

float roundedBoxSDF(vec2 p, vec2 b, float r) {
	vec2 q = abs(p) - (b - r);
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

vec4 getShadow() {
	float distance = roundedBoxSDF(fragCoord - rectCenter, halfSize, radius);
	float shadowAlpha = 1.0 - smoothstep(-windowTopLeft.x * 0.5, windowTopLeft.x, distance);
	return mix(vec4(0), shadowColor, shadowAlpha);
}

vec4 getBase(sampler2D sampler) {
	vec4 tex = texture(sampler, uv2);
	float distance = roundedBoxSDF(fragCoord - rectCenter, halfSize, 0.0);
	return mix(
		vec4(1, 1, 1, 0),
		tex,
		min(tex.a, 1.0 - distance)
	);
}

vec4 shapeCorner(vec2 coord0, vec4 tex, vec2 start, float angle, vec4 coord_shadowColor) {
	vec2 angle_vector = vec2(cos(angle), sin(angle));
	float corner_length = (abs(angle_vector.x) < 0.1 || abs(angle_vector.y) < 0.1) ? 1.0 : sqrt(2.0);
	vec2 roundness_center = start + radius * angle_vector * corner_length;
	vec2 outlineStart = start + outlineThickness * angle_vector * corner_length;
	vec2 secondOutlineStart = start + (outlineThickness + secondOutlineThickness) * angle_vector * corner_length;
	vec2 outerOutlineEnd = start - outerOutlineThickness * angle_vector * corner_length;
	float distance_from_center = distance(coord0, roundness_center);

	if (hasOuterOutline()) {
		vec4 outerOutlineOverlay = mix(coord_shadowColor, outerOutlineColor, outerOutlineColor.a);
		if (distance_from_center > radius + outerOutlineThickness - 0.5) {
			float antialiasing = clamp(distance_from_center - radius - outerOutlineThickness + 0.5, 0.0, 1.0);
			return mix(outerOutlineOverlay, coord_shadowColor, antialiasing);
		} else if (distance_from_center > radius - 0.5) {
			float antialiasing = clamp(distance_from_center - radius + 0.5, 0.0, 1.0);
			if (hasPrimaryOutline()) {
				vec4 outlineOverlay = vec4(mix(tex.rgb, outlineColor.rgb, outlineColor.a), 1.0);
				return mix(outlineOverlay, outerOutlineOverlay, antialiasing);
			} else if (hasSecondOutline()) {
				vec4 secondOutlineOverlay = vec4(mix(tex.rgb, secondOutlineColor.rgb, secondOutlineColor.a), 1.0);
				return mix(secondOutlineOverlay, outerOutlineOverlay, antialiasing);
			} else {
				return mix(tex, outerOutlineOverlay, antialiasing);
			}
		}
	} else {
		if (distance_from_center > radius - 0.5) {
			float antialiasing = clamp(distance_from_center - radius + 0.5, 0.0, 1.0);
			if (hasPrimaryOutline()) {
				vec4 outlineOverlay = vec4(mix(tex.rgb, outlineColor.rgb, outlineColor.a), 1.0);
				return mix(outlineOverlay, coord_shadowColor, antialiasing);
			} else if (hasSecondOutline()) {
				vec4 secondOutlineOverlay = vec4(mix(tex.rgb, secondOutlineColor.rgb, secondOutlineColor.a), 1.0);
				return mix(secondOutlineOverlay, coord_shadowColor, antialiasing);
			} else {
				return mix(tex, coord_shadowColor, antialiasing);
			}
		}
	}

	if (hasPrimaryOutline()) {
		vec4 outlineOverlay = vec4(mix(tex.rgb, outlineColor.rgb, outlineColor.a), 1.0);

		if (outlineThickness >= radius && is_within(coord0, outlineStart, start)) {
			return outlineOverlay;
		} else if (distance_from_center > radius - outlineThickness - 0.5) {
			float antialiasing = clamp(distance_from_center - radius + outlineThickness + 0.5, 0.0, 1.0);
			if (hasSecondOutline()) {
				vec4 secondOutlineOverlay = vec4(mix(tex.rgb, secondOutlineColor.rgb, secondOutlineColor.a), 1.0);
				return mix(secondOutlineOverlay, outlineOverlay, antialiasing);
			} else {
				return mix(tex, outlineOverlay, antialiasing);
			}
		}
	}

	if (hasSecondOutline()) {
		vec4 secondOutlineOverlay = vec4(mix(tex.rgb, secondOutlineColor.rgb, secondOutlineColor.a), 1.0);

		if (outlineThickness + secondOutlineThickness >= radius && is_within(coord0, secondOutlineStart, start)) {
			return secondOutlineOverlay;
		} else if (distance_from_center > radius - outlineThickness - secondOutlineThickness - 0.5) {
			float antialiasing =
					clamp(distance_from_center - radius + outlineThickness + secondOutlineThickness + 0.5, 0.0, 1.0);
			return mix(tex, secondOutlineOverlay, antialiasing);
		}
	}

	float antialiasing = clamp(radius - distance_from_center + 0.5, 0.0, 1.0);
	return mix(coord_shadowColor, tex, antialiasing);
}

vec4 run(vec2 texcoord0, vec4 tex) {
	if (tex.a == 0.0) {
		return tex;
	}

	float r = max(radius, outlineThickness);
	vec2 coord0 = tex_to_pixel(texcoord0);
	vec4 coord_shadowColor = getShadow();

	if (coord0.y < r) {
		if (coord0.x < r) {
			return shapeCorner(coord0, tex, vec2(0.0, 0.0), radians(45.0), coord_shadowColor);
		} else if (coord0.x > windowSize.x - r) {
			return shapeCorner(coord0, tex, vec2(windowSize.x, 0.0), radians(135.0), coord_shadowColor);
		} else if (coord0.y < outlineThickness + secondOutlineThickness) {
			return shapeCorner(coord0, tex, vec2(coord0.x, 0.0), radians(90.0), coord_shadowColor);
		}
	} else if (coord0.y > windowSize.y - r) {
		if (coord0.x < r) {
			return shapeCorner(coord0, tex, vec2(0.0, windowSize.y), radians(315.0), coord_shadowColor);
		} else if (coord0.x > windowSize.x - r) {
			return shapeCorner(coord0, tex, vec2(windowSize.x, windowSize.y), radians(225.0),
							   coord_shadowColor);
		} else if (coord0.y > windowSize.y - outlineThickness - secondOutlineThickness) {
			return shapeCorner(coord0, tex, vec2(coord0.x, windowSize.y), radians(270.0),
							   coord_shadowColor);
		}
	} else {
		if (coord0.x < r) {
			return shapeCorner(coord0, tex, vec2(0.0, coord0.y), radians(0.0), coord_shadowColor);
		} else if (coord0.x > windowSize.x - r) {
			return shapeCorner(coord0, tex, vec2(windowSize.x, coord0.y), radians(180.0),
							   coord_shadowColor);
		}
	}
	return tex;
}

void vertex() {
	windowSize = windowExpandedSize - windowTopLeft * 2.0;
	fragCoord = UV * windowExpandedSize;
	rectCenter = windowExpandedSize * 0.5;
	halfSize = windowSize * 0.5;
	uv2 = UV * (windowExpandedSize / windowSize) - (windowTopLeft / windowExpandedSize);
	modulate = COLOR;
}

void fragment() {
	vec4 tex = getBase(TEXTURE);
	COLOR = run(UV, tex) * modulate;
}

shader_type canvas_item;

uniform float radius = 10.0;
uniform vec2 windowTopLeft = vec2(5);
uniform vec2 windowExpandedSize = vec2(1162, 658);
uniform bool hasRoundCorners = true;

uniform vec4 outlineColor: source_color = vec4(0.8, 0.8, 0.8, 0.45);
uniform float outlineThickness;
uniform vec4 secondOutlineColor: source_color = vec4(0.8, 0.8, 0.8, 0.45);
uniform float secondOutlineThickness = 1.0;
uniform vec4 shadowColor: source_color = vec4(0.0, 0.0, 0.0, 1.0);

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
bool isDrawingShadows() { return hasExpandedSize(); }
bool hasPrimaryOutline() { return outlineColor.a > 0.0 && outlineThickness > 0.0; }
bool hasSecondOutline() { return hasExpandedSize() && secondOutlineColor.a > 0.0 && secondOutlineThickness > 0.0; }

float roundedBoxSDF(vec2 p, vec2 b, float r) {
	vec2 q = abs(p) - (b - r);
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}

vec4 getShadow(sampler2D sampler){
	vec4 tex = texture(sampler, uv2);
	float distance = roundedBoxSDF(fragCoord - rectCenter, halfSize, hasRoundCorners ? radius : 0.0);

	float smoothedAlpha = 1.0 - smoothstep(0.0, 1.0, distance);
	float shadowAlpha = 1.0 - smoothstep(-windowTopLeft.x, windowTopLeft.x, distance);
	vec4 res_shadow_color = mix(vec4(0), shadowColor, shadowAlpha);
	return mix(
		res_shadow_color,
		tex,
		min(tex.a, smoothedAlpha)
	);
}

vec4 shapeCorner(vec2 coord0, vec4 tex, vec2 start, float angle, vec4 coord_shadowColor) {
	float diagonal_length = (hasRoundCorners && (abs(cos(angle)) > 0.1) && (abs(sin(angle)) > 0.1)) ? sqrt(2.0) : 1.0;
	float r = hasRoundCorners ? radius: outlineThickness;
	vec2 center = start + r * diagonal_length * vec2(cos(angle), sin(angle));
	float distance_from_center = distance(coord0, center);

	vec4 secondaryOutlineOverlay = mix(coord_shadowColor, secondOutlineColor, secondOutlineColor.a);
	if (hasPrimaryOutline()) {
		vec4 outlineOverlay = vec4(mix(tex.rgb, outlineColor.rgb, outlineColor.a), 1.0);

		if (distance_from_center < r - outlineThickness + 0.5) {
			float antialiasing = clamp(r - outlineThickness + 0.5 - distance_from_center, 0.0, 1.0);
			return mix(outlineOverlay, tex, antialiasing);
		}
		else if (hasSecondOutline()) {
			if (distance_from_center < r + 0.5) {
				float antialiasing = clamp(r + 0.5 - distance_from_center, 0.0, 1.0);
				return mix(secondaryOutlineOverlay, outlineOverlay, antialiasing);
			}
			else {
				float antialiasing = clamp(distance_from_center - r - secondOutlineThickness + 0.5, 0.0, 1.0);
				return mix(secondaryOutlineOverlay, coord_shadowColor, antialiasing);
			}
		} else {
			float antialiasing = clamp(distance_from_center - r + 0.5, 0.0, 1.0);
			return mix(outlineOverlay, coord_shadowColor, antialiasing);
		}
	}
	else if (hasSecondOutline()) {
		if (distance_from_center < r + 0.5) {
			float antialiasing = clamp(r + 0.5 - distance_from_center, 0.0, 1.0);
			return mix(secondaryOutlineOverlay, tex, antialiasing);
		}
		else {
			float antialiasing = clamp(distance_from_center - r - secondOutlineThickness + 0.5, 0.0, 1.0);
			return mix(secondaryOutlineOverlay, coord_shadowColor, antialiasing);
		}
	}

	float antialiasing = clamp(r - distance_from_center + 0.5, 0.0, 1.0);
	return mix(coord_shadowColor, tex, antialiasing);
}

vec4 run(vec4 tex, vec2 texcoord0, sampler2D sampler) {
	if(tex.a == 0.0) {
		return tex;
	}

	float r = hasRoundCorners? radius: outlineThickness;

	vec2 coord0 = tex_to_pixel(texcoord0);
	vec2 uv = texcoord0 * (windowExpandedSize / windowSize) - (windowTopLeft / windowExpandedSize);

	if (coord0.y < r) {
		if (coord0.x < r) {
			return shapeCorner(coord0, tex, vec2(0.0, 0.0), radians(45.0), tex);			// Section TL
		} else if (coord0.x > windowSize.x - r) {
			return shapeCorner(coord0, tex, vec2(windowSize.x, 0.0), radians(135.0), tex);   // Section TR
		} else if (coord0.y < outlineThickness) {
			return shapeCorner(coord0, tex, vec2(coord0.x, 0.0), radians(90.0), tex);		// Section T
		}
	} else if (coord0.y > windowSize.y - r) {
		if (coord0.x < r) {
			return shapeCorner(coord0, tex, vec2(0.0, windowSize.y), radians(315.0), tex);	   // Section BL
		} else if (coord0.x > windowSize.x - r) {
			return shapeCorner(coord0, tex, vec2(windowSize.x, windowSize.y), radians(225.0), tex);// Section BR
		} else if (coord0.y > windowSize.y - outlineThickness) {
			return shapeCorner(coord0, tex, vec2(coord0.x, windowSize.y), radians(270.0), tex);	// Section B
		}
	} else {
		if (coord0.x < r) {
			return shapeCorner(coord0, tex, vec2(0.0, coord0.y), radians(0.0), tex);			 // Section L
		} else if (coord0.x > windowSize.x - r) {
			return shapeCorner(coord0, tex, vec2(windowSize.x, coord0.y), radians(180.0), tex);  // Section R
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
	vec4 tex = getShadow(TEXTURE);
	COLOR = run(tex, UV, TEXTURE) * modulate;
}

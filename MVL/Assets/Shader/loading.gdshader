shader_type canvas_item;

// Configuration uniforms for the ring
uniform float dot_scale : hint_range(0.01, 2.0) = 1.0;
uniform float ring_radius : hint_range(0.1, 2.0) = 1.0;
uniform int num_points : hint_range(3, 200) = 10;
uniform float gap_ratio : hint_range(0.0, 0.9) = 0.2;
uniform bool all_dots_same_size = false;

// Visual properties for the ring dots
uniform vec4 dot_color_start : source_color = vec4(0.0, 0.0, 1.0, 1.0);
uniform vec4 dot_color_end : source_color = vec4(0.0, 0.0, 1.0, 1.0);
uniform bool enable_shadow = false;
uniform vec4 shadow_color : source_color = vec4(0.0, 0.0, 0.0, 0.5);
uniform vec2 shadow_offset = vec2(0.05, -0.05);

// Animation controls for the ring
uniform float rotation_speed : hint_range(-2.0, 10.0) = 1.0;
uniform bool reverse_rotation = false;

// New uniforms for the icon texture and appearance
uniform sampler2D icon_texture;
uniform float icon_size : hint_range(0.0, 1.0) = 0.3;  // Controls the icon's "radius"

// New uniforms for icon rotation controls
uniform bool enable_icon_rotation = false;
uniform bool reverse_icon_rotation = false;
uniform float icon_rotation_speed : hint_range(-10.0, 10.0) = 1.0;

// Uniform to control whether the icon is drawn.
// When false, the icon_texture is ignored (so the default white texture won't be blended).
uniform bool show_icon = false;

void fragment() {
    vec2 uv = UV * 2.0 - 1.0;

    // Compute the ring (dots and optional shadow)
    vec4 ring_color = vec4(0.0);
    float dot_accum = 0.0;
    float shadow_accum = 0.0;

    float total_angle = 6.283185307; // Full circle in radians (2π)
    float gap = total_angle * gap_ratio;
    float spacing = (total_angle - gap) / float(num_points);

    // Determine rotation direction for the ring.
    float direction = reverse_rotation ? 1.0 : -1.0;
    // Offset by -π/2 so the ring starts from the upward direction.
    float rotation = TIME * rotation_speed * direction - 1.570796;

    for (int i = 0; i < num_points; i++) {
        int index = reverse_rotation ? (num_points - 1 - i) : i;
        float angle = rotation + spacing * float(index);

        vec2 pos = vec2(cos(angle), sin(angle)) * ring_radius;
        if (reverse_rotation) {
            pos = -pos; // Invert position to mirror ring direction.
        }

        // Compute dot size:
        // If all_dots_same_size is true, use a constant size.
        // Otherwise, size diminishes along the ring.
        float size_factor = 1.0 - (float(i) / float(num_points));
        float radius = all_dots_same_size ? 0.1 * dot_scale : 0.1 * size_factor * dot_scale;

        // Interpolate dot color along the ring from start to end.
        float gradient_factor = float(i) / float(num_points - 1);
        vec4 dot_color = mix(dot_color_start, dot_color_end, gradient_factor);

        // Compute smooth dot contribution based on distance.
        float distance = length(uv - pos);
        float contribution = smoothstep(radius + 0.005, radius, distance);

        if (contribution > 0.0) {
            ring_color = mix(ring_color, dot_color, contribution);
            dot_accum += contribution;
        }

        // Optional shadow behind each dot.
        if (enable_shadow) {
            vec2 shadow_pos = pos + shadow_offset;
            float shadow_dist = length(uv - shadow_pos);
            float shadow_contrib = smoothstep(radius * 1.2 + 0.005, radius * 1.2, shadow_dist);
            shadow_accum += shadow_contrib;
        }
    }

    // Determine the base color from the ring (or shadow).
    vec4 base_color = vec4(0.0);
    if (dot_accum > 0.0) {
        base_color = ring_color;
    } else if (enable_shadow && shadow_accum > 0.0) {
        base_color = shadow_color;
    }

    // Icon Drawing - Only proceed if show_icon is true.
    if (show_icon) {
        // Create a radial mask so that the icon is visible inside the icon_size radius.
        float icon_mask = smoothstep(icon_size, icon_size - 0.005, length(uv));

        // Normalize UV coordinates from (-icon_size ... +icon_size) to (0 ... 1)
        vec2 icon_uv = (uv / icon_size + vec2(1.0)) / 2.0;

        // Apply icon rotation if enabled.
        if (enable_icon_rotation) {
            float icon_angle = TIME * icon_rotation_speed;
            if (reverse_icon_rotation) {
                icon_angle = -icon_angle;
            }
            // Rotate around the center (0.5, 0.5) of the icon texture.
            vec2 centered_uv = icon_uv - vec2(0.5);
            float s = sin(icon_angle);
            float c = cos(icon_angle);
            centered_uv = vec2(centered_uv.x * c - centered_uv.y * s,
                               centered_uv.x * s + centered_uv.y * c);
            icon_uv = centered_uv + vec2(0.5);
        }

        vec4 icon_sample = texture(icon_texture, icon_uv);
        // Blend the icon texture with the ring based on the icon mask and the texture's alpha.
        base_color = mix(base_color, icon_sample, icon_mask * icon_sample.a);
    }

    COLOR = base_color;
}

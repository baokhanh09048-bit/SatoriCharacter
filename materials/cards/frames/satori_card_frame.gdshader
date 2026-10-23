shader_type canvas_item;
// Retain native frame shape, relief and transparency at every rarity.
varying vec4 vertex_tint;
varying vec2 frame_uv;
void vertex() {
    vertex_tint = COLOR;
    // Native Frame TextureRect is 300x422. UV itself points into a shared atlas.
    frame_uv = VERTEX / vec2(300.0, 422.0);
}
void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    float relief = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    vec3 plum = mix(vec3(0.19, 0.115, 0.23), vec3(0.64, 0.365, 0.49), relief);
    vec2 px = TEXTURE_PIXEL_SIZE * 1.7;
    float inside = min(min(texture(TEXTURE, UV + vec2(px.x, 0.0)).a,
                           texture(TEXTURE, UV - vec2(px.x, 0.0)).a),
                       min(texture(TEXTURE, UV + vec2(0.0, px.y)).a,
                           texture(TEXTURE, UV - vec2(0.0, px.y)).a));
    float edge = clamp((tex.a - inside) * 2.0, 0.0, 1.0);
    vec3 gold = vec3(0.80, 0.64, 0.40) * (0.62 + 0.38 * relief);
    vec3 result = mix(plum, gold, edge * 0.7);
    // One small eye in the footer; never draws outside the original frame.
    vec2 p = (frame_uv - vec2(0.5, 0.957)) / vec2(0.038, 0.012);
    float ellipse = length(p);
    float eye = (1.0 - smoothstep(0.1, 0.22, abs(ellipse - 1.0))) * step(ellipse, 1.18);
    float pupil = 1.0 - smoothstep(0.25, 0.42, length(p * vec2(1.0, 0.45)));
    result = mix(result, gold, max(eye, pupil) * 0.85);
    COLOR = vec4(result, tex.a) * vertex_tint;
}

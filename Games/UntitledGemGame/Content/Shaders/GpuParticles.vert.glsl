#version 150

uniform sampler2D PositionMap;
uniform sampler2D VelocityMap;
uniform mat4 ViewProjection;
uniform int StateSize;
uniform int FirstSlot;
uniform float TargetY;
out vec2 Corner;
out vec4 Color;

void main()
{
    // One instance per state texel, four vertices per particle. No CPU particle geometry.
    int slot = FirstSlot + gl_InstanceID;
    ivec2 cell = ivec2(slot % StateSize, slot / StateSize);
    vec4 position = texelFetch(PositionMap, cell, 0);
    if (position.z <= 0.0)
    {
        gl_Position = vec4(2.0, 2.0, 0.0, 1.0);
        Corner = vec2(0.0);
        Color = vec4(0.0);
        return;
    }
    vec4 velocity = texelFetch(VelocityMap, cell, 0);
    vec2 corner = vec2(gl_VertexID & 1, gl_VertexID >> 1) - 0.5;
    float life = clamp(position.z / max(position.w, 0.00001), 0.0, 1.0);
    float size = abs(velocity.z) * (0.35 + 0.65 * life);
    vec2 offset = corner * size;
    if (velocity.z < 0.0)
    {
        float speed = length(velocity.xy);
        vec2 direction = velocity.xy / max(speed, 0.0001);
        vec2 across = vec2(-direction.y, direction.x);
        offset = direction * offset.x * (1.0 + min(speed * 0.055, 18.0)) + across * offset.y;
    }
    gl_Position = ViewProjection * vec4(position.xy + offset, 0.0, 1.0);
    // Match MonoGame's render-target Y flip and D3D-style depth convention.
    gl_Position.y *= TargetY;
    gl_Position.z = gl_Position.z * 2.0 - gl_Position.w;
    Corner = corner * 2.0;
    float blue = floor(velocity.w / 65536.0);
    float green = floor((velocity.w - blue * 65536.0) / 256.0);
    float red = velocity.w - blue * 65536.0 - green * 256.0;
    Color = vec4(vec3(red, green, blue) / 255.0, life);
}

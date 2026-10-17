#version 150

in vec2 Corner;
in vec4 Color;
out vec4 OutputColor;

void main()
{
    // Soft additive discs drawn in code, without a sprite asset.
    float falloff = max(0.0, 1.0 - dot(Corner, Corner));
    float energy = falloff * falloff * Color.a;
    OutputColor = vec4(Color.rgb * energy, 0.0);
}

#if __KNIFX__
    #define SV_POSITION SV_Position
    #define VS_SHADERMODEL vs_4_0
    #define PS_SHADERMODEL ps_4_0
#elif OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#elif VULKAN
    #define SV_POSITION SV_Position
    #define VS_SHADERMODEL vs_6_0
    #define PS_SHADERMODEL ps_6_0
#else
    #define SV_POSITION SV_Position
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 view_projection;
float2 Size;
float Time;
float4 Accent;

struct VertexInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

PixelInput MainVS(VertexInput input)
{
    PixelInput output;
    output.Position = mul(input.Position, view_projection);
    output.TexCoord = input.TexCoord;
    return output;
}

float4 MainPS(PixelInput input) : COLOR0
{
    float2 uv = input.TexCoord;
    float2 p = uv * Size;
    // The light shares the artwork's left-to-right fade and never escapes the button.
    float fade = pow(saturate(1.0 - uv.x), 2.4);
    float inset = smoothstep(0.0, 1.5, p.x) * smoothstep(0.0, 1.5, p.y)
        * smoothstep(0.0, 1.5, Size.y - p.y);

    // A glass reflection crosses the highlight, followed by a quieter broad glow.
    // Let it travel fully offscreen before wrapping, so the loop has a natural pause.
    float cycle = Time / 3.6;
    float phase = frac(cycle) * 1.95 - 0.35;
    float diagonal = uv.x + uv.y * 0.15;
    float sweep = exp2(-220.0 * (diagonal - phase) * (diagonal - phase));
    float wake = exp2(-55.0 * (diagonal - phase + 0.12) * (diagonal - phase + 0.12));
    float scans = pow(0.5 + 0.5 * sin(p.y * 0.785398 - cycle * 25.132741), 12.0);
    float breathing = 0.5 + 0.5 * sin(cycle * 6.283185);
    float rim = exp2(-p.x * 0.9) * (0.12 + breathing * 0.12);
    float edge = exp2(-min(p.y, Size.y - p.y) * 1.0) * sweep * 0.18;
    float alpha = ((sweep * 0.28 + wake * 0.045 + scans * 0.022) * fade
        + rim + edge * fade) * inset * Accent.a;
    float3 light = lerp(Accent.rgb, float3(0.86, 0.97, 1.0), sweep * 0.26);
    return float4(light * alpha, alpha);
}

technique MenuHover
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

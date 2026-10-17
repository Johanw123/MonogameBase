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

Texture2D SpriteTexture;
float4x4 view_projection;
float4 _OutlineColor;
float2 TexelSize;
float _OutlineWidth = 4.0f;
float _TotalTime;     

sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

struct VertexInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelInput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

PixelInput SpriteVertexShader(VertexInput v) {
    PixelInput output;
    output.Position = mul(v.Position, view_projection);
    output.Color = v.Color;
    output.TexCoord = v.TexCoord;
    return output;
}

// Twelve evenly spaced samples avoid the square/diamond shape of an eight-tap
// outline. Return both solid coverage and averaged coverage for the outer glow.
float2 OutlineCoverage(float2 uv, float2 radius)
{
    float a0 = tex2D(SpriteTextureSampler, uv + radius * float2(1, 0)).a;
    float a1 = tex2D(SpriteTextureSampler, uv + radius * float2(0.8660254, 0.5)).a;
    float a2 = tex2D(SpriteTextureSampler, uv + radius * float2(0.5, 0.8660254)).a;
    float a3 = tex2D(SpriteTextureSampler, uv + radius * float2(0, 1)).a;
    float a4 = tex2D(SpriteTextureSampler, uv + radius * float2(-0.5, 0.8660254)).a;
    float a5 = tex2D(SpriteTextureSampler, uv + radius * float2(-0.8660254, 0.5)).a;
    float a6 = tex2D(SpriteTextureSampler, uv + radius * float2(-1, 0)).a;
    float a7 = tex2D(SpriteTextureSampler, uv + radius * float2(-0.8660254, -0.5)).a;
    float a8 = tex2D(SpriteTextureSampler, uv + radius * float2(-0.5, -0.8660254)).a;
    float a9 = tex2D(SpriteTextureSampler, uv + radius * float2(0, -1)).a;
    float a10 = tex2D(SpriteTextureSampler, uv + radius * float2(0.5, -0.8660254)).a;
    float a11 = tex2D(SpriteTextureSampler, uv + radius * float2(0.8660254, -0.5)).a;
    float coverage = max(max(max(a0, a1), max(a2, a3)), max(max(a4, a5), max(a6, a7)));
    coverage = max(coverage, max(max(a8, a9), max(a10, a11)));
    float glow = (a0 + a1 + a2 + a3 + a4 + a5 + a6 + a7 + a8 + a9 + a10 + a11) / 12.0f;
    return float2(coverage, glow);
}

float4 MainPS(PixelInput input) : COLOR
{
    float4 texColor = tex2D(SpriteTextureSampler, input.TexCoord);
    float alpha = texColor.a;
    float stateAlpha = input.Color.a;
    // Derivatives must be evaluated before the per-pixel transparency branch.
    // UV footprint makes the outline follow screen pixels rather than atlas texels.
    float2 pixelUV = sqrt(ddx(input.TexCoord) * ddx(input.TexCoord)
        + ddy(input.TexCoord) * ddy(input.TexCoord));

    float4 baseColor = texColor * float4(input.Color.rgb, 1.0f);
    if (stateAlpha < 0.35f)
        baseColor.rgb += float3(0.25f, 0.25f, 0.25f) * alpha;

    // Collecting ships and refuel progress keep their normal hull rendering.
    if (stateAlpha >= 0.5f)
        return baseColor;

    float2 radius = clamp(pixelUV * _OutlineWidth, TexelSize * 1.5f, TexelSize * 7.0f);
    float4 outlineColor = _OutlineColor;
    if (stateAlpha >= 0.35f)
    {
        // Breathe between 65% and full brightness; a stranded ship never goes dark.
        outlineColor *= 0.825f + 0.175f * sin(_TotalTime * 5.0f);
    }
    else if (stateAlpha < 0.21f)
    {
        float progress = saturate(stateAlpha / 0.21f);
        radius *= 1.0f + progress * 0.2f;
        outlineColor = lerp(float4(1, 1, 1, 1), _OutlineColor, progress) * (1.0f - progress);
    }

    // Opaque hull interiors need no outline samples. At the antialiased edge,
    // composite the halo behind the hull instead of cutting at alpha == 0.1.
    if (alpha >= 0.99f)
        return baseColor;
    float2 outer = OutlineCoverage(input.TexCoord, radius);
    float2 halo = OutlineCoverage(input.TexCoord, radius * 1.6f);
    float core = smoothstep(0.05f, 0.8f, outer.x);
    float opacity = saturate(core + halo.y * 0.55f);
    return baseColor + outlineColor * (opacity * (1.0f - alpha));
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

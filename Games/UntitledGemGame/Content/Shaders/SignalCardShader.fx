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
Texture2D GlassTexture;
float4x4 view_projection;
float4 CardRect;
float4 RarityColor;
float Time;
float Rarity;
float Hovered;
float EffectMode;

sampler2D SpriteTextureSampler : register(s0) = sampler_state
{
    Texture = <SpriteTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;
    AddressU = clamp;
    AddressV = clamp;
};

sampler2D GlassTextureSampler : register(s1) = sampler_state
{
    Texture = <GlassTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

struct VertexInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
    float2 CardUV : TEXCOORD1;
};

PixelInput SpriteVertexShader(VertexInput v)
{
    PixelInput output;
    output.Position = mul(v.Position, view_projection);
    output.Color = v.Color;
    output.TexCoord = v.TexCoord;
    float2 size = max(CardRect.zw, float2(1.0, 1.0));
    output.CardUV = (v.Position.xy - CardRect.xy) / size;
    return output;
}

float4 MainPS(PixelInput input) : COLOR
{
    float4 texColor = tex2D(SpriteTextureSampler, input.TexCoord);
    float4 baseColor = texColor * input.Color;
    if (baseColor.a <= 0.001)
        return float4(0.0, 0.0, 0.0, 0.0);

    float2 uv = saturate(input.CardUV);
    float tier = clamp(Rarity, 0.0, 4.0);
    float hoverBoost = 1.0 + saturate(Hovered) * 0.35;

    // Mode 0 softens the perimeter of background patterns inside the upper reactor bay so nothing ever hard-cuts
    if (EffectMode < 0.5)
    {
        float2 edgeDist = min(uv, 1.0 - uv);
        float edgeFade = smoothstep(0.0, 0.09, edgeDist.x) * smoothstep(0.0, 0.12, edgeDist.y);
        baseColor *= edgeFade;
    }

    // Smooth, continuous (sin/cos) holographic glass foil & reactor wave — no frac() seams or distracting border flashes
    float speed = 0.75 + tier * 0.22;
    float2 glassUV = frac(uv * float2(1.10, 1.15) + float2(-Time * 0.06, Time * 0.04));
    float4 glassTex = tex2D(GlassTextureSampler, glassUV);
    float glassMask = 0.35 + 0.65 * max(glassTex.r, glassTex.a);

    // Smooth periodic diagonal sheen across the upper bay
    float diagPhase = (uv.x * 0.80 + uv.y * 0.60) * 6.2831853 - Time * speed;
    float sheenWave = pow(0.5 + 0.5 * sin(diagPhase), 5.0);
    float sheenSub = tier >= 2.0 ? pow(0.5 + 0.5 * sin(diagPhase * 1.5 + 1.2), 7.0) * 0.45 : 0.0;

    // Gentle vertical scanner wave through the hex grid (Uncommon+)
    float scanWave = tier >= 1.0
        ? pow(0.5 + 0.5 * sin(uv.y * 6.2831853 - Time * (speed * 1.15)), 6.0) * (0.18 + tier * 0.07)
        : 0.0;

    // Radial reactor breathing centered on the icon socket inside the upper bay (uv ~ (0.5, 0.43))
    float2 iconDelta = (uv - float2(0.5, 0.43)) * float2(1.0, 0.72);
    float iconDist = length(iconDelta);
    float ringWave = tier >= 2.0
        ? (0.5 + 0.5 * sin(iconDist * 22.0 - Time * (1.8 + tier * 0.4))) * exp(-iconDist * 3.8) * (0.16 + tier * 0.07)
        : 0.0;

    float foilStrength = (0.14 + tier * 0.16) * hoverBoost;
    float combinedWave = ((sheenWave + sheenSub) * glassMask + scanWave + ringWave) * foilStrength;

    float3 shimmerTint = RarityColor.rgb;
    if (tier >= 3.5)
    {
        float3 warmGold = float3(1.0, 0.90, 0.58);
        float3 prism = float3(1.0, 0.78 + 0.16 * sin(diagPhase), 0.50 + 0.25 * cos(diagPhase));
        shimmerTint = lerp(warmGold, prism, 0.30);
    }
    else if (tier >= 2.5)
    {
        shimmerTint = lerp(RarityColor.rgb, float3(0.86, 0.94, 1.0), 0.35);
    }

    float3 addedLight = lerp(shimmerTint, float3(1.0, 0.99, 0.96), saturate(sheenWave * 0.45)) * combinedWave;
    baseColor.rgb += addedLight * baseColor.a;
    return saturate(baseColor);
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

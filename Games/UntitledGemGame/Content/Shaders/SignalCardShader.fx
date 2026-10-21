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
float4 HoverOutlineParams; // x: PrimaryRim, y: OuterTrace, z: SoftGlow, w: PulseRipple
float HoverOutlineWaveSpeed;

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
    float2 uv = saturate(input.CardUV);
    float tier = clamp(Rarity, 0.0, 4.0);
    float hover = saturate(Hovered);

    // Mode 2: Razor-sharp geometric contour frame + parallel outer bezel + traveling energy wave
    if (EffectMode > 1.5)
    {
        if (texColor.r <= 0.002 && texColor.g <= 0.002 && texColor.b <= 0.002)
            return float4(0.0, 0.0, 0.0, 0.0);

        float sd = texColor.r * 30.0 - 4.0;
        float primaryRim = texColor.g * HoverOutlineParams.x;
        float outerTrace = texColor.b * HoverOutlineParams.y;
        float tightGlow = texColor.a * HoverOutlineParams.z;
        if (sd > 24.5 || sd < -1.0)
            return float4(0.0, 0.0, 0.0, 0.0);

        float2 centered = uv - float2(0.5, 0.5);
        float angle = atan2(centered.y, centered.x);
        float normAngle = angle / 6.2831853 + 0.5;

        // Smooth traveling energy wave around the perimeter on top of a strong 360-degree baseline
        float wave1 = 0.5 + 0.5 * sin(normAngle * 6.2831853 * 2.0 - Time * HoverOutlineWaveSpeed);
        float comet = pow(wave1, 2.2);

        // Crisp parallel outer tech-trace contour (sd = 13.0)
        float dashWave = 0.75 + 0.25 * smoothstep(-0.2, 0.2, sin(angle * 36.0));
        float activeOuter = outerTrace * dashWave * (0.62 + 0.38 * comet);

        // Subtle expanding contour ripple between primaryRim (sd=5.0) and outerTrace (sd=13.0)
        float pulseProg = frac(Time * 0.55);
        float pulsePos = lerp(5.0, 13.0, pulseProg);
        float pulseFade = sin(pulseProg * 3.14159265) * HoverOutlineParams.w;
        float pulseLine = (1.0 - smoothstep(0.0, 1.5, abs(sd - pulsePos))) * pulseFade;

        // Bold, crisp geometric contour lines + tight neon edge falloff all 360 degrees around the card
        float coreIntensity = saturate(max(primaryRim, max(activeOuter, pulseLine)));
        float glowIntensity = saturate(tightGlow * (0.62 + 0.28 * comet));
        float totalAlpha = saturate(max(coreIntensity, glowIntensity)) * hover * input.Color.a;
        if (totalAlpha <= 0.001)
            return float4(0.0, 0.0, 0.0, 0.0);

        float3 baseAccent = RarityColor.rgb;
        if (tier < 0.5)
            baseAccent = lerp(baseAccent, float3(0.38, 0.86, 1.0), 0.58);
        float3 midCol = lerp(baseAccent, float3(1.0, 0.90, 0.45), 0.45);
        float3 hotCol = lerp(baseAccent, float3(1.0, 0.99, 0.92), 0.88);

        float3 col = lerp(baseAccent, midCol, saturate(coreIntensity * 0.75 + glowIntensity * 0.35));
        col = lerp(col, hotCol, saturate(primaryRim * (0.72 + 0.28 * comet) + activeOuter * 0.40));
        return float4(col * totalAlpha, totalAlpha);
    }

    float4 baseColor = texColor * input.Color;
    if (baseColor.a <= 0.001)
        return float4(0.0, 0.0, 0.0, 0.0);

    float effectiveTier = max(tier, hover * 2.6);
    float hoverBoost = 1.0 + hover * 0.75;

    // Mode 0 softens the perimeter of background patterns inside the upper reactor bay so nothing ever hard-cuts
    if (EffectMode < 0.5)
    {
        float2 edgeDist = min(uv, 1.0 - uv);
        float edgeFade = smoothstep(0.0, 0.09, edgeDist.x) * smoothstep(0.0, 0.12, edgeDist.y);
        baseColor *= edgeFade;
    }

    // Smooth, continuous (sin/cos) holographic glass foil & reactor wave — no frac() seams or distracting border flashes
    float speed = 0.75 + effectiveTier * 0.22;
    float2 glassUV = frac(uv * float2(1.10, 1.15) + float2(-Time * 0.06, Time * 0.04));
    float4 glassTex = tex2D(GlassTextureSampler, glassUV);
    float glassMask = 0.35 + 0.65 * max(glassTex.r, glassTex.a);

    // Smooth periodic diagonal sheen across the upper bay
    float diagPhase = (uv.x * 0.80 + uv.y * 0.60) * 6.2831853 - Time * speed;
    float sheenWave = pow(0.5 + 0.5 * sin(diagPhase), 5.0);
    float sheenSub = effectiveTier >= 2.0 ? pow(0.5 + 0.5 * sin(diagPhase * 1.5 + 1.2), 7.0) * 0.45 : 0.0;

    // Gentle vertical scanner wave through the hex grid
    float scanWave = effectiveTier >= 1.0
        ? pow(0.5 + 0.5 * sin(uv.y * 6.2831853 - Time * (speed * 1.15)), 6.0) * (0.18 + effectiveTier * 0.07)
        : 0.0;

    // Radial reactor breathing centered on the icon socket inside the upper bay (uv ~ (0.5, 0.41))
    float2 iconDelta = (uv - float2(0.5, 0.41)) * float2(1.0, 0.72);
    float iconDist = length(iconDelta);
    float ringWave = effectiveTier >= 2.0
        ? (0.5 + 0.5 * sin(iconDist * 22.0 - Time * (1.8 + effectiveTier * 0.4))) * exp(-iconDist * 3.8) * (0.16 + effectiveTier * 0.07)
        : 0.0;

    float foilStrength = (0.14 + effectiveTier * 0.16) * hoverBoost;
    float combinedWave = ((sheenWave + sheenSub) * glassMask + scanWave + ringWave) * foilStrength;

    float3 shimmerTint = RarityColor.rgb;
    if (tier >= 3.5)
    {
        float3 warmGold = float3(1.0, 0.90, 0.58);
        float3 prism = float3(1.0, 0.78 + 0.16 * sin(diagPhase), 0.50 + 0.25 * cos(diagPhase));
        shimmerTint = lerp(warmGold, prism, 0.30);
    }
    else if (tier >= 2.5 || hover > 0.1)
    {
        shimmerTint = lerp(RarityColor.rgb, float3(0.90, 0.96, 1.0), 0.35);
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

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
float4 FullCardRect;
float4 RarityColor;
float Time;
float Rarity;
float Hovered;
float EffectMode;
float4 HoverOutlineParams; // x: PrimaryRim, y: OuterTrace, z: SoftGlow, w: PulseRipple
float HoverOutlineWaveSpeed;
float4 DissolveParams;     // x: Dissolve (0..1), y: SelectionFlash (0..1), z: IsSelected (1=chosen, 0=unchosen), w: BurnWidthScale

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
    float4 CardUV : TEXCOORD1; // xy: pass UV (CardRect), zw: unified full card UV (FullCardRect)
};

PixelInput SpriteVertexShader(VertexInput v)
{
    PixelInput output;
    output.Position = mul(v.Position, view_projection);
    output.Color = v.Color;
    output.TexCoord = v.TexCoord;
    float2 size = max(CardRect.zw, float2(1.0, 1.0));
    float2 fullSize = max(FullCardRect.zw, float2(1.0, 1.0));
    output.CardUV = float4(
        (v.Position.xy - CardRect.xy) / size,
        (v.Position.xy - FullCardRect.xy) / fullSize
    );
    return output;
}

float4 ApplySignalDissolveAndFlash(float4 col, float2 cardUV, float tier, bool isAuraPass)
{
    float dissolve = saturate(DissolveParams.x);
    float flash = saturate(DissolveParams.y);
    bool isSelected = DissolveParams.z > 0.5;
    float burnScale = max(0.15, DissolveParams.w);

    if (dissolve <= 0.0005 && flash <= 0.0005)
        return col;

    float3 baseAccent = RarityColor.rgb;
    if (tier < 0.5)
        baseAccent = lerp(baseAccent, float3(0.38, 0.86, 1.0), 0.58);
    float3 goldCol = lerp(baseAccent, float3(1.0, 0.90, 0.45), 0.50);
    float3 hotWhite = float3(1.0, 0.99, 0.94);

    // Focal point is the central hex icon socket at (0.5, 0.253) of the full card
    float2 focal = float2(0.5, 0.253);
    float2 delta = (cardUV - focal) * float2(1.0, 1.18);
    float radialDist = saturate(length(delta) / 0.90);

    // Selection overcharge flash & expanding shockwave across the chosen card
    if (isSelected && flash > 0.001 && !isAuraPass && col.a > 0.001)
    {
        float coreSurge = exp(-radialDist * 3.0) * flash * (0.50 + tier * 0.08);
        float shockPos = saturate((1.0 - flash) * 1.45);
        float shockRing = (1.0 - smoothstep(0.0, 0.14, abs(radialDist - shockPos))) * flash * (0.55 + tier * 0.10);
        float surgeAmount = saturate(flash * (0.36 + tier * 0.06) + coreSurge + shockRing);
        float3 surgeTint = lerp(goldCol, hotWhite, saturate(coreSurge + shockRing * 0.8 + flash * 0.45));
        col.rgb = lerp(col.rgb, surgeTint * col.a, surgeAmount);
    }

    if (dissolve <= 0.0005)
        return col;

    // Multi-octave crystalline glass + 60-degree hexagonal cell interference noise
    float2 gUV1 = frac(cardUV * float2(1.85, 2.35) + float2(0.17, 0.31));
    float2 gUV2 = frac(cardUV * float2(4.70, 6.00) - float2(0.41, 0.23));
    float4 gTex1 = tex2D(GlassTextureSampler, gUV1);
    float4 gTex2 = tex2D(GlassTextureSampler, gUV2);
    float glass1 = max(gTex1.r, gTex1.a);
    float glass2 = max(gTex2.r, gTex2.a);

    float2 hp = cardUV * float2(34.0, 44.0);
    float h1 = sin(hp.x * 1.73205 + hp.y);
    float h2 = sin(hp.x * 1.73205 - hp.y);
    float h3 = sin(hp.y * 2.0);
    float hexNoise = 0.5 + 0.5 * (h1 * h2 * h3);
    float noise = saturate(glass1 * 0.44 + glass2 * 0.31 + hexNoise * 0.25);

    // Chosen card disintegrates from the outer edges inward toward the central hex reactor;
    // unchosen cards dematerialize quickly with a clean top/bottom tech fade.
    float erodeOrder = isSelected
        ? saturate((1.0 - radialDist) * 0.62 + noise * 0.38)
        : saturate((1.0 - cardUV.y) * 0.42 + (1.0 - radialDist) * 0.28 + noise * 0.30);

    float burnWidth = isSelected
        ? (0.115 + tier * 0.024) * burnScale
        : 0.055 * burnScale;

    float threshold = dissolve * (1.0 + burnWidth + 0.06) - 0.03;
    float margin = erodeOrder - threshold;
    if (margin < 0.0)
        return float4(0.0, 0.0, 0.0, 0.0);

    if (!isSelected)
    {
        float fadeOut = saturate(1.0 - dissolve * 0.65);
        float tip = smoothstep(0.0, max(burnWidth, 0.001), margin);
        return col * (fadeOut * tip);
    }

    if (isAuraPass)
    {
        float auraFade = smoothstep(0.0, max(burnWidth * 0.7, 0.001), margin);
        return col * auraFade;
    }

    // Superheated 3-stage plasma burn edge along the active disintegration frontier
    if (margin < burnWidth && col.a > 0.001)
    {
        float burnT = 1.0 - saturate(margin / max(burnWidth, 0.001));
        float rimCore = pow(burnT, 2.5);
        float rimMid = pow(burnT, 1.2);
        float3 burnCol = lerp(baseAccent, goldCol, rimMid);
        burnCol = lerp(burnCol, hotWhite, rimCore);

        float edgeAlpha = max(col.a, saturate(rimMid * 0.96));
        float3 baseUnpremult = col.rgb / max(col.a, 0.001);
        float3 litRgb = lerp(baseUnpremult, burnCol * (1.35 + tier * 0.15), saturate(burnT * 1.18));
        float tipFade = smoothstep(0.0, 0.14, margin / max(burnWidth, 0.001));
        float finalA = edgeAlpha * tipFade;
        return float4(saturate(litRgb) * finalA, finalA);
    }

    return col;
}

float4 MainPS(PixelInput input) : COLOR
{
    float4 texColor = tex2D(SpriteTextureSampler, input.TexCoord);
    float2 uv = saturate(input.CardUV.xy);
    float2 cardUV = input.CardUV.zw;
    float tier = clamp(Rarity, 0.0, 4.0);
    float hover = saturate(Hovered);

    // Mode 3: Structural armor & foreground compartments (unmodified during idle/hover, dissolves on selection)
    if (EffectMode > 2.5)
    {
        float4 structColor = texColor * input.Color;
        if (structColor.a <= 0.001)
            return float4(0.0, 0.0, 0.0, 0.0);
        return ApplySignalDissolveAndFlash(structColor, cardUV, tier, false);
    }

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
        return ApplySignalDissolveAndFlash(float4(col * totalAlpha, totalAlpha), cardUV, tier, true);
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
    return ApplySignalDissolveAndFlash(saturate(baseColor), cardUV, tier, false);
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

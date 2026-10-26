// Pixel shader combines the bloom image with the original
// scene, using tweakable intensity levels and saturation.
// This is the final step in applying a bloom postprocess.

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

Texture2D BloomTexture;
Texture2D BaseTexture;

sampler BloomSampler : register(s0)
{
    Filter = Linear;
    AddressU = clamp;
    AddressV = clamp;
};
sampler BaseSampler : register(s1)
{
	Texture = (BaseTexture); // <--- must use SetValue for BaseTexture
	Filter = Linear;
	AddressU = clamp;
	AddressV = clamp;
};

float BloomIntensity;
float BaseIntensity;
float BloomSaturation;
float BaseSaturation;

struct VSOutput 
{
	float4 position		: SV_Position;
	float4 color		: COLOR0;
	float2 texCoord		: TEXCOORD0;
};


// Values are neutral until VisualEffects.Prepare configures this cached effect.
float2 PixelSize = float2(0.00052083, 0.00092593);
float VisualEnabled = 0;
float VisualTime = 0;
float HaloMix = 0;
float HaloRadius = 8;
float Anamorphic = 1;
float Exposure = 0;
float Contrast = 1;
float Saturation = 1;
float Temperature = 0;
float Tint = 0;
float ShadowLift = 0;
float HighlightRolloff = 0;
float VignetteStrength = 0;
float VignetteRadius = 0.35;
float VignetteSoftness = 0.55;
float AberrationPixels = 0;
float GrainAmount = 0;
float SharpenAmount = 0;
float LensAmount = 0;

float3 AdjustSaturation(float3 color, float saturation)
{
    float grey = dot(color, float3(0.3, 0.59, 0.11));
    return lerp(grey.xxx, color, saturation);
}

float4 PixelShaderFunction(VSOutput input) : COLOR0
{
    float2 uv = input.texCoord;
    float2 centered = uv * 2.0 - 1.0;
    if (VisualEnabled > 0.5 && LensAmount != 0)
    {
        // Fit the distorted image into the frame instead of exposing black corners.
        float fit = 1.0 + max(LensAmount, 0.0) * 2.0;
        uv = 0.5 + centered * (1.0 + LensAmount * dot(centered, centered)) / (2.0 * fit);
    }
    float3 scene = tex2D(BaseSampler, uv).rgb;
    float3 bloom = tex2D(BloomSampler, uv).rgb;
    if (VisualEnabled > 0.5)
    {
        if (SharpenAmount > 0)
        {
            float3 average = (tex2D(BaseSampler, uv + float2(PixelSize.x, 0)).rgb
                + tex2D(BaseSampler, uv - float2(PixelSize.x, 0)).rgb
                + tex2D(BaseSampler, uv + float2(0, PixelSize.y)).rgb
                + tex2D(BaseSampler, uv - float2(0, PixelSize.y)).rgb) * 0.25;
            scene = saturate(scene + (scene - average) * SharpenAmount);
        }
        if (AberrationPixels > 0)
        {
            float2 shift = centered * dot(centered, centered) * AberrationPixels
                * float2(PixelSize.x / PixelSize.y, 1.0) / 1080.0;
            scene.r = tex2D(BaseSampler, uv + shift).r;
            scene.b = tex2D(BaseSampler, uv - shift).b;
        }
        if (HaloMix > 0)
        {
            // Nine-tap tent over the existing Gaussian buffer: a second glow scale
            // without extra full-screen render targets. Radius is relative to 1080p.
            float2 d = float2(PixelSize.x / PixelSize.y * Anamorphic, 1.0) * HaloRadius / 1080.0;
            float3 halo = bloom * 4.0;
            halo += (tex2D(BloomSampler, uv + float2(d.x,0)).rgb + tex2D(BloomSampler, uv - float2(d.x,0)).rgb
                   + tex2D(BloomSampler, uv + float2(0,d.y)).rgb + tex2D(BloomSampler, uv - float2(0,d.y)).rgb) * 2.0;
            halo += tex2D(BloomSampler, uv + d).rgb + tex2D(BloomSampler, uv - d).rgb
                  + tex2D(BloomSampler, uv + float2(d.x,-d.y)).rgb + tex2D(BloomSampler, uv + float2(-d.x,d.y)).rgb;
            bloom = lerp(bloom, halo / 16.0, HaloMix);
        }
    }
    bloom = AdjustSaturation(bloom, BloomSaturation) * BloomIntensity;
    scene = AdjustSaturation(scene, BaseSaturation) * BaseIntensity;
    float3 color = scene * (1.0 - saturate(bloom)) + bloom;
    if (VisualEnabled > 0.5)
    {
        color *= exp2(Exposure) * float3(1.0 + Temperature, 1.0 + Tint, 1.0 - Temperature);
        color += ShadowLift * (1.0 - saturate(color));
        color = max(0.0, (color - 0.18) * Contrast + 0.18);
        color = max(0.0, AdjustSaturation(color, Saturation));
        // Smooth shoulder only above 0.6: leave the dark space backdrop alone.
        float3 highlight = max(0.0, color - 0.6);
        color -= highlight * highlight / (0.4 + highlight) * HighlightRolloff;
        float distance = length(centered) * 0.70710678;
        color *= 1.0 - smoothstep(VignetteRadius, VignetteRadius + VignetteSoftness, distance) * VignetteStrength;
        if (GrainAmount > 0)
        {
            float2 pixel = floor(input.texCoord / PixelSize);
            float grain = frac(sin(dot(pixel, float2(12.9898,78.233)) + floor(VisualTime * 24.0)) * 43758.5453) - 0.5;
            color += grain * GrainAmount;
        }
    }
    return float4(saturate(color), 1.0);
}


technique BloomCombine
{
    pass Pass1
    {
		PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}

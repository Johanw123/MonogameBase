// Pixel shader extracts the brighter areas of an image.
// This is the first step in applying a bloom postprocess.

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
sampler2D SpriteTextureSampler = sampler_state
{
  Texture = <SpriteTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;
    Filter = Linear;

    AddressU = clamp;
    AddressV = clamp;
};

float BloomThreshold;

struct VSOutput
{
	float4 position		: SV_Position;
	float4 color		: COLOR0;
	float2 texCoord		: TEXCOORD0;
};


// Manual bloom boost for the warm orange accents on ships (was 0.65).
float ShipOrangeBloom = 3.00;
float SoftBloom = 0;
float SoftKnee = 0.45;

float4 PixelShaderFunction(VSOutput input) : COLOR0
{    
    float4 c = tex2D(SpriteTextureSampler, input.texCoord);  // Look up the original image color.    
	
    // Per-channel thresholding strips any grey/blue floor so bloom stays richly saturated.
    // Raise the threshold on low-chroma grey hull surfaces so they stay crisp, and boost
    // warm orange emissive accents so they cast a vibrant orange glow.
    float brightness = max(c.r, max(c.g, c.b));
    float chroma = brightness - min(c.r, min(c.g, c.b));
    float threshold = clamp(BloomThreshold + (0.25 - chroma) * 0.35, 0.22, 0.75);
    float3 extracted = saturate((c.rgb - threshold) / (1.0 - threshold));
    if (SoftBloom > 0.5)
    {
        // Brightness-based extraction preserves hue across the threshold. A quadratic
        // knee gradually introduces small highlights instead of abruptly switching on.
        threshold = clamp(BloomThreshold + (0.25 - chroma) * 0.35, 0.05, 0.95);
        float knee = max(threshold * SoftKnee, 0.0001);
        float soft = clamp(brightness - threshold + knee, 0.0, 2.0 * knee);
        soft = soft * soft / (4.0 * knee);
        float contribution = max(brightness - threshold, soft) / max(brightness, 0.0001);
        extracted = c.rgb * contribution;
    }

    float orange = saturate((c.r - 0.65) * 3.0) * saturate((c.g - 0.30) * 3.0) * saturate((0.78 - c.g) * 4.0) * saturate((c.r - c.b - 0.45) * 3.0);
    extracted *= (1.0 + orange * ShipOrangeBloom);

    return float4(extracted, 1.0);
}


technique BloomExtract
{
    pass Pass1
    {
		PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}

// Pixel shader extracts the brighter areas of an image.
// This is the first step in applying a bloom postprocess.

#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

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


float4 PixelShaderFunction(VSOutput input) : COLOR0
{    
    float4 c = tex2D(SpriteTextureSampler, input.texCoord);  // Look up the original image color.    
	
	// Adjust it to keep only values brighter than the specified threshold.
    //return saturate((c - BloomThreshold) / (1 - BloomThreshold));
    return saturate((c - BloomThreshold) / (1 - BloomThreshold));
}


technique BloomExtract
{
    pass Pass1
    {
		PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
    }
}

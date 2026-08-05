#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D ScreenTexture;
sampler TextureSampler = sampler_state
{
    Texture = <ScreenTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MagFilter = Linear;
    MinFilter = Linear;
    Mipfilter = Linear;
};

// Size of a single pixel (1.0 / Width, 1.0 / Height)
float2 TexelSize;
float4x4 view_projection;

// Pre-calculated Gaussian weights
static const float Weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

float4 BlurFunction(float2 texCoord, float2 direction)
{
    // Start with the center pixel
    float4 color = tex2D(TextureSampler, texCoord) * Weights[0];
    
    // Sample pixels on both sides
    for (int i = 1; i < 5; i++)
    {
        float2 offset = direction * (TexelSize * i);
        color += tex2D(TextureSampler, texCoord + offset) * Weights[i];
        color += tex2D(TextureSampler, texCoord - offset) * Weights[i];
    }
    
    return color;
}

float4 PixelShader_Horizontal(float2 texCoord : TEXCOORD0) : COLOR0
{
    return BlurFunction(texCoord, float2(1, 0));
}

float4 PixelShader_Vertical(float2 texCoord : TEXCOORD0) : COLOR0
{
    return BlurFunction(texCoord, float2(0, 1));
}

technique HorizontalBlur
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL PixelShader_Horizontal();
    }
}

technique VerticalBlur
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL PixelShader_Vertical();
    }
}

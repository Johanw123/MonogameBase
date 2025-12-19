#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
    // IMPORTANT: Set Address modes to Wrap for tiling
    AddressU = Wrap;
    AddressV = Wrap;
    Filter = Linear;
};

// Simple hash function to get a random float2 from a grid position
float2 hash(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453123);
}

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TextureCoordinates;
    
    // Scale the UVs to determine how many times it tiles
    // (You can also pass this as a parameter)
    float2 tiledUV = uv * 5.0; 
    
    float2 i = floor(tiledUV); // The "ID" of the current tile
    float2 f = frac(tiledUV);  // The local coordinate inside the tile

    // Get a random offset for this specific tile
    float2 offset = hash(i);
    
    // Sample the texture with the random offset
    // The 'frac' ensures we stay within the 0-1 range of the source texture
    float4 color = tex2D(SpriteTextureSampler, frac(f + offset));

    return color * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

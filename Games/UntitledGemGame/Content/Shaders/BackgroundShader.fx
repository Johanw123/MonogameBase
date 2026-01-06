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
    // Valid HLSL values: Clamp, Wrap, Mirror, Border, MirrorOnce
    AddressU = Wrap;
    AddressV = Wrap;
    //Filter = Linear;
    Filter = Linear;
};

// Simple hash function to get a random float2 from a grid position
float2 hash(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453123);
}

float4x4 view_projection;

struct VertexInput {
  float4 Position : POSITION0;
  float4 Color : COLOR0;
  float2 TexCoord : TEXCOORD0;
};
struct PixelInput {
  float4 Position : SV_Position0;
  float4 Color : COLOR0;
  float2 TexCoord : TEXCOORD0;
};

PixelInput SpriteVertexShader(VertexInput v) {
  PixelInput output;

  output.Position = mul(v.Position, view_projection);
  //output.Position = v.Position;
  output.Color = v.Color;
  output.TexCoord = v.TexCoord;

  return output;
}

float4 MainPS(PixelInput input) : COLOR
{
    float2 uv = input.TexCoord;
    
    // Scale the UVs to determine how many times it tiles
    // (You can also pass this as a parameter)
    float2 tiledUV = uv * 2.0; 
    
    float2 i = floor(tiledUV); // The "ID" of the current tile
    float2 f = frac(tiledUV);  // The local coordinate inside the tile

    // Get a random offset for this specific tile
    float2 offset = hash(i);
    
    // Sample the texture with the random offset
    // The 'frac' ensures we stay within the 0-1 range of the source texture
    //float4 color = tex2D(SpriteTextureSampler, frac(f + offset));
        float4 color = tex2D(SpriteTextureSampler, uv);

    return color * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

//DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
//WINEPREFIX=$HOME/.winemonogame wine mgfxc /Users/johanwangsell/Dev/MonogameBase/Games/UntitledGemGame/Content/Shaders/BackgroundShader.fx /Users/johanwangsell/Dev/MonogameBase/Games/UntitledGemGame/Content/Shaders/GeneratedShaders/BackgroundShader.mgfx /Profile:OpenGL

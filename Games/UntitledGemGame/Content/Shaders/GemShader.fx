#if OPENGL
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

cbuffer MatrixBlock : register(b0)
{
    float4x4 view_projection;
};

cbuffer ParameterBlock : register(b1)
{
    float4 _OutlineColor;
};

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
    float4 Position : SV_Position0;
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

// SpriteTexture contains precomputed surface weights, not the original artwork.
// One filtered read replaces the edge/alpha-average/outline neighborhood reads.
float4 MainPS(PixelInput input) : COLOR
{
    float4 surface = tex2D(SpriteTextureSampler, input.TexCoord);
    float3 color = input.Color.rgb * surface.r + surface.g;
    // Vertex alpha is an effect flag, not opacity (ordinary gems use zero).
    float outlined = step(1.0f, input.Color.a);
    // The outline stays inside the silhouette and preserves premultiplied alpha.
    float3 outlinedColor = lerp(color, _OutlineColor.rgb * surface.a, surface.b);
    float4 finalColor = float4(lerp(color, outlinedColor, outlined), surface.a);

    return finalColor;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

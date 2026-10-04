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

// Full-screen warp for the prestige time loop: the rendered world twists and is
// pulled into a black hole (BlackHole.fx draws the hole itself on top). Center is
// the hole in texture coordinates; distances are measured in screen heights
// (Aspect = width / height).
Texture2D SpriteTexture;
sampler2D SceneSampler = sampler_state
{
    Texture = <SpriteTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    Filter = Linear;
};

float4x4 view_projection;
float2 Center;
float Aspect;
float Radius;    // reach of the warp
float Twist;     // radians of swirl at the centre
float Pull;      // how hard the scene is pulled inward
float Flash;     // white-out

struct VertexInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelInput
{
    float4 Position : SV_Position0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

PixelInput SpriteVertexShader(VertexInput v)
{
    PixelInput output;
    output.Position = mul(v.Position, view_projection);
    output.Color = v.Color;
    output.TexCoord = v.TexCoord;
    return output;
}

float4 MainPS(PixelInput input) : COLOR
{
    float2 d = input.TexCoord - Center;
    d.x *= Aspect;
    float r = length(d);
    float influence = saturate(1.0 - r / Radius);

    // Swirl: rotate the sample around the hole, strongest at its centre.
    float swirl = Twist * influence * influence;
    float s = sin(swirl);
    float c = cos(swirl);
    float2 rotated = float2(d.x * c - d.y * s, d.x * s + d.y * c);

    // Suction: sample further out than this pixel, so the scene shrinks into the hole.
    rotated *= 1.0 + Pull * influence;
    rotated.x /= Aspect;
    float2 uv = Center + rotated;
    float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
    float3 color = tex2D(SceneSampler, uv).rgb * inside;
    color = lerp(color, float3(1.0, 1.0, 1.0), saturate(Flash));
    return float4(color, 1.0);
}

technique BlackHoleWarp
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

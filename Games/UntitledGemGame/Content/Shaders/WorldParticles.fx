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
    #define VS_SHADERMODEL vs_4_0_level_9_3
    #define PS_SHADERMODEL ps_4_0_level_9_3
#endif

float4x4 view_projection;
struct VertexInput { float4 Position : POSITION0; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
struct PixelInput { float4 Position : SV_Position0; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
PixelInput SpriteVertexShader(VertexInput v)
{
    PixelInput o;
    o.Position = mul(v.Position, view_projection);
    o.Color = v.Color;
    o.TexCoord = v.TexCoord;
    return o;
}

// Texture-free fragments: an irregular hexagon, with three differently lit faces.
// Vertex color is already premultiplied by lifetime opacity.
float4 RockPS(PixelInput input) : COLOR0
{
    float2 p = (input.TexCoord - 0.5) * 2.0;
    float shape = max(abs(p.x) * 0.92 + abs(p.y) * 0.46,
        max(abs(p.y) * 0.94 + p.x * 0.16, -p.x * 0.9 - p.y * 0.35));
    float aa = max(fwidth(shape), 0.025);
    float mask = 1.0 - smoothstep(0.78 - aa, 0.78 + aa, shape);
    float face = p.y < p.x * 0.35 ? 1.2 : (p.x < -p.y * 0.45 ? 0.45 : 0.75);
    float edge = smoothstep(0.58, 0.77, shape) * saturate(-p.y + 0.2);
    float3 color = input.Color.rgb * (face + edge * 0.35);
    return float4(color * mask, input.Color.a * mask);
}
float4 DustPS(PixelInput input) : COLOR0
{
    float2 p = (input.TexCoord - 0.5) * 2.0;
    float r2 = dot(p,p);
    float mask = exp(-r2 * 4.0) * (1.0 - smoothstep(0.4, 1.0, r2));
    return float4(input.Color.rgb * mask, input.Color.a * mask);
}
technique Rock
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL RockPS();
    }
}
technique Dust
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL DustPS();
    }
}

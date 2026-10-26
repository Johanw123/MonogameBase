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
    // IMPORTANT: Set Address modes to Wrap for tiling
    // Valid HLSL values: Clamp, Wrap, Mirror, Border, MirrorOnce
    AddressU = Wrap;
    AddressV = Wrap;
    //Filter = Linear;
    Filter = Linear;
};

// Procedural atmosphere is evaluated only on the opaque far layer.
float3 FogColor = float3(0.1, 0.22, 0.42);
float VisualTime = 0;
float BackgroundBrightness = 1;
float BackgroundSaturation = 1;
float FogDensity = 0;
float FogScale = 6;
float FogSpeed = 0.035;
float FogCoverage = 0.55;
float CloudWarp = 0;
float Twinkle = 0;
float TwinkleSpeed = 0.65;

float noiseHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float noise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(noiseHash(i), noiseHash(i + float2(1,0)), f.x),
                lerp(noiseHash(i + float2(0,1)), noiseHash(i + 1), f.x), f.y);
}
float cloudNoise(float2 p)
{
    return noise(p) * 0.57 + noise(p * 2.03 + 13.1) * 0.29 + noise(p * 4.11 + 7.7) * 0.14;
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
    // Distort clouds only: sharp foreground stars never swim with the nebula.
    if (CloudWarp > 0)
        uv += float2(sin(uv.y * 19 + VisualTime * 0.06),
                     cos(uv.x * 17 - VisualTime * 0.045)) * CloudWarp;
    float4 color = tex2D(SpriteTextureSampler, uv);
    float grey = dot(color.rgb, float3(0.2126, 0.7152, 0.0722));
    color.rgb = lerp(grey.xxx, color.rgb, BackgroundSaturation) * BackgroundBrightness;
    if (Twinkle > 0)
    {
        float phase = sin(uv.x * 213.0 + uv.y * 167.0) * 6.28;
        color.rgb *= 1.0 + sin(VisualTime * TwinkleSpeed + phase) * Twinkle;
    }
    if (FogDensity > 0)
    {
        float2 flow = float2(VisualTime * FogSpeed, -VisualTime * FogSpeed * 0.37);
        float2 p = uv * FogScale;
        float warp = noise(p * 0.7 + flow * 0.4);
        float fog = cloudNoise(p + flow + warp * 1.4);
        fog = smoothstep(0.7 - FogCoverage * 0.6, 1.0 - FogCoverage * 0.35, fog);
        color.rgb = lerp(color.rgb, FogColor, fog * FogDensity);
    }
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

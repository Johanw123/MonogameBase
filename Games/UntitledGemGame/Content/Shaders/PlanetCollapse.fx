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

// The planet sprite as a failed core extraction destabilises it (GameScreen.TimeLoop.cs):
// molten fissures spread over its turning surface and burn hotter, then the surface is
// dragged into the core, twisting and pinching inward until the planet is a white-hot
// point. Drawn with SpriteBatch over one frame of the planet sheet; premultiplied
// output (BlendState.AlphaBlend), tinted by the vertex colour.
#define DISC 0.3229 // the opaque disc's radius in frame UVs (31 of 96 texels)

Texture2D SpriteTexture;
sampler2D PlanetSampler = sampler_state
{
    Texture = <SpriteTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    Filter = Linear;
};

float4x4 view_projection;
float4 FrameRect; // the frame within the sheet: UV origin (xy) and size (zw)
float Rotation;   // the planet's spin for this frame, radians
float Crack;      // 0-1: how far the fissures have spread
float Heat;       // 0-1: how hot the rock and fissures burn
float Crush;      // 0-1: how far the surface has been dragged into the core
float Time;

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

float Hash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float Noise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
             lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
             lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

// Thin bright ridges where the noise crosses its midpoint: a web of fissures.
float Ridge(float3 q, float sharpness)
{
    return pow(1.0 - abs(2.0 * Noise(q) - 1.0), sharpness);
}

float4 MainPS(PixelInput input) : COLOR
{
    float2 local = (input.TexCoord - FrameRect.xy) / FrameRect.zw;
    float2 p = (local - 0.5) / DISC;
    float r = length(p);

    // The crush: the surface spirals into the core, tighter and faster toward the centre,
    // and is pinched inward (each pixel shows rock from further out).
    float inner = saturate(1.0 - r);
    float twist = Crush * (1.2 + 3.5 * Crush) * inner * inner;
    float s = sin(twist);
    float c = cos(twist);
    float2 q2 = float2(p.x * c - p.y * s, p.x * s + p.y * c);
    q2 *= 1.0 + Crush * 0.9 * inner;
    float2 sampleLocal = saturate(0.5 + q2 * DISC);
    float4 color = tex2D(PlanetSampler, FrameRect.xy + sampleLocal * FrameRect.zw) * input.Color;

    float rq = length(q2);
    float disc = 1.0 - smoothstep(0.96, 1.0, rq);
    // The visible hemisphere, turned back into planet-fixed coordinates so the
    // fissures ride the surface as it spins.
    float z = sqrt(saturate(1.0 - rq * rq));
    float3 view = float3(q2.x, -q2.y, z);
    float sr = sin(Rotation);
    float cr = cos(Rotation);
    float3 surface = float3(view.x * cr - view.z * sr, view.y, view.x * sr + view.z * cr);

    // Fissures open across the globe as Crack grows, from scattered seeds outward.
    float spread = Noise(surface * 1.6 + 11.0);
    float open = 1.0 - smoothstep(Crack * 1.25 - 0.15, Crack * 1.25, spread);
    float sharp = lerp(18.0, 5.0, Heat);
    float flicker = 0.8 + 0.2 * sin(Time * 23.0 + spread * 40.0);
    float veins = (Ridge(surface * 2.6, sharp) + 0.6 * Ridge(surface * 6.3 + 5.0, sharp * 1.4)) * open * flicker;
    float limb = smoothstep(1.0, 0.75, rq);

    float3 hot = lerp(float3(1.0, 0.36, 0.08), float3(1.0, 0.93, 0.8), saturate(Heat * 1.2 - 0.2));
    float luma = dot(color.rgb, float3(0.3, 0.55, 0.15));
    // The rock itself glows like a coal as it heats, then goes white as it collapses.
    color.rgb = lerp(color.rgb, luma * hot * 1.6, Heat * 0.55);
    float3 emissive = hot * veins * limb * (0.55 + 1.6 * Heat);
    emissive += hot * pow(saturate(rq), 5.0) * Heat * 0.6; // the limb burns as it caves in
    color.rgb += emissive * color.a * disc;
    float whiteout = Crush * Crush;
    color.rgb = lerp(color.rgb, float3(1.0, 0.95, 0.88) * color.a, whiteout * 0.85);
    return color;
}

technique PlanetCollapse
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

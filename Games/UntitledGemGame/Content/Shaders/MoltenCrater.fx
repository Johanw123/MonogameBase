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

// Molten craters (Thermite Rounds, Incendiary Warheads, Firestorm) burned into the
// planet. Each crater is one sprite quad, but the shader works on the planet
// sprite's texel grid, so a crater reads as part of the pixel art: an irregular
// scorched patch, squashed toward the limb like a mark on a sphere, whose molten
// core churns and cools through a stepped lava palette, then crumbles away.
//
// Vertex colour: r = life (1 fresh, 0 gone), g = seed, b = radius in texels / 32.
// The quad's half size is radius * QUAD_SCALE + QUAD_PAD texels
// (CraterQuadScale and CraterQuadPad in GameScreen.TalentCombos.cs).
// Output is premultiplied (BlendState.AlphaBlend).
#define QUAD_SCALE 1.6
#define QUAD_PAD 2.0

float4x4 view_projection;
float2 PlanetCenter;   // world units, including the planet's shake
float TexelSize;       // world units per planet texel
float DiscRadius;      // texels from the centre that stay inside the planet's outline
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
    float2 World : TEXCOORD1;
};

PixelInput SpriteVertexShader(VertexInput v)
{
    PixelInput output;
    output.Position = mul(v.Position, view_projection);
    output.Color = v.Color;
    output.TexCoord = v.TexCoord;
    output.World = v.Position.xy;
    return output;
}

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float Noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = Hash(i);
    float b = Hash(i + float2(1.0, 0.0));
    float c = Hash(i + float2(0.0, 1.0));
    float d = Hash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// 4x4 ordered dither threshold in [0, 1): the lava's palette steps break up into
// texel patterns instead of smooth gradients.
float Bayer2(float2 a)
{
    a = floor(a);
    return frac(a.x / 2.0 + a.y * a.y * 0.75);
}

float Bayer4(float2 a)
{
    return Bayer2(0.5 * a) * 0.25 + Bayer2(a);
}

// Cooled crust, then dull red, orange, yellow and white-hot.
float3 LavaColor(float level)
{
    if (level >= 4.0) return float3(1.0, 0.9, 0.66);
    if (level >= 3.0) return float3(1.0, 0.7, 0.28);
    if (level >= 2.0) return float3(0.92, 0.36, 0.1);
    if (level >= 1.0) return float3(0.52, 0.12, 0.07);
    return float3(0.17, 0.1, 0.09);
}

float4 MainPS(PixelInput input) : COLOR
{
    float life = input.Color.r;
    float seed = input.Color.g * 255.0;
    float radius = input.Color.b * 32.0;
    float extent = radius * QUAD_SCALE + QUAD_PAD;

    // Everything below is in planet texels, measured from the planet's centre.
    float2 rel = (input.World - PlanetCenter) / TexelSize;
    float2 texel = floor(rel) + 0.5;
    if (dot(texel, texel) > DiscRadius * DiscRadius)
        return float4(0.0, 0.0, 0.0, 0.0);
    float2 center = rel - (input.TexCoord - 0.5) * 2.0 * extent;
    float2 d = texel - center;

    // Seen on a sphere, a mark near the limb is squashed toward the planet's centre.
    float reach = length(center) / DiscRadius;
    if (reach > 0.001)
    {
        float2 radial = normalize(center);
        float squash = sqrt(saturate(1.0 - reach * reach));
        d += radial * dot(d, radial) * (1.0 / max(squash, 0.35) - 1.0);
    }

    // An irregular outline with ragged texels along its edge.
    float angle = atan2(d.y, d.x);
    float shape = 1.0 + 0.22 * sin(2.0 * angle + seed) + 0.14 * sin(3.0 * angle + seed * 1.7)
        + 0.08 * sin(5.0 * angle + seed * 2.3);
    float q = (length(d) + (Hash(texel + seed) - 0.5) * 0.9) / (radius * shape);

    // Fades crumble texel by texel in a random order, like ash, rather than in a
    // regular dither pattern; the ordered dither only blends the lava's colour steps.
    float grain = Hash(texel * 1.37 + seed * 0.13);
    float dither = Bayer4(texel);
    // The last part of its life the whole crater crumbles away.
    float fade = saturate(life * 5.0);
    if (q > 1.45 || grain >= fade)
        return float4(0.0, 0.0, 0.0, 0.0);

    // Soot scorched around the crater, thinning out toward its edge.
    float3 color = float3(0.09, 0.06, 0.06);
    float alpha = 0.5;
    if (grain >= saturate((1.45 - q) / 0.5))
        return float4(0.0, 0.0, 0.0, 0.0);

    if (q < 0.85)
    {
        // Hottest in the middle, broken up by slowly churning lava that cools
        // over the crater's life. The churn steps at 6 fps, like sprite frames.
        float churn = Noise(texel * 0.75 + float2(seed * 3.1, floor(Time * 6.0) * 0.37));
        float heat = (1.0 - q / 0.75) * 1.25 + (churn - 0.5) * 1.1;
        heat *= pow(saturate(life), 0.7) * 1.1;
        heat += saturate((life - 0.85) * 6.0) * (1.0 - q);
        float level = floor(saturate(heat) * 4.0 + (dither - 0.5) * 0.7);
        color = LavaColor(level);
        alpha = level >= 1.0 ? 1.0 : 0.85;
    }
    return float4(color * alpha, alpha);
}

technique MoltenCrater
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

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
    #define VS_SHADERMODEL vs_4_0_level_9_3
    #define PS_SHADERMODEL ps_4_0_level_9_3
#endif

// The cannon's incendiary plasma round (DrawIncendiaryRounds in GameScreen.Weapons.cs):
// a small white-hot slug with a flickering flame tail that cools from yellow through
// orange to deep red, shedding a few embers as it flies. It is drawn additively and
// bloomed, so it glows without a sprite.
//
// Each round is one quad: its x axis runs along the flight (u = 1 at the front) and it
// is ASPECT times longer than it is tall, with the slug at u = HEAD (RoundAspect and
// RoundHead in GameScreen.Weapons.cs). Vertex colour: r = heat (0.5 an automatic round,
// 1 a critical hit: hotter and whiter), g = seed, a = fade.
// Output is premultiplied, for additive One/One blending.
#define ASPECT 4.0
#define HEAD 0.75
#define TAIL 2.9
#define EMBERS 3

float4x4 view_projection;
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

// Deep red, orange, yellow, then white-hot.
float3 FireColor(float heat)
{
    float3 red = float3(0.6, 0.07, 0.03);
    float3 orange = float3(1.0, 0.38, 0.07);
    float3 yellow = float3(1.0, 0.8, 0.35);
    float3 white = float3(1.0, 0.97, 0.9);
    heat = saturate(heat);
    if (heat < 0.4) return lerp(red, orange, heat / 0.4);
    if (heat < 0.75) return lerp(orange, yellow, (heat - 0.4) / 0.35);
    return lerp(yellow, white, (heat - 0.75) / 0.25);
}

float4 MainPS(PixelInput input) : COLOR
{
    float hot = input.Color.r;
    float seed = input.Color.g * 255.0;
    float fade = input.Color.a;
    // In units of the round's height: x along the flight from the slug, y across it.
    float2 p = float2((input.TexCoord.x - HEAD) * ASPECT, input.TexCoord.y - 0.5);

    // The slug, and a soft glow around it.
    float slug = exp(-(p.x * p.x / 0.09 + p.y * p.y / 0.012));
    float glow = 0.3 * exp(-(p.x * p.x / 0.3 + p.y * p.y / 0.05));

    // The flame tail narrows and cools behind it, licking at the edges.
    float behind = saturate(-p.x / TAIL);
    float flicker = Noise(float2(p.x * 3.0 + Time * 26.0 + seed, p.y * 7.0 + seed * 0.37));
    float width = (0.17 * (1.0 - behind) + 0.03) * (0.7 + 0.6 * flicker);
    float tail = p.x < 0.0
        ? saturate(1.0 - abs(p.y) / width) * pow(1.0 - behind, 1.6) * (0.55 + 0.45 * flicker)
        : 0.0;

    // A few embers shed off the tail, drifting outward as they fall behind and fade.
    float embers = 0.0;
    for (int i = 0; i < EMBERS; i++)
    {
        float phase = frac(Time * 2.3 + Hash(float2(seed, i)));
        float2 at = float2(-phase * TAIL * 0.9, (Hash(float2(i, seed)) - 0.5) * 0.7 * phase);
        float2 d = p - at;
        embers += exp(-dot(d, d) / 0.0025) * (1.0 - phase);
    }

    float heat = slug + glow + 0.8 * tail + 0.6 * embers;
    // Fade out before the quad's edges.
    float2 uv = input.TexCoord;
    float edges = smoothstep(0.0, 0.06, uv.x) * smoothstep(0.0, 0.06, 1.0 - uv.x)
        * smoothstep(0.0, 0.2, 0.5 - abs(uv.y - 0.5));
    float strength = saturate(heat) * edges * fade;
    float3 color = FireColor(heat * (0.7 + 0.45 * hot));
    return float4(color * strength, strength);
}

technique IncendiaryRound
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

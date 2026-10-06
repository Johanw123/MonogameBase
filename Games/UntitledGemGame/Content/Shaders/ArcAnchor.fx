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

// The Arc Harpoon's anchor, drawn as pure energy on sprite quads (no texture).
// Two shapes, picked by the vertex colour's blue channel:
//  - b = 0: the lance, a slim diamond of plasma pointing along +x whose energy
//    streams back from its tip. Its tip is at x = LANCE_TIP and its tail at
//    x = LANCE_TAIL (quad units, -1..1; ArcHarpoonLanceTip/Tail in C#).
//  - b > 0: the ring lying on the planet's surface around the anchor, x pointing
//    away from the planet's centre; b is how much the sphere squashes it along x.
//    Three arcs turn around it and grow as the charge builds; a pulse throws the
//    ring outward.
// Vertex colour: r = charge (0..1), g = pulse flash (0..1), a = fade.
// Output is premultiplied, for additive blending (One, One).
#define LANCE_TIP 0.95
#define LANCE_TAIL -0.5

float4x4 view_projection;
float Time;

static const float3 Glow = float3(0.27, 0.86, 1.0);
static const float3 Hot = float3(0.85, 0.98, 1.0);

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

// Signed distance to a rhombus with half diagonals b (after Inigo Quilez).
float SdRhombus(float2 p, float2 b)
{
    p = abs(p);
    float2 w = b - 2.0 * p;
    float h = clamp((w.x * b.x - w.y * b.y) / dot(b, b), -1.0, 1.0);
    float d = length(p - 0.5 * b * float2(1.0 - h, 1.0 + h));
    return d * sign(p.x * b.y + p.y * b.x - b.x * b.y);
}

float4 Lance(float2 p, float charge, float flash)
{
    // A long point forward and a short tail, from one stretched diamond.
    float centre = (LANCE_TIP + LANCE_TAIL) * 0.5;
    float front = LANCE_TIP - centre;
    float back = centre - LANCE_TAIL;
    float2 q = p - float2(centre, 0.0);
    float stretch = q.x > 0.0 ? front : back;
    float d = SdRhombus(float2(q.x / stretch, q.y), float2(1.0, 0.24)) * stretch;

    float inside = smoothstep(0.015, -0.015, d);
    // Energy streams back from the tip in flickering bands.
    float flow = Noise(float2(p.x * 7.0 + Time * 6.0, p.y * 14.0 + Time * 2.0));
    // A soft, matte glow: a faint bright spine and edge rather than glassy highlights.
    float spine = exp(-abs(p.y) * 18.0) * smoothstep(LANCE_TAIL, LANCE_TIP, p.x);
    float rim = exp(-abs(d) * 24.0);
    float halo = exp(-max(d, 0.0) * 10.0) * 0.25;
    float energy = inside * (0.38 + 0.22 * flow) + rim * 0.25 + halo + spine * inside * 0.25;
    energy *= 0.6 + 0.3 * charge + 0.7 * flash;
    float3 color = lerp(Glow, Hot, saturate(spine * 0.35 + 0.4 * flash));
    return float4(color * energy, energy);
}

float4 Ring(float2 p, float squash, float charge, float flash)
{
    float2 r = float2(p.x / squash, p.y);
    float radius = length(r);
    float angle = atan2(r.y, r.x);
    float ringRadius = 0.5 + 0.38 * flash;
    float band = exp(-pow((radius - ringRadius) * 16.0, 2.0));
    // Three arcs turn around the ring and lengthen as the charge builds.
    float slot = frac((angle + Time * 1.7) / 6.2831853 * 3.0);
    float arcLength = 0.3 + 0.55 * charge;
    float arc = smoothstep(0.0, 0.05, slot) * smoothstep(0.0, 0.05, arcLength - slot);
    float faint = 0.12 + 0.2 * charge;
    float glowCore = exp(-radius * 8.0) * (0.3 + 0.7 * max(charge * 0.5, flash));
    float energy = band * lerp(faint, 1.0, arc) * (0.6 + 0.4 * charge + flash) + glowCore;
    float3 color = lerp(Glow, Hot, saturate(flash + glowCore));
    return float4(color * energy, energy);
}

float4 MainPS(PixelInput input) : COLOR
{
    float2 p = (input.TexCoord - 0.5) * 2.0;
    float charge = input.Color.r;
    float flash = input.Color.g;
    float4 result = input.Color.b < 0.1
        ? Lance(p, charge, flash)
        : Ring(p, input.Color.b, charge, flash);
    // Fade out toward the quad's edge so it never shows.
    result *= saturate((1.0 - max(abs(p.x), abs(p.y))) * 6.0) * input.Color.a;
    return result;
}

technique ArcAnchor
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

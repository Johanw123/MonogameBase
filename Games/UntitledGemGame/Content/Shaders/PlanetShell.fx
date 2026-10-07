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

// The dark shell sealing the planet at the start of a run (GameScreen.PlanetShell.cs).
// It works on the planet sprite's texel grid, so it reads as pixel art: a near-black
// sphere turning with the planet, lit faintly from the upper left, with a violet glow
// around its edge. Damage opens cracks along the seams between its plates (layout in
// PlanetShell.cs); light from the planet inside shines through them.
//
// Each draw is one quad covering a whole planet frame (FRAME texels). Vertex colour:
// r = the one plate to draw (a fragment flying off after the shell bursts), or 1 for
// the whole shell; a = opacity. Output is premultiplied (BlendState.AlphaBlend).
#define FRAME 96.0
#define RADIUS 31.0
#define PLATES 14
#define ORIGINS 12
#define SEAM_WARP 0.045
#define SEAM_FREQUENCY 11.0
#define CRACK_REACH 0.9

float4x4 view_projection;
float Rotation;   // the planet sprite frame's turn, radians
float Wear;       // seams whose crack wear is at most this have cracked open
float Glow;       // 0-1: light flooding the cracks as the shell strains
float Flash;      // 0-1: hit pulse
float Time;
float3 Plates[PLATES];
float4 CrackOrigins[ORIGINS];

static const float3 Light = float3(-0.5, 0.62, 0.6);
static const float3 RimColor = float3(0.55, 0.32, 1.0);
static const float3 CrackColor = float3(0.68, 1.0, 0.93);

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

float Bayer2(float2 a)
{
    a = floor(a);
    return frac(a.x / 2.0 + a.y * a.y * 0.75);
}

float Bayer4(float2 a)
{
    return Bayer2(0.5 * a) * 0.25 + Bayer2(a);
}

bool Inside(float2 t)
{
    return dot(t, t) <= RADIUS * RADIUS;
}

// Seen from the viewer: x right, y up, z toward the viewer.
float3 ViewNormal(float2 t)
{
    float2 xy = float2(t.x, -t.y) / RADIUS;
    return float3(xy, sqrt(saturate(1.0 - dot(xy, xy))));
}

// The same point in the planet's own frame, undoing its turn (turn = cos, sin of Rotation).
float3 PlanetPoint(float3 n, float2 turn)
{
    return float3(n.x * turn.x - n.z * turn.y, n.y, n.x * turn.y + n.z * turn.x);
}

// Matches PlanetShell.PlateAt.
float PlateAt(float2 t, float2 turn)
{
    float3 p = PlanetPoint(ViewNormal(t), turn);
    float3 warped = p + SEAM_WARP * sin(SEAM_FREQUENCY * p.yzx + float3(1.3, 0.4, 2.1));
    float best = -2.0;
    float plate = 0.0;
    for (int i = 0; i < PLATES; i++)
    {
        float d = dot(warped, Plates[i]);
        if (d > best)
        {
            best = d;
            plate = i;
        }
    }
    return plate;
}

// Matches PlanetShell.CrackWear.
float CrackWear(float3 p)
{
    float wear = 100.0;
    for (int i = 0; i < ORIGINS; i++)
    {
        float spread = acos(clamp(dot(p, CrackOrigins[i].xyz), -1.0, 1.0)) / CRACK_REACH;
        wear = min(wear, CrackOrigins[i].w + spread * spread);
    }
    return wear;
}

float3 BodyColor(float level)
{
    if (level >= 4.0) return float3(0.235, 0.165, 0.41);
    if (level >= 3.0) return float3(0.15, 0.11, 0.25);
    if (level >= 2.0) return float3(0.094, 0.07, 0.157);
    if (level >= 1.0) return float3(0.055, 0.043, 0.094);
    return float3(0.031, 0.024, 0.055);
}

float4 MainPS(PixelInput input) : COLOR
{
    float alpha = input.Color.a;
    float onlyPlate = round(input.Color.r * 255.0);
    bool fragment = onlyPlate < 255.0;
    // Texel centres, in texels from the planet's centre (y down).
    float2 t = floor(input.TexCoord * FRAME) - FRAME * 0.5 + 0.5;
    float2 grid = t + FRAME * 0.5 - 0.5;

    if (!Inside(t))
    {
        if (fragment) return float4(0.0, 0.0, 0.0, 0.0);
        // A soft violet halo in stepped rings, like the planet sprite's own glow.
        float band = floor((length(t) - RADIUS) / 3.0);
        float halo = band < 1.0 ? 0.22 : band < 2.0 ? 0.11 : band < 3.0 ? 0.04 : 0.0;
        halo *= (0.85 + 0.15 * sin(Time * 1.3)) * (1.0 + 1.5 * Glow + Flash);
        return float4(RimColor * halo, halo * 0.5) * alpha;
    }

    float2 turn = float2(cos(Rotation), sin(Rotation));
    float3 n = ViewNormal(t);
    float3 p = PlanetPoint(n, turn);
    float plate = PlateAt(t, turn);
    if (fragment && plate != onlyPlate) return float4(0.0, 0.0, 0.0, 0.0);

    // Near black, a little lighter toward the light, with a faint gloss and a violet
    // sheen toward the edge. A dim pattern drifts through it, stepping at 6 fps.
    float lit = saturate(dot(n, normalize(Light)));
    float gloss = pow(saturate(dot(n, normalize(normalize(Light) + float3(0.0, 0.0, 1.0)))), 18.0);
    float edge = 1.0 - n.z;
    float swirl = sin(p.y * 6.0 + sin(p.x * 4.0 + p.z * 3.0 + floor(Time * 6.0) / 12.0) * 1.8);
    float level = 0.3 + 1.3 * lit + 2.4 * edge * edge * edge + 1.6 * gloss + 0.7 * saturate((swirl - 0.6) / 0.4) + Flash;
    level = floor(clamp(level + (Bayer4(grid) - 0.5) * 0.8, 0.0, 4.0));
    float3 color = BodyColor(level);
    if (dot(t, t) > (RADIUS - 1.2) * (RADIUS - 1.2))
        color = RimColor * 0.45 * (1.0 + Flash + Glow);

    // Seams: one texel wide, where the texel to the right or below is another plate.
    // A fragment shows its whole outline instead, still glowing from the burst.
    float2 right = t + float2(1.0, 0.0), below = t + float2(0.0, 1.0);
    bool seam = (Inside(right) && PlateAt(right, turn) != plate) || (Inside(below) && PlateAt(below, turn) != plate);
    if (fragment)
    {
        float2 left = t - float2(1.0, 0.0), above = t - float2(0.0, 1.0);
        bool outline = seam || !Inside(right) || !Inside(below) || !Inside(left) || !Inside(above)
            || PlateAt(left, turn) != plate || PlateAt(above, turn) != plate;
        if (outline)
            color = lerp(BodyColor(1.0), CrackColor * 1.2, Glow);
    }
    else if (seam)
    {
        float opened = CrackWear(p);
        if (Wear >= opened)
        {
            // The spreading tip burns brightest.
            float tip = saturate(1.0 - (Wear - opened) / 0.05);
            color = CrackColor * (0.55 + 0.45 * Glow + 0.6 * tip + 0.5 * Flash);
        }
    }
    return float4(color * alpha, alpha);
}

technique PlanetShell
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

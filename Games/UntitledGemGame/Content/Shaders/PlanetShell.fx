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

// The mysterious ancient containment shell sealing the planet at the start of a run
// (GameScreen.PlanetShell.cs). Rendered at full screen resolution as a faceted 3D
// polyhedral vault turning with the planet, ringed by a deep violet-indigo containment
// aura. Damage opens glowing fractures along the seams between its plates (layout in
// PlanetShell.cs), and the planet's electric cyan-blue crystal glow seeps through the
// cracks, across adjacent plates, and out into the surrounding halo.
//
// Each draw is one quad covering a whole planet frame (FRAME units). Vertex colour:
// r = the one plate to draw (a fragment flying off after the shell bursts), or 1 for
// the whole shell; a = opacity. Output is premultiplied (BlendState.AlphaBlend).
#define FRAME 96.0
#define RADIUS 31.0
#define PLATES 14
#define ORIGINS 12
#define SEAM_WARP 0.045
#define SEAM_FREQUENCY 11.0
#define CRACK_REACH 0.9

// Manual tuning values for the planet shell and its glowing cracks:
#define CRACK_WIDTH 0.007          // Half-width of the bright inner crack line along a seam
#define CRACK_SEEP_WIDTH 0.018     // How far the soft blue glow seeps out from a cracked seam
#define CRACK_GLOW_STRENGTH 0.72   // Brightness of the blue light seeping through the cracks
#define SHELL_RIM_STRENGTH 0.24    // Intensity of the soft violet atmospheric rim around the shell

float4x4 view_projection;
float Rotation;   // the planet sprite frame's turn, radians
float Wear;       // seams whose crack wear is at most this have cracked open
float Glow;       // 0-1: light flooding the cracks as the shell strains
float Flash;      // 0-1: hit pulse
float Time;
float3 Plates[PLATES];
float4 CrackOrigins[ORIGINS];

static const float3 Light = float3(-0.52, 0.60, 0.61);
static const float3 RimColor = float3(0.50, 0.30, 0.95);
static const float3 SeepBlue = float3(0.08, 0.58, 0.98);
static const float3 CrackCyan = float3(0.32, 0.86, 1.00);
static const float3 CrackCore = float3(0.78, 0.96, 1.00);

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

// Rotates a planet-space vector into view space.
float3 ViewPoint(float3 p, float2 turn)
{
    return float3(p.x * turn.x + p.z * turn.y, p.y, -p.x * turn.y + p.z * turn.x);
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

float4 MainPS(PixelInput input) : COLOR
{
    float alpha = input.Color.a;
    float onlyPlate = round(input.Color.r * 255.0);
    bool fragment = onlyPlate < 255.0;

    // Continuous coordinates in frame units from the planet's centre (y down).
    float2 t = (input.TexCoord - 0.5) * FRAME;
    float r = length(t);
    float2 turn = float2(cos(Rotation), sin(Rotation));

    if (r > RADIUS)
    {
        if (fragment) return float4(0.0, 0.0, 0.0, 0.0);

        // Clean, uniform soft violet halo around the shell.
        float dist = r - RADIUS;
        float haloFalloff = exp(-dist * 0.28) * smoothstep(12.0, 0.0, dist);
        float pulse = 0.90 + 0.10 * sin(Time * 1.5);
        float3 haloCol = lerp(RimColor, CrackCyan, Glow * 0.5);
        float haloStrength = haloFalloff * pulse * SHELL_RIM_STRENGTH * (1.0 + 1.2 * Glow + 0.5 * Flash);
        return float4(haloCol * haloStrength, haloStrength * 0.5) * alpha;
    }

    float3 n = ViewNormal(t);
    float3 p = PlanetPoint(n, turn);
    float3 warped = p + SEAM_WARP * sin(SEAM_FREQUENCY * p.yzx + float3(1.3, 0.4, 2.1));

    // Find the owning plate and nearest neighbour in one pass so seamDist is continuous.
    float best1 = -2.0;
    float best2 = -2.0;
    float plate = 0.0;
    float3 plateDir = float3(0.0, 0.0, 1.0);
    for (int i = 0; i < PLATES; i++)
    {
        float d = dot(warped, Plates[i]);
        if (d > best1)
        {
            best2 = best1;
            best1 = d;
            plate = i;
            plateDir = Plates[i];
        }
        else if (d > best2)
        {
            best2 = d;
        }
    }

    float seamDist = best1 - best2;
    float discAA = smoothstep(RADIUS, RADIUS - 0.45, r);

    if (fragment)
    {
        if (abs(plate - onlyPlate) > 0.5) return float4(0.0, 0.0, 0.0, 0.0);
        discAA *= smoothstep(0.0, 0.012, seamDist);
        if (discAA <= 0.001) return float4(0.0, 0.0, 0.0, 0.0);
    }

    // Smooth dark sphere with subtle 3D faceted plate normals and recessed seam grooves.
    float3 plateViewN = ViewPoint(plateDir, turn);
    float3 facetN = normalize(lerp(n, plateViewN, 0.28));
    float3 lightDir = normalize(Light);
    float3 halfVec = normalize(lightDir + float3(0.0, 0.0, 1.0));

    float diffSphere = saturate(dot(n, lightDir));
    float diffFacet = saturate(dot(facetN, lightDir));
    float specFacet = pow(saturate(dot(facetN, halfVec)), 24.0);
    float fresnel = pow(saturate(1.0 - n.z), 2.8);

    // Subtle dark groove along plate seams so interlocking plates are faintly visible.
    float seamGroove = smoothstep(0.003, 0.028, seamDist);

    float3 darkBase = float3(0.030, 0.024, 0.056);
    float3 litPlate = float3(0.11, 0.085, 0.20) * (0.55 * diffSphere + 0.45 * diffFacet)
                    + float3(0.20, 0.15, 0.36) * (specFacet * 0.55);
    float3 color = (darkBase + litPlate) * (0.58 + 0.42 * seamGroove);
    color += RimColor * (fresnel * 0.32 * (1.0 + 0.4 * Flash));

    // Cracks open along the seams and seep a tight blue-cyan glow right along the fissure edges.
    float opened = CrackWear(p);
    if (fragment)
    {
        float edgeGlow = exp(-seamDist / CRACK_SEEP_WIDTH) + (1.0 - smoothstep(RADIUS - 1.2, RADIUS - 0.2, r));
        color += (SeepBlue * 0.8 + CrackCyan * 0.5) * saturate(edgeGlow) * Glow * CRACK_GLOW_STRENGTH;
    }
    else
    {
        // Smoothly taper crack openness from 0 at the advancing tip to 1 along the opened seam.
        float crackOpen = smoothstep(0.0, 0.08, Wear - opened);
        crackOpen = max(crackOpen, Glow);

        if (crackOpen > 0.001)
        {
            float tip = saturate(1.0 - max(0.0, Wear - opened) / 0.05) * step(opened, Wear);

            // Soft blue light seeping tightly from the crack edges (fades within a couple texels of the seam).
            float seepWidth = (CRACK_SEEP_WIDTH + 0.014 * Glow) * (0.35 + 0.65 * crackOpen);
            float seep = exp(-seamDist / max(seepWidth, 0.0001)) * crackOpen;

            // Thin bright fissure core right on the seam, tapering to a point at the tip.
            float coreWidth = (CRACK_WIDTH + 0.005 * Glow) * crackOpen;
            float fissure = 1.0 - smoothstep(0.001, max(coreWidth, 0.0015), seamDist);
            float innerCore = 1.0 - smoothstep(0.0, max(coreWidth * 0.45, 0.0008), seamDist);

            float3 seepLight = (SeepBlue * 0.85 + CrackCyan * 0.45 * fissure) * seep * CRACK_GLOW_STRENGTH;
            color += seepLight * (1.0 + 0.35 * tip + 0.35 * Flash + 0.40 * Glow);

            float3 coreCol = lerp(CrackCyan, CrackCore, innerCore) * (0.75 + 0.25 * CRACK_GLOW_STRENGTH + 0.35 * tip + 0.35 * Glow);
            color = lerp(color, coreCol, fissure * crackOpen);
        }
    }

    // Subtle rim highlight at the edge of the sphere.
    float rimBand = smoothstep(RADIUS - 1.4, RADIUS - 0.2, r);
    color = lerp(color, color + RimColor * (0.38 + 0.35 * Glow + 0.35 * Flash), rimBand * 0.6);

    color += float3(0.12, 0.16, 0.28) * Flash;
    float finalAlpha = alpha * discAA;
    return float4(color * finalAlpha, finalAlpha);
}

technique PlanetShell
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

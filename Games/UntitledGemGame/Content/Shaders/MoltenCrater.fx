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

// Molten craters (Thermite Rounds, Incendiary Warheads, Firestorm) and laser magma
// scars burned into the planet. Each spot is one sprite quad projected onto the
// curved 3D planet disc: squashed toward the limb like a mark on a sphere, with a
// dark scorched rim and a churning molten core that cools through glowing magma
// veins before eroding away.
//
// Vertex colour: r = life (1 fresh, 0 gone), g = seed, b = radius in frame units / 32,
// a = soot strength (1 for a crater; the laser's scars, many small overlapping spots,
// keep only a trace so their soot doesn't bury the lava).
// The quad's half size is radius * QUAD_SCALE + QUAD_PAD frame units
// (CraterQuadScale and CraterQuadPad in GameScreen.TalentCombos.cs).
// Output is premultiplied (BlendState.AlphaBlend).
#define QUAD_SCALE 1.6
#define QUAD_PAD 2.0

float4x4 view_projection;
float2 PlanetCenter;   // world units, including the planet's shake
float TexelSize;       // world units per planet frame unit (96-unit frame)
float DiscRadius;      // frame units from the centre that stay inside the planet's outline
float Rotation;        // the planet's spin angle in radians
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

float Hash3(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.x + p.y) * p.z);
}

float Noise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = Hash3(i);
    float n100 = Hash3(i + float3(1.0, 0.0, 0.0));
    float n010 = Hash3(i + float3(0.0, 1.0, 0.0));
    float n110 = Hash3(i + float3(1.0, 1.0, 0.0));
    float n001 = Hash3(i + float3(0.0, 0.0, 1.0));
    float n101 = Hash3(i + float3(1.0, 0.0, 1.0));
    float n011 = Hash3(i + float3(0.0, 1.0, 1.0));
    float n111 = Hash3(i + float3(1.0, 1.0, 1.0));
    float4 n0 = lerp(float4(n000, n010, n001, n011), float4(n100, n110, n101, n111), f.x);
    float2 n1 = lerp(n0.xy, n0.zw, f.y);
    return lerp(n1.x, n1.y, f.z);
}

// Deep ember red, molten orange, bright yellow, and white-hot core.
float3 LavaColor(float heat)
{
    float3 darkRed = float3(0.44, 0.06, 0.02);
    float3 red     = float3(0.88, 0.18, 0.04);
    float3 orange  = float3(1.00, 0.50, 0.10);
    float3 yellow  = float3(1.00, 0.85, 0.36);
    float3 white   = float3(1.00, 0.97, 0.86);
    heat = saturate(heat);
    if (heat < 0.34) return lerp(darkRed, red, heat / 0.34);
    if (heat < 0.66) return lerp(red, orange, (heat - 0.34) / 0.32);
    if (heat < 0.88) return lerp(orange, yellow, (heat - 0.66) / 0.22);
    return lerp(yellow, white, (heat - 0.88) / 0.12);
}

float4 MainPS(PixelInput input) : COLOR
{
    float life = input.Color.r;
    float radius = input.Color.b * 32.0;
    float sootStrength = input.Color.a;
    float extent = radius * QUAD_SCALE + QUAD_PAD;

    // Continuous coordinates in planet frame units, measured from the planet's centre.
    float2 rel = (input.World - PlanetCenter) / TexelSize;
    float distFromCenter = length(rel);
    float discAA = smoothstep(DiscRadius, DiscRadius - 0.55, distFromCenter);
    if (discAA <= 0.001)
        return float4(0.0, 0.0, 0.0, 0.0);

    float2 center = rel - (input.TexCoord - 0.5) * 2.0 * extent;
    float2 xy = rel / DiscRadius;
    float3 viewNormal = float3(xy, sqrt(saturate(1.0 - dot(xy, xy))));
    float2 centerXY = center / DiscRadius;
    float3 centerNormal = float3(centerXY, sqrt(saturate(1.0 - dot(centerXY, centerXY))));

    float limbFade = smoothstep(0.02, 0.18, viewNormal.z) * smoothstep(0.02, 0.16, centerNormal.z);
    if (limbFade <= 0.001)
        return float4(0.0, 0.0, 0.0, 0.0);

    // 3D position on the planet's surface in frame units, undoing the planet's spin so the
    // volcanic contour and lava convection cells stay locked to the rotating globe and
    // overlapping spots along a scar share one seamless surface field.
    float2 turn = float2(cos(Rotation), sin(Rotation));
    float3 surfacePos = float3(
        viewNormal.x * turn.x - viewNormal.z * turn.y,
        viewNormal.y,
        viewNormal.x * turn.y + viewNormal.z * turn.x) * DiscRadius;

    // 3D chord distance on the sphere naturally foreshortens craters and scars toward the limb.
    float sphereDist = length(viewNormal - centerNormal) * DiscRadius;
    float warpLow = Noise3(surfacePos * 0.28 + float3(2.3, 7.1, 4.9)) - 0.5;
    float warpHigh = Noise3(surfacePos * 0.65 + float3(8.7, 1.9, 6.2)) - 0.5;
    float contour = 1.0 + warpLow * 0.34 + warpHigh * 0.18;
    float q = sphereDist / max(radius * contour, 0.15);

    // As life runs out, the spot cools, shrinks inward, and erodes smoothly.
    float erodeNoise = Noise3(surfacePos * 0.55 + float3(5.1, 3.4, 9.8));
    float fade = smoothstep(0.0, 0.22, life + (erodeNoise - 0.5) * 0.14 * saturate(1.0 - life))
        * limbFade * discAA;
    if (q > 1.38 || fade <= 0.001)
        return float4(0.0, 0.0, 0.0, 0.0);

    float shrink = 0.68 + 0.32 * pow(saturate(life), 0.45);
    float3 flowPos = surfacePos * 0.46;
    float n1 = Noise3(flowPos + float3(Time * 0.35, -Time * 0.28, Time * 0.22));
    float n2 = Noise3(flowPos * 2.05 + float3(-Time * 0.62, Time * 0.48, -Time * 0.51) + 4.7);
    float churn = n1 * 0.60 + n2 * 0.40;
    float veins = pow(saturate(1.0 - abs(n1 - 0.5) * 2.3), 1.6);
    float lifeHeat = pow(saturate(life), 0.55);

    // Pass 1 (sootStrength > 0.25): dark scorched volcanic basin and soot rim.
    if (sootStrength > 0.25)
    {
        float rockTex = Noise3(surfacePos * 0.85 + 1.7);
        float3 sootColor = lerp(float3(0.035, 0.022, 0.032), float3(0.095, 0.048, 0.042), rockTex)
            + float3(0.20, 0.040, 0.010) * (veins * lifeHeat * 0.45);
        float sootAlpha = smoothstep(1.36, 0.46, q) * saturate(sootStrength * 0.88) * fade;
        return float4(sootColor * sootAlpha, sootAlpha);
    }

    // Pass 2 (0.04 < sootStrength <= 0.25): deep red-to-orange churning magma bed.
    // Because bedColor depends only on the shared 3D surface point, overlapping spots fuse
    // into a continuous river or pool of lava without concentric rings.
    if (sootStrength > 0.04)
    {
        float bedQ = q / shrink;
        float bedAlpha = smoothstep(0.90, 0.36, bedQ) * fade;
        if (bedAlpha <= 0.001)
            return float4(0.0, 0.0, 0.0, 0.0);
        float bedHeat = saturate((0.28 + churn * 0.50 + veins * 0.34) * (0.35 + 0.65 * lifeHeat));
        float3 bedColor = LavaColor(bedHeat * 0.70);
        return float4(bedColor * bedAlpha, bedAlpha);
    }

    // Pass 3 (sootStrength <= 0.04): incandescent golden-yellow and white-hot core/fissures.
    float coreQ = q / shrink;
    float coreMask = smoothstep(0.58, 0.14, coreQ) * saturate(0.45 + 0.55 * churn + 0.45 * veins);
    float coreAlpha = coreMask * pow(saturate(life), 0.50) * fade;
    if (coreAlpha <= 0.001)
        return float4(0.0, 0.0, 0.0, 0.0);
    float freshFlash = saturate((life - 0.82) * 5.5);
    float coreHeat = saturate(0.68 + churn * 0.22 + veins * 0.24 + freshFlash * 0.25);
    float3 coreColor = LavaColor(coreHeat);
    return float4(coreColor * coreAlpha, coreAlpha);
}

technique MoltenCrater
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

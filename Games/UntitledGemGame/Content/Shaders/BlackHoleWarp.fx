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

// Full-screen gravity lens for the core extraction (GameScreen.TimeLoop.cs), applied to
// the rendered world before bloom and the HUD. It also draws the black hole itself: a
// plain black spot with a shader accretion disk and photon ring.
//  - Lens: light passing the collapsing core / black hole at distance r appears to come
//    from r - Lens / r, bending the field into an Einstein ring around the hole.
//  - Shock: the supernova's refracting shock front, with a faint bright, colour-split edge.
//  - Pull and Twist: the hole draws the scene in, with a swirl only close to the hole.
//  - Glow: the core's flash, local to the hole rather than a full-screen white-out.
//  - Fade: the swallowed scene fades into the void.
// Center is the hole in texture coordinates; distances are in screen heights
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
float2 Shake;         // sample offset while the shock rattles the view
float Horizon;        // event horizon radius
float Lens;           // Einstein radius squared
float Pull;           // suction toward the hole
float Twist;          // swirl at the hole, radians
float SwirlReach;     // how far the swirl reaches
float ShockRadius;
float ShockWidth;
float ShockStrength;  // refraction across the front
float ShockGlow;      // brightness of the front
float3 GlowColor;
float Glow;
float GlowRadius;
float Fade;
float DiskTime;       // drives the disk's orbit
float DiskGlow;       // 0-1 brightness of the accretion disk and photon ring
float DiskHeat;       // 0-1: a newborn disk burns white-hot
float DiskReach;      // the disk's outer edge, in horizon radii

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
    float r = max(length(d), 0.00001);
    float2 dir = d / r;

    // Bent light, then the shock front refracting what lies across it.
    float rs = r - Lens / r;
    float x = (r - ShockRadius) / max(ShockWidth, 0.0001);
    float ring = exp(-x * x);
    rs -= ShockStrength * x * ring;
    // Suction: sample further out, so the scene shrinks into the hole.
    rs *= 1.0 + Pull * saturate(1.0 - r / 2.2);

    // A swirl that only twists the space close to the hole.
    float swirl = Twist * exp(-r / max(SwirlReach, 0.0001));
    float s = sin(swirl);
    float c = cos(swirl);
    float2 sd = dir * rs;
    sd = float2(sd.x * c - sd.y * s, sd.x * s + sd.y * c);
    sd.x /= Aspect;
    float2 uv = Center + sd;
    float2 edge = min(uv, 1.0 - uv);
    float soft = 0.004 + 0.12 * saturate(Pull);
    float inside = smoothstep(-soft, 0.0, min(edge.x, edge.y));

    // Colour splits along the shock front.
    float split = ShockStrength * ring * 0.2;
    float2 splitDir = float2(dir.x / Aspect, dir.y) * split;
    uv += Shake;
    float3 color;
    color.r = tex2D(SceneSampler, uv + splitDir).r;
    color.g = tex2D(SceneSampler, uv).g;
    color.b = tex2D(SceneSampler, uv - splitDir).b;
    color *= inside;
    color += ring * ShockGlow * float3(0.8, 0.62, 1.0);

    float horizon = max(Horizon, 0.00001);
    color *= smoothstep(horizon * 0.94, horizon * 1.02, r);
    color += GlowColor * Glow * exp(-(r * r) / max(GlowRadius * GlowRadius, 0.000001));

    // The accretion disk, seen from above: spiral streaks of gas, the inner layer orbiting
    // faster than the outer (rigid layers, so the pattern never shears into rings),
    // white-hot at its inner edge and cooling to orange and violet further out.
    float rh = r / horizon;
    float angle = atan2(d.y, d.x);
    float lr = log(max(rh, 1.0));
    float innerSpin = angle + DiskTime * 1.7;
    float outerSpin = angle + DiskTime * 0.6;
    float innerArms = 0.5 + 0.5 * sin(innerSpin * 3.0 - lr * 8.0);
    float outerArms = 0.5 + 0.5 * sin(outerSpin * 5.0 - lr * 12.0 + 1.3);
    float grain = 0.5 + 0.5 * sin(innerSpin * 11.0 - lr * 21.0) * sin(outerSpin * 7.0 + lr * 15.0);
    float streaks = lerp(innerArms, outerArms, saturate((rh - 1.1) / 1.2)) * (0.6 + 0.4 * grain);
    float band = smoothstep(1.0, 1.1, rh) * (1.0 - smoothstep(1.35, max(DiskReach, 1.5), rh));
    float hot = saturate((1.9 - rh) / 0.8 + DiskHeat * 0.5);
    float3 diskColor = lerp(lerp(float3(0.45, 0.12, 0.6), float3(1.0, 0.45, 0.15), saturate(hot * 2.0)),
        float3(1.0, 0.9, 0.75), saturate(hot * 2.0 - 1.0));
    float disk = band * (0.15 + 1.1 * streaks * streaks) * (0.7 + 0.6 * DiskHeat);
    float ringDistance = (rh - 1.03) / 0.035;
    float photon = exp(-ringDistance * ringDistance);

    // The swallowed scene fades into the void; the hole's own light stays on top.
    color = lerp(color, float3(0.008, 0.0, 0.02), saturate(Fade));
    color += (diskColor * disk + float3(1.0, 0.88, 0.74) * photon * (0.9 + DiskHeat)) * DiskGlow;
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

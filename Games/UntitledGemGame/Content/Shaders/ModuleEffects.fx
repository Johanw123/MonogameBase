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

// The fleet's module effects and the ship's lighter weapon effects (ModuleEffectBatch.cs),
// drawn through SdfLineRenderer with the LaserBeam.fx vertex layout. Every effect is one
// quad around the segment PointA to PointB, padded by Thickness: the effect's reach in
// world units. All light fades to nothing within that reach, so the quad's edge never shows.
//
// Output is premultiplied (One, InverseSourceAlpha): rgb is added light, and alpha
// darkens what is behind it. Only the vortex's event horizon uses alpha.
//
//  ModuleBolt: lightning from PointA to PointB. It strikes outward from PointA, is
//    re-drawn in a new jagged shape many times a second and leaves a bright node where
//    it lands. PulseProgress = age over its life. CoreColor: r = seed, g = core width,
//    b = how far it strays (fraction of the reach), a = intensity. GlowColor tints it;
//    its alpha below 1 adds forks that leave the channel and die out (0.5 one, 0 two).
//
//  ModuleRing: a pulse around PointA. PulseProgress = age over its life. CoreColor:
//    r = style (0 shock front running outward, 0.5 field collapsing inward, 1 burst with
//    rays), g = seed, b = largest radius over the reach, a = intensity. GlowColor tints it.
//
//  ModuleBeam: energy flowing from PointA to PointB. CoreColor: r = seed, g = style
//    (below 0.5 a beam of packets, PulseProgress = age over its life, and the higher g
//    the leaner its halo, for crowds of beams; 1 plume that streams out of PointA and
//    thins to nothing at PointB, PulseProgress unused), b = half width over the reach,
//    a = intensity. GlowColor tints it.
//
//  ModuleVortex: a gravity well around PointA: spiral arms of matter falling into a
//    dark centre. PulseProgress = the event horizon's radius over the reach (0 for a
//    well without one). CoreColor: r = seed, g = spin, b = the arms' brightness,
//    a = intensity. GlowColor tints it.
//
//  ModuleGhost: a holographic dart at PointA, pointing at PointB. CoreColor:
//    r = seed, a = intensity. GlowColor tints it.
//
//  ModuleJet: a geyser bursting out of PointA toward PointB. PulseProgress = age over
//    its life. CoreColor: r = seed, b = half width at its mouth over the reach,
//    a = intensity. GlowColor tints it.
//
//  ModuleCrack: one piece of a molten fracture, from PointA to PointB. The pieces' quads
//    are cut where they meet, so a bent crack joins up without overlaps. PulseProgress =
//    how far along the whole crack the piece starts (0 its mouth, 1 its tip). CoreColor:
//    r = the crack's length so far over 64 (keeps the heat pattern unbroken), g = the
//    share of the crack the piece covers, b = half width over the reach, a = intensity.
//    GlowColor tints it.
//
//  ModuleLance: the streak a warp jump leaves from PointA, where the drive fired, to
//    PointB, where the ship went. PulseProgress = age over its life. CoreColor: r = seed,
//    b = half width over the reach, a = intensity. GlowColor tints it.
//
//  ModuleGlint: a star at PointA with four long rays, the first toward PointB, and four
//    short ones between them. PulseProgress = how much it twinkles. CoreColor: r = seed,
//    a = intensity. GlowColor tints it.
//
//  ModuleSlug: a railgun slug lodged in the ground: a steel rod from PointA, where it went
//    in, to its tail at PointB. Its body covers what is behind it. PulseProgress = how far it
//    has cooled (0 white hot as it lands, 1 bare steel). CoreColor: r = seed, g = a pulse of
//    charge on its way through (1 as it enters at PointA, 0 once it has left by the tail),
//    b = half width over the reach, a = intensity. GlowColor tints its glow.
//
//  ModuleLink: a spark that jumps from PointA to PointB and leaves a thread of light, which
//    lets go of PointA first. PulseProgress = age over its life. CoreColor: r = seed,
//    b = half width over the reach, a = intensity. GlowColor tints it.

float4x4 MatrixTransform;
float Time;

struct VertexShaderInput
{
    float4 Position      : POSITION0;
    float2 LocalPos      : TEXCOORD0;
    float2 PointA        : TEXCOORD1;
    float2 PointB        : TEXCOORD2;
    float Thickness      : TEXCOORD3;
    float PulseProgress  : TEXCOORD4;
    float4 CoreColor     : COLOR0;
    float4 GlowColor     : COLOR1;
};

struct VertexShaderOutput
{
    float4 Position      : SV_POSITION;
    float2 LocalPos      : TEXCOORD0;
    float2 PointA        : TEXCOORD1;
    float2 PointB        : TEXCOORD2;
    float Thickness      : TEXCOORD3;
    float PulseProgress  : TEXCOORD4;
    float4 CoreColor     : COLOR0;
    float4 GlowColor     : COLOR1;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.LocalPos = input.LocalPos;
    output.PointA = input.PointA;
    output.PointB = input.PointB;
    output.Thickness = input.Thickness;
    output.PulseProgress = input.PulseProgress;
    output.CoreColor = input.CoreColor;
    output.GlowColor = input.GlowColor;
    return output;
}

float Hash1(float n)
{
    return frac(sin(n * 127.1) * 43758.5453);
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

float Gauss(float d, float w)
{
    return exp(-(d * d) / max(w * w, 0.0001));
}

// 1 near the source, easing to 0 at the reach so nothing is left at the quad's edge.
float Reach(float d, float reach)
{
    float t = 1.0 - smoothstep(reach * 0.5, reach, d);
    return t * t;
}

// A zigzag through random heights, one kink per unit of x: x = height (-1..1), y = slope.
float2 Zigzag(float x, float seed)
{
    float i = floor(x);
    float a = Hash1(i + seed);
    float b = Hash1(i + 1.0 + seed);
    return float2(lerp(a, b, frac(x)) * 2.0 - 1.0, (b - a) * 2.0);
}

// One jagged strand that leaves the line by up to amp: x = offset, y = slope.
float2 Strand(float along, float len, float amp, float seed, float coarse)
{
    float u = saturate(along / len);
    // Pinned at both ends, free in between.
    float pin = pow(max(sin(u * 3.14159), 0.0), 0.55);
    float2 big = Zigzag(along * coarse, seed);
    float2 mid = Zigzag(along * coarse * 2.9, seed * 1.7 + 13.0);
    float2 fine = Zigzag(along * coarse * 7.3, seed * 2.3 + 71.0);
    float offset = (big.x + mid.x * 0.42 + fine.x * 0.16) * amp * pin;
    float slope = (big.y + mid.y * 0.42 * 2.9 + fine.y * 0.16 * 7.3) * coarse * amp * pin;
    return float2(offset, slope);
}

// Distance from a point to a strand.
float StrandDistance(float along, float across, float len, float2 strand)
{
    float beyond = max(max(-along, along - len), 0.0);
    // Steep stretches would otherwise look thinner than flat ones.
    return length(float2(beyond, (across - strand.x) / sqrt(1.0 + strand.y * strand.y)));
}

// A fork: it follows the channel to where it splits, then veers off to one side and
// tapers to nothing. Returns its light (0..1).
float Fork(float along, float across, float len, float amp, float width, float2 channel, float seed, float coarse)
{
    float split = len * (0.18 + 0.42 * Hash1(seed * 0.731 + 5.0));
    float side = Hash1(seed * 1.37 + 9.0) < 0.5 ? -1.0 : 1.0;
    float run = along - split;
    float reachAlong = max(len * (0.22 + 0.2 * Hash1(seed * 2.11 + 3.0)), 10.0);
    float veer = side * run * (0.45 + 0.3 * Hash1(seed * 0.37 + 1.0))
        + Zigzag(run * coarse * 3.1, seed + 23.0).x * amp * 0.5 * saturate(run / 5.0);
    float d = abs(across - channel.x - veer) * 0.85;
    float taper = saturate(1.0 - run / reachAlong) * step(0.0, run);
    return (Gauss(d, width * 0.55) * 0.85 + Gauss(d, width * 2.4) * 0.14) * taper;
}

float4 BoltPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 ba = input.PointB - input.PointA;
    float len = max(length(ba), 0.001);
    float2 dir = ba / len;
    float2 rel = input.LocalPos - input.PointA;
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));

    float age = saturate(input.PulseProgress);
    float seed = input.CoreColor.r * 97.0;
    float width = 0.55 + input.CoreColor.g * 2.2;
    float amp = input.CoreColor.b * reach;

    // The bolt jumps to a new shape many times a second.
    float frame = floor(Time * 24.0 + seed);
    float shape = seed + frame * 17.3;
    // About one large kink every 26 units, with at least three along a short bolt.
    float coarse = max(1.0 / 26.0, 3.0 / len);

    float2 channel = Strand(along, len, amp, shape, coarse);
    float d1 = StrandDistance(along, across, len, channel);
    float d2 = StrandDistance(along, across, len, Strand(along, len, amp * 1.3, shape + 41.0, coarse * 0.8));

    float core = Gauss(d1, width);
    float halo = width * 3.2 / (d1 + width * 3.2);
    halo = halo * halo * Reach(d1, reach);
    float strand = Gauss(d2, width * 0.5) * 0.35 + Gauss(d2, width * 2.5) * 0.06;

    // Forks, for the callers that ask for them through the tint's alpha.
    float forks = 1.0 - input.GlowColor.a;
    float fork = Fork(along, across, len, amp, width, channel, shape, coarse) * saturate(forks * 4.0)
        + Fork(along, across, len, amp, width, channel, shape + 57.0, coarse) * saturate(forks * 4.0 - 2.5);
    fork *= Reach(abs(across), reach);

    // It strikes from A to B, then burns out, flickering.
    float front = saturate(age * 9.0) * (len + reach);
    float struck = smoothstep(front + 6.0, front - 6.0, along);
    float flicker = 0.7 + 0.3 * Hash1(frame * 3.7 + seed);
    float fade = 1.0 - age;
    float life = (fade * fade * flicker + exp(-age * 16.0) * 0.9) * struck;

    // Where it lands.
    float node = Gauss(length(input.LocalPos - input.PointB), 2.0 + width * 2.0) * smoothstep(0.8, 1.0, age * 9.0) * fade;

    float3 tint = input.GlowColor.rgb;
    float3 light = tint * (halo * 0.6 + strand + node * 0.9 + fork * 0.6)
        + float3(1.0, 1.0, 1.0) * (core + node * 0.5 + fork * 0.7);
    return float4(light * (life + node) * input.CoreColor.a, 0.0);
}

float4 RingPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 rel = input.LocalPos - input.PointA;
    float r = length(rel);
    float angle = atan2(rel.y, rel.x);

    float age = saturate(input.PulseProgress);
    float style = input.CoreColor.r;
    float seed = input.CoreColor.g * 53.0;
    float radius = input.CoreColor.b * reach;
    float fade = 1.0 - age;
    float3 tint = input.GlowColor.rgb;
    // The tint's alpha is the ring's width: 1 as designed, lower for thinner fronts.
    float width = max(input.GlowColor.a, 0.2);
    float3 white = float3(1.0, 1.0, 1.0);
    float energy = 0.0;
    float hot = 0.0;

    if (style < 0.25)
    {
        // A shock front: fast at first, slowing as it thins out.
        float eased = 1.0 - fade * fade * fade;
        float front = radius * lerp(0.1, 1.0, eased);
        float ripple = 0.6 + 0.4 * Noise(float2(angle * 4.0 + seed, Time * 3.0 + r * 0.04));
        float edge = Gauss(r - front, (1.2 + 2.2 * age) * width);
        float bloom = Gauss(r - front, (5.0 + 9.0 * age) * width) * 0.35;
        // The disturbed space it leaves behind, and a fainter second front inside.
        float wake = smoothstep(front * 0.45, front, r) * step(r, front) * 0.22 * ripple;
        float echo = Gauss(r - front * 0.68, (1.5 + 2.0 * age) * width) * 0.3;
        float flash = Gauss(r, radius * 0.22) * exp(-age * 9.0);
        energy = (edge * ripple + bloom + wake + echo) * pow(fade, 1.4) + flash * 0.8;
        hot = edge * fade * 0.7 + flash * 0.5;
    }
    else if (style < 0.75)
    {
        // A field collapsing onto its centre, dragging spiral streaks with it.
        float front = radius * (1.0 - age * age);
        float edge = Gauss(r - front, (2.5 + 4.0 * fade) * width) * (0.35 + 0.65 * age);
        float inside = smoothstep(front + 4.0, front - 10.0, r);
        float arms = 0.5 + 0.5 * sin(angle * 5.0 + log(r + 2.0) * 7.0 + Time * 9.0 + seed);
        arms = arms * arms * arms * inside * smoothstep(0.0, radius * 0.25, r);
        float haze = inside * 0.12;
        float pinch = Gauss(r, radius * 0.1 + 2.0) * smoothstep(0.55, 1.0, age);
        float live = sin(age * 3.14159);
        energy = (edge * 0.8 + arms * 0.6 + haze) * sqrt(max(live, 0.0)) + pinch * fade * 3.0;
        hot = edge * live * 0.12 + pinch * fade * 2.0;
    }
    else
    {
        // A burst: a ring thrown outward with rays behind it.
        float eased = 1.0 - fade * fade;
        float front = radius * lerp(0.15, 1.0, eased);
        float edge = Gauss(r - front, (1.4 + 2.5 * age) * width);
        float bloom = Gauss(r - front, (6.0 + 8.0 * age) * width) * 0.3;
        float ray = Noise(float2(angle * 7.0 + seed, 3.0 + seed));
        ray = pow(ray, 3.0) * smoothstep(front + 2.0, front * 0.3, r) * smoothstep(0.0, radius * 0.12, r);
        float flash = Gauss(r, radius * 0.3) * exp(-age * 7.0);
        energy = (edge + bloom + ray * 1.4) * pow(fade, 1.3) + flash;
        hot = edge * fade * 0.6 + flash * 0.7 + ray * fade * 0.5;
    }

    float3 light = tint * energy + white * hot;
    // Full strength out to the ring's largest radius, gone by the reach.
    float within = 1.0 - smoothstep(lerp(radius, reach, 0.25), reach, r);
    return float4(light * within * input.CoreColor.a, 0.0);
}

float4 BeamPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 ba = input.PointB - input.PointA;
    float len = max(length(ba), 0.001);
    float2 dir = ba / len;
    float2 rel = input.LocalPos - input.PointA;
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));

    float seed = input.CoreColor.r * 61.0;
    float halfWidth = max(input.CoreColor.b * reach, 0.3);
    float3 tint = input.GlowColor.rgb;
    float3 white = float3(1.0, 1.0, 1.0);

    if (input.CoreColor.g < 0.5)
    {
        // A beam: a thin wavering core carrying packets of light from A to B.
        float age = saturate(input.PulseProgress);
        float fade = 1.0 - age;
        // A crowd of beams would add up to a white fog, so its beams carry less halo and a
        // coloured core, and are pulled taut: a hair-thin beam that wavers only looks unsteady.
        float lean = saturate(1.0 - input.CoreColor.g * 2.2);
        float taut = saturate(1.0 - input.CoreColor.g * 4.4);
        float beyond = max(max(-along, along - len), 0.0);
        float waver = sin(along * 0.11 + Time * 11.0 + seed) * halfWidth * 0.6 * saturate(min(along, len - along) / 12.0) * taut;
        float d = length(float2(beyond, across - waver));
        float packets = 0.55 + 0.45 * sin(along * 0.2 - Time * 34.0 + seed);
        float core = Gauss(d, halfWidth);
        float halo = halfWidth * 3.0 / (d + halfWidth * 3.0);
        halo = halo * halo * Reach(d, reach);
        float ends = Gauss(length(rel), halfWidth * 3.0) + Gauss(length(input.LocalPos - input.PointB), halfWidth * 3.0);
        float energy = core * (0.6 + 0.6 * packets) + (halo + ends) * 0.5 * lean;
        // Where a crowd of them arrives they overlap, so each fades out over its last stretch.
        float arrive = lerp(smoothstep(0.0, 20.0, len - along), 1.0, lean);
        return float4((tint * energy + white * core * packets * 0.6 * (0.3 + 0.7 * lean)) * arrive * fade * input.CoreColor.a, 0.0);
    }

    // A plume: turbulent exhaust streaming out of A and thinning to nothing at B.
    float t = along / len;
    float body = saturate(1.0 - t);
    float widthHere = halfWidth * (0.35 + 0.65 * pow(body, 0.6));
    float flow = Noise(float2(along * 0.09 - Time * 9.0 + seed, across * 0.22 + seed));
    float wisps = Noise(float2(along * 0.25 - Time * 16.0, across * 0.5 + seed * 2.0));
    float shapeAcross = Gauss(across + (flow - 0.5) * halfWidth * 0.9 * t, widthHere);
    float behind = along < 0.0 ? Gauss(along, halfWidth * 0.8) : 1.0;
    float density = shapeAcross * pow(body, 1.3) * (0.45 + 0.5 * flow + 0.35 * wisps) * behind;
    float heart = Gauss(across, widthHere * 0.4) * pow(body, 3.0) * behind;
    float3 lit = tint * density + white * heart * 0.5;
    return float4(lit * Reach(abs(across), reach) * input.CoreColor.a, 0.0);
}

float4 VortexPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 rel = input.LocalPos - input.PointA;
    float r = length(rel);
    float rn = r / reach;
    float angle = atan2(rel.y, rel.x);

    float seed = input.CoreColor.r * 37.0;
    float spin = 0.6 + input.CoreColor.g * 3.0;
    float horizon = input.PulseProgress * reach;
    float3 tint = input.GlowColor.rgb;

    // Matter winds tighter the closer it gets: a spiral in angle and log radius.
    // The noise wraps once around the circle, so it has no seam.
    float winding = log(rn + 0.03) * 2.2 + Time * spin * 0.35;
    float2 around = float2(cos(angle + winding), sin(angle + winding));
    float arms = Noise(around * 2.6 + float2(seed, rn * 5.0 - Time * spin * 0.6));
    arms = smoothstep(0.3, 0.85, arms);
    float fineArms = Noise(around * 6.0 + float2(seed * 2.0, rn * 14.0 - Time * spin));

    // The disc is brightest just outside the horizon and thins toward the rim.
    float inner = max(input.PulseProgress, 0.04);
    float disc = Gauss(rn - inner * 1.5, 0.2 + inner * 0.6) * (0.35 + 0.65 * arms) * (0.7 + 0.3 * fineArms);
    float field = (1.0 - rn) * (1.0 - rn) * (0.07 + 0.2 * arms * fineArms);

    float hole = horizon > 0.0 ? smoothstep(horizon * 1.04, horizon * 0.9, r) : 0.0;
    float photon = horizon > 0.0 ? Gauss(r - horizon * 1.07, 1.2 + horizon * 0.07) : Gauss(r, reach * 0.08);

    float3 hotTint = lerp(tint, float3(1.0, 0.92, 1.0), 0.75);
    float3 light = tint * (disc * (0.3 + 0.6 * input.CoreColor.b) + field) + hotTint * photon * 0.8;
    light *= (1.0 - hole) * (1.0 - smoothstep(0.8, 1.0, rn)) * input.CoreColor.a;
    // The disc is matter: it covers what is behind it as far as it glows, so a crowd of
    // wells settles into one bright disc rather than adding up to white.
    float cover = saturate(max(light.r, max(light.g, light.b)) * 1.3) * 0.85;
    return float4(light, max(hole * input.CoreColor.a, cover));
}

float4 GhostPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 ba = input.PointB - input.PointA;
    float2 dir = ba / max(length(ba), 0.001);
    float2 rel = input.LocalPos - input.PointA;
    // Nose along +x, in units of the reach.
    float2 p = float2(dot(rel, dir), dot(rel, float2(-dir.y, dir.x))) / reach;

    // A dart: a long nose, swept wings and a notched tail.
    float wing = abs(p.y);
    float nose = p.x * 0.5 + wing * 1.05 - 0.3;
    float tail = -p.x - 0.42 + wing * 0.7;
    float d = max(nose, tail);

    float inside = smoothstep(0.02, -0.02, d);
    float edge = Gauss(d, 0.03);
    float halo = exp(-max(d, 0.0) * 9.0) * 0.3;
    // A hologram: scanlines and a slow flicker.
    float scan = 0.6 + 0.4 * sin(input.LocalPos.y * 1.6 - Time * 18.0);
    float flicker = 0.85 + 0.15 * sin(Time * 31.0 + input.CoreColor.r * 40.0);
    float energy = (inside * 0.4 * scan + edge * 0.9 + halo) * flicker;
    float3 light = input.GlowColor.rgb * energy + float3(1.0, 1.0, 1.0) * edge * 0.35;
    return float4(light * Reach(length(rel), reach) * input.CoreColor.a, 0.0);
}

// Smooth noise along a path that repeats every `period` cells, so a pattern laid along
// a crack survives the wrap of its 8-bit path length.
float Wave(float x, float period)
{
    float i = floor(x);
    float f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    float a = Hash1(i - period * floor(i / period));
    float b = Hash1(i + 1.0 - period * floor((i + 1.0) / period));
    return lerp(a, b, f);
}

float4 JetPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 ba = input.PointB - input.PointA;
    float len = max(length(ba), 0.001);
    float2 dir = ba / len;
    float2 rel = input.LocalPos - input.PointA;
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));

    float age = saturate(input.PulseProgress);
    float fade = 1.0 - age;
    float seed = input.CoreColor.r * 61.0;
    float mouth = max(input.CoreColor.b * reach, 0.5);
    float3 tint = input.GlowColor.rgb;
    float3 white = float3(1.0, 1.0, 1.0);

    // It bursts out fast and slows as it climbs.
    float climb = 1.0 - fade * fade * fade;
    float front = len * (0.22 + 0.78 * climb);
    float t = saturate(along / front);

    // Tight at the mouth, billowing out toward the top, and swaying as it goes.
    float widthHere = mouth * (0.6 + 1.9 * t * t) * (0.6 + 0.4 * fade);
    float rush = Time * 13.0;
    float sway = (Noise(float2(along * 0.035 - rush * 0.35 + seed * 2.0, seed)) - 0.5) * widthHere * 1.3 * t;
    float lane = (across - sway) / widthHere;
    float streak = Noise(float2(along * 0.05 - rush * 0.8 + seed, lane * 1.3 + seed));
    float fine = Noise(float2(along * 0.13 - rush * 1.5, lane * 2.6 + seed * 3.0));

    float column = Gauss(across - sway, widthHere);
    float body = pow(1.0 - t, 0.8) * smoothstep(1.0, 0.7, t);
    // Near the top the column breaks into clumps.
    float clumps = lerp(1.0, smoothstep(0.25, 0.8, streak * 0.6 + fine * 0.5) * 1.6, t);
    float below = along < 0.0 ? Gauss(along, mouth * 0.9) : 1.0;
    float density = column * body * (0.45 + 0.6 * streak + 0.35 * fine) * clumps * below;
    float heart = Gauss(across - sway * 0.6, widthHere * 0.42) * pow(1.0 - t, 1.6) * below;
    // Bright streaks of ejecta thrown up with it.
    float flecks = Noise(float2(along * 0.09 - rush * 2.2 + seed * 5.0, lane * 4.0 + seed));
    flecks = pow(flecks, 6.0) * Gauss(across - sway, widthHere * 1.4) * body * below * 5.0;
    // The blast where it breaks the surface.
    float blast = Gauss(length(rel), mouth * (1.2 + 2.2 * age)) * fade * fade;

    float life = pow(fade, 0.9) + exp(-age * 14.0) * 0.6;
    float3 light = tint * (density * 1.8 + blast * 0.9 + flecks) + white * (heart * 1.4 + blast * 0.8 + flecks * 0.6);
    return float4(light * life * Reach(abs(across), reach) * input.CoreColor.a, 0.0);
}

float4 CrackPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 ba = input.PointB - input.PointA;
    float len = max(length(ba), 0.001);
    float2 dir = ba / len;
    float2 rel = input.LocalPos - input.PointA;
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));

    // Where this pixel is along the whole crack: it narrows and cools toward its tip.
    float whole = saturate(input.PulseProgress + input.CoreColor.g * saturate(along / len));
    float path = input.CoreColor.r * 64.0 + along;
    float halfWidth = max(input.CoreColor.b * reach, 0.2);
    float taper = 0.25 + 0.75 * pow(1.0 - whole, 0.7);
    float tip = saturate((1.0 - whole) * 9.0);

    // A ragged edge, and hot spots creeping toward the tip.
    float ragged = (Wave(path * 0.75, 48.0) - 0.5) * halfWidth * 1.1;
    float d = abs(across - ragged);
    float hot = Wave(path * 0.25 - Time * 0.9, 16.0);
    float flicker = 0.9 + 0.1 * sin(Time * 9.0);

    float core = Gauss(d, halfWidth * 0.45 * taper + 0.12);
    float melt = Gauss(d, halfWidth * 1.5 * taper + 0.3);
    float spread = halfWidth * 4.5;
    // The glow around it keeps a clean edge: only the crack itself is ragged.
    float away = abs(across);
    float halo = spread * spread / (away * away + spread * spread) * Reach(away, reach) * taper;

    float3 tint = input.GlowColor.rgb;
    float3 whiteHot = float3(1.0, 0.94, 0.72);
    float3 ember = float3(0.85, 0.1, 0.02);
    float3 light = whiteHot * core * (0.7 + 0.6 * hot)
        + tint * (melt * (0.55 + 0.65 * hot) + halo * 0.3)
        + ember * halo * 0.22;
    return float4(light * flicker * tip * input.CoreColor.a, 0.0);
}

float4 LancePS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 ba = input.PointB - input.PointA;
    float len = max(length(ba), 0.001);
    float2 dir = ba / len;
    float2 rel = input.LocalPos - input.PointA;
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));

    float age = saturate(input.PulseProgress);
    float fade = 1.0 - age;
    float seed = input.CoreColor.r * 47.0;
    float halfWidth = max(input.CoreColor.b * reach, 0.5);
    float3 tint = input.GlowColor.rgb;
    float3 white = float3(1.0, 1.0, 1.0);

    // The streak thins as it ages, and lets go of where it started first.
    float width = halfWidth * (0.15 + 0.85 * fade * fade);
    float let = age * 1.25 * min(len, reach * 14.0);
    float held = smoothstep(let - reach * 6.0, let, along) * (along < 0.0 ? Gauss(along, halfWidth) : 1.0);
    float past = along > len ? Gauss(along - len, halfWidth) : 1.0;

    // Starlight pulled out into filaments, with ripples racing after the ship.
    float filaments = Noise(float2(across / width * 2.2 + seed, along * 0.01 - Time * 7.0));
    filaments = filaments * filaments * 1.7;
    float ripple = 0.72 + 0.28 * sin(along * 0.045 - Time * 46.0 + seed);
    float core = Gauss(across, width * 0.24);
    float sheath = Gauss(across, width) * (0.35 + filaments);
    float halo = Gauss(across, width * 2.7) * 0.22;

    float3 light = tint * (sheath * ripple + halo) + white * core * (0.9 + 0.5 * ripple);
    float life = fade * (0.6 + 0.4 * fade) + exp(-age * 10.0) * 0.8;
    return float4(light * held * past * life * Reach(abs(across), reach) * input.CoreColor.a, 0.0);
}

float4 GlintPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 ba = input.PointB - input.PointA;
    float2 dir = ba / max(length(ba), 0.001);
    float2 rel = input.LocalPos - input.PointA;
    // In units of the reach, the first ray along +x.
    float2 p = float2(dot(rel, dir), dot(rel, float2(-dir.y, dir.x))) / reach;
    float2 q = float2(p.x + p.y, p.x - p.y) * 0.7071;
    float r = length(p);

    float seed = input.CoreColor.r * 40.0;
    float twinkle = 1.0 - saturate(input.PulseProgress) * 0.75 * (0.5 + 0.5 * sin(Time * 9.0 + seed));

    float core = Gauss(r, 0.075);
    float glow = 0.03 / (r * r + 0.03);
    float rays = Gauss(p.y, 0.02 + abs(p.x) * 0.02) * exp(-abs(p.x) * 4.2)
        + Gauss(p.x, 0.02 + abs(p.y) * 0.02) * exp(-abs(p.y) * 4.2);
    float shortRays = Gauss(q.y, 0.016) * exp(-abs(q.x) * 10.0) + Gauss(q.x, 0.016) * exp(-abs(q.y) * 10.0);

    float within = 1.0 - smoothstep(0.55, 1.0, r);
    float3 light = input.GlowColor.rgb * (glow * 0.55 + rays * 0.9 + shortRays * 0.5)
        + float3(1.0, 1.0, 1.0) * (core + rays * 0.45);
    return float4(light * within * within * twinkle * input.CoreColor.a, 0.0);
}

float4 SlugPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 ba = input.PointB - input.PointA;
    float len = max(length(ba), 0.001);
    float2 dir = ba / len;
    float2 rel = input.LocalPos - input.PointA;
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));
    float u = along / len;

    float cooled = saturate(input.PulseProgress);
    float heat = (1.0 - cooled) * (1.0 - cooled);
    float seed = input.CoreColor.r * 53.0;
    float charge = input.CoreColor.g;
    float halfWidth = max(input.CoreColor.b * reach, 0.5);
    float3 tint = input.GlowColor.rgb;
    float3 white = float3(1.0, 1.0, 1.0);

    // The rod: a thick body out of the ground, a collar, then a spike thinning to the tail.
    float spike = halfWidth * lerp(0.6, 0.28, saturate((u - 0.6) / 0.4));
    float w = u < 0.5 ? halfWidth : (u < 0.6 ? halfWidth * 1.35 : spike);
    float d = max(abs(across) - w, max(-along, along - len));
    // Its nose is buried: the body rises out of the scorched ground around it.
    float body = saturate(0.5 - d / 0.6) * smoothstep(-0.5, 2.0, along);
    float fromRoot = length(rel);
    float scorch = Gauss(fromRoot, halfWidth * 1.5) * 0.6;

    // Steel, lit from one side, with a hard highlight along it.
    float n = across / max(w, 0.01);
    float lit = 0.22 + 0.78 * Gauss(n - 0.3, 0.55);
    float3 steel = lerp(float3(0.05, 0.07, 0.13), float3(0.46, 0.56, 0.74), lit)
        + white * Gauss(n - 0.38, 0.17) * 0.4;
    steel *= 0.45 + 0.55 * smoothstep(0.1, -0.9, d);

    // Two bands set into the body hold a little charge and breathe with it.
    float breathe = 0.5 + 0.5 * sin(Time * 3.1 + seed);
    float bands = Gauss(along - len * 0.2, 0.45) + Gauss(along - len * 0.36, 0.45);
    // A pulse runs the rod from root to tail and leaves by its tip.
    float where = (1.0 - charge) * 1.25 - 0.1;
    float pulse = Gauss(u - where, 0.2) * step(0.004, charge);
    float tipPulse = Gauss(1.0 - where, 0.3) * step(0.004, charge);

    // Charge crackling round it: with each pulse, and now and then between them.
    float frame = floor(Time * 22.0 + seed);
    float2 strand = Strand(along, len, halfWidth * 2.0, seed + frame * 13.7, 3.5 / len);
    float ds = StrandDistance(along, across, len, strand);
    float stray = step(0.88, Hash1(floor(Time * 7.0) + seed)) * 0.45;
    float crackle = (Gauss(ds, 0.4) + Gauss(ds, 1.4) * 0.25) * saturate(charge * 1.6 + stray);

    // The emitter at its tail, and the ground glowing where the current goes in.
    float toTail = length(input.LocalPos - input.PointB);
    float tip = Gauss(toTail, halfWidth * 0.5);
    float tipHalo = Gauss(toTail, halfWidth * 1.6) * (0.2 + 0.1 * breathe + 1.2 * tipPulse);
    float crater = Gauss(fromRoot, halfWidth * 1.4);
    float aura = Gauss(max(d, 0.0), halfWidth * 1.1) * (0.05 + 0.2 * charge + 0.6 * pulse);

    // It lands white hot and cools through orange to bare steel.
    float3 hot = lerp(float3(1.0, 0.33, 0.06), float3(1.0, 0.93, 0.78), heat);
    float3 glow = tint * tint * bands * (0.9 + 0.4 * breathe) + tint * pulse * 1.2 + white * pulse * 0.5;
    float3 surface = lerp(steel, steel * 0.35 + hot, saturate(heat * 1.5)) + glow;
    float entering = Gauss(where, 0.25) * step(0.004, charge);
    float3 light = tint * (aura + tipHalo + crackle * 0.8 + crater * (0.08 + 0.5 * entering))
        + white * (tip * (0.7 + 0.3 * breathe + tipPulse) + crackle * 0.9)
        + hot * (Gauss(max(d, 0.0), halfWidth * 1.1) * 0.5 + crater * 0.7) * heat
        + white * Gauss(fromRoot, halfWidth * 2.2) * exp(-cooled * 40.0);
    light *= Reach(max(d, 0.0), reach);
    float cover = max(body, scorch * (1.0 - heat));
    return float4(surface * body + light, cover) * input.CoreColor.a;
}

float4 LinkPS(VertexShaderOutput input) : COLOR
{
    float reach = max(input.Thickness, 1.0);
    float2 ba = input.PointB - input.PointA;
    float len = max(length(ba), 0.001);
    float2 dir = ba / len;
    float2 rel = input.LocalPos - input.PointA;
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));

    float age = saturate(input.PulseProgress);
    float fade = 1.0 - age;
    float seed = input.CoreColor.r * 61.0;
    float halfWidth = max(input.CoreColor.b * reach, 0.3);
    float3 tint = input.GlowColor.rgb;
    float3 white = float3(1.0, 1.0, 1.0);

    // The spark crosses in the first part of the link's life, fast and then settling.
    float crossing = saturate(age / 0.22);
    float head = len * crossing * (2.0 - crossing);
    float beyond = max(max(-along, along - len), 0.0);
    float d = length(float2(beyond, across));

    // The thread it leaves reaches as far as the spark has got, and lets go of its start first.
    float soft = halfWidth * 2.0;
    float drawn = smoothstep(head + soft, head - soft, along);
    float let = (age - 0.3) / 0.7 * (len + reach * 2.0);
    float held = smoothstep(let - reach * 2.0, let, along);
    float thread = Gauss(d, halfWidth);
    float halo = halfWidth * 2.6 / (d + halfWidth * 2.6);
    halo = halo * halo * Reach(d, reach);
    // Beads of light run along it after the spark.
    float beads = 0.72 + 0.28 * sin(along * 1.1 / halfWidth - Time * 42.0 + seed);

    // The spark: a bright head with a short tail behind it, which dies where it lands.
    float back = along - head;
    float toHead = length(float2(back * (back < 0.0 ? 0.3 : 1.0), across));
    float spark = Gauss(toHead, halfWidth * 1.7) * exp(-max(age - 0.22, 0.0) * 14.0);

    float life = fade * (0.35 + 0.65 * fade);
    float3 light = (tint * (thread * (0.7 + 0.5 * beads) + halo * 0.4) + white * thread * beads * 0.6) * drawn * held * life
        + (tint * 0.8 + white) * spark;
    return float4(light * input.CoreColor.a, 0.0);
}

technique ModuleBolt
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL BoltPS();
    }
}

technique ModuleRing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL RingPS();
    }
}

technique ModuleBeam
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL BeamPS();
    }
}

technique ModuleVortex
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL VortexPS();
    }
}

technique ModuleGhost
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL GhostPS();
    }
}

technique ModuleJet
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL JetPS();
    }
}

technique ModuleCrack
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL CrackPS();
    }
}

technique ModuleLance
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL LancePS();
    }
}

technique ModuleGlint
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL GlintPS();
    }
}

technique ModuleSlug
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL SlugPS();
    }
}

technique ModuleLink
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL LinkPS();
    }
}

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

// The railgun's energy (GameScreen.Railgun.cs), drawn through SdfLineRenderer with
// the LaserBeam.fx vertex layout and additive blending. Two techniques:
//
//  RailTrail: the round and the channel it tears through space. PointA is the
//    muzzle, PointB the impact point, PulseProgress how far along the round is
//    (0..1). While in flight the round is a white-hot sabot with a bow glow and a
//    shock cone; the ionised channel behind it flashes white and cools to blue
//    while a double helix of ions unwinds around it, widens and breaks up.
//    CoreColor: r = seconds since the shot / TRAIL_SPAN, g = seed, b = flight
//    seconds / 0.5, a = intensity. GlowColor tints the ions.
//
//  RailMuzzle: everything at the muzzle. PointA is the muzzle, PointB a point one
//    unit down the line of fire. The wind-up (CoreColor.r, 0..1 over the wind-up
//    sound) is a vortex of energy drawn into the muzzle that tightens into a star
//    and holds; the shot (CoreColor.g = seconds since firing / MUZZLE_SPAN) is a
//    star flash, a shock ring that races out ahead of the gun, gas venting out of
//    both sides and a backflash down the barrel. CoreColor.a is the intensity.
//
// Thickness carries the effect scale (world units per effect unit) for both.
// Output is premultiplied, for additive blending.
#define TRAIL_SPAN 2.0
#define TRAIL_FADE 0.9
#define MUZZLE_SPAN 1.0

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

// A soft ring of radius r and half-width w around the distance d.
float Ring(float d, float r, float w)
{
    return Gauss(d - r, w);
}

static const float3 White = float3(1.0, 1.0, 1.0);
static const float3 Deep = float3(0.2, 0.35, 0.95);

float4 TrailPS(VertexShaderOutput input) : COLOR
{
    float s = max(input.Thickness, 0.05);
    float2 ba = input.PointB - input.PointA;
    float len = max(length(ba), 0.0001);
    float2 dir = ba / len;
    float2 rel = input.LocalPos - input.PointA;
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));

    float age = input.CoreColor.r * TRAIL_SPAN;
    float seed = input.CoreColor.g * 6.2832;
    float flight = max(input.CoreColor.b * 0.5, 0.02);
    float intensity = input.CoreColor.a;
    float3 tint = input.GlowColor.rgb;

    float head = len * saturate(input.PulseProgress);
    float inFlight = step(age, flight);
    float since = max(age - flight, 0.0);
    // How much of the lingering trail is left, and how far it has diffused.
    float life = saturate(since / TRAIL_FADE);
    float fade = pow(1.0 - life, 1.4);
    float spread = 1.0 - (1.0 - life) * (1.0 - life);
    // Only the channel behind the round exists; it fades in just past the muzzle.
    float channel = (1.0 - smoothstep(head - 4.0 * s, head + 2.0 * s, along)) * smoothstep(-1.0 * s, 3.0 * s, along);
    float ad = across / s;

    // The ionised channel: a white flash that cools to blue over a quarter second,
    // then a thin blue filament that flickers as it fades.
    float flash = pow(saturate(1.0 - age / 0.28), 1.3);
    float coreW = 1.1 + 2.0 * flash;
    float core = Gauss(ad, coreW);
    float flicker = 0.75 + 0.25 * sin(along * 0.37 / s + Time * 90.0 + seed);
    float glow = exp(-abs(ad) / (6.0 + 14.0 * flash)) * (0.75 * flash + 0.08 * fade);
    float filamentNoise = Noise(float2(along * 0.06 / s - Time * 3.0, seed * 3.0));
    float filament = core * fade * flicker * (0.5 + 0.5 * filamentNoise);

    // The ion helix: two strands unwinding around the channel, widening as it
    // diffuses, thickening and breaking into patches as they fade.
    float radius = (3.0 + 19.0 * spread) * s;
    float reach = min(1.0, 0.25 + along / (90.0 * s));
    float phase = along * (6.2832 / (30.0 * s)) + seed - since * 5.0;
    float sinP = sin(phase);
    float cosP = cos(phase);
    float strandW = (1.0 + 1.8 * spread) * s;
    float strandA = Gauss(across - radius * reach * sinP, strandW) * (0.55 + 0.45 * cosP);
    float strandB = Gauss(across + radius * reach * sinP, strandW) * (0.55 - 0.45 * cosP);
    float breakup = Noise(float2(along * 0.045 / s + seed * 2.0, since * 2.5));
    float dissolve = smoothstep(life * 0.8 - 0.45, life * 0.8 + 0.35, breakup + 0.2);
    float strands = (strandA + strandB) * fade * dissolve * (0.5 + 0.5 * (1.0 - flash)) * 1.5;

    // A turbulent haze that widens around the channel as the ions disperse.
    float hazeW = (3.0 + 14.0 * spread) * s;
    float turbulence = Noise(float2(along * 0.035 / s - since * 1.5 + seed, across * 0.09 / s + since * 0.8));
    float haze = Gauss(across, hazeW) * (0.25 + 0.75 * turbulence) * fade * (0.22 + 0.2 * flash);

    float3 color = (White * core * (1.6 * flash) + tint * (core * 0.5 * flash + filament * 0.9 + glow)
        + lerp(tint, Deep, life * 0.7) * (strands * 0.9 + haze)) * channel;

    // The round itself: a white-hot sabot with a bow of compressed light ahead of
    // it and a shock cone trailing from its tip.
    float2 fromHead = float2(along - head, across) / s;
    float sabot = Gauss(fromHead.x / 5.0, 1.0) * Gauss(fromHead.y / 1.3, 1.0);
    float bow = Gauss(max(fromHead.x, 0.0) / 12.0, 1.0) * Gauss(fromHead.y / 5.0, 1.0) * 0.45;
    float back = max(-fromHead.x, 0.0);
    float cone = Gauss(abs(fromHead.y) - back * 0.22, 1.1) * exp(-back / 36.0) * saturate(back * 2.0) * 0.7;
    float round = (sabot * 1.8 + bow + cone) * inFlight;
    color += (White * (sabot * 1.8 + bow * 0.5) + tint * (bow * 0.6 + cone)) * inFlight;

    // The impact: a flash where the round struck, and a wide bloom that dies off.
    float2 fromEnd = (input.LocalPos - input.PointB) / s;
    float impact = Gauss(length(fromEnd) / (6.0 + 24.0 * saturate(since / 0.3)), 1.0)
        * pow(saturate(1.0 - since / 0.45), 1.5) * step(flight, age);
    color += (White * 0.8 + tint * 0.6) * impact;

    color *= intensity;
    float alpha = saturate((core + glow + strands + haze) * channel + round + impact) * intensity;
    return float4(color, alpha);
}

float4 MuzzlePS(VertexShaderOutput input) : COLOR
{
    float s = max(input.Thickness, 0.05);
    float2 ba = input.PointB - input.PointA;
    float2 dir = ba / max(length(ba), 0.0001);
    float2 rel = (input.LocalPos - input.PointA) / s;
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));
    float r = max(length(rel), 0.001);
    float theta = atan2(across, along);
    // Nothing reaches back over the barrel except a little backflash.
    float ahead = smoothstep(-12.0, 6.0, along);

    float windUp = input.CoreColor.r;
    float shot = input.CoreColor.g * MUZZLE_SPAN;
    float firing = step(0.001, input.CoreColor.g) * step(input.CoreColor.g, 0.999);
    float intensity = input.CoreColor.a;
    float3 tint = input.GlowColor.rgb;
    float3 color = float3(0.0, 0.0, 0.0);
    float alpha = 0.0;

    // ---- Wind-up: the sound rises to a climax, then drops away before the shot. ----
    if (windUp > 0.0)
    {
        float build = smoothstep(0.08, 0.72, windUp);
        float climax = smoothstep(0.55, 0.78, windUp);
        float hold = smoothstep(0.78, 1.0, windUp);
        float active = smoothstep(0.0, 0.1, windUp);

        // A vortex of energy spirals into the muzzle, tightening as the charge builds.
        float ringR = lerp(42.0, 7.0, pow(build, 0.7)) * (1.0 - 0.75 * hold);
        float arms = 0.5 + 0.5 * sin(theta * 3.0 - log(r + 1.0) * 5.0 + Time * 11.0);
        float arms2 = 0.5 + 0.5 * sin(theta * 5.0 + log(r + 1.0) * 7.0 - Time * 16.0);
        float swirlNoise = Noise(float2(theta * 1.6 + Time * 2.0, r * 0.08 - Time * 1.5));
        float ring = Ring(r, ringR, 5.0 + 7.0 * (1.0 - build)) * (0.15 + 0.85 * arms * arms) * (0.4 + 0.6 * swirlNoise) * ahead;
        float inner = Ring(r, ringR * 0.55, 3.0 + 3.0 * (1.0 - build)) * (0.2 + 0.8 * arms2 * arms2) * ahead * build;
        float vortex = (ring * 1.1 + inner * 0.9) * active * (1.0 - 0.6 * hold);

        // Streaks of energy drawn in along the spokes, flowing toward the muzzle.
        float spokes = pow(0.5 + 0.5 * cos(theta * 9.0 + Time * 1.3), 14.0)
            + 0.6 * pow(0.5 + 0.5 * cos(theta * 5.0 - Time * 0.9 + 1.0), 20.0);
        float flow = pow(frac(r / 24.0 + Time * 2.6), 2.5);
        float streaks = spokes * flow * exp(-r / 48.0) * smoothstep(ringR * 0.8, ringR * 1.6, r) * build * ahead * (1.0 - hold) * 1.3;

        // Light gathering at the muzzle: a glow that grows to the climax, then
        // collapses into a hard white star for the held breath before the shot.
        float glowR = (3.0 + 9.0 * build) * (1.0 - 0.55 * hold);
        float gather = Gauss(r, glowR) * (0.25 + 0.75 * build) * active;
        float coreR = (0.8 + 2.2 * climax) * (1.0 - 0.4 * hold) + 0.6 * hold;
        float star = exp(-r / coreR) * (0.3 + 1.2 * climax);
        // Anamorphic streaks through the star, stretching out as it holds.
        float flareL = 14.0 + 26.0 * hold;
        float flare = (Gauss(along, 0.7) * exp(-abs(across) / flareL)
            + Gauss(across, 0.7) * exp(-abs(along) / (flareL * 0.45)) * ahead) * (0.6 * climax + 0.9 * hold);
        // The hum: a faint pulsing field around the muzzle as the charge builds.
        float hum = Gauss(r, 20.0 + 12.0 * build) * 0.12 * build * (0.8 + 0.2 * sin(Time * 40.0)) * ahead;

        color += tint * (vortex * 1.2 + streaks * 1.1 + gather * 0.9 + hum)
            + White * (gather * 0.3 * climax + star + flare);
        alpha += vortex + streaks + gather + star + flare + hum;
    }

    // ---- The shot: loud for the first 0.15 s, then dying away. ----
    if (firing > 0.0)
    {
        float t = shot;
        float flash = pow(saturate(1.0 - t / 0.16), 1.2);
        float decay = pow(saturate(1.0 - t / 0.5), 1.5);

        // The star flash: a white core with rays, thrown ahead of the gun.
        float forward = 0.55 + 0.45 * smoothstep(-0.6, 1.0, cos(theta));
        float rays = pow(0.5 + 0.5 * cos(theta * 7.0 + 0.4), 24.0) + pow(0.5 + 0.5 * cos(theta * 11.0 - 1.7), 40.0) * 0.7;
        float burst = rays * exp(-r / (26.0 + 60.0 * flash)) * forward * flash;
        float bloom = Gauss(r, 10.0 + 22.0 * flash) * flash * 0.9;
        float hot = exp(-r / (1.5 + 3.0 * flash)) * (1.6 * flash);
        // A spike of light straight down the line of fire.
        float spike = Gauss(across, 0.8 + 1.2 * flash) * exp(-max(along, 0.0) / (60.0 + 80.0 * flash))
            * step(0.0, along) * flash;

        // The shock ring: a shell of compressed light racing out, stretched ahead.
        float ringT = saturate(t / 0.5);
        float ease = 1.0 - (1.0 - ringT) * (1.0 - ringT);
        float q = length(float2(along * (along > 0.0 ? 0.7 : 1.25), across));
        float ringR = 3.0 + 78.0 * ease;
        float ring = Ring(q, ringR, 1.8 + 3.0 * ringT) * (1.0 - ringT) * (0.9 + 0.3 * sin(theta * 6.0 + Time * 20.0));
        float disc = Ring(q, ringR * 0.6, 1.2 + 1.5 * ringT) * (1.0 - ringT) * 0.5 * step(t, 0.3);

        // Gas vents sideways out of the muzzle, then thins out.
        float jetT = saturate(t / 0.24);
        float jetReach = 5.0 + 26.0 * jetT;
        float side = abs(across);
        float jet = Gauss(along + 1.5 + 2.0 * jetT, 1.2 + 0.22 * side) * step(side, jetReach)
            * (1.0 - side / jetReach) * (1.0 - jetT) * step(side, 60.0);

        // A backflash down the barrel.
        float backflash = exp(-r / 14.0) * step(along, 0.0) * flash * 0.35;

        color += White * (hot + bloom * 0.8 + burst * 0.9 + spike * 0.9 + disc * 0.6)
            + tint * (bloom * 0.7 + burst * 0.6 + spike * 0.6 + ring * 1.1 + jet * 0.9 + backflash);
        alpha += hot + bloom + burst + spike + ring + disc + jet + backflash;
    }

    color *= intensity;
    return float4(color, saturate(alpha) * intensity);
}

technique RailTrail
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL TrailPS();
    }
}

technique RailMuzzle
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MuzzlePS();
    }
}

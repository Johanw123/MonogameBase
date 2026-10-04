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

// A continuous laser beam, drawn through SdfLineRenderer (same vertex layout as
// LineSDF.fx) with additive blending. PointA is the emitter, PointB the impact.
// Thickness is the core half-width in world units; PulseProgress carries the
// beam's intensity. CoreColor tints the beam, GlowColor the emitter and impact.
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

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 px = input.LocalPos;
    float2 pA = input.PointA;
    float2 pB = input.PointB;
    float2 ba = pB - pA;
    float len = max(length(ba), 0.0001);
    float2 dir = ba / len;
    float2 rel = px - pA;

    // Beam coordinates: distance from the emitter along the beam, and across it.
    float along = dot(rel, dir);
    float across = dot(rel, float2(-dir.y, dir.x));
    float h = along / len;
    float inside = step(0.0, h) * step(h, 1.0);

    // Fast flicker and a slower breathing of the beam width.
    float flicker = 1.0 + 0.08 * sin(Time * 71.0) + 0.06 * sin(Time * 17.0 + along * 0.04);
    float w = max(input.Thickness * flicker, 0.5);

    // Heat shimmer: the beam edge ripples as waves travel toward the impact.
    float shimmer = sin(along * 0.11 - Time * 26.0) * 0.18 * w;
    float beamDist = abs(across + shimmer);
    float endDist = length(px - (pA + ba * saturate(h)));
    float dist = lerp(endDist, beamDist, inside);

    // Energy pulses flowing from the ship to the planet.
    float flowA = 0.5 + 0.5 * sin(along * 0.16 - Time * 40.0);
    float flowB = 0.5 + 0.5 * sin(along * 0.05 - Time * 19.0 + 1.7);
    float energy = 0.55 + 0.45 * flowA * flowB;

    float core = exp(-(dist * dist) / (w * w * 0.25));
    float inner = exp(-(dist * dist) / (w * w * 2.2)) * energy;
    float halo = exp(-dist / (w * 4.0)) * 0.3;

    // Fade in just past the emitter so the beam leaves a lens, not a hard cut.
    float emerge = smoothstep(-w, w * 3.0, along);
    float beam = emerge * (1.0 - step(1.0, h) * 0.6);

    float toEmitter = length(rel);
    float emitter = exp(-(toEmitter * toEmitter) / (w * w * 6.0)) * (0.8 + 0.2 * sin(Time * 33.0));
    float toImpact = length(px - pB);
    float impact = exp(-(toImpact * toImpact) / (w * w * 14.0)) * (1.1 + 0.35 * sin(Time * 47.0));

    float3 white = float3(1.0, 1.0, 1.0);
    float3 beamColor = input.CoreColor.rgb;
    float3 flareColor = input.GlowColor.rgb;
    float3 color = (white * core * 1.4 + beamColor * (inner * 1.2 + halo)) * beam
                 + (white * 0.7 + flareColor) * impact
                 + (white * 0.4 + flareColor) * emitter;
    color *= input.PulseProgress;

    float alpha = saturate((core + inner + halo) * beam + impact + emitter);
    return float4(color, alpha);
}

technique LaserBeam
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

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

// The Tractor Belt, drawn additively from signed distances (TractorBeltRenderer.cs).
// Each quad is one piece of the belt or a mark at its intake; Shape2.x picks which:
//   0  a straight stretch: Shape = A.xy, B.xy
//   1  an arc:             Shape = center.xy, radius, start angle; Shape2.z = turn direction
//   2  the intake's mouth: Shape = center.xy, ring radius, run time left (0..1)
//   3  the catch ring:     Shape = center.xy, radius, rotation
// Belt pieces carry Shape2.y = distance along the belt where they start, and Style =
// (alpha, belt length, pod phase, reeled in): a shut-off belt only shows past the last. Their quads tile at shared cross-sections, so the
// joints are seamless. Widths are in world units; PixelSize is one pixel in world units,
// for anti-aliasing.
float4x4 MatrixTransform;
float PixelSize;
float RailOffset;
float RailHalfWidth;
float BedRadius;
float ChevronSize;
float ChevronHalfWidth;
float ChevronSpacing;
float EndReach;

static const float TAU = 6.28318530718;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Shape    : TEXCOORD0;
    float4 Shape2   : TEXCOORD1;
    float4 Style    : TEXCOORD2;
    float4 Tint     : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Shape    : TEXCOORD0;
    float4 Shape2   : TEXCOORD1;
    float4 Style    : TEXCOORD2;
    float2 World    : TEXCOORD3;
    float4 Tint     : COLOR0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Shape = input.Shape;
    output.Shape2 = input.Shape2;
    output.Style = input.Style;
    output.World = input.Position.xy;
    output.Tint = input.Tint;
    return output;
}

// How much of a pixel at this distance from a line's center lies inside its half width.
float Coverage(float distance, float halfWidth)
{
    return 1.0 - smoothstep(halfWidth - PixelSize * 0.5, halfWidth + PixelSize * 0.5, distance);
}

float SegmentDistance(float2 p, float2 a, float2 b)
{
    float2 ab = b - a;
    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-6));
    return length(p - a - ab * t);
}

float WrapAngle(float angle)
{
    return angle - TAU * floor(angle / TAU);
}

// Belt space: s runs along the belt from the intake, u across it from the centerline.
float4 Belt(float s, float u, float4 style, float4 tint)
{
    float total = style.y;
    float au = abs(u);
    float bed = exp(-(u * u) / (BedRadius * BedRadius)) * 0.12;
    float rails = Coverage(abs(au - RailOffset), RailHalfWidth) * 0.85;
    // Two chevrons per gap, a third and two thirds of the way between pods (ChevronSpacing is
    // the pod spacing), pointing toward the home base, the way the pods go.
    float gap = frac((total - s - style.z) / ChevronSpacing) * ChevronSpacing;
    float x = gap - (gap < ChevronSpacing * 0.5 ? ChevronSpacing * 0.34 : ChevronSpacing * 0.66);
    float h = ChevronSize * 0.7;
    float chevron = Coverage(SegmentDistance(float2(x, au), float2(-h, 0.0), float2(h, ChevronSize)),
        ChevronHalfWidth) * 0.7;
    // Rails ease out of the intake's mouth (or the end being reeled in) and into the home
    // base; chevrons stop short of both.
    float from = s - style.w;
    float start = saturate(from / 10.0);
    float end = saturate((total - s) / 24.0);
    chevron *= saturate(min(from - 6.0, total - EndReach - s) / 24.0);
    float intensity = (bed + rails) * start * end + chevron;
    return float4(tint.rgb * intensity, style.x);
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float2 p = input.World;
    float kind = input.Shape2.x;

    if (kind < 0.5)
    {
        float2 a = input.Shape.xy;
        float2 ab = input.Shape.zw - a;
        float len = max(length(ab), 1e-4);
        float2 dir = ab / len;
        float2 d = p - a;
        float s = input.Shape2.y + dot(d, dir);
        float u = dir.x * d.y - dir.y * d.x;
        return Belt(s, u, input.Style, input.Tint);
    }

    float2 v = p - input.Shape.xy;
    float dist = length(v);
    float radius = input.Shape.z;

    if (kind < 1.5)
    {
        // The quad already ends at the arc's radial cuts; a small margin keeps edge pixels
        // from wrapping round to the far end.
        float turn = WrapAngle(input.Shape2.z * (atan2(v.y, v.x) - input.Shape.w) + 0.5) - 0.5;
        return Belt(input.Shape2.y + turn * radius, dist - radius, input.Style, input.Tint);
    }

    if (kind < 2.5)
    {
        // The intake's mouth: a small soft dot, a hairline track and the run time left as a
        // thin arc draining clockwise from the top. Shape2.y = dot radius, z = line half width,
        // w = placement flash; Style.yzw = the run time's colour.
        float dotRadius = input.Shape2.y;
        float lineWidth = input.Shape2.z;
        float glow = input.Shape2.w;
        float core = Coverage(dist, dotRadius);
        float halo = exp(-(dist * dist) / (dotRadius * dotRadius * 5.0)) * 0.3;
        float ring = abs(dist - radius);
        float track = Coverage(ring, lineWidth * 0.6) * 0.22;
        // Clockwise on screen from the top: y grows downward in the world.
        float along = WrapAngle(atan2(v.x, -v.y)) / TAU;
        float edge = PixelSize / max(radius * TAU, 1e-4);
        // A full ring (a permanent belt) has no cut at the top.
        float cut = input.Shape.w >= 0.999 ? 1.0
            : smoothstep(0.0, edge, along) * (1.0 - smoothstep(input.Shape.w - edge, input.Shape.w + edge, along));
        float life = Coverage(ring, lineWidth) * cut * step(0.001, input.Shape.w) * 0.85;
        float flashRadius = radius * (1.0 + 1.5 * (1.0 - glow));
        float flash = Coverage(abs(dist - flashRadius), lineWidth * 0.7) * glow * 0.6;
        float3 color = input.Tint.rgb * (core * 0.9 + halo + track + flash) + input.Style.yzw * life;
        return float4(color, input.Style.x);
    }

    // The catch ring: eighteen dashes turning with the belt, over a faint fill.
    float dashes = 18.0;
    float t = frac(WrapAngle(atan2(v.y, v.x) - input.Shape.w) / TAU * dashes);
    float dashEdge = PixelSize * dashes / max(radius * TAU, 1e-4);
    float dash = smoothstep(0.0, dashEdge, t) * (1.0 - smoothstep(0.55 - dashEdge, 0.55, t));
    float outline = Coverage(abs(dist - radius), input.Shape2.z) * dash * 0.55;
    float fill = (1.0 - smoothstep(radius - PixelSize, radius, dist)) * 0.035;
    return float4(input.Tint.rgb * (outline + fill), input.Style.x);
}

technique TractorBelt
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

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

// A procedural black hole drawn on a sprite quad: event horizon, a tilted accretion
// disk whose far side is lensed up over the hole, a photon ring and a soft haze.
// Output is premultiplied (BlendState.AlphaBlend); vertex alpha fades it out.
// The event horizon is HORIZON of the quad's half size.
#define HORIZON 0.3
#define TILT 0.28

float4x4 view_projection;
float Time;
float Rotation;

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

// Streaks of gas orbiting the hole, faster further in. Whole-number angular
// frequencies keep the atan2 seam invisible.
float Swirl(float radius, float angle)
{
    float orbit = angle + Time * 1.8 / max(radius, 0.8);
    float n = sin(orbit * 3.0 + radius * 5.0) * 0.5
        + sin(orbit * 7.0 - radius * 11.0 + Time * 0.7) * 0.3
        + sin(orbit * 13.0 + radius * 23.0 - Time * 1.3) * 0.2;
    return 0.5 + 0.5 * n;
}

// White-hot at the inner edge, through orange to a cool violet further out.
float3 DiskColor(float radius)
{
    float heat = saturate((3.0 - radius) / 1.6);
    float3 cool = float3(0.5, 0.12, 0.5);
    float3 warm = float3(1.0, 0.42, 0.14);
    float3 hot = float3(1.0, 0.88, 0.68);
    return lerp(lerp(cool, warm, saturate(heat * 2.0)), hot, saturate(heat * 2.0 - 1.0));
}

float4 MainPS(PixelInput input) : COLOR
{
    float2 p = (input.TexCoord - 0.5) * 2.0;
    float s = sin(Rotation);
    float c = cos(Rotation);
    p = float2(p.x * c - p.y * s, p.x * s + p.y * c) / HORIZON;
    float r = length(p);
    float angle = atan2(p.y, p.x);

    // The accretion disk, seen nearly edge on.
    float2 q = float2(p.x, p.y / TILT);
    float diskRadius = length(q);
    float disk = smoothstep(1.35, 1.75, diskRadius) * (1.0 - smoothstep(2.2, 3.0, diskRadius));
    float beaming = 1.0 + 0.5 * (-q.x / max(diskRadius, 0.001)); // the approaching side is brighter
    float3 diskLight = DiskColor(diskRadius) * disk * beaming
        * (0.3 + 0.75 * Swirl(diskRadius, atan2(q.y, q.x)));

    // The far side of the disk, bent up over the top of the hole (and faintly under it).
    float halo = smoothstep(1.02, 1.15, r) * (1.0 - smoothstep(1.3, 1.85, r));
    float over = 0.4 + 0.6 * saturate(-p.y / max(r, 0.001));
    float3 haloLight = DiskColor(r + 0.6) * halo * over * (0.3 + 0.7 * Swirl(r + 0.6, angle));

    float ringDistance = (r - 1.04) / 0.045;
    float photon = exp(-ringDistance * ringDistance);
    float horizon = 1.0 - smoothstep(0.96, 1.02, r);
    float front = step(0.0, p.y); // the near half of the disk crosses in front of the hole
    float haze = 0.22 * exp(-max(r - 1.0, 0.0) * 1.1) * (1.0 - smoothstep(2.4, 3.3, r));

    float3 back = float3(0.45, 0.2, 0.9) * haze + haloLight + diskLight * (1.0 - front);
    float3 color = back * (1.0 - horizon) + float3(1.0, 0.86, 0.72) * photon * 0.85 + diskLight * front;
    // Space darkens around the hole; the disk is slightly opaque.
    float alpha = max(max(horizon, 0.55 * (1.0 - smoothstep(1.0, 2.4, r))), disk * 0.45);

    float fade = input.Color.a;
    return float4(color * 0.85 * fade, alpha * fade);
}

technique BlackHole
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

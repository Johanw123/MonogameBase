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

Texture2D SpriteTexture;

cbuffer MatrixBlock : register(b0)
{
    float4x4 view_projection;
};

cbuffer ParameterBlock : register(b1)
{
    float4 _OutlineColor;
    // Gem.FlightClock: seconds of simulation, for gems still settling after their spawn
    // and gems flying into a collector.
    float FlightTime;
    // GemCollectors: the current position (xy) of each collector slot.
    float4 Collectors[128];
    // Gem.SwallowPlanet: the planet gems sink into during a core fracture (centre, rim).
    float3 SwallowPlanet;
};

sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;

    AddressU = clamp;
    AddressV = clamp;
};

struct VertexInput {
    // The quad corner, 0..1 across (shared by every gem).
    float2 Corner : POSITION0;
    // One gem (GemInstance, GemRenderBatch.cs): centre (xy), rotation (z), depth (w).
    float4 Placement : TEXCOORD1;
    // The quad around the centre: left, top, right, bottom.
    float4 Extent : TEXCOORD2;
    // Texture coordinates of the top-left (xy) and bottom-right (zw) corners.
    float4 TextureRect : TEXCOORD3;
    float4 Color : COLOR0;
    // The animation's data: see below.
    float4 Flight : TEXCOORD4;
    // Start time and kind: > 0 spawning (starting scale share), -1 collecting,
    // <= -2 swallowed (-(2 + duration)), >= 2 chain pull (2 + duration), 0 settled.
    float2 Timing : TEXCOORD5;
};

struct PixelInput {
    float4 Position : SV_Position0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

// Gems the CPU no longer steps each frame are animated here (Entities/GemFlight.cs):
// - Spawning: glides in from its launch point (velocity decaying at e^-8t, Gem.LaunchDamping;
//   Flight.xy = the glide) and grows from Timing.y of its size (e^-5t, Gem.GrowRate).
// - Collecting: bursts away from its collector at 300/s, accelerates toward the collector's
//   live position at 2500/s^2 up to 800/s and shrinks at e^-5t (Gem.CollectTime;
//   Flight.xy = collector slot and starting distance).
// - Swallowed by a core fracture: a launch played backwards into the planet, shrinking away
//   below the rim (GemSwallow.cs; Flight.xy = the point it sinks to).
// - Chain pull: a quintic ease-out by Flight.xy (HomeBase.cs).
PixelInput SpriteVertexShader(VertexInput v) {
    PixelInput output;

    float2 center = v.Placement.xy;
    float2 corner = lerp(v.Extent.xy, v.Extent.zw, v.Corner);
    float rotationSin = sin(v.Placement.z), rotationCos = cos(v.Placement.z);
    float2 local = float2(corner.x * rotationCos - corner.y * rotationSin,
        corner.x * rotationSin + corner.y * rotationCos);

    float age = max(0.0, FlightTime - v.Timing.x);
    float pulled = step(1.5, v.Timing.y);
    float settling = step(0.00001, v.Timing.y) * (1.0 - pulled);
    float swallowed = step(1.5, -v.Timing.y);
    float collecting = step(0.5, -v.Timing.y) * (1.0 - swallowed);
    float shrink = exp(-5.0 * age);

    // Settled, or spawning.
    float glide = exp(-8.0 * age) * settling;
    float grow = lerp(1.0, 1.0 - (1.0 - v.Timing.y) * shrink, settling);
    float2 world = center - v.Flight.xy * glide + local * grow;

    // Collecting.
    float topSpeedTime = (800.0 + 300.0) / 2500.0;
    float travel = age < topSpeedTime
        ? -300.0 * age + 1250.0 * age * age
        : -300.0 * topSpeedTime + 1250.0 * topSpeedTime * topSpeedTime + 800.0 * (age - topSpeedTime);
    float along = min(travel / max(v.Flight.y, 0.001), 1.0);
    float2 collector = Collectors[(int)(v.Flight.x + 0.5)].xy;
    world = lerp(world, center + (collector - center) * along + local * shrink, collecting);

    // Swallowed.
    float duration = max(-v.Timing.y - 2.0, 0.05);
    float t = saturate((FlightTime - v.Timing.x) / duration);
    float progress = 1.0 - (1.0 - exp(-2.5 * (1.0 - t))) / (1.0 - exp(-2.5));
    float2 sinking = lerp(center, v.Flight.xy, progress);
    float depth = distance(sinking, SwallowPlanet.xy) / max(SwallowPlanet.z, 0.001);
    world = lerp(world, sinking + local * saturate((depth - 0.55) / 0.45), swallowed);

    // Chain pull.
    float inverse = 1.0 - saturate((FlightTime - v.Timing.x) / max(v.Timing.y - 2.0, 0.001));
    float eased = 1.0 - inverse * inverse * inverse * inverse * inverse;
    world = lerp(world, center + v.Flight.xy * eased + local, pulled);

    output.Position = mul(float4(world, v.Placement.w, 1.0), view_projection);
    output.Color = v.Color;
    output.TexCoord = lerp(v.TextureRect.xy, v.TextureRect.zw, v.Corner);

    return output;
}

// SpriteTexture contains precomputed surface weights, not the original artwork.
// One filtered read replaces the edge/alpha-average/outline neighborhood reads.
float4 MainPS(PixelInput input) : COLOR
{
    float4 surface = tex2D(SpriteTextureSampler, input.TexCoord);
    float3 color = input.Color.rgb * surface.r + surface.g;
    // Vertex alpha is an effect flag, not opacity (ordinary gems use zero).
    float outlined = step(1.0f, input.Color.a);
    // The outline stays inside the silhouette and preserves premultiplied alpha.
    float3 outlinedColor = lerp(color, _OutlineColor.rgb * surface.a, surface.b);
    float4 finalColor = float4(lerp(color, outlinedColor, outlined), surface.a);

    return finalColor;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

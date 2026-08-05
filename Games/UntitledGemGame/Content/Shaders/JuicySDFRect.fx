/*#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// --- Base Parameters ---
float Time;
float2 Resolution; 
float2 RectSize;     // Inner size of the rectangle
float Thickness;   
float CornerRadius;

float4 CoreColor;
float4 GlowColor;

// --- Traveling Upgrade Pulse Parameters ---
float PulseProgress; // 0.0 to 1.0 loops around the perimeter
float4 PulseColor;   

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

// IQ's standard Rounded Box SDF
float sdRoundBox(float2 p, float2 b, float r)
{
    float2 q = abs(p) - b + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 px = input.TextureCoordinates * Resolution;

    // 1. --- THE JUICE: Energy Wobble ---
    px.x += sin(px.y * 0.01 + Time * 3.0) * 0.5f; 
    px.y += cos(px.x * 0.01 + Time * 3.0) * 0.5f;

    // 2. --- Base SDF Math ---
    // Calculate position relative to the center of our padded bounding box
    float2 center = Resolution * 0.5;
    float2 p = px - center;
    float2 halfSize = RectSize * 0.5;

    // Distance to solid rounded rectangle
    float d_solid = sdRoundBox(p, halfSize, CornerRadius);

    // We only want the outline, so take the absolute value
    float d = abs(d_solid);

    // Continuous ambient pulse
    float pulse = sin(Time * 8.0) * 0.1 + 0.1; 
    float baseThickness = Thickness + (pulse * 2.0);

    // 3. --- PERIMETER UPGRADE PULSE JUICE ---
    // Convert coordinate to a radial angle (-PI to PI), then normalize to 0.0 - 1.0
    float angle = atan2(p.y, p.x);
    float pixelProgress = (angle / 6.2831853) + 0.5; 

    // Find shortest distance along the continuous loop (handles wrapping seamlessly)
    float diff = frac(pixelProgress - PulseProgress + 0.5) - 0.5;

    // Scale radial distance back up to approximate pixel distance around perimeter
    float approxPerimeter = (RectSize.x + RectSize.y) * 2.0;
    float distAlongPerimeter = diff * approxPerimeter; 

    // Create an elliptical energy bolt
    float lengthScale = 600.0;
    float widthScale = 80.0;
    float pulseIntensity = exp(-(distAlongPerimeter * distAlongPerimeter / lengthScale) - (d * d / widthScale));

    // Fade in/out at the start and end of the lifespan if passing a one-shot variable
    pulseIntensity *= smoothstep(-0.2, 0.05, PulseProgress) * (1.0 - smoothstep(0.95, 1.2, PulseProgress));

    // Widen the line dynamically ONLY directly under the elliptical pulse
    float activeThickness = baseThickness + (pulseIntensity * 8.0); 

    // --- Core Rendering ---
    float core = 1.0 - smoothstep(activeThickness - 1.0, activeThickness + 1.0, d);

    // --- Glow Rendering ---
    float glowSpread = 8.0 + (pulseIntensity * 10.0);
    float glow = exp(-d / glowSpread) * ((0.6 + pulse * 0.4) + pulseIntensity * 3.5);

    // Blend base colors with the intense energy burst color
    float4 combinedCoreColor = lerp(CoreColor, PulseColor * 1.8, pulseIntensity);
    float4 combinedGlowColor = lerp(GlowColor, PulseColor, pulseIntensity);

    float4 finalColor = (combinedCoreColor * core) + (combinedGlowColor * glow);

    // Clamp alpha
    finalColor.a = saturate(core + glow);

    return finalColor * input.Color;
}

technique JuicySDFRect
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}*/

//#if OPENGL
//    #define SV_POSITION POSITION
//    #define VS_SHADERMODEL vs_3_0
//    #define PS_SHADERMODEL ps_3_0
//#else
//    #define VS_SHADERMODEL vs_4_0_level_9_1
//    #define PS_SHADERMODEL ps_4_0_level_9_1
//#endif
//
//float4x4 MatrixTransform;
//
//struct VertexShaderInput
//{
//    float4 Position     : POSITION0;
//    float2 LocalPos     : TEXCOORD0; // 0,0 is center of the box
//    float2 HalfSize     : TEXCOORD1; // Half-width, half-height
//    float  CornerRadius : TEXCOORD2;
//    float  Thickness    : TEXCOORD3; // If < 0, it fills the rectangle
//    float4 CoreColor    : COLOR0;
//    float4 GlowColor    : COLOR1;
//};
//
//struct VertexShaderOutput
//{
//    float4 Position     : SV_POSITION;
//    float2 LocalPos     : TEXCOORD0;
//    float2 HalfSize     : TEXCOORD1;
//    float  CornerRadius : TEXCOORD2;
//    float  Thickness    : TEXCOORD3;
//    float4 CoreColor    : COLOR0;
//    float4 GlowColor    : COLOR1;
//};
//
//VertexShaderOutput MainVS(VertexShaderInput input)
//{
//    VertexShaderOutput output;
//    output.Position = mul(input.Position, MatrixTransform);
//    output.LocalPos = input.LocalPos;
//    output.HalfSize = input.HalfSize;
//    output.CornerRadius = input.CornerRadius;
//    output.Thickness = input.Thickness;
//    output.CoreColor = input.CoreColor;
//    output.GlowColor = input.GlowColor;
//    return output;
//}
//
//float4 MainPS(VertexShaderOutput input) : COLOR
//{
//    // SDF Box Math
//    float2 q = abs(input.LocalPos) - input.HalfSize + input.CornerRadius;
//    float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - input.CornerRadius;
//
//    // Handle outline vs fill
//    float dist = d;
//    if (input.Thickness > 0.0)
//    {
//        dist = abs(d) - input.Thickness;
//    }
//
//    // Core
//    float core = 1.0 - smoothstep(-0.7, 0.7, dist);
//
//    // Glow
//    float glow = exp(-max(dist, 0.0) * 0.35);
//    glow *= 0.5;
//
//    float4 finalColor = (input.CoreColor * core) + (input.GlowColor * glow);
//    finalColor.a = saturate(core + glow);
//
//    return finalColor;
//}
//
//technique JuicySDF
//{
//    pass P0
//    {
//        VertexShader = compile VS_SHADERMODEL MainVS();
//        PixelShader = compile PS_SHADERMODEL MainPS();
//    }
//}
/*
#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 MatrixTransform;
float Time;

struct VertexShaderInput
{
    float4 Position     : POSITION0;
    float2 LocalPos     : TEXCOORD0; // Centered coords
    float2 HalfSize     : TEXCOORD1;
    float  Radius       : TEXCOORD2;
    float  Thickness    : TEXCOORD3;
    float4 CoreColor    : COLOR0;
    float4 GlowColor    : COLOR1;
};

struct VertexShaderOutput
{
    float4 Position     : SV_POSITION;
    float2 LocalPos     : TEXCOORD0;
    float2 HalfSize     : TEXCOORD1;
    float  Radius       : TEXCOORD2;
    float  Thickness    : TEXCOORD3;
    float4 CoreColor    : COLOR0;
    float4 GlowColor    : COLOR1;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.LocalPos = input.LocalPos;
    output.HalfSize = input.HalfSize;
    output.Radius = input.Radius;
    output.Thickness = input.Thickness;
    output.CoreColor = input.CoreColor;
    output.GlowColor = input.GlowColor;
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    // The wobble
    float2 p = input.LocalPos;
    p.x += sin(p.y * 0.01 + Time * 3.0) * 0.5;
    p.y += cos(p.x * 0.01 + Time * 3.0) * 0.5;

    // SD Box Math (using passed HalfSize)
    float2 q = abs(p) - input.HalfSize + input.Radius;
    float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - input.Radius;
    float dist = abs(d); 

    // Pulse & Thickness
    float pulse = sin(Time * 8.0) * 0.1 + 0.1;
    float pulse2 = 2.0;
    float baseThickness = input.Thickness + (pulse * 2.0);

    // Perimeter Pulse
    float angle = atan2(p.y, p.x);
    float pixelProgress = (angle / 6.2831853) + 0.5;
    float diff = frac(pixelProgress - pulse2 + 0.5) - 0.5;
    float approxPerimeter = (input.HalfSize.x + input.HalfSize.y) * 4.0;
    float distAlongPerimeter = diff * approxPerimeter;

    float pulseIntensity = exp(-(distAlongPerimeter * distAlongPerimeter / 600.0) - (dist * dist / 80.0));
    pulseIntensity *= smoothstep(-0.2, 0.05, pulse2) * (1.0 - smoothstep(0.95, 1.2, pulse2));

    //pulseIntensity = 0;

    float activeThickness = baseThickness + (pulseIntensity * 8.0);
    
    // Core & Glow
    float core = 1.0 - smoothstep(activeThickness - 1.0, activeThickness + 1.0, dist);
    float glowSpread = 5.0 + (pulseIntensity * 10.0);
    float glow = exp(-dist / glowSpread) * ((0.6 + pulse * 0.4) + pulseIntensity * 3.5);

    // Hardcode White for Pulse (avoiding COLOR2 limit)
    //float4 pulseColor = float4(1.0, 1.0, 1.0, 1.0);
    float4 pulseColor = float4(1.0, 0.0, 0.0, 1.0);
    float4 combinedCore = lerp(input.CoreColor, pulseColor * 1.8, pulseIntensity);
    float4 combinedGlow = lerp(input.GlowColor, pulseColor, pulseIntensity);

    float4 final = (combinedCore * core) + (combinedGlow * glow);
    final.a = saturate(core + glow);
    return final;
}

technique JuicySDFRect { pass P0 { VertexShader = compile VS_SHADERMODEL MainVS(); PixelShader = compile PS_SHADERMODEL MainPS(); } }
*/







#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 MatrixTransform;
float Time;
float RippleProgress; // <-- NEW: 0.0 to 1.0 to drive the click shockwave

struct VertexShaderInput
{
  float4 Position : POSITION0;
  float2 LocalPos : TEXCOORD0;
  float2 HalfSize : TEXCOORD1;
  float  CornerRadius : TEXCOORD2;
  float  Thickness : TEXCOORD3;
  float  RippleProgress : TEXCOORD4;
  float  HoverState : TEXCOORD5;
  float4 CoreColor : COLOR0;
  float4 GlowColor : COLOR1;
};

struct VertexShaderOutput
{
  float4 Position : SV_POSITION;
  float2 LocalPos : TEXCOORD0;
  float2 HalfSize : TEXCOORD1;
  float  CornerRadius : TEXCOORD2;
  float  Thickness : TEXCOORD3;
  float  RippleProgress : TEXCOORD4;
  float  HoverState : TEXCOORD5;
  float4 CoreColor : COLOR0;
  float4 GlowColor : COLOR1;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
  VertexShaderOutput output;
  output.Position = mul(input.Position, MatrixTransform);
  output.LocalPos = input.LocalPos;
  output.HalfSize = input.HalfSize;
  output.CornerRadius = input.CornerRadius;
  output.Thickness = input.Thickness;
  output.RippleProgress = input.RippleProgress;
  output.HoverState = input.HoverState;
  output.CoreColor = input.CoreColor;
  output.GlowColor = input.GlowColor;
  return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
  // SDF Box Math
  float2 q = abs(input.LocalPos) - input.HalfSize + input.CornerRadius;
  float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - input.CornerRadius;
  float dist = input.Thickness > 0.0 ? abs(d) - input.Thickness : d;


  float core = 1.0 - smoothstep(-0.7, 0.7, dist);
  float baseGlow = exp(-max(dist, 0.0) * 0.35);

  // Fade the breathing and comet effects in based on HoverState
  float breath = (sin(Time * 3.0) * 0.3) + 1.0;
  float finalGlow = (baseGlow * 0.5) * lerp(1.0, breath, input.HoverState);

  float angle = atan2(input.LocalPos.y, input.LocalPos.x);
  float normalizedAngle = (angle / 6.2831853) + 0.5;
  float cometWave = frac(normalizedAngle - (Time * 0.4));
  float cometIntensity = pow(cometWave, 5.0) * exp(-max(dist, 0.0) * 0.8) * 2.0;

  //  // Base Glow & Breathing
//  float baseGlow = exp(-max(dist, 0.0) * 0.35);
//  float breath = (sin(Time * 3.0) * 0.3) + 1.0;
//  float finalGlow = (baseGlow * 0.5) * breath;

  //  // Comet Highlight
//  float angle = atan2(input.LocalPos.y, input.LocalPos.x);
//  float normalizedAngle = (angle / 6.2831853) + 0.5;
//  float cometWave = frac(normalizedAngle - (Time * 0.4));
//  float cometHighlight = pow(cometWave, 5.0);
//  float cometIntensity = cometHighlight * exp(-max(dist, 0.0) * 0.8) * 2.0;
//  finalGlow += cometIntensity;

  // Only apply comet intensity if hovered
  finalGlow += (cometIntensity * input.HoverState);

  // Click Ripple (using input.RippleProgress instead of global)
  float ripple = 0.0;
  if (input.RippleProgress > 0.0 && input.RippleProgress < 1.0)
  {
    float distToCenter = length(input.LocalPos);
    float maxRadius = length(input.HalfSize) + 15.0;
    float currentRadius = input.RippleProgress * maxRadius;
    float ringDist = abs(distToCenter - currentRadius);
    ripple = smoothstep(8.0, 0.0, ringDist) * (1.0 - input.RippleProgress);
  }

  float4 finalColor = (input.CoreColor * core) + (input.GlowColor * finalGlow);
  finalColor.rgb += (input.CoreColor.rgb * ripple * 2.5);
  finalColor.a = saturate(core + finalGlow + ripple);

  return finalColor;

  // Core Outline
//  float core = 1.0 - smoothstep(-0.7, 0.7, dist);
//
//  // Base Glow & Breathing
//  float baseGlow = exp(-max(dist, 0.0) * 0.35);
//  float breath = (sin(Time * 3.0) * 0.3) + 1.0;
//  float finalGlow = (baseGlow * 0.5) * breath;
//
//  // Comet Highlight
//  float angle = atan2(input.LocalPos.y, input.LocalPos.x);
//  float normalizedAngle = (angle / 6.2831853) + 0.5;
//  float cometWave = frac(normalizedAngle - (Time * 0.4));
//  float cometHighlight = pow(cometWave, 5.0);
//  float cometIntensity = cometHighlight * exp(-max(dist, 0.0) * 0.8) * 2.0;
//  finalGlow += cometIntensity;
//
//  // --- EFFECT 3: Click Ripple ---
//  float ripple = 0.0;
//  if (RippleProgress > 0.0 && RippleProgress < 1.0)
//  {
//    // Calculate distance from the exact center of the button
//    float distToCenter = length(input.LocalPos);
//
//    // Calculate max radius (corner to corner) so it clears the box entirely
//    float maxRadius = length(input.HalfSize) + 15.0;
//    float currentRadius = RippleProgress * maxRadius;
//
//    // Create an expanding ring
//    float ringDist = abs(distToCenter - currentRadius);
//
//    // Smooth out the ring (change 8.0 to make the wave thicker/thinner)
//    ripple = smoothstep(8.0, 0.0, ringDist);
//
//    // Fade the ripple out as it reaches the end of its lifespan
//    ripple *= (1.0 - RippleProgress);
//}
//
//  // Combine
//  float4 finalColor = (input.CoreColor * core) + (input.GlowColor * finalGlow);
//
//  // Add the ripple as a bright, overblown version of the core color
//  finalColor.rgb += (input.CoreColor.rgb * ripple * 2.5);
//
//  finalColor.a = saturate(core + finalGlow + ripple);
//
//  return finalColor;
}

technique JuicySDF
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
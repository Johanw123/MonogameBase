#if OPENGL
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
float2 PointA;     
float2 PointB;     
float Thickness;   

float4 CoreColor;
float4 GlowColor;

// --- Traveling Upgrade Pulse Parameters ---
float PulseProgress; // 0.0 at PointA -> 1.0 at PointB. Set < -0.2 or > 1.2 when inactive
float4 PulseColor;   // Intense flash color (e.g. Gold, Cyan, White)

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

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 px = input.TextureCoordinates * Resolution;
    float2 pA = PointA * Resolution;
    float2 pB = PointB * Resolution;

    // 1. --- THE JUICE: Energy Wobble ---
    // This MUST happen before we do the distance math, so the whole line bends!
    px.x += sin(px.y * 0.05 + Time * 6.0) * 2.0; 
    px.y += cos(px.x * 0.05 + Time * 6.0) * 2.0;

    // 2. --- Base SDF Math ---
    float2 pa = px - pA;
    float2 ba = pB - pA;
    float lineLength = length(ba);
    float baSqLength = dot(ba, ba);
    
    // Normalized position along the segment (0.0 = Start, 1.0 = End)
    float h = dot(pa, ba) / max(baSqLength, 0.0001); 
    float hClamped = clamp(h, 0.0, 1.0);
    
    // Perpendicular distance to the line core
    float d = length(pa - ba * hClamped);

    // Continuous ambient pulse
    float pulse = sin(Time * 8.0) * 0.3 + 0.3; 
    float baseThickness = Thickness + (pulse * 2.0);

    // 3. --- ONE-SHOT UPGRADE PULSE JUICE ---
    // Calculate distance along the line in actual pixels
    float distAlongLine = (h - PulseProgress) * lineLength;
    
    // Create an elliptical energy bolt (longer along the line, tighter perpendicular)
    // 900.0 controls the length of the comet, 200.0 controls the width. 
    float length = 500;
    float width = 80;
    float pulseIntensity = exp(-(distAlongLine * distAlongLine / length) - (d * d / width));
    
    pulseIntensity *= smoothstep(-0.2, 0.05, PulseProgress) * (1.0 - smoothstep(0.95, 1.2, PulseProgress));

    // Widen the line dynamically ONLY directly under the elliptical pulse
    float activeThickness = baseThickness + (pulseIntensity * 8.0); 

    // --- Core Rendering ---
    float core = 1.0 - smoothstep(activeThickness - 1.0, activeThickness + 1.0, d);

    // --- Glow Rendering ---
    // Dynamically increase bloom spread under the wave
    float glowSpread = 15.0 + (pulseIntensity * 20.0);
    float glow = exp(-d / glowSpread) * ((0.6 + pulse * 0.4) + pulseIntensity * 3.5);

    // Blend base colors with the intense energy burst color
    float4 combinedCoreColor = lerp(CoreColor, PulseColor * 1.8, pulseIntensity);
    float4 combinedGlowColor = lerp(GlowColor, PulseColor, pulseIntensity);

    float4 finalColor = (combinedCoreColor * core) + (combinedGlowColor * glow);
    
    // Clamp alpha
    finalColor.a = saturate(core + glow);

    return finalColor * input.Color;
}

technique JuicySDF
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

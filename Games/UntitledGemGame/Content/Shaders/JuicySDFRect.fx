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
}

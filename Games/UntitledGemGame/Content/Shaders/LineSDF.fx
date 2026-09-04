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

// --- Globals ---
// MatrixTransform takes over the camera projection that SpriteBatch used to do for us.
float4x4 MatrixTransform;
float Time;
float WobbleAmount;
float ThicknessPulseAmount;
float PulseLengthScale;
float PulseWidthScale;
float PulseThicknessBoost;
float BaseGlowSpread;
float PulseGlowSpread;

// --- Vertex Structures ---
struct VertexShaderInput
{
    float4 Position      : POSITION0; // Screen-space position of the quad corner
    float2 LocalPos      : TEXCOORD0; // The local pixel coordinate inside the bounding box
    float2 PointA        : TEXCOORD1; // Local start point
    float2 PointB        : TEXCOORD2; // Local end point
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

// --- Vertex Shader ---
// This acts as the bridge. It takes the custom vertex data from C# and 
// passes it down to the Pixel Shader for every pixel of the line's quad.
VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    
    // Transform position to screen space
    output.Position = mul(input.Position, MatrixTransform);
    
    // Pass everything else straight through
    output.LocalPos = input.LocalPos;
    output.PointA = input.PointA;
    output.PointB = input.PointB;
    output.Thickness = input.Thickness;
    output.PulseProgress = input.PulseProgress;
    output.CoreColor = input.CoreColor;
    output.GlowColor = input.GlowColor;
    
    return output;
}

// --- Pixel Shader ---
float4 MainPS(VertexShaderOutput input) : COLOR
{
    //return float4(1.0,1.0,1.0,1.0);
    // We no longer need Resolution. The VS hands us the exact local pixel coordinates!
    float2 px = input.LocalPos;
    float2 pA = input.PointA;
    float2 pB = input.PointB;

    // 1. --- THE JUICE: Energy Wobble ---
    px.x += sin(px.y * 0.05 + Time * 6.0) * WobbleAmount;
    px.y += cos(px.x * 0.05 + Time * 6.0) * WobbleAmount;

    // 2. --- Base SDF Math ---
    float2 pa = px - pA;
    float2 ba = pB - pA;
    float lineLength = length(ba);
    float baSqLength = dot(ba, ba);
    
    float h = dot(pa, ba) / max(baSqLength, 0.0001); 
    float hClamped = clamp(h, 0.0, 1.0);
    float d = length(pa - ba * hClamped);

    float pulse = sin(Time * 8.0) * 0.3 + 0.3; 
    float baseThickness = input.Thickness + (pulse * ThicknessPulseAmount);

    // 3. --- ONE-SHOT UPGRADE PULSE JUICE ---
    float distAlongLine = (h - input.PulseProgress) * lineLength;
    
    float pulseIntensity = exp(
        -(distAlongLine * distAlongLine / PulseLengthScale)
        - (d * d / PulseWidthScale));
    
    pulseIntensity *= smoothstep(-0.2, 0.05, input.PulseProgress) * (1.0 - smoothstep(0.95, 1.2, input.PulseProgress));

    float activeThickness = baseThickness + (pulseIntensity * PulseThicknessBoost);

    // --- Core Rendering ---
    float core = 1.0 - smoothstep(activeThickness - 1.0, activeThickness + 1.0, d);

    // --- Glow Rendering ---
    float glowSpread = BaseGlowSpread + (pulseIntensity * PulseGlowSpread);
    float glow = exp(-d / glowSpread) * ((0.6 + pulse * 0.4) + pulseIntensity * 3.5);

    // Blend base colors with the intense energy burst color
    float4 combinedCoreColor = lerp(input.CoreColor, float4(1.0,1.0,1.0,1.0) * 1.8, pulseIntensity);
    float4 combinedGlowColor = lerp(input.GlowColor, float4(1.0,1.0,1.0,1.0), pulseIntensity);

    float4 finalColor = (combinedCoreColor * core) + (combinedGlowColor * glow);
    
    finalColor.a = saturate(core + glow);

    return finalColor; // We drop input.Color here since we aren't using SpriteBatch tints
}

technique JuicySDF
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}

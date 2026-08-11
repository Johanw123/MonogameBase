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
float4x4 view_projection;
float4 _OutlineColor;
float2 TexelSize;     
float _TotalTime;     

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
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelInput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

PixelInput SpriteVertexShader(VertexInput v) {
    PixelInput output;
    output.Position = mul(v.Position, view_projection);
    output.Color = v.Color;
    output.TexCoord = v.TexCoord;
    return output;
}

/*float4 MainPS(PixelInput input) : COLOR
{
    float4 texColor = tex2D(SpriteTextureSampler, input.TexCoord);
    float alpha = texColor.a;
    float stateAlpha = input.Color.a;

    if (stateAlpha >= 0.99f && alpha == 0.0f) return float4(0, 0, 0, 0);

    // --- SOFT OUTLINE PASS ---
    if (stateAlpha < 0.99f && alpha < 0.1f) 
    {
        // 1.5f spreads the samples out slightly to create a wider, softer blur. 
        // You can increase this to 2.0f for an even wider (but slightly grainier) glow.
        float2 tx = TexelSize * 1.5f; 

        // Sample Cardinals (Up, Down, Left, Right)
        float alphaSum = 0.0f;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(0, -tx.y)).a;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(0, tx.y)).a;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(-tx.x, 0)).a;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(tx.x, 0)).a;
        
        // Sample Diagonals (Multiplied by 0.7f because they are physically further away)
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(-tx.x, -tx.y)).a * 0.7f;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(tx.x, -tx.y)).a * 0.7f;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(-tx.x, tx.y)).a * 0.7f;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(tx.x, tx.y)).a * 0.7f;

        // smoothstep creates a gorgeous SDF-like gradient. 
        // If alphaSum is 0, it returns 0. If it's 2.5 or higher, it returns 1.0. 
        // Everything in between becomes a smooth, curved gradient.
        float softOutlineAlpha = smoothstep(0.0f, 1.5f, alphaSum);

        if (softOutlineAlpha > 0.0f)
        {
            float4 outColor = _OutlineColor;
            
            // State Machine Logic
            //Static Fade (Alpha 150-254) mapped to 0.5f - 0.99f
            if (stateAlpha >= 0.5f) { 
                outColor *= (stateAlpha - 0.5f) * 2.0f; 
            }
            //Pulsate (Alpha 100-149) mapped to 0.35f - 0.49f
            else if (stateAlpha >= 0.35f) {
                outColor *= 0.5f + (0.5f * sin(_TotalTime * 5.0f));
            }
            //Hover (Alpha 50-99) mapped to 0.15f - 0.34f
            else if (stateAlpha >= 0.15f) {
                outColor = 0.5f; 
            }
            //Click Burst (Alpha 0-49) mapped to 0.0f - 0.14f
            else {
                float burst = saturate(stateAlpha / 0.15f); 
                outColor = lerp(_OutlineColor, float4(1, 1, 1, 0.5f), burst);
                outColor *= burst; 
            }
            
            // Multiply the final state color by our smooth, soft gradient
            return outColor * softOutlineAlpha;
        }
    }

    // --- SPRITE PASS ---
    float4 baseColor = texColor * float4(input.Color.rgb, 1.0f);
    
    if (alpha > 0.0f) 
    {
        if (stateAlpha < 0.15f) {
            float burst = saturate(stateAlpha / 0.15f);
            baseColor.rgb = lerp(baseColor.rgb, float3(1, 1, 1), burst);
        }
        else if (stateAlpha >= 0.15f && stateAlpha < 0.35f) {
            baseColor.rgb += float3(0.25f, 0.25f, 0.25f) * alpha;
        }
    }

    return baseColor;
}*/

float4 MainPS(PixelInput input) : COLOR
{
    float4 texColor = tex2D(SpriteTextureSampler, input.TexCoord);
    float alpha = texColor.a;
    float stateAlpha = input.Color.a;

    if (stateAlpha >= 0.99f && alpha == 0.0f) return float4(0, 0, 0, 0);

    // --- OUTLINE PASS ---
    if (stateAlpha < 0.99f && alpha < 0.1f) 
    {
        float2 tx = TexelSize * 1.5f; 
        float4 outColor = _OutlineColor;
        
        // State configurations
        if (stateAlpha >= 0.5f) { 
            // Static Fade
            outColor *= (stateAlpha - 0.5f) * 2.0f; 
            outColor *= 0.0f; 
            texColor *= (stateAlpha - 0.5f) * 2.0f;
        }
        else if (stateAlpha >= 0.35f) {
            // Pulsate
            outColor *= 0.5f + (0.5f * sin(_TotalTime * 5.0f));
        }
        else if (stateAlpha >= 0.21f) {
            // Hover (Solid)
            outColor *= 1.0f; 
        }
        else {
            // progress goes from 0.0 (start of click) to 1.0 (end of burst)
            float progress = saturate(stateAlpha / 0.21f); 
            //progress = 1.0f;
            
            // Expand outward: Starts at normal 1.5f and grows wider (stays expanded)
            tx = TexelSize * (1.5f + (progress * 2.0f)); 
            
            // Start bright white, then transition back to your outline color as it expands
            outColor = lerp(float4(1, 1, 1, 1), _OutlineColor, progress);
        }

        // Sample the 8 directions cleanly using our dynamic radius `tx`
        float alphaSum = 0.0f;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(0, -tx.y)).a;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(0, tx.y)).a;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(-tx.x, 0)).a;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(tx.x, 0)).a;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(-tx.x, -tx.y)).a * 0.7f;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(tx.x, -tx.y)).a * 0.7f;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(-tx.x, tx.y)).a * 0.7f;
        alphaSum += tex2D(SpriteTextureSampler, input.TexCoord + float2(tx.x, tx.y)).a * 0.7f;

        float outlineAlpha = smoothstep(0.0f, 2.5f, alphaSum);

        if (outlineAlpha > 0.0f)
        {
            // If it's the burst state, fade the final outline opacity out as it finishes
            if (stateAlpha < 0.21f) {
                float impact = 1.0f - saturate(stateAlpha / 0.21f);
                return outColor * outlineAlpha * impact;
            }
            
            return outColor * outlineAlpha;
        }
    }

    // --- SPRITE PASS ---
    float4 baseColor = texColor * float4(input.Color.rgb, 1.0f);
    
    if (alpha > 0.0f) 
    {
        // Subtle brightness boost on the ship during hover and click states
        if (stateAlpha < 0.35f) {
            baseColor.rgb += float3(0.25f, 0.25f, 0.25f) * alpha;
        }
    }

    return baseColor;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

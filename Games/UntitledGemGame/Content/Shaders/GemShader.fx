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
    float4x4 mvp;
};

cbuffer ParameterBlock : register(b1)
{
    float4 _OutlineColor;
    float2 TexelSize;
    float _Time;
    float _Padding; // 16-byte alignment padding for std140 / SPIRV-Cross
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
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelInput {
    float4 Position : SV_Position0;
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

float random (float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
}

float gold_noise(float2 uv, float seed)
{
    float PHI = 1.61803398874989484820459; // Golden Ratio
    return frac(tan(distance(uv * PHI, uv) * seed) * uv.x);
}

float avg_alpha(PixelInput input)
{
    int dist = 1;
    float result = 0.0;
    for (int i = -dist; i <= dist; i++){
        for (int j = -dist; j <= dist; j++){
            result += tex2D(SpriteTextureSampler, input.TexCoord + float2(float(i), float(j)) * TexelSize).a;
        }
    }
    float d = (1.0 + float(2 * dist));
    return result / (d * d);
}

float4 MainPS(PixelInput input) : COLOR
{
    float4 TexColor = tex2D(SpriteTextureSampler, input.TexCoord);
    float4 ResultColor = TexColor;

    if (TexColor.r == TexColor.g && TexColor.g == TexColor.b)
    {
        // Retain the original high-contrast gem shading: dark texture values
        // shade the tint down, while bright facets move toward white. Limiting
        // the white blend keeps the quality color visible through the bloom.
        float brightness = TexColor.r;
        float shadowAmount = saturate(brightness * 2.0f);
        float highlightAmount = saturate((brightness - 0.5f) * 2.0f) * 0.85f;
        float3 shadedColor = input.Color.rgb * shadowAmount;
        shadedColor = lerp(shadedColor, float3(1.0f, 1.0f, 1.0f), highlightAmount);

        // SpriteBatch uses premultiplied-alpha blending.
        ResultColor = float4(shadedColor * TexColor.a, TexColor.a);
    }

    int width = 18;
    int height = 30;
    float _Distance = 0.8f;
    float4 _Color = float4(0, 0, 0, 2) * 2.2f;

    // Simple sobel filter for the alpha channel
    float2 d = TexelSize.xy * _Distance;

    float a1 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(-1, -1)).a;
    float a2 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2( 0, -1)).a;
    float a3 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(+1, -1)).a;

    float a4 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(-1,  0)).a;
    float a6 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(+1,  0)).a;

    float a7 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(-1, +1)).a;
    float a8 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2( 0, +1)).a;
    float a9 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(+1, +1)).a;

    float gx = - a1 - a2 * 2 - a3 + a7 + a8 * 2 + a9;
    float gy = - a1 - a4 * 2 - a7 + a3 + a6 * 2 + a9;

    float w = sqrt(gx * gx + gy * gy) / 4;

    // Mix the contour color
    float4 source = tex2D(SpriteTextureSampler, input.TexCoord);
    float4 finalColor = float4(lerp(ResultColor.rgb, _Color.rgb, w), ResultColor.a);

    float4 col2 = avg_alpha(input);
    float4 col = float4(0, 0, 0, 0);

    if (TexColor.a != 0 && input.Color.a >= 1.0f)
    {
        float totalAlpha = 1.0;
        for (int i = 1; i < 3; i++) 
        {
            float4 pixelUp = tex2D(SpriteTextureSampler, input.TexCoord + float2(0, i * TexelSize.y));
            float4 pixelDown = tex2D(SpriteTextureSampler, input.TexCoord - float2(0, i * TexelSize.y));
            float4 pixelRight = tex2D(SpriteTextureSampler, input.TexCoord + float2(i * TexelSize.x, 0));
            float4 pixelLeft = tex2D(SpriteTextureSampler, input.TexCoord - float2(i * TexelSize.x, 0));
            totalAlpha = totalAlpha * pixelUp.a * pixelDown.a * pixelRight.a * pixelLeft.a;
        }  

        if (totalAlpha == 0) {
            col = float4(1, 1, 1, 1) * _OutlineColor;
        }
    }

    finalColor = finalColor + (col * col2);

    float _LineWidth = 0.1f;
    float _Offset = 0.0f;
    float _LineLength = 0.3f;
    float2 uv = input.TexCoord;
    float _Speed = 1.0f;

    float pauseTime = input.Color.a * 2.0f;
    float totalCycle = 1.0 + pauseTime;

    float rawCycle = fmod(_Time * _Speed, totalCycle);
    float animatedOffset = (1.0 - saturate(rawCycle)) - 0.5;

    float diagonalPos = uv.y - (1.0 - uv.x);
    float2 center = float2(0.5, 0.5);
    float distFromCenter = distance(uv, center);

    float scaleFactor = saturate(1.0 - abs(_Offset)); 
    float dynamicLength = _LineLength * scaleFactor;
    float dynamicWidth = _LineWidth * scaleFactor;

    float edgeFade = smoothstep(dynamicLength, dynamicLength - 0.05, distFromCenter);

    if (abs(diagonalPos - animatedOffset) < (dynamicWidth) && distFromCenter < dynamicLength)
    {
        float3 whiteColor = finalColor.rgb + float3(1.5, 1.5, 1.5) * 0.2f;
        finalColor = lerp(finalColor, float4(whiteColor.r, whiteColor.g, whiteColor.b, finalColor.a), edgeFade);
    }

    float distanceFromLine = abs(diagonalPos - animatedOffset);
    float widthMask = smoothstep(dynamicWidth, dynamicWidth - 0.02, distanceFromLine);
    float combinedMask = widthMask * edgeFade;

    float shineStrength = 3.5;
    float3 glintColor = finalColor.rgb + (finalColor.rgb * shineStrength); 
    float luminance = dot(finalColor.rgb, float3(0.299, 0.587, 0.114));
    
    finalColor.rgb = lerp(finalColor.rgb, glintColor, combinedMask * luminance);
    
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

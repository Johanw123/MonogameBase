#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D SpriteTexture;
float4x4 view_projection;
float4x4 view_matrix;
float4x4 mvp;
float4x4 inv_view_matrix;
float _Time;
float2 TexelSize;
float4 _OutlineColor;
// float4 _Color;
// float _Radius;

sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;

    AddressU = clamp;
    AddressV = clamp;
};

//sampler BaseSampler : register(s1)
//{
//  Texture = (BaseTexture);
//  Filter = Linear;
//  AddressU = clamp;
//  AddressV = clamp;
//};

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
  //output.Position = v.Position;
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
  for (int i = -dist; i<=dist; i++){
    for (int j = -dist; j<=dist; j++){
      result += tex2D(SpriteTextureSampler, input.TexCoord + float2(float(i),float(j))*TexelSize).a;
    }
  }
  float d = (1.0+float(2*dist));
  return result/(d*d);
}

float4 MainPS(PixelInput input) : COLOR
{
   // input.Color.a = 1.0f;
    
    float4 TexColor = tex2D(SpriteTextureSampler,input.TexCoord);
    float4 ResultColor = TexColor;
    float4 Shade = float4(0.5f,0.5f,0.5f,0.5f);

    /*if(input.Color.b > 0.05f)
    {
      input.Color.rgb = float3(0.2f, 0.7f, 0.0f);
    }

    if(input.Color.b > 0.1f)
    {
      input.Color.rgb = float3(0.2f, 0.7f, input.Color.b);
    }*/

    if(input.Color.b > 0.0f)
    {
      float b = input.Color.b;

      input.Color.rgb = float3(0.0f, 0.0f, 0.2f + (b * 3.0f));
    }

    //check here if a colour is truly "grayscale", otherwise return original colour
    if (TexColor.r == TexColor.g && TexColor.g == TexColor.b)
    {
      //this uses additive blending for colours brighter than middle gray and multiplication for darker colours
          ResultColor = float4(input.Color.rgb, 1.0f) + (TexColor - Shade) * 2; //if texture colour ranges 0.5..1, output colour is input + texture colour ranging 0..1
          //output colour ranges between input colour and white
          if (TexColor.r < 0.5f)                        //if texture colour ranges 0..0.5, output colour is input multiplied by texture colour ranging 0...1 of input
          { //output colour ranges between black and input colour
              ResultColor = float4(input.Color.rgb, 1.0f) * (TexColor) * 2;
          }
    }

      //return  ResultColor;
       int width = 18;
       int height = 30;
      float _Distance = 0.8f;
      float4 _Color = float4(0,0,0,2) * 2.2f;
      //float4 _Color = float4(1,0.4f,0.4f,1) * 2.0f;

        // Simple sobel filter for the alpha channel.
        float d = TexelSize.xy * _Distance;

        half a1 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(-1, -1)).a;
        half a2 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2( 0, -1)).a;
        half a3 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(+1, -1)).a;

        half a4 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(-1,  0)).a;
        half a6 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(+1,  0)).a;

        half a7 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(-1, +1)).a;
        half a8 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2( 0, +1)).a;
        half a9 = tex2D(SpriteTextureSampler, input.TexCoord + d * float2(+1, +1)).a;

        float gx = - a1 - a2*2 - a3 + a7 + a8*2 + a9;
        float gy = - a1 - a4*2 - a7 + a3 + a6*2 + a9;

        float w = sqrt(gx * gx + gy * gy) / 4;

        // Mix the contour color.
        half4 source = tex2D(SpriteTextureSampler, input.TexCoord);
        half4 finalColor = half4(lerp(ResultColor.rgb, _Color.rgb, w), ResultColor.a);



        float4 col2 = avg_alpha(input);
        float4 col = float4(0,0,0,0);
    if (TexColor.a != 0 && input.Color.a >= 1.0f)
    {
          float totalAlpha = 1.0;
          for (int i = 1; i < 3; i++) 
          {
            float4 pixelUp = tex2D(SpriteTextureSampler, input.TexCoord + float2(0, i * TexelSize.y));
            float4 pixelDown = tex2D(SpriteTextureSampler, input.TexCoord - float2(0, i *TexelSize.y));
            float4 pixelRight = tex2D(SpriteTextureSampler, input.TexCoord + float2(i * TexelSize.x, 0));
            float4 pixelLeft = tex2D(SpriteTextureSampler, input.TexCoord - float2(i * TexelSize.x, 0));
            totalAlpha = totalAlpha * pixelUp.a * pixelDown.a * pixelRight.a * pixelLeft.a;
          }  

          if (totalAlpha == 0) {
            //TexColor.rgba = float4(1, 1, 1, 1) * _OutlineColor;
            col = float4(1, 1, 1, 1) * _OutlineColor;
          }
    }

        return finalColor + (col * col2);

        float _LineY = 0.5f;       // The vertical position (0.0 to 1.0)
        float _LineWidth = 0.1f;   // The thickness of the line
        float _Offset = 0.0f;      // Shifts the line (effectively the 'b' in y = mx + b)
        float _LineLength = 0.3f;  // Base length (e.g., 0.3)
        float2 uv = input.TexCoord;
        float _Speed = 1.0f;
        float edgeFade = 0.3f;

    // 1. Diagonal Logic with Pause
    float pauseTime = input.Color.a * 2.0f; // Adjust this for a longer/shorter pause
    float totalCycle = 1.0 + pauseTime;

    // Calculate cycle progress (0.0 to 1.0+pauseTime)
    float rawCycle = fmod(_Time * _Speed, totalCycle);

    // Saturate clamps it at 1.0 during the pause duration
    // We use 1.0 - saturate(...) to maintain your "reverse" direction
    float animatedOffset = (1.0 - saturate(rawCycle)) - 0.5;

    // 1. Diagonal Logic (Flipped X)
    float diagonalPos = uv.y - (1.0 - uv.x);
    //float animatedOffset = frac(-_Time * _Speed) - 0.5;
    // 2. Distance from Center Logic
    // We calculate how far we are from the center of the sprite
    float2 center = float2(0.5, 0.5);
    float distFromCenter = distance(uv, center);

    // 3. Make length/width react to _Offset
    // 'abs(_Offset)' ensures it shrinks/grows regardless of direction
    float scaleFactor = saturate(1.0 - abs(_Offset)); 
    float dynamicLength = _LineLength * scaleFactor;
    float dynamicWidth = _LineWidth * scaleFactor;

    // 5. Drawing the line with a length limit
    // We check: Is it within the diagonal bounds AND within the length bounds?
    if (abs(diagonalPos - animatedOffset) < (dynamicWidth) && distFromCenter < dynamicLength)
    {
        float3 whiteColor = finalColor + float3(1.5, 1.5, 1.5) * 0.2f;
        // Smoothly fade the edges of the line length for a cleaner look
        float edgeFade = smoothstep(dynamicLength, dynamicLength - 0.05, distFromCenter);
        finalColor = lerp(finalColor, float4(whiteColor.r, whiteColor.g, whiteColor.b, finalColor.a), edgeFade);
    }

// Calculate the mask for the line width (replaces your 'if' check)
float distanceFromLine = abs(diagonalPos - animatedOffset);

// Create a soft mask for the width (Anti-aliasing)
// This replaces the harsh "if" boundary with a 1-2 pixel fade
float widthMask = smoothstep(dynamicWidth, dynamicWidth - 0.02, distanceFromLine);

// Combine Width Mask and Length Mask (edgeFade)
float combinedMask = widthMask * edgeFade;

// --- THE FIX FOR THE "PASTED" LOOK ---
// Instead of a flat white, we use the mask to "brighten" the existing sprite.
// This preserves the sprite's shadows and details.

float shineStrength = 3.5; // How bright the glint is
float3 glintColor = finalColor.rgb + (finalColor.rgb * shineStrength); 

// Apply the blend
// We use the combinedMask to decide where the glint happens.
//finalColor.rgb = lerp(finalColor.rgb, glintColor, combinedMask * finalColor.a);

float luminance = dot(finalColor.rgb, float3(0.299, 0.587, 0.114));
finalColor.rgb = lerp(finalColor.rgb, glintColor, combinedMask * luminance);
        return finalColor; 


      // int width = 18;
      // int height = 30;
      // float _Radius = 2;
      // float4 _Color = float4(0,0,0,1);
      // float4 _MainTex_TexelSize = float4(1.0 / width, 1.0 / height, width, height);

      // float na = 0;
      // float r = _Radius;

      // for (int nx = -r; nx <= r; nx++)
      // {
      //     for (int ny = -r; ny <= r; ny++)
      //     {
      //         if (nx*nx+ny*ny <= r)
      //         {
      //             float4 nc = tex2D(SpriteTextureSampler, input.TexCoord + float2(_MainTex_TexelSize.x*nx, _MainTex_TexelSize.y*ny));
      //             na+=ceil(nc.a);
      //         }
      //     }
      // }

      // na = clamp(na,0,1);

      // //float4 c = tex2D(SpriteTextureSampler, input.TexCoord);
      // na-=ceil(TexColor.a);

      // float4 outline = lerp(ResultColor, _Color, na);

      // return outline;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};

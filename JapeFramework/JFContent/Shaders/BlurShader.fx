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

#define SAMPLE_COUNT 15

float2 TexelSize;
float2 BlurDirection; // e.g., (1, 0) for Horizontal, (0, 1) for Vertical
float BlurRadius;     // e.g., 1.0 for exact pixels, 2.0 or 3.0 for a wider blur

float4x4 view_projection;

sampler2D InputSampler : register(s0);

static const float SampleWeights[SAMPLE_COUNT] = {
    0.015, 0.025, 0.040, 0.060, 0.090, 0.120, 0.150,
    0.160,
    0.150, 0.120, 0.090, 0.060, 0.040, 0.025, 0.015
};

static const float OffsetSteps[SAMPLE_COUNT] = {
    -7.0, -6.0, -5.0, -4.0, -3.0, -2.0, -1.0,
     0.0,
     1.0,  2.0,  3.0,  4.0,  5.0,  6.0,  7.0
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

float4 PixelShaderFunction(PixelInput input) : COLOR0
{
    float4 c = 0;

    [unroll]
    for (int i = 0; i < SAMPLE_COUNT; i++)
    {
      // Multiply by direction to isolate the axis, and Radius to stretch it
      float2 offset = OffsetSteps[i] * TexelSize * BlurDirection * BlurRadius;

      c += tex2D(InputSampler, input.TexCoord + offset) * SampleWeights[i];
    }

    // Start with the center pixel
    //float4 color = tex2D(InputSampler, input.TexCoord) * OffsetSteps[0] * BlurRadius;

    //// Sample pixels on both sides
    //[unroll]
    //for (int i = 1; i < SAMPLE_COUNT; i++)
    //{
    //  float2 offset = BlurDirection * (TexelSize * i) * TexelSize * BlurRadius;
    //  color += tex2D(InputSampler, input.TexCoord + offset) * SampleWeights[i];
    //  color += tex2D(InputSampler, input.TexCoord - offset) * SampleWeights[i];
    //}

    // Optional: Keep the alpha of the original texture if you have transparent edges
     //c.a = tex2D(InputSampler, input.TexCoord).a; 

    //return color;
    return c;
}

technique GaussianBlur
{
  pass
  {
    VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
    PixelShader = compile PS_SHADERMODEL PixelShaderFunction();
  }
}

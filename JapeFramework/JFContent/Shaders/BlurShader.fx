#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif


#define SAMPLE_COUNT 35

float2 SampleOffsets[SAMPLE_COUNT];
float SampleWeights[SAMPLE_COUNT];

float4x4 view_projection;
float2 xResolution = float2(0, 0);

sampler2D InputSampler : register(s0);

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
    
    // Combine a number of weighted image filter taps.
    for (int i = 0; i < SAMPLE_COUNT; i++)
    {
        c += tex2D(InputSampler, input.TexCoord + SampleOffsets[i]) * SampleWeights[i];
    }
    
    float4 c2 = tex2D(InputSampler, input.TexCoord);
    
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
